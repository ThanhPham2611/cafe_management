namespace Cafe
{
    /// Chữ của màn khởi động / màn tải / màn tiêu đề (tiếng Việt). Tách riêng khỏi Strings.cs.
    public static class BootStrings
    {
        public const string LogoFallback = "Quán Cà Phê Việt";
        public const string Loading = "Đang pha cà phê...";
        public const string TipPrefix = "Mẹo: ";

        public const string TitleStart = "MỞ QUÁN";
        public const string TitleSettings = "CÀI ĐẶT";

        /// Mẹo ngẫu nhiên dưới thanh tải (docs/onboarding_spec.md §4.2).
        public static readonly string[] Tips =
        {
            "Thu ly sớm để ghế trống cho khách mới.",
            "Chạm vào khách để nhận đơn, chạm lần nữa để mang ly ra.",
            "Làm theo thứ tự nguyên liệu trên bảng pha để ly đạt chuẩn.",
            "Vòng tròn quanh thẻ đơn là kiên nhẫn. Hết vòng là khách bỏ đi!",
            "Phục vụ nhanh sẽ được thêm tiền tip.",
            "Sao tăng thì khách đông hơn.",
            "Gom đủ tiền cọc để thuê mặt bằng lớn hơn.",
            "Kéo một ngón để xoay quán, hai ngón để phóng to.",
            "Hết ly sạch thì khách phải chờ. Nhớ thu ly về nhé."
        };
    }
}
