namespace Cafe
{
    /// Chỉ thêm giá trị mới vào cuối: save lưu theo chuỗi id nhưng giữ thứ tự ổn định cho UI.
    public enum StaffRole
    {
        Cleaner,
        Server,
        Brewer
    }

    public static class StaffRoles
    {
        public static readonly StaffRole[] All = { StaffRole.Cleaner, StaffRole.Server, StaffRole.Brewer };

        public static string ToId(StaffRole role)
        {
            switch (role)
            {
                case StaffRole.Server: return "serve";
                case StaffRole.Brewer: return "brew";
                default: return "clean";
            }
        }

        public static bool TryParse(string id, out StaffRole role)
        {
            switch (id)
            {
                case "clean": role = StaffRole.Cleaner; return true;
                case "serve": role = StaffRole.Server; return true;
                case "brew": role = StaffRole.Brewer; return true;
            }
            role = StaffRole.Cleaner;
            return false;
        }

        private static readonly string[] CleanerNames = { "Cô Út", "Cô Hạnh", "Chị Mai" };
        private static readonly string[] ServerNames = { "Bạn An", "Bạn Lộc" };
        private static readonly string[] BrewerNames = { "Anh Bảo", "Anh Khoa" };

        /// Tên mặc định cho người thứ n (từ 0) của vai trò; vượt danh sách thì quay vòng.
        public static string DefaultName(StaffRole role, int n = 0)
        {
            string[] names = role == StaffRole.Server ? ServerNames : (role == StaffRole.Brewer ? BrewerNames : CleanerNames);
            return names[((n % names.Length) + names.Length) % names.Length];
        }

        /// Dáng nhân viên (0..3 = PF_Char_Staff_M1, M2, F1, F2): mỗi vai trò một dáng riêng, người thứ hai đổi dáng.
        public static int DefaultPreset(StaffRole role, int n = 0)
        {
            int baseLook = role == StaffRole.Server ? 0 : (role == StaffRole.Brewer ? 1 : 2);
            return (baseLook + 3 * (n & 1)) % 4;
        }
    }
}
