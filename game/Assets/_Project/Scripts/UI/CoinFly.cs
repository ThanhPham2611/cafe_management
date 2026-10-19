using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Xu bay (docs/hud_visual_polish_2026-10-18.md §2): 3-5 đồng xu bay theo cung từ chỗ khách tới đĩa coin trên chip Xu
    /// (0,5 s, ease-in, so le 0,06 s); đồng cuối chạm thì gọi onArrive (HUD mới cộng số). Pool cố định, không cấp phát khi chạy.
    /// Component tự tắt khi không còn đồng nào bay -> không tốn Update / không dựng lại canvas lúc đứng yên.
    public class CoinFly : MonoBehaviour
    {
        private const int PoolSize = 15;
        private const int MaxGroups = 6;
        private const float FlySeconds = 0.5f;
        private const float Stagger = 0.06f;
        private const float CoinSize = 44f;

        private class Coin
        {
            public RectTransform rt;
            public float t;
            public float delay;
            public Vector2 from;
            public int group = -1;
        }

        private class Group
        {
            public bool used;
            public int left;
            public RectTransform target;
            public Action onArrive;
        }

        private readonly Coin[] coins = new Coin[PoolSize];
        private readonly Group[] groups = new Group[MaxGroups];
        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform root;

        /// Dựng pool dưới canvas HUD (overlay: toạ độ thế giới = pixel màn hình). Trên cùng, không chặn chạm.
        public static CoinFly Create(Transform canvas, Sprite coin)
        {
            RectTransform r = HudBuilder.NewRect("CoinFly", canvas);
            HudBuilder.Stretch(r);
            var f = r.gameObject.AddComponent<CoinFly>();
            f.root = r;
            for (int i = 0; i < PoolSize; i++)
            {
                Image img = HudBuilder.Img(r, "Coin" + i, coin, HudBuilder.Accent, false, false);
                img.preserveAspect = true;
                img.rectTransform.sizeDelta = new Vector2(CoinSize, CoinSize);
                img.gameObject.SetActive(false);
                f.coins[i] = new Coin { rt = img.rectTransform };
            }
            for (int i = 0; i < MaxGroups; i++) f.groups[i] = new Group();
            f.enabled = false;
            return f;
        }

        /// Bay count đồng (3-5; 1 khi đã có ≥2 lượt đang bay) từ fromScreen tới target. Trả false nếu đang tắt hiệu ứng / hết pool (người gọi tự cộng ngay).
        public bool Fly(Vector2 fromScreen, RectTransform target, int count, Action onArrive)
        {
            if (GraphicsQuality.ReduceMotion || target == null || ModalMarker.AnyOpen) return false; // false: người gọi cộng số ngay
            int g = -1, busy = 0;
            for (int i = 0; i < MaxGroups; i++) { if (groups[i].used) busy++; else if (g < 0) g = i; }
            // Giờ cao điểm (nhà hàng nhiều khách trả tiền liên tục): mỗi lượt chỉ 1 đồng để góc Xu không bị phủ kín xu
            count = busy >= 2 ? 1 : Mathf.Clamp(count, 3, 5);
            if (g < 0) return false;
            int free = 0;
            for (int i = 0; i < PoolSize; i++) if (coins[i].group < 0) free++;
            if (free < count) return false;

            Group grp = groups[g];
            grp.used = true;
            grp.left = count;
            grp.target = target;
            grp.onArrive = onArrive;
            float scale = root.lossyScale.x > 0.0001f ? root.lossyScale.x : 1f;
            int n = 0;
            for (int i = 0; i < PoolSize && n < count; i++)
            {
                Coin c = coins[i];
                if (c.group >= 0) continue;
                c.group = g;
                c.t = 0f;
                c.delay = n * Stagger;
                // toả nhẹ quanh chỗ khách để các đồng không chồng lên nhau
                float a = n * 2.1f;
                c.from = fromScreen + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (18f * scale);
                c.rt.position = c.from;
                c.rt.localScale = Vector3.zero;
                c.rt.gameObject.SetActive(true);
                n++;
            }
            enabled = true;
            return true;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float scale = root.lossyScale.x > 0.0001f ? root.lossyScale.x : 1f;
            bool any = false;
            for (int i = 0; i < PoolSize; i++)
            {
                Coin c = coins[i];
                if (c.group < 0) continue;
                any = true;
                Group grp = groups[c.group];
                if (c.delay > 0f)
                {
                    c.delay -= dt;
                    if (c.delay > 0f) continue;
                }
                c.t += dt / FlySeconds;
                Vector2 to = TargetCenter(grp.target);
                if (c.t >= 1f)
                {
                    c.rt.gameObject.SetActive(false);
                    c.group = -1;
                    grp.left--;
                    if (grp.left <= 0)
                    {
                        Action cb = grp.onArrive;
                        grp.used = false;
                        grp.onArrive = null;
                        grp.target = null;
                        if (cb != null) cb();
                    }
                    continue;
                }
                float k = c.t * c.t; // ease-in: chậm lúc rời khách, nhanh dần khi tới đĩa coin
                Vector2 ctrl = (c.from + to) * 0.5f + new Vector2(0f, 160f * scale);
                Vector2 p = (1f - k) * (1f - k) * c.from + 2f * (1f - k) * k * ctrl + k * k * to;
                c.rt.position = p;
                float s = Mathf.Min(1f, c.t * 6f) * (1f - 0.25f * k);
                c.rt.localScale = new Vector3(s, s, 1f);
            }
            if (!any) enabled = false;
        }

        private Vector2 TargetCenter(RectTransform t)
        {
            if (t == null) return Vector2.zero;
            t.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }
    }
}
