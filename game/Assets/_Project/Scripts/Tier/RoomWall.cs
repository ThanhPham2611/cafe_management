using UnityEngine;

namespace Cafe
{
    /// Một bức tường của phòng thuê. Camera orbit 360° nên tường nào đang nằm giữa camera và phòng
    /// sẽ hạ xuống chỉ còn phần chân tường thấp (kiểu "cắt lớp nhà búp bê"); camera ở phía trong thì tường lên đủ cao.
    public class RoomWall : MonoBehaviour
    {
        private const float Speed = 7f;           // 1/s

        /// Pháp tuyến ngang hướng ra ngoài phòng.
        public Vector3 Outward = Vector3.back;
        /// Phần tường trên (gốc pivot ở mép trên chân tường); scale Y thu về 0 khi hạ.
        public Transform Upper;

        private Camera cam;
        private float height01 = 1f;
        private bool dropped;

        private void LateUpdate()
        {
            if (Upper == null) return;
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
            }
            // Camera nằm phía ngoài mặt phẳng tường (có trễ) thì hạ tường: đúng cả khi nhìn ngang tầm mắt, lúc tường dài che hết
            Vector3 d = cam.transform.position - transform.position;
            d.y = 0f;
            dropped = CameraRules.WallDropped(Vector3.Dot(d, Outward), dropped);
            float target = dropped ? 0f : 1f;
            if (Mathf.Approximately(height01, target)) return;

            height01 = Mathf.MoveTowards(height01, target, Speed * Time.unscaledDeltaTime);
            bool show = height01 > 0.01f;
            if (Upper.gameObject.activeSelf != show) Upper.gameObject.SetActive(show);
            if (show) Upper.localScale = new Vector3(1f, height01, 1f);
        }
    }
}
