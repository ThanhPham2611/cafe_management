using UnityEngine;

namespace Cafe
{
    public enum FurnitureKind { Seat, Table, Decor, BrewStation }

    /// Định nghĩa một món nội thất. Asset SO_Furniture_* do Editor/AssetPipeline tạo từ FBX.
    [CreateAssetMenu(menuName = "Cafe/Furniture Def", fileName = "SO_Furniture_New")]
    public class FurnitureDef : ScriptableObject
    {
        public string id;
        public string displayName;
        public GameObject prefab;
        [Tooltip("Số ô 0.5 m theo X (ngang) và Z (sâu) của Unity")]
        public Vector2Int footprint = Vector2Int.one;
        public int price;
        public FurnitureKind kind;
        [Range(0, 10)] public int comfort;
        [Range(0, 15)] public int style;
        [Tooltip("Chiều cao mặt ngồi (m), chỉ dùng cho Seat")]
        public float seatHeight;

        [Header("Treo trên cao (quạt, đèn thả, chậu treo)")]
        [Tooltip("Không chiếm ô lưới, không chặn đường đi; có thể đặt đè lên đồ khác")]
        public bool overhead;
        [Tooltip("Độ cao (m) của đáy model khi treo")]
        public float hangY;

        [Header("Đặt trên mặt quầy pha (bóng đèn, đèn bàn)")]
        [Tooltip("Đồ đặt lên mặt quầy pha: chỉ đặt được trên quầy, không chiếm ô sàn, đặt đè được")]
        public bool counterMount;
        [Tooltip("(Trên def của quầy) độ cao mặt quầy (m); 0 = quầy không nhận đồ đặt trên mặt")]
        public float counterTopY;
        [Tooltip("(Trên def của quầy) vùng XZ cục bộ (m) mà tâm đồ đặt được, để tránh tủ bánh")]
        public Rect counterMountArea;

        [Header("Cấp quán")]
        [Tooltip("Cấp quán tối thiểu để mua/đặt (1 = có từ vỉa hè)")]
        public int minTier = 1;
        [Tooltip("Chỉ đặt được ở cấp quán này (0 = mọi cấp từ minTier)")]
        public int onlyTier;

        [Header("Cửa hàng (GDD §2.1)")]
        [Tooltip("Hiện trong cửa hàng để mua")]
        public bool shopVisible = true;
        [Tooltip("Ảnh thu nhỏ trong cửa hàng")]
        public Sprite icon;
        [Tooltip("Giá cộng thêm cho mỗi món cùng loại đã sở hữu (ghế nhựa: 75)")]
        public int priceStep;
        [Tooltip("Số lượng tối đa được đặt (0 = không giới hạn)")]
        public int maxCount;
        [Tooltip("% khách tăng thêm mỗi món (chậu cây 5, bảng menu 12). Tổng bị chặn ở GameBalance.MaxAttractPercent")]
        public int attract;

        [Header("Máy móc (cấp 3)")]
        [Tooltip("Id máy (grinder, cold_brew, espresso); rỗng = không phải máy. Đặt trong quán mới có tác dụng (MachineRules)")]
        public string machineId;

        [Header("Quầy pha phụ / quầy line (cấp 3, docs/stations_spec.md)")]
        [Tooltip("Số chỗ làm của Pha chế ở quầy pha phụ (đứng phía nhân viên, pha × GameBalance.BrewSpotFactor); 0 = không phải quầy pha phụ")]
        public int brewSpots;
        [Tooltip("Số chỗ ly cộng thêm vào kệ giao (ReadyShelf) khi đặt quầy line; 0 = không phải quầy line")]
        public int shelfSlots;
    }
}
