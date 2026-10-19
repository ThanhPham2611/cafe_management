using UnityEngine;

namespace Cafe
{
    /// Gắn lên gốc của mỗi bảng modal (Cửa hàng, Nhân viên, Tổng kết, Nhiệm vụ...): đếm số bảng đang mở.
    /// FloatLayer (canvas nằm TRÊN HUD) dựa vào đây để không vẽ chữ nổi / lời khách / xu bay đè lên bảng.
    public class ModalMarker : MonoBehaviour
    {
        public static int OpenCount { get; private set; }

        public static bool AnyOpen { get { return OpenCount > 0; } }

        public static void Mark(GameObject root)
        {
            if (root != null && root.GetComponent<ModalMarker>() == null) root.AddComponent<ModalMarker>();
        }

        private void OnEnable() { OpenCount++; }

        private void OnDisable() { OpenCount = Mathf.Max(0, OpenCount - 1); }
    }
}
