using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Bong bóng order phía trên đầu khách: bóng thoại clay + icon món + vòng kiên nhẫn (+ trái tim nhỏ nếu là khách quen).
    /// Không có chữ: nhận món qua icon, tên món vẫn ở thẻ "Đơn chờ".
    /// Vẽ ở lớp UI overlay riêng (chiếu từ tọa độ thế giới) để không bị mái dù/mô hình che.
    public class OrderBubble : MonoBehaviour
    {
        private RectTransform rect;
        private GameObject content;
        private Image heart;
        private Image star;
        private Image ringFill;
        private Image drinkIcon;
        private Customer customer;
        private Transform target;
        private Canvas canvas;
        private Camera cam;
        private bool shown;
        private bool contentVisible;
        private float lastPatience = -1f;
        // Chibi cao ~1.15 m (ngồi thấp hơn): đuôi bóng cách đầu ~0.2 m để không trôi xa khỏi khách.
        private readonly Vector3 offset = new Vector3(0f, 1.35f, 0f);

        public static OrderBubble Create(RectTransform layer, Transform target)
        {
            RectTransform rt = HudBuilder.NewRect("OrderBubble", layer);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(116f, 150f);
            var b = rt.gameObject.AddComponent<OrderBubble>();
            b.Build(rt, target, layer.GetComponentInParent<Canvas>());
            return b;
        }

        private void Build(RectTransform root, Transform tgt, Canvas c)
        {
            rect = root;
            target = tgt;
            customer = tgt != null ? tgt.GetComponent<Customer>() : null;
            canvas = c != null ? c.rootCanvas : null;

            RectTransform holder = HudBuilder.NewRect("Content", root);
            HudBuilder.Stretch(holder);
            content = holder.gameObject;

            // thân bóng thoại (đuôi nằm dưới, đầu đuôi = điểm neo của root)
            Image body = HudBuilder.Img(holder, "Body", HudBuilder.T.bubble, HudBuilder.Cream, true, false);
            HudBuilder.SetRect(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 34f), Vector2.zero);

            Image tail = HudBuilder.Img(holder, "Tail", HudBuilder.T.bubbleTail, new Color(0f, 0f, 0f, 0f), false, false);
            HudBuilder.SetFixed(tail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(56f, 40f));
            tail.enabled = HudBuilder.T.bubbleTail != null;

            RectTransform ring;
            ringFill = HudBuilder.CreateRing(body.transform, "Ring", 92f, out ring);
            HudBuilder.SetFixed(ring, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(92f, 92f));

            drinkIcon = HudBuilder.Icon(ring, "Drink", null, 64f);
            HudBuilder.SetFixed(drinkIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));

            heart = HudBuilder.Icon(body.transform, "Regular", HudBuilder.T.iconHeart, 44f);
            HudBuilder.SetFixed(heart.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-6f, -6f), new Vector2(44f, 44f));
            heart.enabled = false;

            star = HudBuilder.Icon(body.transform, "Special", HudBuilder.T.iconStar, 44f);
            HudBuilder.SetFixed(star.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(6f, -6f), new Vector2(44f, 44f));
            star.enabled = false;

            content.SetActive(false);
        }

        public void Show(bool regular, bool special = false)
        {
            shown = true;
            heart.enabled = regular && HudBuilder.T.iconHeart != null;
            star.enabled = special && HudBuilder.T.iconStar != null;   // đơn đặc biệt: sao ở góc trái-trên
            Sprite icon = customer != null && customer.CurrentOrder != null
                ? HudBuilder.T.IconFor(customer.CurrentOrder.recipe)
                : HudBuilder.T.iconCup;
            HudBuilder.SetIcon(drinkIcon, icon);
            lastPatience = -1f;
            SetPatience(1f);
        }

        public void Hide()
        {
            shown = false;
            SetContent(false);
        }

        public void SetPatience(float v)
        {
            if (Mathf.Abs(v - lastPatience) < 0.004f) return;
            lastPatience = v;
            HudBuilder.SetRing(ringFill, v);
        }

        private void SetContent(bool v)
        {
            if (contentVisible == v) return;
            contentVisible = v;
            content.SetActive(v);
        }

        private void LateUpdate()
        {
            if (!shown || target == null || canvas == null)
            {
                SetContent(false);
                return;
            }
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Vector3 sp = cam.WorldToScreenPoint(target.position + offset);
            if (sp.z <= 0.1f)
            {
                SetContent(false);
                return;
            }
            GameBootstrap g = GameBootstrap.Instance;
            if (g != null && g.Hud != null && g.Hud.IsUnderOrderCards(new Vector2(sp.x, sp.y + 60f)))
            {
                SetContent(false); // bị thẻ "Đơn chờ" che: ẩn hẳn, không lòi ra giữa các thẻ
                return;
            }
            SetContent(true);
            rect.anchoredPosition = new Vector2(sp.x, sp.y) / canvas.scaleFactor;
        }
    }
}
