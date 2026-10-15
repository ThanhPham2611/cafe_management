using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Bong bóng "thu ly" nổi trên ly để lại (T1): báo cho người chơi biết khách đã uống xong và ly chạm được.
    /// Chuyển màu đỏ + nhảy nhanh khi ghế sắp bị chặn. Vẽ ở BubbleLayer như OrderBubble, không chặn chạm (chạm xuyên xuống ly).
    public class CupCollectBubble : MonoBehaviour
    {
        private RectTransform rect;
        private Image body;
        private Transform target;
        private Canvas canvas;
        private Camera cam;
        private float urgency;
        private float phase;
        private bool visible = true;
        private readonly Vector3 offset = new Vector3(0f, 0.55f, 0f);

        public static CupCollectBubble Create(RectTransform layer, Transform target)
        {
            RectTransform rt = HudBuilder.NewRect("CupBubble", layer);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(96f, 112f);
            var b = rt.gameObject.AddComponent<CupCollectBubble>();
            b.Build(rt, target, layer.GetComponentInParent<Canvas>());
            return b;
        }

        private void Build(RectTransform root, Transform tgt, Canvas c)
        {
            rect = root;
            target = tgt;
            canvas = c != null ? c.rootCanvas : null;
            phase = Random.value * 6f;

            body = HudBuilder.Img(root, "Body", HudBuilder.T.bubble, HudBuilder.Cream, true, false);
            HudBuilder.SetRect(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 30f), Vector2.zero);

            Image tail = HudBuilder.Img(root, "Tail", HudBuilder.T.bubbleTail, new Color(0f, 0f, 0f, 0f), false, false);
            HudBuilder.SetFixed(tail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(48f, 34f));
            tail.enabled = HudBuilder.T.bubbleTail != null;

            Image icon = HudBuilder.Icon(body.transform, "Cup", HudBuilder.T.iconCup, 64f);
            HudBuilder.SetFixed(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            SetVisible(false);
        }

        public void SetUrgency(float u)
        {
            urgency = u;
            if (body != null)
                body.color = CupCollectRules.IsWarning(u) ? Color.Lerp(HudBuilder.Cream, HudBuilder.Red, 0.55f) : HudBuilder.Cream;
        }

        private void SetVisible(bool v)
        {
            if (visible == v) return;
            visible = v;
            for (int i = 0; i < rect.childCount; i++) rect.GetChild(i).gameObject.SetActive(v);
        }

        private void LateUpdate()
        {
            if (target == null || canvas == null) { SetVisible(false); return; }
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(target.position + offset);
            if (sp.z <= 0.1f) { SetVisible(false); return; }
            SetVisible(true);
            phase += Time.unscaledDeltaTime * CupCollectRules.PulseSpeed(urgency);
            float bounce = Mathf.Abs(Mathf.Sin(phase)) * 10f;
            rect.anchoredPosition = new Vector2(sp.x, sp.y + bounce) / canvas.scaleFactor;
        }
    }
}
