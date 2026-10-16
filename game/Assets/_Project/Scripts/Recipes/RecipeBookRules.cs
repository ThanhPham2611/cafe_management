namespace Cafe
{
    /// Khi nào nút nổi "Sổ công thức" hiện trên HUD: chỉ lúc đang pha, đang mở sổ, hoặc bước hướng dẫn trỏ vào nút.
    /// Lúc khác vào sổ bằng mục trong MENU.
    public static class RecipeBookRules
    {
        public static bool ShowFloating(bool brewExpanded, bool tipPinned, bool overlayOpen)
        {
            return overlayOpen || brewExpanded || tipPinned;
        }
    }
}
