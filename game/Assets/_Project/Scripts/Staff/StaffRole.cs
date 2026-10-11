namespace Cafe
{
    public enum StaffRole
    {
        Cleaner,
        Server
    }

    public static class StaffRoles
    {
        public static string ToId(StaffRole role)
        {
            return role == StaffRole.Cleaner ? "clean" : "serve";
        }

        public static bool TryParse(string id, out StaffRole role)
        {
            if (id == "clean")
            {
                role = StaffRole.Cleaner;
                return true;
            }
            if (id == "serve")
            {
                role = StaffRole.Server;
                return true;
            }
            role = StaffRole.Cleaner;
            return false;
        }

        public static string DefaultName(StaffRole role)
        {
            return role == StaffRole.Cleaner ? "Cô Út" : "Bạn An";
        }
    }
}
