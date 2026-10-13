using UnityEngine;

namespace Cafe
{
    /// 3 mức đồ họa. Thứ tự = chỉ số mức chất lượng trong ProjectSettings/QualitySettings (Low 0, Medium 1, High 2);
    /// mỗi mức dùng một URP asset riêng (URP_Mobile_Low / URP_Mobile_Medium / URP_Mobile).
    public enum GraphicsLevel { Low = 0, Medium = 1, High = 2 }

    /// Bảng thông số và luật chọn mức theo máy. Thuần logic (không đụng Unity runtime) để kiểm thử được.
    ///   Thấp : không bóng đổ, độ nét 70%, tối đa 30 FPS, bỏ viền chữ UI  — máy yếu.
    ///   Vừa  : bóng cứng 512, độ nét 85%, 60 FPS                          — máy tầm trung.
    ///   Cao  : bóng mềm 1024, khử răng cưa MSAA 2x, độ nét 100%, 60 FPS   — máy mạnh / máy tính.
    public static class GraphicsPresets
    {
        public const int Count = 3;
        public static readonly GraphicsLevel[] All = { GraphicsLevel.Low, GraphicsLevel.Medium, GraphicsLevel.High };

        public static GraphicsLevel Clamp(int index)
        {
            return (GraphicsLevel)Mathf.Clamp(index, 0, Count - 1);
        }

        public static string Name(GraphicsLevel level)
        {
            switch (level)
            {
                case GraphicsLevel.Low: return "THẤP";
                case GraphicsLevel.Medium: return "VỪA";
                default: return "CAO";
            }
        }

        public static string Description(GraphicsLevel level)
        {
            switch (level)
            {
                case GraphicsLevel.Low: return "Máy yếu: tắt bóng đổ, giảm độ nét, tối đa 30 khung hình/giây";
                case GraphicsLevel.Medium: return "Cân bằng: bóng đổ nhẹ, độ nét 85%, 60 khung hình/giây";
                default: return "Đẹp nhất: bóng mềm, khử răng cưa, độ nét đầy đủ, 60 khung hình/giây";
            }
        }

        /// Giới hạn khung hình trên điện thoại (máy yếu chạy 30 để mát và pin lâu).
        public static int TargetFps(GraphicsLevel level)
        {
            return level == GraphicsLevel.Low ? 30 : 60;
        }

        /// Viền chữ UI (Outline) nhân bốn số đỉnh của mọi chữ: tắt ở mức Thấp.
        public static bool UiOutlines(GraphicsLevel level)
        {
            return level != GraphicsLevel.Low;
        }

        /// Mức gợi ý cho lần chạy đầu. Máy tính luôn Cao. Điện thoại: RAM ≤ 3,2 GB hoặc ≤ 4 nhân → Thấp;
        /// RAM ≥ 5,5 GB và ≥ 8 nhân → Cao; còn lại → Vừa.
        public static GraphicsLevel Recommend(int systemMemoryMb, int processorCount, bool mobile)
        {
            if (!mobile) return GraphicsLevel.High;
            if (systemMemoryMb <= 3200 || processorCount <= 4) return GraphicsLevel.Low;
            if (systemMemoryMb >= 5500 && processorCount >= 8) return GraphicsLevel.High;
            return GraphicsLevel.Medium;
        }
    }
}
