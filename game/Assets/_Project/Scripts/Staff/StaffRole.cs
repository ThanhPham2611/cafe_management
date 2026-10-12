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

        public static string DefaultName(StaffRole role)
        {
            switch (role)
            {
                case StaffRole.Server: return "Bạn An";
                case StaffRole.Brewer: return "Anh Bảo";
                default: return "Cô Út";
            }
        }
    }
}
