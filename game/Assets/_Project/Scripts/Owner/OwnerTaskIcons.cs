using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Icon việc đang chờ trên đầu chủ quán (tối đa 3 + việc tự về quầy). Vẽ ở lớp bong bóng overlay
    /// (chiếu từ toạ độ thế giới) giống OrderBubble nên không bị mái dù che.
    public class OwnerTaskIcons : MonoBehaviour
    {
        private const int Slots = 4;
        private const float DiscSize = 64f;
        private const float Gap = 8f;

        private class Slot
        {
            public GameObject root;
            public Image disc;
            public Image icon;
        }

        private RectTransform rect;
        private Canvas canvas;
        private Camera cam;
        private OwnerController owner;
        private readonly Slot[] slots = new Slot[Slots];
        private bool dirty = true;
        private bool shown = true;
        private readonly Vector3 offset = new Vector3(0f, 1.55f, 0f);

        public static OwnerTaskIcons Create(RectTransform layer, OwnerController owner)
        {
            RectTransform rt = HudBuilder.NewRect("OwnerTasks", layer);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(Slots * (DiscSize + Gap), DiscSize);
            var ic = rt.gameObject.AddComponent<OwnerTaskIcons>();
            ic.Build(rt, owner, layer.GetComponentInParent<Canvas>());
            return ic;
        }

        private void Build(RectTransform root, OwnerController o, Canvas c)
        {
            rect = root;
            owner = o;
            canvas = c != null ? c.rootCanvas : null;
            for (int i = 0; i < Slots; i++)
            {
                var s = new Slot();
                Image d = HudBuilder.Img(root, "Task" + i, HudBuilder.T.disc, HudBuilder.Cream, false, false);
                HudBuilder.SetFixed(d.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(DiscSize, DiscSize));
                s.disc = d;
                s.root = d.gameObject;
                s.icon = HudBuilder.Icon(d.transform, "Icon", null, DiscSize * 0.72f);
                HudBuilder.SetFixed(s.icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DiscSize * 0.72f, DiscSize * 0.72f));
                s.root.SetActive(false);
                slots[i] = s;
            }
            owner.Changed += MarkDirty;
        }

        private void OnDestroy()
        {
            if (owner != null) owner.Changed -= MarkDirty;
        }

        private void MarkDirty()
        {
            dirty = true;
        }

        private void Rebuild()
        {
            dirty = false;
            var tasks = owner.Tasks;
            int n = Mathf.Min(tasks.Count, Slots);
            float total = n * DiscSize + (n - 1) * Gap;
            for (int i = 0; i < Slots; i++)
            {
                Slot s = slots[i];
                if (i >= n)
                {
                    s.root.SetActive(false);
                    continue;
                }
                OwnerTask t = tasks[i];
                s.root.SetActive(true);
                Sprite sp;
                Color tint;
                switch (t.type)
                {
                    case OwnerTaskType.Deliver:
                        sp = HudBuilder.T.IconFor(t.recipe);
                        tint = HudBuilder.Accent;
                        break;
                    case OwnerTaskType.CollectCup:
                        sp = HudBuilder.T.iconCup;
                        tint = HudBuilder.Mint;
                        break;
                    case OwnerTaskType.CleanTable:
                    case OwnerTaskType.MopFloor:
                        sp = CleaningRules.BroomIcon(HudBuilder.T);
                        tint = HudBuilder.Mint;
                        break;
                    case OwnerTaskType.WashCups:
                        sp = HudBuilder.T.iconCup;
                        tint = HudBuilder.CreamDark;
                        break;
                    default:
                        sp = HudBuilder.T.iconPhin;
                        tint = HudBuilder.Cream;
                        break;
                }
                HudBuilder.SetIcon(s.icon, sp);
                s.disc.color = HudBuilder.T.disc != null ? tint : tint;
                s.disc.rectTransform.anchoredPosition = new Vector2(-total * 0.5f + DiscSize * 0.5f + i * (DiscSize + Gap), 0f);
                float k = i == 0 ? 1.12f : 0.92f; // việc đang làm to hơn
                s.disc.rectTransform.localScale = new Vector3(k, k, 1f);
            }
        }

        private void LateUpdate()
        {
            if (owner == null) return;
            if (dirty) Rebuild();

            bool any = owner.QueueCount > 0;
            if (!any || canvas == null)
            {
                SetShown(false);
                return;
            }
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Vector3 sp = cam.WorldToScreenPoint(owner.transform.position + offset);
            if (sp.z <= 0.1f)
            {
                SetShown(false);
                return;
            }
            SetShown(true);
            rect.anchoredPosition = new Vector2(sp.x, sp.y) / canvas.scaleFactor;
        }

        private void SetShown(bool v)
        {
            if (shown == v) return;
            shown = v;
            for (int i = 0; i < Slots; i++)
                if (!v) slots[i].root.SetActive(false);
            if (v) dirty = true;
        }
    }
}
