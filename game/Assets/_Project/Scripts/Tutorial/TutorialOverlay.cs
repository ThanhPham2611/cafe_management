using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    public enum HandMode { None, Tap, Hold }

    /// Mục tiêu bàn tay chỉ vào: một RectTransform UI hoặc một điểm 3D chiếu ra màn hình mỗi khung (như OrderBubble).
    public sealed class TutorialTarget
    {
        public Func<RectTransform> Rect;
        public Func<Transform> World;
        public Vector3 WorldOffset;
        public Vector2 Box = new Vector2(240f, 280f); // kích thước vùng sáng cho mục tiêu 3D (đơn vị canvas)

        public static TutorialTarget Ui(Func<RectTransform> rect)
        {
            return new TutorialTarget { Rect = rect };
        }

        public static TutorialTarget At(Func<Transform> world, Vector3 offset, Vector2 box)
        {
            return new TutorialTarget { World = world, WorldOffset = offset, Box = box };
        }
    }

    /// Một lời nhắc đang hiện: chữ + mục tiêu + có chặn chạm ngoài vùng sáng hay không.
    public sealed class TutorialPrompt
    {
        public string text;
        public TutorialTarget target;
        /// Làm tối màn hình và chặn chạm ngoài vùng sáng (chỉ khi mục tiêu tìm thấy, để không bao giờ kẹt).
        public bool block;
        /// Chặn toàn màn hình kể cả khi không có mục tiêu (hộp thoại chào).
        public bool modal;
        /// Vùng sáng thứ hai cũng cho chạm xuyên (vd. thẻ đơn trong cột đơn khi bước là "chạm khách"). Không vẽ khung / tay.
        public TutorialTarget altTarget;
        /// Bảng đang được giải thích: thẻ lời thoại không được che nó (đặt gọn cạnh bên nếu còn chỗ).
        public Func<RectTransform> avoid;
        public HandMode hand = HandMode.Tap;
        public bool showNext;
        public string nextLabel;
        public bool showSkip;
        public Action onNext;
    }

    /// Lớp phủ hướng dẫn: màn tối có "lỗ" (4 Image quanh hình chữ nhật, không cần shader), khung sáng nhấp nháy,
    /// bàn tay động và thẻ lời thoại của bà Sáu. Canvas riêng, nằm trên mọi UI khác.
    /// Chặn chạm: các Image tối có raycastTarget nên TapInput.IsOverUI coi là UI -> chạm ngoài lỗ không xuống thế giới 3D;
    /// trong lỗ không có Graphic nào nên chạm xuyên xuống nút / khách bên dưới.
    public sealed class TutorialOverlay : MonoBehaviour
    {
        private const float CardW = 820f;
        private const float CardH = 230f;
        private const float HolePad = 14f;
        private const float CompactW = 420f;
        private const float CompactH = 340f;
        private const float MinSideW = 300f;   // dải cạnh bảng hẹp hơn mức này thì đặt thẻ ở đáy màn hình
        private const float BottomW = 600f;    // thẻ ở đáy màn hình khi bảng modal (tổng kết) đang mở
        private const float BottomH = 220f;
        private const float BottomMargin = 16f;
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.62f);

        private static Sprite fallbackDot;

        private Canvas canvas;
        private RectTransform contentRoot;
        private readonly List<Image> dimPool = new List<Image>();
        private readonly List<Vector4> pieces = new List<Vector4>();
        private readonly List<Vector4> scratch = new List<Vector4>();
        private readonly List<Vector4> holes = new List<Vector4>();
        private readonly float[] avoidRects = new float[12];
        private bool cardCompact;
        private readonly Image[] frame = new Image[4];
        private Image dimFull;
        private Image hand;
        private RectTransform card;
        private Text speakerText;
        private Text bodyText;
        private ClayButton nextButton;
        private Text nextLabel;
        private ClayButton skipButton;
        private GameObject confirmRoot;
        private Action onSkipConfirmed;

        private TutorialPrompt prompt;
        private bool suppressed;
        private float shownAt;
        private bool handHasSprite;
        private Camera cam;
        private readonly Vector3[] corners = new Vector3[4];

        public bool IsShowing { get { return prompt != null; } }
        public bool NextVisible { get { return prompt != null && prompt.showNext && !suppressed; } }
        public bool ConfirmOpen { get { return confirmRoot != null && confirmRoot.activeSelf; } }

        public static TutorialOverlay Create(Transform parent, Action onSkipConfirmed)
        {
            var go = new GameObject("TutorialOverlay");
            go.transform.SetParent(parent, false);
            var o = go.AddComponent<TutorialOverlay>();
            o.Build(onSkipConfirmed);
            return o;
        }

        // ------------------------------------------------------------------ dựng

        private void Build(Action skipConfirmed)
        {
            onSkipConfirmed = skipConfirmed;
            canvas = HudBuilder.CreateCanvas("TutorialCanvas", 60, true, transform);
            contentRoot = HudBuilder.NewRect("Content", canvas.transform);
            HudBuilder.Stretch(contentRoot);

            dimFull = MakeBlock("DimFull");
            HudBuilder.Stretch(dimFull.rectTransform);

            for (int i = 0; i < frame.Length; i++)
            {
                Image f = HudBuilder.Img(contentRoot, "Frame" + i, null, HudBuilder.Accent, false, false);
                AnchorBottomLeft(f.rectTransform);
                frame[i] = f;
            }

            BuildCard();

            hand = HudBuilder.Img(contentRoot, "Hand", null, Color.white, false, false);
            Sprite hs = HudBuilder.T.tutorialHand;
            handHasSprite = hs != null;
            hand.sprite = handHasSprite ? hs : FallbackDot();
            hand.color = Color.white;
            hand.preserveAspect = true;
            RectTransform hr = hand.rectTransform;
            hr.anchorMin = Vector2.zero;
            hr.anchorMax = Vector2.zero;
            hr.pivot = handHasSprite ? new Vector2(0.109f, 0.93f) : new Vector2(0.5f, 0.5f);
            hr.sizeDelta = handHasSprite ? new Vector2(150f, 150f) : new Vector2(56f, 56f);

            BuildConfirm();
            contentRoot.gameObject.SetActive(false);
        }

        private Image MakeBlock(string name)
        {
            Image img = HudBuilder.Img(contentRoot, name, null, DimColor, false, true);
            img.color = DimColor;
            img.gameObject.AddComponent<CameraPassThrough>(); // chặn chạm xuống thế giới nhưng camera vẫn kéo / chụm được
            return img;
        }

        private static void AnchorBottomLeft(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
        }

        private static void SetBox(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
        }

        private void BuildCard()
        {
            Image panel = HudBuilder.Panel(contentRoot, "Card", true);
            card = panel.rectTransform;
            HudBuilder.SetFixed(card, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -TutorialRules.TopInset), new Vector2(CardW, CardH));

            speakerText = HudBuilder.AddText(card, "Speaker", TutorialStrings.Speaker, 28, TextAnchor.MiddleLeft, HudBuilder.GoodDark, true, false);
            HudBuilder.SetFixed(speakerText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -16f), new Vector2(500f, 38f));

            bodyText = HudBuilder.AddText(card, "Body", "", 32, TextAnchor.UpperLeft, HudBuilder.Ink, false, false);
            HudBuilder.SetFixed(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -58f), new Vector2(CardW - 68f, 100f));
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Truncate;

            nextButton = HudBuilder.AddLabelButton(card, "Next", ClayStyle.Red, TutorialStrings.Next, 34, OnNextClicked, out nextLabel);
            HudBuilder.SetFixed((RectTransform)nextButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 14f), new Vector2(210f, 84f));

            Text skipLabel;
            skipButton = HudBuilder.AddLabelButton(card, "Skip", ClayStyle.Cream, TutorialStrings.SkipBtn, 24, OpenConfirm, out skipLabel);
            HudBuilder.SetFixed((RectTransform)skipButton.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 14f), new Vector2(290f, 66f));
        }

        private void BuildConfirm()
        {
            Image d = HudBuilder.Dim(contentRoot, "Confirm", 0.7f);
            HudBuilder.Stretch(d.rectTransform);
            confirmRoot = d.gameObject;
            Image p = HudBuilder.Panel(d.transform, "Panel", true);
            HudBuilder.SetFixed(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 360f));
            Text t = HudBuilder.AddText(p.transform, "Title", TutorialStrings.SkipTitle, 46, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(700f, 64f));
            Text b = HudBuilder.AddText(p.transform, "Body", TutorialStrings.SkipBody, 30, TextAnchor.MiddleCenter, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(b.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(700f, 50f));
            Text l;
            ClayButton yes = HudBuilder.AddLabelButton(p.transform, "Yes", ClayStyle.Red, TutorialStrings.SkipYes, 38, OnConfirmYes, out l);
            HudBuilder.SetFixed((RectTransform)yes.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 40f), new Vector2(290f, 100f));
            ClayButton no = HudBuilder.AddLabelButton(p.transform, "No", ClayStyle.Green, TutorialStrings.SkipNo, 38, CloseConfirm, out l);
            HudBuilder.SetFixed((RectTransform)no.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(290f, 100f));
            confirmRoot.SetActive(false);
        }

        // ------------------------------------------------------------------ điều khiển

        public void Show(TutorialPrompt p)
        {
            bool changedText = prompt == null || prompt.text != p.text;
            prompt = p;
            if (changedText) shownAt = Time.unscaledTime;
            bodyText.text = p.text;
            nextButton.gameObject.SetActive(p.showNext);
            if (p.showNext) nextLabel.text = string.IsNullOrEmpty(p.nextLabel) ? TutorialStrings.Next : p.nextLabel;
            skipButton.gameObject.SetActive(p.showSkip);
            ApplyVisible();
        }

        public void Hide()
        {
            prompt = null;
            CloseConfirm();
            ApplyVisible();
        }

        public void SetSuppressed(bool v)
        {
            if (suppressed == v) return;
            suppressed = v;
            ApplyVisible();
        }

        /// Chỉ cho bot (-tutorial): bấm TIẾP.
        public bool DebugPressNext()
        {
            if (!NextVisible || ConfirmOpen) return false;
            OnNextClicked();
            return true;
        }

        private void ApplyVisible()
        {
            bool on = prompt != null && !suppressed;
            if (contentRoot.gameObject.activeSelf != on) contentRoot.gameObject.SetActive(on);
        }

        private void OnNextClicked()
        {
            if (prompt != null && prompt.onNext != null) prompt.onNext();
        }

        private void OpenConfirm() { confirmRoot.SetActive(true); }
        private void CloseConfirm() { if (confirmRoot != null) confirmRoot.SetActive(false); }

        private void OnConfirmYes()
        {
            CloseConfirm();
            if (onSkipConfirmed != null) onSkipConfirmed();
        }

        // ------------------------------------------------------------------ vẽ mỗi khung

        private void LateUpdate()
        {
            if (prompt == null || suppressed) return;
            float scale = canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;
            float cw = Screen.width / scale;
            float ch = Screen.height / scale;

            float x0, y0, x1, y1;
            bool hasHole = ResolveHole(prompt.target, scale, out x0, out y0, out x1, out y1);
            if (hasHole)
            {
                Pad(ref x0, ref y0, ref x1, ref y1, cw, ch);
                if (x1 <= x0 || y1 <= y0) hasHole = false;
            }

            holes.Clear();
            if (hasHole) holes.Add(new Vector4(x0, y0, x1, y1));
            float ax0, ay0, ax1, ay1;
            if (prompt.altTarget != null && ResolveHole(prompt.altTarget, scale, out ax0, out ay0, out ax1, out ay1))
            {
                Pad(ref ax0, ref ay0, ref ax1, ref ay1, cw, ch);
                if (ax1 > ax0 && ay1 > ay0) holes.Add(new Vector4(ax0, ay0, ax1, ay1));
            }

            LayoutDim(hasHole, scale);
            LayoutFrame(hasHole, x0, y0, x1, y1);
            LayoutHand(hasHole, x0, y0, x1, y1);
            LayoutCard(hasHole, x0, y0, x1, y1, cw, ch, scale);
        }

        private static void Pad(ref float x0, ref float y0, ref float x1, ref float y1, float cw, float ch)
        {
            x0 = Mathf.Max(0f, x0 - HolePad);
            y0 = Mathf.Max(0f, y0 - HolePad);
            x1 = Mathf.Min(cw, x1 + HolePad);
            y1 = Mathf.Min(ch, y1 + HolePad);
        }

        private bool ResolveHole(TutorialTarget t, float scale, out float x0, out float y0, out float x1, out float y1)
        {
            x0 = y0 = x1 = y1 = 0f;
            if (t == null) return false;

            if (t.Rect != null)
            {
                RectTransform rt = t.Rect();
                if (rt == null || !rt.gameObject.activeInHierarchy) return false;
                rt.GetWorldCorners(corners); // Overlay canvas: góc thế giới = pixel màn hình
                float minX = corners[0].x, minY = corners[0].y, maxX = corners[2].x, maxY = corners[2].y;
                if (maxX - minX < 2f || maxY - minY < 2f) return false;
                x0 = minX / scale; y0 = minY / scale; x1 = maxX / scale; y1 = maxY / scale;
                return true;
            }
            if (t.World != null)
            {
                Transform w = t.World();
                if (w == null || !w.gameObject.activeInHierarchy) return false;
                if (cam == null) cam = Camera.main;
                if (cam == null) return false;
                Vector3 sp = cam.WorldToScreenPoint(w.position + t.WorldOffset);
                if (sp.z <= 0.1f) return false;
                float cx = sp.x / scale, cy = sp.y / scale;
                x0 = cx - t.Box.x * 0.5f; x1 = cx + t.Box.x * 0.5f;
                y0 = cy - t.Box.y * 0.5f; y1 = cy + t.Box.y * 0.5f;
                return true;
            }
            return false;
        }

        /// Màn tối = cả màn hình trừ các lỗ (cắt hình chữ nhật). Mỗi mảnh là một Image chặn chạm; trong lỗ không có Graphic nào.
        private void LayoutDim(bool hasHole, float scale)
        {
            bool blockHole = prompt.block && hasHole;
            bool full = prompt.modal && !blockHole;
            dimFull.gameObject.SetActive(full);
            int used = 0;
            if (blockHole)
            {
                pieces.Clear();
                pieces.Add(new Vector4(0f, 0f, Screen.width / scale, Screen.height / scale));
                for (int h = 0; h < holes.Count; h++) SubtractHole(holes[h]);
                for (int i = 0; i < pieces.Count; i++)
                {
                    Vector4 p = pieces[i];
                    if (used >= dimPool.Count)
                    {
                        Image n = MakeBlock("Dim" + used);
                        AnchorBottomLeft(n.rectTransform);
                        // Mảnh tạo lười phải nằm DƯỚI thẻ / tay / hộp xác nhận, nếu không sẽ phủ tối và chặn nút Bỏ qua.
                        n.rectTransform.SetSiblingIndex(dimFull.rectTransform.GetSiblingIndex() + 1);
                        dimPool.Add(n);
                    }
                    Image img = dimPool[used++];
                    if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
                    SetBox(img.rectTransform, p.x, p.y, p.z - p.x, p.w - p.y);
                }
            }
            for (int i = used; i < dimPool.Count; i++)
                if (dimPool[i].gameObject.activeSelf) dimPool[i].gameObject.SetActive(false);
        }

        /// Cắt một lỗ (x0, y0, x1, y1) khỏi các mảnh tối hiện có.
        private void SubtractHole(Vector4 h)
        {
            scratch.Clear();
            for (int i = 0; i < pieces.Count; i++)
            {
                Vector4 p = pieces[i];
                if (h.x >= p.z || h.z <= p.x || h.y >= p.w || h.w <= p.y) { scratch.Add(p); continue; }
                float iy0 = Mathf.Max(p.y, h.y), iy1 = Mathf.Min(p.w, h.w);
                AddPiece(p.x, p.y, p.z, iy0);                       // dưới
                AddPiece(p.x, iy1, p.z, p.w);                       // trên
                AddPiece(p.x, iy0, Mathf.Min(p.z, h.x), iy1);       // trái
                AddPiece(Mathf.Max(p.x, h.z), iy0, p.z, iy1);       // phải
            }
            pieces.Clear();
            pieces.AddRange(scratch);
        }

        private void AddPiece(float x0, float y0, float x1, float y1)
        {
            if (x1 - x0 > 0.5f && y1 - y0 > 0.5f) scratch.Add(new Vector4(x0, y0, x1, y1));
        }

        private void LayoutFrame(bool hasHole, float x0, float y0, float x1, float y1)
        {
            for (int i = 0; i < frame.Length; i++) frame[i].gameObject.SetActive(hasHole);
            if (!hasHole) return;
            float s = Mathf.Sin(Time.unscaledTime * 5f);
            float pad = 4f + 5f * Mathf.Abs(s);
            const float th = 8f;
            float a = 0.65f + 0.35f * (0.5f + 0.5f * s);
            Color c = new Color(HudBuilder.Accent.r, HudBuilder.Accent.g, HudBuilder.Accent.b, a);
            float fx0 = x0 - pad, fy0 = y0 - pad, fx1 = x1 + pad, fy1 = y1 + pad;
            SetBox(frame[0].rectTransform, fx0, fy0, fx1 - fx0, th);              // dưới
            SetBox(frame[1].rectTransform, fx0, fy1 - th, fx1 - fx0, th);         // trên
            SetBox(frame[2].rectTransform, fx0, fy0, th, fy1 - fy0);              // trái
            SetBox(frame[3].rectTransform, fx1 - th, fy0, th, fy1 - fy0);         // phải
            for (int i = 0; i < frame.Length; i++) frame[i].color = c;
        }

        private void LayoutHand(bool hasHole, float x0, float y0, float x1, float y1)
        {
            bool on = hasHole && prompt.hand != HandMode.None;
            if (hand.gameObject.activeSelf != on) hand.gameObject.SetActive(on);
            if (!on) return;
            float t = Time.unscaledTime - shownAt;
            Vector2 center = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
            Vector2 away = new Vector2(46f, -46f); // tay vào từ phải-dưới
            float k;     // 0 = đã chạm mục tiêu, 1 = xa nhất
            float press; // 0..1 nhấn xuống
            if (prompt.hand == HandMode.Hold)
            {
                float ph = (t % 1.4f) / 1.4f;
                if (ph < 0.2f) { k = 1f - ph / 0.2f; press = 0f; }
                else if (ph < 0.8f) { k = 0f; press = 1f; }
                else { k = (ph - 0.8f) / 0.2f; press = 0f; }
            }
            else
            {
                float ph = (t % 1.1f) / 1.1f;
                if (ph < 0.45f) { k = 1f - ph / 0.45f; press = 0f; }
                else if (ph < 0.65f) { k = 0f; press = 1f; }
                else { k = (ph - 0.65f) / 0.35f; press = 0f; }
            }
            k = k * k;
            RectTransform hr = hand.rectTransform;
            hr.anchoredPosition = center + away * k;
            float sc = 1f - 0.1f * press;
            hr.localScale = new Vector3(sc, sc, 1f);
        }

        private void LayoutCard(bool hasHole, float x0, float y0, float x1, float y1, float cw, float ch, float scale)
        {
            // Bảng cần tránh (tổng kết): thẻ gọn nằm ở dải trống cạnh bảng, ngang tầm vùng chỉ
            RectTransform avoidRt = prompt.avoid != null ? prompt.avoid() : null;
            float ax0 = 0f, ay0 = 0f, ax1 = 0f, ay1 = 0f;
            bool hasAvoid = false;
            if (avoidRt != null && avoidRt.gameObject.activeInHierarchy)
            {
                avoidRt.GetWorldCorners(corners);
                ax0 = corners[0].x / scale; ay0 = corners[0].y / scale; ax1 = corners[2].x / scale; ay1 = corners[2].y / scale;
                hasAvoid = ax1 - ax0 > 2f && ay1 - ay0 > 2f;
            }

            // Bảng modal (tổng kết) đang mở: thẻ không bao giờ đè lên số liệu (hud_visual_polish §6).
            // 1) dải trống cạnh bảng (co thẻ cho vừa, tối thiểu MinSideW); 2) đáy màn hình, né vùng chỉ.
            float sideX = 0f;
            float sideW = CompactW;
            bool compact = hasAvoid && TutorialRules.PickSideCenterX(ax0, ax1, cw, CompactW, 10f, out sideX);
            if (hasAvoid && !compact)
            {
                sideW = Mathf.Min(CompactW, TutorialRules.SideFreeWidth(ax0, ax1, cw, 10f));
                compact = sideW >= MinSideW && TutorialRules.PickSideCenterX(ax0, ax1, cw, sideW, 10f, out sideX);
            }
            if (compact)
            {
                SetCardSize(sideW, CompactH);
                float cy = hasHole ? (y0 + y1) * 0.5f : ch * 0.35f;
                float yMaxC = Mathf.Clamp(cy + CompactH * 0.5f, CompactH + 24f, ch - TutorialRules.TopInset);
                card.anchoredPosition = new Vector2(sideX - cw * 0.5f, -(ch - yMaxC));
            }
            else if (hasAvoid)
            {
                float w = Mathf.Min(BottomW, cw - 2f * BottomMargin);
                SetCardSize(w, BottomH);
                float cx = TutorialRules.PickBottomCenterX(hasHole, x0, x1, cw, w, BottomMargin);
                card.anchoredPosition = new Vector2(cx - cw * 0.5f, -(ch - (BottomMargin + BottomH)));
            }
            else
            {
                SetCardSize(CardW, CardH);
                int n = 0;
                for (int i = 0; i < holes.Count && n < 2; i++) n = AddAvoid(n, holes[i].x, holes[i].y, holes[i].z, holes[i].w);
                if (hasAvoid) n = AddAvoid(n, ax0, ay0, ax1, ay1);
                int slot = TutorialRules.PickCardSlot(avoidRects, n, cw, ch, CardW, CardH, 24f);
                float yMin, yMax;
                TutorialRules.SlotY(slot, ch, CardH, out yMin, out yMax);
                // card neo (0.5, 1) pivot (0.5, 1): vị trí y = khoảng cách từ đỉnh canvas tới cạnh trên thẻ
                card.anchoredPosition = new Vector2(0f, -(ch - yMax));
            }
            float pop = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.18f);
            float s = 0.9f + 0.1f * (1f - (1f - pop) * (1f - pop));
            card.localScale = new Vector3(s, s, 1f);
        }

        private int AddAvoid(int n, float x0, float y0, float x1, float y1)
        {
            if (n >= 3) return n;
            avoidRects[n * 4] = x0; avoidRects[n * 4 + 1] = y0; avoidRects[n * 4 + 2] = x1; avoidRects[n * 4 + 3] = y1;
            return n + 1;
        }

        /// Đổi cỡ thẻ giữa dạng rộng (mặc định) và gọn (cạnh bảng tổng kết); chữ cao hơn, nút thu nhỏ để vừa.
        private void SetCardSize(float w, float h)
        {
            bool compact = w < CardW;
            if (compact == cardCompact && Mathf.Approximately(card.sizeDelta.x, w)) return;
            cardCompact = compact;
            card.sizeDelta = new Vector2(w, h);
            bodyText.rectTransform.sizeDelta = new Vector2(w - 68f, compact ? h - 58f - 90f : 100f);
            ((RectTransform)skipButton.transform).sizeDelta = new Vector2(compact ? 200f : 290f, compact ? 58f : 66f);
            ((RectTransform)nextButton.transform).sizeDelta = new Vector2(compact ? 150f : 210f, compact ? 72f : 84f);
        }

        // ------------------------------------------------------------------ sprite dự phòng

        /// Chấm tròn kem viền nâu tự vẽ (dùng khi chưa có sprite bàn tay).
        private static Sprite FallbackDot()
        {
            if (fallbackDot != null) return fallbackDot;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float c = (n - 1) * 0.5f;
            Color fill = new Color32(0xFF, 0xF4, 0xDC, 0xFF);
            Color edge = new Color32(0x4A, 0x2C, 0x1E, 0xFF);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    Color px = d > 30f ? Color.clear : (d > 24f ? edge : fill);
                    if (d > 29f && d <= 30f) px.a = 30f - d; // mép mịn
                    tex.SetPixel(x, y, px);
                }
            }
            tex.Apply();
            fallbackDot = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            fallbackDot.name = "TutorialDot";
            return fallbackDot;
        }
    }
}
