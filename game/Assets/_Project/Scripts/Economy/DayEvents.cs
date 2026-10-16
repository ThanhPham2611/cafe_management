namespace Cafe
{
    /// Sự kiện của một ngày: đổi nhịp chơi (khách đông / ít, kiên nhẫn, tip) và món được gọi nhiều. Chọn xác định theo số ngày
    /// (cùng ngày luôn cùng sự kiện, không cần lưu) nên báo trước được ở pha Chuẩn bị. Hai ngày đầu luôn là ngày thường (hướng dẫn).
    /// Cuối tuần (ngày game chia 7 dư 6 hoặc 0) luôn là "Cuối tuần".
    public sealed class DayEvent
    {
        public readonly string id;
        public readonly string name;
        public readonly string blurb;
        public readonly float demand;        // nhân nhu cầu khách trong ngày
        public readonly float tip;           // nhân tiền tip
        public readonly float patience;      // nhân thời gian kiên nhẫn chờ món
        public readonly float favoredWeight; // nhân xác suất gọi các món được ưa chuộng
        private readonly string[] favored;

        public DayEvent(string id, string name, string blurb, float demand, float tip, float patience, float favoredWeight, string[] favored)
        {
            this.id = id;
            this.name = name;
            this.blurb = blurb;
            this.demand = demand;
            this.tip = tip;
            this.patience = patience;
            this.favoredWeight = favoredWeight;
            this.favored = favored ?? new string[0];
        }

        public bool IsNormal { get { return id == "normal"; } }

        /// Hệ số gọi món cho công thức recipeId (1 = bình thường).
        public float WeightFor(string recipeId)
        {
            for (int i = 0; i < favored.Length; i++)
                if (favored[i] == recipeId) return favoredWeight;
            return 1f;
        }
    }

    public static class DayEvents
    {
        public static readonly DayEvent Normal = new DayEvent("normal", "Ngày thường", "", 1f, 1f, 1f, 1f, null);
        public static readonly DayEvent Rain = new DayEvent("rain", "Trời mưa", "khách ít hơn nhưng ngồi lâu, thích cà phê nóng",
            0.80f, 1f, 1.30f, 2f, new[] { "ca_phe_den", "ca_phe_sua" });
        public static readonly DayEvent Rush = new DayEvent("rush", "Giờ cao điểm", "khách đông, hay sốt ruột",
            1.15f, 1f, 0.85f, 1f, null);
        public static readonly DayEvent Weekend = new DayEvent("weekend", "Cuối tuần", "khách đông hơn, tip hậu hĩnh",
            1.10f, 1.10f, 1f, 1f, null);
        public static readonly DayEvent Heat = new DayEvent("heat", "Nắng nóng", "khách gọi trà đá, trà chanh nhiều",
            1.00f, 1f, 1f, 2f, new[] { "tra_da", "tra_chanh" });
        public static readonly DayEvent Students = new DayEvent("students", "Ngày sinh viên", "nhiều bạn trẻ, gọi bạc xỉu và cà phê sữa",
            1.10f, 0.90f, 1f, 2f, new[] { "bac_xiu", "ca_phe_sua" });

        public static readonly DayEvent[] All = { Normal, Rain, Rush, Weekend, Heat, Students };

        public static DayEvent For(int day)
        {
            if (day <= 2) return Normal;
            int dow = day % 7;
            if (dow == 6 || dow == 0) return Weekend;
            int h = (int)((unchecked((uint)day * 2654435761u) >> 8) % 100u);
            if (h < 50) return Normal;
            if (h < 64) return Rain;
            if (h < 78) return Rush;
            if (h < 89) return Heat;
            return Students;
        }
    }
}
