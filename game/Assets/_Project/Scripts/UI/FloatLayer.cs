using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Lớp chứa các chữ nổi (pool cố định, không cấp phát khi chạy). Canvas riêng không có GraphicRaycaster: không chặn chạm.
    /// Kiểu "nhãn kẹo" (docs/hud_visual_polish_2026-10-18.md §5): Baloo, viền coffee 3 px + bóng đổ 0,4 lệch (0, -3), màu sáng;
    /// bật lên 0,6 -> 1,1 -> 1 trong 0,2 s, bay lên rồi mờ. Lời khách (bark) nằm trong bong bóng ui_bubble + đuôi (tối đa 2 cùng lúc).
    /// Banner ruy-băng vàng ("LÊN LEVEL 2! +180 Xu") trượt xuống từ dưới thanh chip, tự ẩn sau 2 s (không đặt giữa màn hình).
    public class FloatLayer : MonoBehaviour
    {
        private const int PoolSize = 14;
        private const float Life = 1.2f;
        private const float Rise = 150f;
        private const float PopUp = 0.1f;
        private const float PopDown = 0.1f;

        // Màu chữ nổi (đọc được trên sàn sáng lẫn tối nhờ viền + bóng)
        private static readonly Color CoinYellow = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        private static readonly Color WarnOrange = new Color32(0xFF, 0x8A, 0x3D, 0xFF);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.4f);

        private class Item
        {
            public RectTransform rt;
            public Text text;
            public Outline outline;
            public Shadow shadow;
            public float age = -1f;
            public Vector2 start;
            public Color color;
            public float rise = Rise;
            public bool world;      // gắn với cảnh (+Xu, -giá): ẩn ngay khi mở bảng
        }

        // Thông báo chung (toast): mỗi lúc chỉ MỘT dòng, dòng tới sau chờ dòng trước hiện đủ TopMinShow rồi thay chỗ
        // (trước đây xếp cách 48 px mà mỗi dòng bay lên 150 px nên dòng cũ bay đè lên dòng mới)
        private const float TopMinShow = 0.8f;
        private const float TopRise = 36f;
        private Item topItem;
        private string pendingTop;
        private Color pendingTopColor;

        // Lời khách (barks): bong bóng kem chữ coffee, tối đa 2 cùng lúc (mới thay cũ nhất)
        private const int BarkPool = 2;
        private const float BarkLife = 2.6f;
        private const float BarkRise = 30f;
        private const float BarkMaxW = 432f;   // 360 ở canvas 900 (FxCanvas tham chiếu 1080)
        private const float BarkPadX = 22f;
        private const float BarkPadY = 14f;
        private const int BarkFont = 26;

        private class Bark
        {
            public RectTransform rt;
            public CanvasGroup group;
            public RectTransform bubble;
            public Text text;
            public float age = -1f;
            public Vector2 start;
        }

        private readonly Bark[] barks = new Bark[BarkPool];
        private int nextBark;

        // Banner ruy-băng
        private const float BannerIn = 0.25f;
        private const float BannerHold = 2f;
        private const float BannerOut = 0.25f;
        private const float BannerTopY = -120f;  // dưới thanh chip (canvas 1080)
        private RectTransform banner;
        private Text bannerText;
        private float bannerAge = -1f;

        private readonly Item[] items = new Item[PoolSize];
        private Canvas canvas;
        private RectTransform layerRect;
        private int next;
        private float lastTopTime = -10f;

        public static FloatLayer Create(Transform parent)
        {
            Canvas c = HudBuilder.CreateCanvas("FxCanvas", 20, false, parent);
            var fl = c.gameObject.AddComponent<FloatLayer>();
            fl.Build(c);
            return fl;
        }

        private void Build(Canvas c)
        {
            canvas = c;
            layerRect = HudBuilder.NewRect("FloatLayer", c.transform);
            HudBuilder.Stretch(layerRect);
            for (int i = 0; i < PoolSize; i++)
            {
                Text t = HudBuilder.AddText(layerRect, "Float" + i, "", FloatFontSize, TextAnchor.MiddleCenter, HudBuilder.Accent, true, true);
                t.rectTransform.anchorMin = Vector2.zero;
                t.rectTransform.anchorMax = Vector2.zero;
                t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                t.rectTransform.sizeDelta = new Vector2(900f, 90f);
                var it = new Item();
                it.rt = t.rectTransform;
                it.text = t;
                it.outline = t.GetComponent<Outline>();
                if (it.outline != null) it.outline.effectDistance = new Vector2(3f, -3f);
                it.shadow = t.gameObject.AddComponent<Shadow>();
                it.shadow.effectColor = ShadowColor;
                it.shadow.effectDistance = new Vector2(0f, -3f);
                t.gameObject.SetActive(false);
                items[i] = it;
            }
            for (int i = 0; i < BarkPool; i++) barks[i] = BuildBark(i);
            BuildBanner();
            UiFx.Register(this);
        }

        private Bark BuildBark(int i)
        {
            var b = new Bark();
            b.rt = HudBuilder.NewRect("Bark" + i, layerRect);
            b.rt.anchorMin = Vector2.zero;
            b.rt.anchorMax = Vector2.zero;
            b.rt.pivot = new Vector2(0.5f, 0f);
            b.rt.sizeDelta = new Vector2(BarkMaxW, 80f);
            b.group = b.rt.gameObject.AddComponent<CanvasGroup>();
            b.group.blocksRaycasts = false;
            b.group.interactable = false;

            // đuôi ở đáy giữa, bong bóng ngay trên
            Image tail = HudBuilder.Img(b.rt, "Tail", HudBuilder.T.bubbleTail, HudBuilder.Cream, false, false);
            HudBuilder.SetFixed(tail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(34f, 24f));
            Image bub = HudBuilder.Img(b.rt, "Bubble", HudBuilder.T.bubble, HudBuilder.Cream, true, false);
            bub.pixelsPerUnitMultiplier = 2f; // viền 9-slice 44/52 px -> 22/26: bong bóng một dòng vẫn tròn trịa
            b.bubble = bub.rectTransform;
            HudBuilder.SetRect(b.bubble, Vector2.zero, Vector2.one, new Vector2(0f, 18f), Vector2.zero);
            tail.rectTransform.SetAsLastSibling(); // đuôi đè lên mép dưới bong bóng (che đường viền chỗ nối)

            b.text = HudBuilder.AddText(b.bubble, "Text", "", BarkFont, TextAnchor.MiddleCenter, HudBuilder.Ink, false, false);
            HudBuilder.SetRect(b.text.rectTransform, Vector2.zero, Vector2.one, new Vector2(BarkPadX, BarkPadY), new Vector2(-BarkPadX, -BarkPadY));
            b.text.horizontalOverflow = HorizontalWrapMode.Wrap;
            b.text.verticalOverflow = VerticalWrapMode.Overflow;
            b.rt.gameObject.SetActive(false);
            return b;
        }

        private void BuildBanner()
        {
            Image img = HudBuilder.Img(layerRect, "Banner", HudBuilder.T.card, HudBuilder.Accent, true, false);
            img.color = HudBuilder.T.card != null ? new Color(1f, 0.83f, 0.38f, 1f) : HudBuilder.Accent; // thẻ kem nhuộm vàng
            banner = img.rectTransform;
            HudBuilder.SetFixed(banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, BannerTopY), new Vector2(760f, 96f));
            bannerText = HudBuilder.AddText(banner, "Text", "", 40, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(bannerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 14f), new Vector2(-28f, -6f));
            HudBuilder.Fit(bannerText, 22);
            banner.gameObject.SetActive(false);
        }

        /// Màu chữ nổi kiểu mới: vàng cho Xu, kem cho tin tốt / "Thu ly!" (mint khó đọc trên sàn be), cam cho cảnh báo.
        private static Color CandyColor(Color c)
        {
            if (Near(c, HudBuilder.Accent)) return CoinYellow;
            if (Near(c, HudBuilder.Mint) || Near(c, HudBuilder.Good)) return HudBuilder.Cream;
            if (Near(c, HudBuilder.Red) || Near(c, HudBuilder.Bad)) return WarnOrange;
            return c;
        }

        private static bool Near(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
        }

        /// Lời thoại của khách (barks) tại vị trí màn hình (pixel): bong bóng trên đầu khách, không chặn chạm.
        public void SpawnBark(Vector2 screenPos, string msg, Color color)
        {
            if (ModalMarker.AnyOpen) return; // canvas này nằm trên HUD: không vẽ lời khách đè lên bảng đang mở
            float k = canvas != null && canvas.scaleFactor > 0.01f ? 1f / canvas.scaleFactor : 1f;
            Bark b = barks[nextBark];
            nextBark = (nextBark + 1) % BarkPool;
            b.text.text = msg;

            // Bề rộng theo chữ (một dòng nếu vừa), tối đa BarkMaxW rồi xuống dòng
            float inner = BarkMaxW - 2f * BarkPadX;
            TextGenerationSettings gs = b.text.GetGenerationSettings(new Vector2(10000f, 0f));
            float lineW = b.text.cachedTextGeneratorForLayout.GetPreferredWidth(msg, gs) / b.text.pixelsPerUnit;
            float w = Mathf.Min(inner, Mathf.Ceil(lineW) + 2f);
            gs = b.text.GetGenerationSettings(new Vector2(w, 0f));
            float h = b.text.cachedTextGeneratorForLayout.GetPreferredHeight(msg, gs) / b.text.pixelsPerUnit;
            b.rt.sizeDelta = new Vector2(w + 2f * BarkPadX, Mathf.Ceil(h) + 2f * BarkPadY + 18f);

            b.age = 0f;
            b.start = screenPos * k;
            b.rt.anchoredPosition = b.start;
            b.rt.localScale = Vector3.one;
            b.group.alpha = 0f;
            b.rt.SetAsLastSibling();
            b.rt.gameObject.SetActive(true);
        }

        /// Banner ruy-băng vàng dưới thanh chip; đang hiện thì thay chữ và tính lại 2 s.
        public void ShowBanner(string msg)
        {
            bannerText.text = msg;
            if (bannerAge < 0f || bannerAge > BannerIn) bannerAge = bannerAge < 0f ? 0f : BannerIn; // đang hiện: giữ nguyên chỗ, chỉ kéo dài
            banner.SetAsLastSibling();
            banner.gameObject.SetActive(true);
            PlaceBanner();
        }

        private void OnDestroy()
        {
            UiFx.Unregister(this);
        }

        public void Spawn(Vector2 screenPos, string msg, Color color)
        {
            if (ModalMarker.AnyOpen) return; // chữ nổi gắn với cảnh (+Xu, -giá) không đè lên bảng đang mở
            float k = canvas != null && canvas.scaleFactor > 0.01f ? 1f / canvas.scaleFactor : 1f;
            Item it = SpawnCanvas(screenPos * k, msg, color);
            it.world = true;
        }

        private string lastTopMsg;

        public void SpawnTop(string msg, Color color)
        {
            float now = Time.unscaledTime;
            if (msg == lastTopMsg && now - lastTopTime < 1.5f) return; // chống spam khi chạm liên tục
            lastTopMsg = msg;
            lastTopTime = now;
            if (topItem != null && topItem.age >= 0f && topItem.age < TopMinShow)
            {
                pendingTop = msg; // chờ dòng đang hiện; nhiều dòng dồn thì giữ dòng mới nhất
                pendingTopColor = color;
                return;
            }
            ShowTop(msg, color);
        }

        private void ShowTop(string msg, Color color)
        {
            if (topItem != null && topItem.age >= 0f)
            {
                topItem.age = -1f;
                topItem.text.gameObject.SetActive(false);
            }
            Vector2 size = layerRect.rect.size;
            if (size.x < 10f || size.y < 10f) size = new Vector2(1920f, 1080f);
            // HUD gọn (hud_brew_redesign A2): toast nhỏ ngay trên mép bàn pha thay vì chữ to giữa màn hình
            if (UiFx.ToastScreenY > 0f)
            {
                float k = canvas != null && canvas.scaleFactor > 0.01f ? 1f / canvas.scaleFactor : 1f;
                topItem = SpawnCanvas(new Vector2(size.x * 0.5f, UiFx.ToastScreenY * k), msg, color, ToastFontSize, TopRise, false);
                return;
            }
            // toạ độ canvas: giữa ngang, dưới thanh chip (cao ~110px)
            topItem = SpawnCanvas(new Vector2(size.x * 0.5f, size.y - 330f), msg, color, FloatFontSize, TopRise, false);
        }

        /// Cỡ chữ toast chung (canvas 1080): ~26 px ở 720. Chữ nổi thường giữ 54.
        private const int ToastFontSize = 40;
        private const int FloatFontSize = 54;

        private Item SpawnCanvas(Vector2 canvasPos, string msg, Color color, int fontSize = FloatFontSize,
                                 float rise = Rise, bool avoidStack = true)
        {
            if (avoidStack) canvasPos = AvoidStack(canvasPos);
            Item it = items[next];
            next = (next + 1) % PoolSize;
            if (it == topItem) topItem = null; // pool quay vòng chiếm mất dòng toast đang hiện
            it.rise = rise;
            it.world = false;
            it.text.fontSize = fontSize;
            it.age = 0f;
            it.start = canvasPos;
            it.color = CandyColor(color);
            it.text.text = msg;
            it.text.color = it.color;
            it.rt.anchoredPosition = canvasPos;
            it.rt.localScale = new Vector3(0.6f, 0.6f, 1f);
            it.rt.SetAsLastSibling();
            it.text.gameObject.SetActive(true);
            return it;
        }

        private const float StackGap = 46f;

        /// Chữ mới sinh gần chỗ một chữ đang bay thì đẩy lên trên nó, để "+1 Ly" liên tiếp không đè nhau.
        private Vector2 AvoidStack(Vector2 pos)
        {
            for (int guard = 0; guard < PoolSize; guard++)
            {
                bool hit = false;
                for (int i = 0; i < PoolSize; i++)
                {
                    Item o = items[i];
                    if (o == null || o.age < 0f || o == topItem) continue;
                    Vector2 cur = o.rt.anchoredPosition; // vị trí hiện tại (đã bay lên), không phải điểm xuất phát
                    if (Mathf.Abs(cur.x - pos.x) < 160f && Mathf.Abs(cur.y - pos.y) < StackGap * 0.9f)
                    {
                        pos.y = cur.y + StackGap; // đặt trên: chữ mới bay nhanh hơn nên càng xa chữ cũ
                        hit = true;
                    }
                }
                if (!hit) break;
            }
            return pos;
        }

        private void HideSceneFloats()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                Item it = items[i];
                if (it.age < 0f || !it.world) continue;
                it.age = -1f;
                it.text.gameObject.SetActive(false);
            }
            for (int i = 0; i < BarkPool; i++)
            {
                Bark b = barks[i];
                if (b.age < 0f) continue;
                b.age = -1f;
                b.rt.gameObject.SetActive(false);
            }
        }

        private void UpdateBarks(float dt)
        {
            for (int i = 0; i < BarkPool; i++)
            {
                Bark b = barks[i];
                if (b.age < 0f) continue;
                b.age += dt;
                if (b.age >= BarkLife)
                {
                    b.age = -1f;
                    b.rt.gameObject.SetActive(false);
                    continue;
                }
                float t = b.age / BarkLife;
                b.rt.anchoredPosition = b.start + new Vector2(0f, BarkRise * (1f - (1f - t) * (1f - t)));
                float pop = Mathf.Clamp01(b.age / 0.15f);
                float s = 0.85f + 0.15f * pop;
                b.rt.localScale = new Vector3(s, s, 1f);
                b.group.alpha = b.age < 2f ? pop : 1f - (b.age - 2f) / (BarkLife - 2f);
            }
        }

        private void UpdateBanner(float dt)
        {
            if (bannerAge < 0f) return;
            bannerAge += dt;
            if (bannerAge >= BannerIn + BannerHold + BannerOut)
            {
                bannerAge = -1f;
                banner.gameObject.SetActive(false);
                return;
            }
            PlaceBanner();
        }

        /// Trượt xuống (vào) / đứng / trượt lên (ra); "Giảm chuyển động" thì chỉ hiện / ẩn.
        private void PlaceBanner()
        {
            float hide = banner.sizeDelta.y + 20f;
            float y = BannerTopY;
            if (!GraphicsQuality.ReduceMotion)
            {
                if (bannerAge < BannerIn)
                {
                    float k = bannerAge / BannerIn;
                    y = BannerTopY + hide * (1f - k) * (1f - k);
                }
                else if (bannerAge > BannerIn + BannerHold)
                {
                    float k = (bannerAge - BannerIn - BannerHold) / BannerOut;
                    y = BannerTopY + hide * k * k;
                }
            }
            banner.anchoredPosition = new Vector2(0f, y);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            UpdateBarks(dt);
            UpdateBanner(dt);
            if (ModalMarker.AnyOpen) HideSceneFloats(); // bảng vừa mở: chữ nổi / lời khách đang bay không được nằm đè lên bảng
            if (pendingTop != null && (topItem == null || topItem.age < 0f || topItem.age >= TopMinShow))
            {
                string m = pendingTop;
                pendingTop = null;
                ShowTop(m, pendingTopColor);
            }
            for (int i = 0; i < PoolSize; i++)
            {
                Item it = items[i];
                if (it.age < 0f) continue;
                it.age += dt;
                if (it.age >= Life)
                {
                    it.age = -1f;
                    it.text.gameObject.SetActive(false);
                    continue;
                }
                float t = it.age / Life;
                float e = 1f - (1f - t) * (1f - t);
                it.rt.anchoredPosition = it.start + new Vector2(0f, it.rise * e);

                // bật lên 0,6 -> 1,1 -> 1 trong 0,2 s
                float s;
                if (it.age < PopUp) s = Mathf.Lerp(0.6f, 1.1f, it.age / PopUp);
                else if (it.age < PopUp + PopDown) s = Mathf.Lerp(1.1f, 1f, (it.age - PopUp) / PopDown);
                else s = 1f;
                it.rt.localScale = new Vector3(s, s, 1f);

                float a = it.age < 0.7f ? 1f : 1f - (it.age - 0.7f) / (Life - 0.7f);
                Color c = it.color;
                c.a = a;
                it.text.color = c;
                if (it.outline != null)
                {
                    Color oc = HudBuilder.OutlineColor;
                    oc.a = a;
                    it.outline.effectColor = oc;
                }
                Color sc = ShadowColor;
                sc.a *= a;
                it.shadow.effectColor = sc;
            }
        }
    }
}
