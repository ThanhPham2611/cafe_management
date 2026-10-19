using UnityEngine;

namespace Cafe
{
    /// Hỗ trợ nhìn ngang tầm mắt: (1) quạt trần / đèn thả / đèn chùm (đồ treo) nằm đúng dải đầu nhân vật và chắn tầm nhìn: ẩn khi góc
    /// nghiêng dưới 14°, hiện lại trên 18° (CameraRules.HideOverhead; đồ treo không có collider nên ẩn không ảnh hưởng chạm / xây);
    /// (2) khách / chủ quán đi sát camera (lối vào quán nằm cạnh camera ở một số hướng nhìn) che kín màn hình: ẩn khi cách dưới 1,6 m.
    public sealed class OverheadVisibility : MonoBehaviour
    {
        private GameBootstrap game;
        private bool hidden;
        private bool dirty = true;
        private float timer;
        private readonly System.Collections.Generic.HashSet<Component> nearHidden = new System.Collections.Generic.HashSet<Component>();
        private readonly System.Collections.Generic.List<Component> scratch = new System.Collections.Generic.List<Component>();

        public void Init(GameBootstrap g)
        {
            game = g;
            if (g.Grid != null) g.Grid.LayoutChanged += MarkDirty;
        }

        private void OnDestroy()
        {
            if (game != null && game.Grid != null) game.Grid.LayoutChanged -= MarkDirty;
        }

        private void MarkDirty() { dirty = true; }

        private void UpdateNearCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            float pitch = game.CameraRig.Pitch;
            Vector3 cp = cam.transform.position;
            scratch.Clear();
            if (game.Spawner != null)
                for (int i = 0; i < game.Spawner.Active.Count; i++)
                    if (game.Spawner.Active[i] != null) scratch.Add(game.Spawner.Active[i]);
            if (game.Owner != null) scratch.Add(game.Owner);
            for (int i = 0; i < scratch.Count; i++)
            {
                Component c = scratch[i];
                bool was = nearHidden.Contains(c);
                bool now = CameraRules.HideNearCamera(Vector3.Distance(c.transform.position + Vector3.up * 0.9f, cp), pitch, was);
                if (now == was) continue;
                if (now) nearHidden.Add(c); else nearHidden.Remove(c);
                foreach (Renderer r in c.GetComponentsInChildren<Renderer>(true)) r.enabled = !now;
            }
            // Khách đã đi mất khỏi danh sách thì quên (đối tượng bị huỷ)
            nearHidden.RemoveWhere(x => x == null);
        }

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = 0.1f;
            if (game == null || game.CameraRig == null || game.Grid == null) return;
            UpdateNearCamera();
            bool next = CameraRules.HideOverhead(game.CameraRig.Pitch, hidden);
            if (next == hidden && !dirty) return;
            hidden = next;
            dirty = false;
            for (int i = 0; i < game.Grid.Placed.Count; i++)
            {
                PlacedFurniture pf = game.Grid.Placed[i];
                if (pf == null || !pf.Overhead || pf.Def == null || pf.Def.hangY <= 0f) continue;
                foreach (Renderer r in pf.GetComponentsInChildren<Renderer>(true)) r.enabled = !hidden;
            }
        }
    }
}
