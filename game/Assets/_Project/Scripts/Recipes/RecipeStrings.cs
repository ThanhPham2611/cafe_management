namespace Cafe
{
    /// Chuỗi tiếng Việt của hệ công thức/pha chế Tier 2 (tách riêng để không đụng Strings.cs dùng chung).
    public static class RecipeStrings
    {
        // ---- Sổ công thức ----
        public const string Book = "Sổ công thức";
        public const string Close = "Đóng";
        public const string MenuCountFmt = "Menu hôm nay: {0}/{1}";
        public const string MenuAllFmt = "Menu: tất cả {0} món";
        public const string OnMenu = "Trong menu";
        public const string OffMenu = "Ngoài menu";
        public const string MenuFullFmt = "Menu đã đủ {0} món - hãy tắt bớt một món";
        public const string MenuNeedOne = "Menu cần ít nhất 1 món";
        public const string MenuLockedT1 = "Vỉa hè: bán tất cả món";
        public const string UnknownName = "???";
        public const string HintCostFmt = "Gợi ý {0} Hạt";
        public const string NeedGemsFmt = "Thiếu {0} Hạt";
        public const string HintBoughtFmt = "Đã mua gợi ý: {0}";
        public const string GemsFmt = "{0} Hạt";
        public const string RecipeCostFmt = "Giá {0} Xu - vốn {1} Xu";
        public const string NeedMachineFmt = "Cần đặt {0} trong quán";
        public const string TeaserT1 = "Còn nhiều công thức bí truyền khi bạn thuê Mặt bằng.";
        public const string NoRecipes = "Chưa có công thức nào";

        // ---- Thử pha tự do ----
        public const string Experiment = "Thử pha tự do";
        public const string ExperimentLockedT2 = "Mở từ Mặt bằng";
        public const string ExperimentTitle = "Thử pha tự do";
        public const string ExperimentHintFmt = "Mỗi nguyên liệu {0} Xu - bấm Xong khi đủ";
        public const string ExperimentStepsFmt = "{0}/{1} nguyên liệu";
        public const string Done = "Xong";
        public const string Exit = "Thoát";
        public const string NeedIdleToExperiment = "Hãy xong ly đang pha trước";
        public const string NeedNoOrderToExperiment = "Bỏ chọn đơn trước khi thử pha";
        public const string ExperimentOn = "Bắt đầu thử pha tự do";

        // ---- Khám phá / món lỗi ----
        public const string DiscoveredFmt = "Khám phá công thức: {0}!";
        public const string DiscoveredSubFmt = "{0} đã vào Sổ công thức";
        public const string AddedToMenuFmt = "Đã thêm {0} vào menu";
        public const string FirstSaleFmt = "Bán thử ly đầu: +{0} Xu";
        public const string FailedDrinkFmt = "Món lỗi! Bán rẻ được {0} Xu";
        public const string KnownDrinkFmt = "Món này đã có trong sổ: {0}";
        public const string NotEnoughXuExperimentFmt = "Cần {0} Xu mỗi nguyên liệu";
        public const string HintBought = "Đã mở gợi ý - xem thứ tự nguyên liệu trong Sổ công thức";

        // ---- Thao tác ----
        public const string HoldHintFmt = "Giữ nút {0} cho đủ lượng";
        public const string SwipeHintFmt = "Vuốt lên trên nút {0}";
        public const string SpinHintFmt = "Xoay vòng tròn trên nút {0}";
        public const string TapHintFmt = "Chạm nút {0}";
        public const string HoldTooShort = "Giữ lâu hơn một chút!";
        public const string SwipeTooShort = "Vuốt lên dài hơn!";
        public const string SpinTooShort = "Xoay đủ một vòng!";
        public const string HoldFailed = "Rót thiếu! Sai bước";
        public const string NeedToolForThree = "Cần dụng cụ cấp 2 để được 3 sao";
        public const string SpeedLabelHidden = "Pha nhanh + dụng cụ cấp 2 = 3 sao";

        // ---- Dụng cụ ----
        public const string ToolsTitle = "Dụng cụ";
        public const string ToolLevelFmt = "{0} cấp {1}";
        public const string ToolUpgradeFmt = "{0}: nâng cấp {1} - {2} Xu";
        public const string ToolMaxFmt = "{0}: cấp tối đa";
        public const string ToolUpgradedFmt = "Đã nâng {0} lên cấp {1}";

        public static string ActionName(StepAction a)
        {
            switch (a)
            {
                case StepAction.Hold: return "Giữ";
                case StepAction.Swipe: return "Vuốt";
                case StepAction.Spin: return "Xoay";
                default: return "Chạm";
            }
        }

        public static string ActionHint(BrewStep step)
        {
            string name = Strings.StepName(step);
            switch (StepActions.For(step))
            {
                case StepAction.Hold: return string.Format(HoldHintFmt, name);
                case StepAction.Swipe: return string.Format(SwipeHintFmt, name);
                case StepAction.Spin: return string.Format(SpinHintFmt, name);
                default: return string.Format(TapHintFmt, name);
            }
        }

        public static string ToolName(string id)
        {
            switch (id)
            {
                case "phin": return "Phin";
                case "may_danh_trung": return "Máy đánh trứng";
                case "binh_lac": return "Bình lắc";
            }
            return id;
        }

        /// Tên nguyên liệu/thao tác. Strings.StepName gọi hàm này cho mọi giá trị nó không tự xử lý.
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
                case BrewStep.Muoi: return "Muối";
                case BrewStep.KemMuoi: return "Kem muối";
                case BrewStep.CotDua: return "Cốt dừa";
                case BrewStep.Trung: return "Trứng";
                case BrewStep.Chanh: return "Chanh";
                case BrewStep.Lac: return "Lắc";
                case BrewStep.Danh: return "Đánh";
                case BrewStep.Cacao: return "Cacao";
            }
            return s.ToString();
        }

        /// Tên ngắn trên thẻ order.
        public static string ShortName(RecipeDef r)
        {
            if (r == null) return "?";
            switch (r.id)
            {
                case "tra_da": return "Trà đá";
                case "ca_phe_den": return "CF đen";
                case "ca_phe_sua": return "CF sữa";
                case "bac_xiu": return "Bạc xỉu";
                case "tra_chanh": return "Trà chanh";
                case "ca_phe_muoi": return "CF muối";
                case "ca_phe_cot_dua": return "CF cốt dừa";
                case "ca_phe_trung": return "CF trứng";
            }
            return string.IsNullOrEmpty(r.displayName) ? r.id : r.displayName;
        }
    }
}
