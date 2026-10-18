using UnityEngine;

namespace Cafe
{
    /// Ẩn một vật thể của môi trường vỉa hè (tường nhà phố, mái che, cửa cuốn) khi camera nằm phía ngoài mặt phẳng của nó,
    /// để nhìn ngang tầm mắt từ phía sau không bị tường chắn kín. Cùng luật với RoomWall (CameraRules.WallDropped).
    public class ViewHide : MonoBehaviour
    {
        /// Pháp tuyến ngang hướng ra ngoài (phía có thể che tầm nhìn).
        public Vector3 Outward = Vector3.forward;
        /// Điểm trên mặt phẳng (thế giới); mặc định lấy vị trí vật thể.
        public Vector3 PlanePoint;

        private Renderer[] renderers;
        private Camera cam;
        private bool hidden;
        public bool IsHidden { get { return hidden; } }

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            if (PlanePoint == Vector3.zero) PlanePoint = transform.position;
        }

        private void LateUpdate()
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
            }
            Vector3 d = cam.transform.position - PlanePoint;
            d.y = 0f;
            bool next = CameraRules.WallDropped(Vector3.Dot(d, Outward), hidden);
            if (next == hidden) return;
            hidden = next;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = !hidden;
        }
    }
}
