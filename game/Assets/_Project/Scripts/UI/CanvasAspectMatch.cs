using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Màn rộng hơn hoặc bằng 16:9 (điện thoại): giữ chiều cao tham chiếu (match height).
    /// Màn hẹp hơn 16:9 (iPad 4:3, 16:10): chuyển sang match width để chiều rộng canvas không tụt dưới thiết kế 16:9,
    /// tránh các phần neo hai bên (MENU, Cửa hàng, chip ly, bảng pha) đè lên nhau. Cập nhật khi đổi kích thước cửa sổ / xoay máy.
    [RequireComponent(typeof(CanvasScaler))]
    public class CanvasAspectMatch : MonoBehaviour
    {
        private const float DesignAspect = 16f / 9f;
        private CanvasScaler scaler;
        private int lastW = -1, lastH = -1;

        public static float MatchFor(int width, int height)
        {
            if (width <= 0 || height <= 0) return 1f;
            return (float)width / height >= DesignAspect - 0.001f ? 1f : 0f;
        }

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        private void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH) Apply();
        }

        private void Apply()
        {
            lastW = Screen.width;
            lastH = Screen.height;
            if (scaler != null) scaler.matchWidthOrHeight = MatchFor(lastW, lastH);
        }
    }
}
