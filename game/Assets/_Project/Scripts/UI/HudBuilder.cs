using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cafe
{
    /// Thanh tiến độ clay: nền (track) + vùng fill thay đổi anchorMax.x.
    public class BarView
    {
        public RectTransform root;
        public RectTransform fill;
        public Image fillImage;

        public void Set(float v, Color color)
        {
            v = Mathf.Clamp01(v);
            fill.anchorMax = new Vector2(v, 1f);
            fillImage.enabled = v > 0.015f;
            fillImage.color = color;
        }
    }

    /// Hàm dựng uGUI bằng code (không prefab). Phong cách "Clay Café" (docs/ui_style.md).
    /// Mọi sprite/font lấy từ UiTheme; theme null -> font mặc định + Image trơn.
    public static class HudBuilder
    {
        public static Color Cream = new Color32(0xFF, 0xF4, 0xDC, 0xFF);
        public static Color CreamDark = new Color32(0xEF, 0xE3, 0xC8, 0xFF);
        public static Color Ink = new Color32(0x4A, 0x2C, 0x1E, 0xFF);       // coffee
        public static Color InkSoft = new Color32(0x6B, 0x40, 0x26, 0xFF);   // coffee_md
        public static Color Red = new Color32(0xD9, 0x36, 0x2B, 0xFF);
        public static Color Good = new Color32(0x2F, 0xA0, 0x5A, 0xFF);
        public static Color GoodDark = new Color32(0x1F, 0x74, 0x40, 0xFF);
        public static Color Accent = new Color32(0xF2, 0xB9, 0x3B, 0xFF);    // gold
        public static Color Mint = new Color32(0x9C, 0xD3, 0xB5, 0xFF);
        public static Color Bad = new Color32(0xD6, 0x28, 0x28, 0xFF);       // danger
        public static readonly Color OutlineColor = new Color32(0x4A, 0x2C, 0x1E, 0xFF);

        public static UiTheme Theme { get; private set; }

        private static UiTheme emptyTheme;
        private static Font fallbackFont;

        /// Theme không bao giờ null (không có theme thì là theme rỗng: toàn sprite/font null).
        public static UiTheme T
        {
            get
            {
                if (Theme != null) return Theme;
                if (emptyTheme == null)
                {
                    emptyTheme = ScriptableObject.CreateInstance<UiTheme>();
                    emptyTheme.hideFlags = HideFlags.HideAndDontSave;
                }
                return emptyTheme;
            }
        }

        public static void SetTheme(UiTheme t)
        {
            Theme = t;
            if (t == null) return;
            Cream = t.cream;
            CreamDark = t.creamDark;
            Ink = t.coffee;
            InkSoft = t.coffeeMid;
            Red = t.red;
            Good = t.green;
            Accent = t.gold;
            Mint = t.mint;
            Bad = t.danger;
        }

        private static Font Fallback
        {
            get
            {
                if (fallbackFont == null) fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return fallbackFont;
            }
        }

        public static Font HeadingFont
        {
            get { return (Theme != null && Theme.heading != null) ? Theme.heading : Fallback; }
        }

        public static Font BodyFont
        {
            get { return (Theme != null && Theme.body != null) ? Theme.body : Fallback; }
        }

        // ---------- canvas / rect ----------

        /// Chiều cao tham chiếu của canvas HUD: 900 (thay vì 1080) để nút/chữ to ~1,2 lần trên điện thoại.
        /// Bảng cao hơn 900 tự thu nhỏ qua FitInCanvas.
        public const float HudRefHeight = 900f;

        /// Canvas ScreenSpaceOverlay, CanvasScaler 16:9 theo chiều cao (mặc định 1080), match height.
        public static Canvas CreateCanvas(string name, int sortingOrder, bool withRaycaster, Transform parent, float refHeight = 1080f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(refHeight * 16f / 9f, refHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = CanvasAspectMatch.MatchFor(Screen.width, Screen.height);
            go.AddComponent<CanvasAspectMatch>(); // màn hẹp hơn 16:9 thì match width để HUD không đè nhau
            if (withRaycaster) go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// Đặt theo anchor tỉ lệ và offset pixel.
        public static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        /// Đặt rect cố định kích thước tại anchor/pivot.
        public static void SetFixed(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        // ---------- hình ----------

        public static Image AddImage(RectTransform rt, Color color, bool raycast)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// Image dùng sprite (9-slice nếu sliced). Không có sprite thì tô màu fallback.
        public static Image Img(Transform parent, string name, Sprite sprite, Color fallback, bool sliced, bool raycast)
        {
            RectTransform rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = raycast;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
                img.color = Color.white;
            }
            else
            {
                img.color = fallback;
            }
            return img;
        }

        /// Lớp phủ tối toàn màn hình sau hộp thoại. Img() ép màu trắng khi có sprite nên phải gán màu sau (bản cũ để lộ nền trắng).
        public static Image Dim(Transform parent, string name, float alpha)
        {
            Image dim = Img(parent, name, T.dim, new Color(0f, 0f, 0f, alpha), false, true);
            dim.color = new Color(0f, 0f, 0f, alpha);
            return dim;
        }

        /// Icon vuông: không có sprite thì ẩn.
        public static Image Icon(Transform parent, string name, Sprite sprite, float size)
        {
            Image img = Img(parent, name, sprite, Color.white, false, false);
            img.preserveAspect = true;
            img.enabled = sprite != null;
            if (size > 0f) img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        public static void SetIcon(Image img, Sprite s)
        {
            img.sprite = s;
            img.enabled = s != null;
        }

        /// Panel kem 9-slice (panel khối cho HUD).
        public static Image Panel(Transform parent, string name, bool raycast)
        {
            return Img(parent, name, T.panel, new Color(Cream.r, Cream.g, Cream.b, 0.95f), true, raycast);
        }

        public static Image Chip(Transform parent, string name, bool raycast)
        {
            return Img(parent, name, T.chip, new Color(Cream.r, Cream.g, Cream.b, 0.95f), true, raycast);
        }

        public static RectTransform Panel(Transform parent, string name, Color color, bool raycast)
        {
            RectTransform rt = NewRect(name, parent);
            AddImage(rt, color, raycast);
            return rt;
        }

        /// Thanh tiến độ clay. fill nằm trong khung inset 3 px của track.
        public static BarView CreateBar(Transform parent, string name)
        {
            var bar = new BarView();
            Image track = Img(parent, name, T.barTrack, new Color(0f, 0f, 0f, 0.35f), true, false);
            bar.root = track.rectTransform;
            RectTransform inner = NewRect("Inner", bar.root);
            SetRect(inner, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            Image fi = Img(inner, "Fill", T.barFill, Good, true, false);
            bar.fill = fi.rectTransform;
            bar.fill.anchorMin = Vector2.zero;
            bar.fill.anchorMax = new Vector2(1f, 1f);
            bar.fill.offsetMin = Vector2.zero;
            bar.fill.offsetMax = Vector2.zero;
            bar.fillImage = fi;
            return bar;
        }

        /// Vòng kiên nhẫn: nền + fill radial. Trả về ảnh fill (đặt fillAmount).
        public static Image CreateRing(Transform parent, string name, float size, out RectTransform root)
        {
            Image bg = Img(parent, name, T.ringBg, new Color(0f, 0f, 0f, 0.3f), false, false);
            bg.rectTransform.sizeDelta = new Vector2(size, size);
            root = bg.rectTransform;
            Image fill = Img(bg.transform, "Fill", T.ringFill, Good, false, false);
            Stretch(fill.rectTransform);
            if (T.ringFill != null)
            {
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Radial360;
                fill.fillOrigin = (int)Image.Origin360.Top;
                fill.fillClockwise = true;
                fill.fillAmount = 1f;
            }
            return fill;
        }

        public static void SetRing(Image fill, float v)
        {
            v = Mathf.Clamp01(v);
            if (fill.type == Image.Type.Filled) fill.fillAmount = v;
            fill.color = PatienceColor(v);
        }

        public static Color PatienceColor(float v)
        {
            if (v < 0.3f) return Bad;
            if (v < 0.6f) return Accent;
            return Good;
        }

        // ---------- chữ ----------

        /// Chữ UI. heading = Baloo 2 (số, tiêu đề, nhãn nút), ngược lại Nunito. outline = viền coffee cho chữ trên nền màu/cảnh 3D.
        public static Text AddText(Transform parent, string name, string text, int size, TextAnchor anchor, Color color, bool heading, bool outline)
        {
            RectTransform rt = NewRect(name, parent);
            Stretch(rt);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = heading ? HeadingFont : BodyFont;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = OutlineColor;
                o.effectDistance = new Vector2(2f, -2f);
                o.enabled = GraphicsQuality.UiOutlines; // mức Thấp bỏ viền chữ (nhân bốn số đỉnh chữ)
            }
            return t;
        }

        /// Thu nhỏ chữ cho vừa khung (ô hẹp).
        public static void Fit(Text t, int minSize)
        {
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = minSize;
            t.resizeTextMaxSize = t.fontSize;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
        }

        // ---------- nút ----------

        private static Color StyleColor(ClayStyle s)
        {
            switch (s)
            {
                case ClayStyle.Red: return Red;
                case ClayStyle.Green: return Good;
                case ClayStyle.Gold: return Accent;
                case ClayStyle.Cream: return Cream;
                default: return InkSoft;
            }
        }

        /// Nút clay trần: Image(9-slice) + Button + ClayButton, con "Content" lệch lên để căn giữa mặt nút.
        public static ClayButton AddClayButton(Transform parent, string name, Sprite normal, Sprite pressed, Color fallback, UnityAction onClick)
        {
            RectTransform rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = true;
            if (normal != null)
            {
                img.sprite = normal;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = fallback;
            }
            var btn = rt.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = img;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            if (onClick != null)
            {
                btn.onClick.AddListener(Audio.UiTap); // chỉ nút có onClick: nút giữ / vuốt / xoay tự có tiếng bước pha
                btn.onClick.AddListener(onClick);
            }

            RectTransform content = NewRect("Content", rt);
            SetRect(content, Vector2.zero, Vector2.one, new Vector2(0f, 20f), Vector2.zero);

            var cb = rt.gameObject.AddComponent<ClayButton>();
            cb.Init(img, normal, pressed, content, btn, normal != null ? Color.white : fallback);
            return cb;
        }

        public static ClayButton AddClayButton(Transform parent, string name, ClayStyle style, UnityAction onClick)
        {
            return AddClayButton(parent, name, T.Button(style, false), T.Button(style, true), StyleColor(style), onClick);
        }

        /// Nút clay có nhãn Baloo 2.
        public static ClayButton AddLabelButton(Transform parent, string name, ClayStyle style, string label, int fontSize, UnityAction onClick, out Text labelText)
        {
            ClayButton b = AddClayButton(parent, name, style, onClick);
            bool light = style == ClayStyle.Gold || style == ClayStyle.Cream;
            Text t = AddText(b.Content, "Label", label, fontSize, TextAnchor.MiddleCenter, light ? Ink : Cream, true, !light);
            t.rectTransform.offsetMin = new Vector2(10f, 0f);
            t.rectTransform.offsetMax = new Vector2(-10f, 0f);
            Fit(t, 20);
            labelText = t;
            return b;
        }
    }

    /// Thu nhỏ bảng cao hơn khung chứa nó (trừ lề 40; khung thường là vùng an toàn) cho vừa màn hình;
    /// giữ nguyên bố cục bên trong.
    public class FitInCanvas : MonoBehaviour
    {
        private RectTransform rt;

        private void LateUpdate()
        {
            if (rt == null) rt = (RectTransform)transform;
            var host = rt.parent as RectTransform;
            if (host == null) return;
            float avail = host.rect.height - 40f;
            float h = rt.rect.height;
            float k = h > avail && h > 0f ? avail / h : 1f;
            if (!Mathf.Approximately(rt.localScale.x, k)) rt.localScale = new Vector3(k, k, 1f);
        }
    }
}
