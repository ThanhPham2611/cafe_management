namespace Cafe
{
    /// Hằng số cấu hình theo cấp quán: ô palette dùng cho phòng thuê, id nội thất bố cục mặc định.
    /// Số cân bằng (giá cọc, tiền thuê, nhu cầu...) nằm ở GameBalance, không ở đây.
    public static class TierConfig
    {
        // ---- Ô palette (art_bible §3) ----
        public const int CellWallCream = 11;     // tường kem
        public const int CellWainscot = 36;      // xanh bạc hà (chân tường)
        public const int CellTileCream = 40;     // gạch bông kem
        public const int CellTileRed = 41;       // gạch bông đỏ gạch
        public const int CellTileBlue = 42;      // gạch bông xanh
        public const int CellTileYellow = 43;    // gạch bông vàng
        public const int CellPavement = 45;      // gạch xám vỉa hè
        public const int CellGrout = 47;         // vữa
        public const int CellRoad = 51;          // lòng đường (than)
        public const int CellWoodDark = 19;      // gỗ óc chó (khung, nẹp)
        public const int CellWoodMid = 18;       // gỗ trung
        public const int CellPaper = 15;         // giấy/menu
        public const int CellCream = 12;         // be ấm
        public const int CellAwningRed = 56;     // đỏ cờ
        public const int CellAwningWhite = 31;   // nhựa trắng
        public const int CellDoorMat = 25;       // đỏ tối
        public const int CellRoadLine = 31;      // vạch đường

        // ---- Id nội thất bố cục mặc định cấp 2 (và bản dự phòng khi chưa import model) ----
        public const string IdBar = "Counter_Bar_4x2";
        public const string IdBarFallback = "Cart_Coffee_3x2";
        public const string IdSink = "Sink_Counter";
        public const string IdShelf = "Shelf_Cups";
        public const string IdTable = "Table_Wood_2Seat";
        public const string IdTableFallback = "Table_Low_Blue_2x2";
        public const string IdChair = "Chair_Wood";
        public const string IdChairFallback = "Stool_Plastic_Red";
        public const string IdFan = "Fan_Ceiling";
        public const string IdLamp = "Lamp_Pendant";

        public static string TierName(int tier)
        {
            switch (tier)
            {
                case 1: return Strings.TierName1;
                case 2: return Strings.TierName2;
                case 3: return Strings.TierName3;
                default: return Strings.TierName4;
            }
        }
    }
}
