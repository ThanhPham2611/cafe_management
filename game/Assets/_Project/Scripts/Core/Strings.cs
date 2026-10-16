namespace Cafe
{
    /// Toàn bộ chuỗi hiển thị tiếng Việt (sau này chuyển sang Unity Localization).
    public static class Strings
    {
        public const string XuFmt = "{0} Xu";
        public const string XuShortFmt = "{0}";
        public const string DayFmt = "Ngày {0}";
        public const string CupsFmt = "{0}";
        public const string RpFmt = "{0} / {1} RP";
        public const string RpMaxFmt = "{0} RP";

        public const string OrdersTitle = "Đơn chờ";
        public const string NoOrders = "Chờ khách ghé quán...";
        public const string MoreOrdersFmt = "+{0} đơn nữa";

        public const string PickOrder = "Chạm một đơn để pha";
        public const string TapInOrder = "Chạm các nút theo đúng thứ tự";
        public const string DoneStepsFmt = "Đã làm: {0}";
        public const string Cancel = "Hủy";
        public const string DiscardCup = "Đổ ly";
        public const string ReadyTitleFmt = "{0} đã sẵn sàng!";
        public const string ReadyHint = "Chạm đúng khách để phục vụ";
        public const string QualityFmt = "Chất lượng: {0}/3 sao";
        public const string TimerFmt = "{0:0.0} s";
        public const string SpeedLabel = "Pha nhanh để được 3 sao";
        public const string SpeedLate = "Chậm rồi, vẫn được 2 sao";
        public const string RecipeTitleFmt = "{0}  -  {1} Xu";

        public const string Wrong = "Sai bước!";
        public const string Ruined = "Hỏng ly! Mất nguyên liệu";
        public const string BrewDone = "Xong rồi! Chạm khách để phục vụ";
        public const string NoCup = "Hết ly sạch! Thu ly khách để lại";
        public const string NoCupWaitingFmt = "Hết ly sạch! Có {0} ly cần thu, chạm ly trên bàn";
        public const string CupReady = "Thu ly!";
        public const string CupsWaitingFmt = "{0} ly chờ thu";
        public const string NoOrderSelected = "Hãy chọn một đơn trước";
        public const string HoldingDrink = "Đang cầm ly - hãy phục vụ khách trước";
        public const string BrewingInProgress = "Đang pha dở - hoàn tất hoặc bấm Hủy";
        public const string WrongCustomer = "Sai khách rồi";
        public const string OrderTakenByBrewer = "Nhân viên đang lo ly này rồi";
        public const string NeedOrderFirst = "Chọn đơn của khách này để pha";
        public const string PaidFmt = "+{0} Xu";
        public const string PaidTipFmt = "+{0} Xu (tip {1})";
        public const string NewRecipeFmt = "Mở khóa món mới: {0}!";

        public const string SummaryTitleFmt = "Tổng kết ngày {0}";
        public const string SumRevenue = "Doanh thu";
        public const string SumTips = "Tiền tip";
        public const string SumScrap = "Thu ly";
        public const string SumIngredients = "Nguyên liệu";
        public const string SumFee = "Phí vỉa hè";
        public const string SumProfit = "Lãi ròng";
        public const string SumServed = "Khách phục vụ";
        public const string SumLost = "Khách bỏ đi";
        public const string SumRp = "Uy tín";
        public const string SumStars = "Hạng sao";
        public const string NewDay = "Ngày mới";
        public const string PrepShop = "Chuẩn bị quán";
        public const string OpenShop = "MỞ CỬA";
        public const string StoryRpFmt = "Truyện khách quen: +{0} RP";
        public const string OpeningGiftFmt = "Quà khai trương: +{0} Xu";

        // Cửa hàng / Build mode
        public const string Shop = "Cửa hàng";
        public const string TabSeats = "Ghế & bàn";
        public const string TabDecor = "Trang trí";
        public const string AttractBadgeFmt = "+{0}% khách";
        public const string ComfortBadgeFmt = "Thoải mái +{0}";
        public const string StyleBadgeFmt = "Phong cách +{0}";
        public const string NeedMoreXuFmt = "Thiếu {0} Xu";
        public const string MaxReachedFmt = "Đã đủ {0} món";
        public const string NotEnoughXu = "Không đủ Xu";
        public const string Rotate = "Xoay";
        public const string PlaceBtn = "Đặt";
        public const string PlaceBtnFmt = "Đặt {0}";
        public const string MoveBtn = "Di chuyển";
        public const string SellBtn = "Bán";
        public const string SellConfirmFmt = "Bán {0} lấy {1} Xu?";
        public const string BlockedPath = "Chặn lối đi rồi!";
        public const string SeatBusy = "Có khách đang ngồi";
        public const string CantPlaceHere = "Không đặt được ở đây";
        public const string CustomerInTheWay = "Có khách đang đứng đó";
        public const string EmptyShopTab = "Chưa có món nào";

        // ---- Chủ quán / tạo nhân vật (GDD §2.1) ----
        public const string OwnerBusy = "Chủ quán đang bận";
        public const string OwnerComing = "Chủ quán đang về quầy…";
        public const string OwnerDelivering = "Chủ quán đang mang ly ra…";
        public const string OwnerFullHands = "Chủ quán cầm đầy ly rồi";
        public const string OwnerCustomerGone = "Khách đã đi mất, đổ ly";
        public const string OwnerCantReach = "Chủ quán không tới được chỗ đó";
        public const string DefaultShopName = "Cà Phê Vỉa Hè";
        public const string CreatorTitle = "Chủ quán của bạn";
        public const string CreatorOwnerName = "Tên chủ quán";
        public const string CreatorShopName = "Tên quán";
        public const string CreatorLook = "Dáng người";
        public const string CreatorSkin = "Màu da";
        public const string CreatorHair = "Màu tóc";
        public const string CreatorShirt = "Màu áo";
        public const string CreatorOpen = "Mở quán!";
        public const string CreatorSave = "Lưu";
        public const string CreatorCancel = "Hủy";
        public const string CreatorNeedOwnerName = "Hãy đặt tên chủ quán";
        public const string CreatorNeedShopName = "Hãy đặt tên quán";
        public const string EditLook = "Sửa nhân vật";
        public static readonly string[] PresetNames = { "Nam 1", "Nam 2", "Nữ 1", "Nữ 2" };

        // ---- Dọn dẹp (T2+) ----
        public const string CleanChipFmt = "{0}%";
        public const string MopBtn = "Lau sàn";
        public const string SumCleanliness = "Độ sạch";
        public const string DirtyTable = "Bàn bẩn";
        public const string DirtyWalkAway = "Bẩn quá, đi chỗ khác!";
        public const string TableHasGuests = "Khách còn ngồi ở bàn này";
        public const string CupsToWashFmt = "+{0} ly bẩn";
        public const string FloorSparkling = "Sàn sạch bóng!";

        public const string CupName = "Ly";
        public const string DayChipFmt = "Ngày {0}";
        public const string TipFmt = "Tip +{0}";

        // ---- Cấp quán / Thuê mặt bằng (Tier) ----
        public const string TierName1 = "Vỉa hè";
        public const string TierName2 = "Thuê mặt bằng";
        public const string TierName3 = "Mua nhà hàng";
        public const string TierName4 = "Chuỗi cửa hàng";
        public const string TierCardTitle = "Thuê mặt bằng";
        public const string TierXuFmt = "Xu {0}/{1}";
        public const string TierRpFmt = "RP {0}/{1}";
        public const string TierCtaTitle = "Thuê mặt bằng!";
        public const string TierCtaSubFmt = "Đặt cọc {0} Xu";
        public const string TierNeedFmt = "Cần đủ {0} Xu và {1} RP để thuê mặt bằng";
        public const string TierConfirmTitle = "Thuê mặt bằng";
        public const string TierConfirmBodyFmt = "Đặt cọc {0} Xu, được hoàn lại khi mua nhà hàng.\nTiền thuê {1} Xu mỗi {2} ngày, tự trừ vào sáng đầu tuần.\nBàn ghế cũ được cất vào kho, bạn đặt lại tùy ý.";
        public const string TierYes = "Thuê ngay";
        public const string Tier3CardTitle = "Mua nhà hàng";
        public const string Tier3CtaTitle = "Mua nhà hàng!";
        public const string Tier3CtaSubFmt = "Trả {0} Xu (đã hoàn cọc)";
        public const string Tier3NeedFmt = "Cần đủ {0} Xu và {1} RP để mua nhà hàng";
        public const string Tier3ConfirmTitle = "Mua nhà hàng";
        public const string Tier3ConfirmBodyFmt = "Giá nhà {0} Xu, cọc {1} Xu được hoàn lại: bạn trả {2} Xu.\nKhông còn tiền thuê hàng tuần; chi phí vận hành {3} Xu mỗi ngày.\nBàn ghế cũ được cất vào kho; nhà hàng rộng hơn rất nhiều.";
        public const string Tier3YesLabel = "Mua ngay";
        public const string Tier3GrandOpening = "Nhà hàng!";
        public const string TierLater = "Để sau";

        // ---- Các khu của nhà hàng (cấp 3) ----
        public const string ZoneHall = "Sảnh chính";
        public const string ZoneGarden = "Sân vườn";
        public const string ZoneTerrace = "Lầu";
        public const string ZoneVip = "VIP";
        public const string ZoneGardenDesc = "Ngoài trời, 6 x 12 m\n+20% khách\nTặng 2 bộ bàn ghế";
        public const string ZoneTerraceDesc = "Gác gỗ ngắm phố, 6 x 6 m\n+20% khách\nTặng 2 bộ bàn ghế";
        public const string ZoneVipDesc = "Phòng riêng, 6 x 6 m\n+20% khách, ly x1,5 giá\nTặng 2 bộ bàn ghế";
        public const string ZoneButton = "MỞ RỘNG";
        public const string ZoneTitle = "Mở rộng nhà hàng";
        public const string ZoneOpenBtn = "Mở khu";
        public const string ZoneOpened = "ĐÃ MỞ";
        public const string ZoneCostFmt = "{0} Xu";
        public const string ZoneHint = "Mỗi khu mở thêm +20% khách và chỗ ngồi mới. Mở theo thứ tự bạn thích.";
        public const string ZoneNeedFmt = "Cần {0} Xu để mở khu {1}";
        public const string ZoneOpenedToastFmt = "Đã mở khu {0}!";
        public const string ZoneBlockedFmt = "Lối sang khu {0} đang bị đồ chặn: dọn một lối rộng 1 m ở tường sảnh rồi mở lại";
        public const string GrandOpening = "Khai trương!";
        public const string RentInfoFmt = "Thuê nhà: còn {0} ngày";
        public const string RentInfoTomorrow = "Thuê nhà: sáng mai";
        public const string DebtInfoFmt = "Nợ: {0} Xu";
        public const string RentPaidFmt = "Đã trả tiền thuê nhà {0} Xu";
        public const string RentShortFmt = "Thiếu Xu! Ghi nợ {0} Xu";
        public const string SumRent = "Thuê nhà";
        public const string SumDebt = "Nợ";
        public const string SumDebtFmt = "Nợ (còn {0})";
        public const string SumFixedCost = "Chi phí vận hành";
        public const string SumStaffWages = "Lương nhân viên";
        public const string InventoryFree = "Đặt miễn phí";
        public const string InventoryNameFmt = "{0}  x{1}";

        /// 1234567 -> "1.234.567" (không phụ thuộc culture).
        public static string FormatInt(int v)
        {
            bool neg = v < 0;
            if (neg) v = -v;
            string s = v.ToString();
            var sb = new System.Text.StringBuilder(s.Length + 4);
            if (neg) sb.Append('-');
            int first = s.Length % 3;
            if (first == 0) first = 3;
            sb.Append(s, 0, first);
            for (int i = first; i < s.Length; i += 3) sb.Append('.').Append(s, i, 3);
            return sb.ToString();
        }

        public static string StepName(BrewStep s)
        {
            switch (s)
            {
                case BrewStep.Phin: return "Phin";
                case BrewStep.SuaDac: return "Sữa đặc";
                case BrewStep.SuaTuoi: return "Sữa tươi";
                case BrewStep.Da: return "Đá";
                case BrewStep.Tra: return "Trà";
                case BrewStep.Duong: return "Đường";
            }
            return RecipeStrings.StepName(s); // các bước Tier 2 (muối, kem muối, cốt dừa...)
        }

        /// Tên ngắn cho bong bóng order.
        public static string ShortName(RecipeDef r)
        {
            if (r == null) return "?";
            switch (r.id)
            {
                case "tra_da": return "Trà đá";
                case "ca_phe_den": return "CF đen";
                case "ca_phe_sua": return "CF sữa";
                case "bac_xiu": return "Bạc xỉu";
            }
            return RecipeStrings.ShortName(r); // món Tier 2
        }

        public static string FullName(RecipeDef r)
        {
            if (r == null) return "?";
            return string.IsNullOrEmpty(r.displayName) ? ShortName(r) : r.displayName;
        }

        /// "Phin > Sữa đặc > Đá"
        public static string StepList(RecipeDef r, int upTo)
        {
            if (r == null || r.steps == null) return "";
            int n = upTo < 0 || upTo > r.steps.Length ? r.steps.Length : upTo;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append("  >  ");
                sb.Append(StepName(r.steps[i]));
            }
            return sb.ToString();
        }
    }
}
