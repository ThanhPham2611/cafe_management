using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Lớp chứa các chữ nổi (pool cố định, không cấp phát khi chạy).
    public class FloatLayer : MonoBehaviour
    {
        private const int PoolSize = 14;
        private const float Life = 1.2f;
        private const float Rise = 150f;

        private class Item
        {
            public RectTransform rt;
            public Text text;
            public Outline outline;
            public float age = -1f;
            public Vector2 start;
            public Color color;
        }

        private readonly Item[] items = new Item[PoolSize];
        private Canvas canvas;
        private RectTransform layerRect;
        private int next;
        private float lastTopTime = -10f;
        private int topSlot;

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
                Text t = HudBuilder.AddText(layerRect, "Float" + i, "", 54, TextAnchor.MiddleCenter, HudBuilder.Accent, true, true);
                t.rectTransform.anchorMin = Vector2.zero;
                t.rectTransform.anchorMax = Vector2.zero;
                t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                t.rectTransform.sizeDelta = new Vector2(900f, 90f);
                var it = new Item();
                it.rt = t.rectTransform;
                it.text = t;
                it.outline = t.GetComponent<Outline>();
                if (it.outline != null) it.outline.effectDistance = new Vector2(3f, -3f);
                t.gameObject.SetActive(false);
                items[i] = it;
            }
            UiFx.Register(this);
        }

        private void OnDestroy()
        {
            UiFx.Unregister(this);
        }

        public void Spawn(Vector2 screenPos, string msg, Color color)
        {
            float k = canvas != null && canvas.scaleFactor > 0.01f ? 1f / canvas.scaleFactor : 1f;
            SpawnCanvas(screenPos * k, msg, color);
        }

        private string lastTopMsg;

        public void SpawnTop(string msg, Color color)
        {
            float now = Time.unscaledTime;
            if (msg == lastTopMsg && now - lastTopTime < 1.5f) return; // chống spam khi chạm liên tục
            lastTopMsg = msg;
            if (now - lastTopTime > 1.0f) topSlot = 0;
            else topSlot = (topSlot + 1) % 3;
            lastTopTime = now;
            // toạ độ canvas: giữa ngang; bay lên ~150px nên bắt đầu thấp hơn để không chạm thanh chip (cao ~110px)
            Vector2 size = layerRect.rect.size;
            if (size.x < 10f || size.y < 10f) size = new Vector2(1920f, 1080f);
            SpawnCanvas(new Vector2(size.x * 0.5f, size.y - 330f - topSlot * 56f), msg, color);
        }

        private void SpawnCanvas(Vector2 canvasPos, string msg, Color color)
        {
            canvasPos = AvoidStack(canvasPos);
            Item it = items[next];
            next = (next + 1) % PoolSize;
            it.age = 0f;
            it.start = canvasPos;
            it.color = color;
            it.text.text = msg;
            it.text.color = color;
            it.rt.anchoredPosition = canvasPos;
            it.rt.localScale = new Vector3(0.6f, 0.6f, 1f);
            it.rt.SetAsLastSibling();
            it.text.gameObject.SetActive(true);
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
                    if (o == null || o.age < 0f) continue;
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

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
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
                it.rt.anchoredPosition = it.start + new Vector2(0f, Rise * e);

                float s;
                if (it.age < 0.12f) s = Mathf.Lerp(0.6f, 1.15f, it.age / 0.12f);
                else if (it.age < 0.25f) s = Mathf.Lerp(1.15f, 1f, (it.age - 0.12f) / 0.13f);
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
            }
        }
    }
}
