using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Icon dự phòng cho nguyên liệu Tier 2 chưa có sprite (bộ sprite do session khác sở hữu):
    /// đĩa tròn tạo lúc chạy, nhuộm màu theo nguyên liệu + nhãn 2 chữ. Nguyên liệu Tier 1 vẫn dùng sprite của UiTheme.
    public static class StepIconFallback
    {
        private const int DiscSize = 64;

        private static Texture2D discTex;
        private static Sprite discSprite;

        /// Đĩa tròn trắng viền xám (nhân với màu Image). Tạo lười, không tạo trong field initializer.
        public static Sprite Disc
        {
            get
            {
                if (discSprite != null) return discSprite;
                discTex = new Texture2D(DiscSize, DiscSize, TextureFormat.RGBA32, false);
                discTex.name = "StepIconDisc";
                discTex.hideFlags = HideFlags.HideAndDontSave;
                discTex.wrapMode = TextureWrapMode.Clamp;
                discTex.filterMode = FilterMode.Bilinear;
                var px = new Color32[DiscSize * DiscSize];
                float c = (DiscSize - 1) * 0.5f;
                float rOut = DiscSize * 0.5f - 1f;
                float rRim = rOut - 4f;
                for (int y = 0; y < DiscSize; y++)
                {
                    for (int x = 0; x < DiscSize; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                        float a = Mathf.Clamp01(rOut - d + 0.5f);              // mép khử răng cưa
                        float rim = Mathf.Clamp01((d - rRim) * 0.5f);          // viền tối dần ra ngoài
                        byte g = (byte)Mathf.RoundToInt(Mathf.Lerp(255f, 150f, rim));
                        px[y * DiscSize + x] = new Color32(g, g, g, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }
                discTex.SetPixels32(px);
                discTex.Apply(false, false);
                discSprite = Sprite.Create(discTex, new Rect(0f, 0f, DiscSize, DiscSize), new Vector2(0.5f, 0.5f), 100f);
                discSprite.name = "StepIconDisc";
                discSprite.hideFlags = HideFlags.HideAndDontSave;
                return discSprite;
            }
        }

        /// 5 nguyên liệu đã có sprite trong UiTheme.
        public static bool HasThemeIcon(BrewStep s)
        {
            return s == BrewStep.Phin || s == BrewStep.SuaDac || s == BrewStep.SuaTuoi || s == BrewStep.Da || s == BrewStep.Tra;
        }

        public static Color TintFor(BrewStep s)
        {
            switch (s)
            {
                case BrewStep.Duong: return new Color(1f, 0.97f, 0.88f, 1f);
                case BrewStep.Muoi: return new Color(0.82f, 0.9f, 1f, 1f);
                case BrewStep.KemMuoi: return new Color(1f, 0.93f, 0.74f, 1f);
                case BrewStep.CotDua: return new Color(0.96f, 0.9f, 0.76f, 1f);
                case BrewStep.Trung: return new Color(1f, 0.8f, 0.2f, 1f);
                case BrewStep.Chanh: return new Color(0.7f, 0.9f, 0.28f, 1f);
                case BrewStep.Lac: return new Color(0.72f, 0.76f, 0.84f, 1f);
                case BrewStep.Danh: return new Color(0.94f, 0.7f, 0.5f, 1f);
                case BrewStep.Cacao: return new Color(0.5f, 0.3f, 0.2f, 1f);
                default: return Color.white;
            }
        }

        public static string TagFor(BrewStep s)
        {
            switch (s)
            {
                case BrewStep.Duong: return "Đg";
                case BrewStep.Muoi: return "Mu";
                case BrewStep.KemMuoi: return "KM";
                case BrewStep.CotDua: return "CD";
                case BrewStep.Trung: return "Tr";
                case BrewStep.Chanh: return "Ch";
                case BrewStep.Lac: return "Lc";
                case BrewStep.Danh: return "Đn";
                case BrewStep.Cacao: return "Ca";
                default: return "";
            }
        }

        /// Gán icon cho một Image: sprite theme nếu có, ngược lại đĩa nhuộm màu + nhãn. Dùng cho mọi chỗ hiện bước pha.
        public static void Apply(Image img, BrewStep step)
        {
            if (img == null) return;
            Sprite themed = HasThemeIcon(step) ? HudBuilder.T.IconFor(step) : null;
            Transform tag = img.transform.Find("Tag");
            if (themed != null)
            {
                img.sprite = themed;
                img.color = Color.white;
                img.enabled = true;
                if (tag != null) tag.gameObject.SetActive(false);
                return;
            }

            img.sprite = Disc;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            Color tint = TintFor(step);
            img.color = tint;
            img.enabled = true;

            Text t;
            if (tag == null)
            {
                t = HudBuilder.AddText(img.transform, "Tag", "", 24, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
                HudBuilder.Fit(t, 12);
            }
            else
            {
                t = tag.GetComponent<Text>();
                tag.gameObject.SetActive(true);
            }
            if (t != null)
            {
                t.text = TagFor(step);
                float lum = tint.r * 0.3f + tint.g * 0.59f + tint.b * 0.11f;
                t.color = lum < 0.45f ? HudBuilder.Cream : HudBuilder.Ink;
            }
        }

        /// Ô trống: ẩn icon và nhãn.
        public static void Clear(Image img)
        {
            if (img == null) return;
            img.enabled = false;
            Transform tag = img.transform.Find("Tag");
            if (tag != null) tag.gameObject.SetActive(false);
        }

        /// Icon món cho Sổ công thức: sprite theme theo nguyên liệu đặc trưng; silhouette = bóng đen mờ (món chưa mở).
        public static void ApplyRecipe(Image img, RecipeDef r, bool silhouette)
        {
            if (img == null) return;
            Transform tag = img.transform.Find("Tag");
            if (tag != null) tag.gameObject.SetActive(false);
            Sprite s = silhouette ? HudBuilder.T.iconCup : HudBuilder.T.IconFor(r);
            if (s == null) s = Disc;
            img.sprite = s;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = silhouette ? new Color(0.16f, 0.1f, 0.07f, 0.7f) : Color.white;
            img.enabled = true;
        }

        /// Lớp phủ tiến độ (quạt tròn hoặc mực dâng) đè lên nút giữ/vuốt/xoay. Ẩn sẵn, không bắt chạm.
        public static Image CreateFillOverlay(Transform parent, Image.FillMethod method, float size)
        {
            Image img = HudBuilder.Img(parent, "FillOverlay", Disc, Color.white, false, false);
            img.color = new Color(1f, 1f, 1f, 0.6f);
            img.type = Image.Type.Filled;
            img.fillMethod = method;
            if (method == Image.FillMethod.Vertical) img.fillOrigin = (int)Image.OriginVertical.Bottom;
            else img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.fillAmount = 0f;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -37f); // trùng tâm icon 66 px đặt cách mép trên 4 px
            rt.sizeDelta = new Vector2(size, size);
            img.enabled = false;
            return img;
        }
    }

    /// Tiện ích dùng chung cho các component thao tác (giữ/vuốt/xoay).
    internal static class StepGestureUtil
    {
        private static float lastHintTime = -10f;

        public static float CanvasScale(Component c)
        {
            Canvas cv = c.GetComponentInParent<Canvas>();
            if (cv == null) return 1f;
            float s = cv.rootCanvas.scaleFactor;
            return s > 0.01f ? s : 1f;
        }

        /// Nhắc người chơi (toast) nhưng không quá dày.
        public static void Hint(string msg)
        {
            float now = Time.unscaledTime;
            if (now - lastHintTime < 1.2f) return;
            lastHintTime = now;
            GameEvents.RaiseToast(msg);
        }

        public static void SetFill(Image img, float amount, Color color)
        {
            if (img == null) return;
            bool show = amount > 0.001f;
            if (img.enabled != show) img.enabled = show;
            img.fillAmount = Mathf.Clamp01(amount);
            img.color = new Color(color.r, color.g, color.b, 0.62f); // mờ để vẫn thấy icon bên dưới
        }
    }
}
