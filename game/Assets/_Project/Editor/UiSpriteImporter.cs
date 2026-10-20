using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cafe.EditorTools
{
    /// Nhập sprite UI "Clay Café": mọi PNG trong Assets/_Project/Art/UI thành Sprite (single, không mipmap,
    /// alphaIsTransparency, bilinear, nén ASTC trên Android / iOS) + spriteBorder đọc từ ui_sprites.json (tools/gen_ui_sprites.py).
    /// EnsureTheme() tạo/cập nhật Assets/_Project/Data/UI/SO_UiTheme.asset.
    public class UiSpriteImporter : AssetPostprocessor
    {
        public const string UiDir = "Assets/_Project/Art/UI";
        public const string JsonPath = UiDir + "/ui_sprites.json";
        public const string FontDir = "Assets/_Project/Art/Fonts";
        public const string ThemeDir = "Assets/_Project/Data/UI";
        public const string ThemePath = ThemeDir + "/SO_UiTheme.asset";

        [System.Serializable]
        public class Entry
        {
            public string file;
            public int[] border;          // left, bottom, right, top
            public int pixelsPerUnit;
        }

        [System.Serializable]
        public class Manifest
        {
            public int pixelsPerUnit;
            public Entry[] sprites;
        }

        private static Manifest LoadManifest()
        {
            try
            {
                string full = Path.GetFullPath(JsonPath);
                if (!File.Exists(full)) return null;
                return JsonUtility.FromJson<Manifest>(File.ReadAllText(full));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[UiSpriteImporter] Không đọc được " + JsonPath + ": " + e.Message);
                return null;
            }
        }

        /// Nén ASTC trên điện thoại (RGBA32 không nén: icon 256x256 = 256 KB, logo 2048x1024 = 8 MB). Icon cửa hàng + logo ngang ASTC 6x6;
        /// sprite UI 9-slice / bàn tay ASTC 4x4 (viền sắc, khối 6x6 dễ lem viền). Ảnh nguồn icon app chỉ để PlayerSettings cắt icon,
        /// không vào bản dựng nên giữ nguyên không nén.
        private static void ApplyCompression(TextureImporter ti)
        {
            string path = ti.assetPath;
            bool keep = path == BrandingSetup.IconFull || path == BrandingSetup.AdaptiveFg || path == BrandingSetup.AdaptiveBg;
            ti.textureCompression = keep ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
            if (keep) return;
            var format = path.StartsWith(UiDir + "/Shop/") || path.EndsWith("/logo_wide.png") ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4;
            foreach (string platform in new[] { "Android", "iOS" })
            {
                TextureImporterPlatformSettings ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.format = format;
                ti.SetPlatformTextureSettings(ps);
            }
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(UiDir + "/") || !assetPath.EndsWith(".png")) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.isReadable = false;
            ti.sRGBTexture = true;
            ApplyCompression(ti);
            ti.spritePixelsPerUnit = 100f;
            ti.spriteBorder = Vector4.zero;

            Manifest m = LoadManifest();
            if (m == null || m.sprites == null) return;
            string fileName = Path.GetFileName(assetPath);
            for (int i = 0; i < m.sprites.Length; i++)
            {
                Entry e = m.sprites[i];
                if (e == null || e.file != fileName) continue;
                if (e.pixelsPerUnit > 0) ti.spritePixelsPerUnit = e.pixelsPerUnit;
                else if (m.pixelsPerUnit > 0) ti.spritePixelsPerUnit = m.pixelsPerUnit;
                if (e.border != null && e.border.Length >= 4)
                    ti.spriteBorder = new Vector4(e.border[0], e.border[1], e.border[2], e.border[3]); // L, B, R, T
                break;
            }
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            for (int i = 0; i < imported.Length; i++)
            {
                if (imported[i] == JsonPath)
                {
                    EditorApplication.delayCall += ReimportSprites;
                    return;
                }
            }
        }

        [MenuItem("Cafe/UI/Reimport UI Sprites")]
        public static void ReimportSprites()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { UiDir });
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (p.EndsWith(".png")) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
        }

        // ---------- theme ----------

        private static Sprite Sp(string name)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(UiDir + "/" + name + ".png");
            if (s == null) Debug.LogWarning("[UiSpriteImporter] Thiếu sprite " + name + ".png (chạy tools/gen_ui_sprites.py)");
            return s;
        }

        private static Font LoadFont(string file)
        {
            string path = FontDir + "/" + file;
            var fi = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
            if (fi != null && (fi.fontTextureCase != FontTextureCase.Dynamic || !fi.includeFontData))
            {
                fi.fontTextureCase = FontTextureCase.Dynamic;
                fi.includeFontData = true;
                fi.SaveAndReimport();
            }
            Font f = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (f == null) Debug.LogWarning("[UiSpriteImporter] Thiếu font " + path);
            return f;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// Tạo/cập nhật SO_UiTheme từ sprite + font đã nhập. Gán kết quả vào GameBootstrap.uiTheme.
        public static UiTheme EnsureTheme()
        {
            AssetDatabase.Refresh();
            EnsureFolder(ThemeDir);

            UiTheme t = AssetDatabase.LoadAssetAtPath<UiTheme>(ThemePath);
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<UiTheme>();
                AssetDatabase.CreateAsset(t, ThemePath);
            }

            t.heading = LoadFont("Baloo2-ExtraBold.ttf");
            t.body = LoadFont("Nunito-Bold.ttf");

            t.panel = Sp("ui_panel");
            t.chip = Sp("ui_chip");
            t.card = Sp("ui_card");
            t.barTrack = Sp("ui_bar_track");
            t.barFill = Sp("ui_bar_fill");
            t.ringBg = Sp("ui_ring_bg");
            t.ringFill = Sp("ui_ring_fill");
            t.disc = Sp("ui_disc");
            t.bubble = Sp("ui_bubble");
            t.bubbleTail = Sp("ui_bubble_tail");
            t.dim = Sp("ui_dim");

            t.btnRed = Sp("ui_btn_red");
            t.btnRedPressed = Sp("ui_btn_red_p");
            t.btnGreen = Sp("ui_btn_green");
            t.btnGreenPressed = Sp("ui_btn_green_p");
            t.btnGold = Sp("ui_btn_gold");
            t.btnGoldPressed = Sp("ui_btn_gold_p");
            t.btnCream = Sp("ui_btn_cream");
            t.btnCreamPressed = Sp("ui_btn_cream_p");
            t.btnCoffee = Sp("ui_btn_coffee");
            t.btnCoffeePressed = Sp("ui_btn_coffee_p");

            t.iconCoin = Sp("icon_coin");
            t.iconStar = Sp("icon_star");
            t.iconClock = Sp("icon_clock");
            t.iconCup = Sp("icon_cup");
            t.iconPhin = Sp("icon_phin");
            t.iconSuaDac = Sp("icon_sua_dac");
            t.iconSuaTuoi = Sp("icon_sua_tuoi");
            t.iconDa = Sp("icon_da");
            t.iconTra = Sp("icon_tra");
            t.iconCheck = Sp("icon_check");
            t.iconCross = Sp("icon_cross");
            t.iconHeart = Sp("icon_heart");
            t.tutorialHand = Sp("Logo/ui_hand_pointer"); // bàn tay hướng dẫn; fingertip pivot (0.109, 0.93)
            t.iconBroom = Sp("icon_broom"); // chưa có file thì null -> CleaningRules.BroomIcon dùng icon ly
            t.iconMenu = Sp("icon_menu");
            t.iconShop = Sp("icon_shop");
            t.iconBook = Sp("icon_book");
            t.iconMission = Sp("icon_mission");
            t.iconStaff = Sp("icon_staff");
            t.iconHome = Sp("icon_home");
            t.iconClose = Sp("icon_close");
            t.gaugeGlass = Sp("ui_gauge_glass");
            t.gaugeMask = Sp("ui_gauge_mask");
            t.badge = Sp("ui_badge");
            t.tag = Sp("ui_tag");
            t.arrowDown = Sp("ui_arrow");
            t.sparkle = Sp("ui_sparkle");
            t.iconDuong = Sp("icon_duong");
            t.iconMuoi = Sp("icon_muoi");
            t.iconKemMuoi = Sp("icon_kem_muoi");
            t.iconCotDua = Sp("icon_cot_dua");
            t.iconTrung = Sp("icon_trung");
            t.iconChanh = Sp("icon_chanh");
            t.iconLac = Sp("icon_lac");
            t.iconDanh = Sp("icon_danh");
            t.iconCacao = Sp("icon_cacao");

            EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssets();
            return t;
        }

        [MenuItem("Cafe/UI/Ensure UI Theme")]
        public static void EnsureThemeMenu()
        {
            UiTheme t = EnsureTheme();
            Selection.activeObject = t;
            EditorGUIUtility.PingObject(t);
        }
    }
}
