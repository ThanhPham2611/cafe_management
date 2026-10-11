using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Hiệu ứng UI tối giản: chữ nổi bay lên rồi mờ dần (+43 Xu...).
    /// HudController tạo FloatLayer; ngoài UI chỉ cần gọi UiFx.FloatText(...).
    public static class UiFx
    {
        private static FloatLayer layer;

        internal static void Register(FloatLayer l)
        {
            layer = l;
        }

        internal static void Unregister(FloatLayer l)
        {
            if (layer == l) layer = null;
        }

        /// Chữ nổi tại vị trí thế giới (chiếu bằng Camera.main).
        public static void FloatText(Vector3 worldPos, string text, Color color)
        {
            if (layer == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(worldPos);
            if (sp.z <= 0.1f) return;
            layer.Spawn(new Vector2(sp.x, sp.y), text, color);
        }

        /// Chữ nổi tại toạ độ màn hình (pixel).
        public static void FloatTextScreen(Vector2 screenPos, string text, Color color)
        {
            if (layer != null) layer.Spawn(screenPos, text, color);
        }

        /// Chữ nổi ở giữa-trên màn hình (dưới thanh chip) cho thông báo chung.
        public static void FloatTextTop(string text, Color color)
        {
            if (layer != null) layer.SpawnTop(text, color);
        }
    }
}
