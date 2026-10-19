using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Hiệu ứng UI tối giản: chữ nổi bay lên rồi mờ dần (+43 Xu...).
    /// HudController tạo FloatLayer; ngoài UI chỉ cần gọi UiFx.FloatText(...).
    public static class UiFx
    {
        private static FloatLayer layer;

        /// Độ cao (pixel màn hình) đặt toast chung: ngay trên mép bàn pha (HudController cập nhật mỗi khung). <= 0 = chỗ cũ giữa-trên.
        public static float ToastScreenY;

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
            GameBootstrap g = GameBootstrap.Instance;
            if (g != null && g.Hud != null) sp.x = g.Hud.PushRightOfOrderCards(new Vector2(sp.x, sp.y));
            layer.Spawn(new Vector2(sp.x, sp.y), text, color);
        }

        /// Câu thoại nền của khách (barks) trên đầu khách: chữ nhỏ, bay chậm.
        public static void Bark(Vector3 worldPos, string text, Color color)
        {
            if (layer == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(worldPos);
            if (sp.z <= 0.1f) return;
            GameBootstrap g = GameBootstrap.Instance;
            if (g != null && g.Hud != null) sp.x = g.Hud.PushRightOfOrderCards(new Vector2(sp.x, sp.y));
            // Bong bóng rộng tối đa ~430 đơn vị, neo đáy: giữ trong màn hình (khách sát mép không bị cắt chữ), không chạm hàng chip trên cùng
            sp.x = Mathf.Clamp(sp.x, Screen.width * 0.2f, Screen.width * 0.8f);
            sp.y = Mathf.Min(sp.y, Screen.height * 0.74f);
            layer.SpawnBark(new Vector2(sp.x, sp.y), text, color);
        }

        /// Chữ nổi tại toạ độ màn hình (pixel).
        public static void FloatTextScreen(Vector2 screenPos, string text, Color color)
        {
            if (layer != null) layer.Spawn(screenPos, text, color);
        }

        /// Banner ruy-băng vàng dưới thanh chip ("LÊN LEVEL 2! +180 Xu"), tự ẩn sau 2 s, không chặn chạm.
        public static void Banner(string text)
        {
            if (layer != null) layer.ShowBanner(text);
        }

        /// Chữ nổi cho thông báo chung: chữ nhỏ, ngay trên mép bàn pha (ToastScreenY), không chặn chạm.
        public static void FloatTextTop(string text, Color color)
        {
            if (layer != null) layer.SpawnTop(text, color);
        }
    }
}
