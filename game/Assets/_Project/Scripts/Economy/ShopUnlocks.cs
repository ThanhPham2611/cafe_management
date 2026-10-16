using System.Collections.Generic;

namespace Cafe
{
    /// Đồ trang trí / nội thất cao cấp mở dần theo LEVEL uy tín (Cửa hàng hiện "Mở ở Lv N"): thêm lý do lên level ở những đoạn dài
    /// chỉ gom tiền. Chỉ khóa MUA món mới: món đang đặt, trong kho hay di chuyển không bị ảnh hưởng. Ghế / bàn thường, máy pha và
    /// món rẻ nhất mỗi cấp để mở sẵn (không khóa thứ cần để chơi). Cấp 2 vào ở Lv6 (★2), cấp 3 vào ở Lv16 (★3).
    public static class ShopUnlocks
    {
        private static readonly Dictionary<string, int> MinLevel = new Dictionary<string, int>
        {
            // Vỉa hè (Lv1-5)
            { "Plant_Areca_Tall", 2 }, { "Board_Menu_A", 3 }, { "Fan_Stand", 3 }, { "Jar_Tea_Big", 4 }, { "Umbrella_Street_4x4", 5 },
            // Mặt bằng (Lv6-15)
            { "Plant_Monstera", 7 }, { "Lamp_Floor", 8 }, { "Lantern_Hoian", 9 }, { "Radio_Stand", 10 },
            { "Shelf_Cups", 11 }, { "Fan_Ceiling", 12 }, { "Bookshelf_Low", 14 },
            // Nhà hàng (Lv16+)
            { "Chair_Leather", 17 }, { "Easel_Art", 18 }, { "Armchair_Velvet", 20 }, { "Aircon_Standing", 20 },
            { "Chandelier_Brass", 22 }, { "Table_Marble_Round", 24 }, { "Piano_Upright", 28 },
        };

        /// Level tối thiểu để mua món `id` (1 = mở sẵn).
        public static int RequiredLevel(string id)
        {
            int lv;
            return !string.IsNullOrEmpty(id) && MinLevel.TryGetValue(id, out lv) ? lv : 1;
        }

        public static bool IsUnlocked(string id, int level) { return level >= RequiredLevel(id); }

        /// Các món mở đúng ở `level` (để báo "Mở khóa: ...").
        public static List<string> UnlockedAt(int level)
        {
            var list = new List<string>();
            foreach (KeyValuePair<string, int> kv in MinLevel)
                if (kv.Value == level) list.Add(kv.Key);
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        public static IEnumerable<KeyValuePair<string, int>> All { get { return MinLevel; } }
    }

    /// Danh hiệu của quán theo level uy tín: hiện ở màn tổng kết, báo khi đạt danh hiệu mới.
    public static class LevelTitles
    {
        private static readonly int[] Levels = { 1, 3, 6, 10, 16, 21, 26, 31, 36, 41 };
        private static readonly string[] Names =
        {
            "Chủ quán tập sự", "Quán của xóm", "Quán có tiếng", "Điểm hẹn của phố", "Nhà hàng cà phê",
            "Thương hiệu địa phương", "Quán được yêu thích", "Có tên trên bản đồ", "Cà phê của cả thành phố", "Huyền thoại cà phê Việt",
        };

        public static string TitleFor(int level)
        {
            int idx = 0;
            for (int i = 0; i < Levels.Length; i++)
                if (level >= Levels[i]) idx = i;
            return Names[idx];
        }

        /// `level` có phải đúng level bắt đầu một danh hiệu mới không (Lv1 không tính).
        public static bool IsTitleLevel(int level)
        {
            for (int i = 1; i < Levels.Length; i++)
                if (Levels[i] == level) return true;
            return false;
        }

        public static int Count { get { return Names.Length; } }
    }
}
