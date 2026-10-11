using UnityEngine;

namespace Cafe
{
    /// Một bước thao tác khi pha. Tier 1 dùng chạm, Tier 2 sẽ thêm giữ/vuốt/xoay.
    /// Giá trị mới (Tier 2) chỉ được THÊM vào cuối: asset cũ lưu theo số nguyên nên không được đổi thứ tự.
    /// Lac/Danh là thao tác (lắc/đánh); Muoi..Cacao là nguyên liệu.
    public enum BrewStep { Phin, SuaDac, SuaTuoi, Da, Tra, Duong, Muoi, KemMuoi, CotDua, Trung, Chanh, Lac, Danh, Cacao }

    /// Công thức đồ uống. Số liệu theo docs/economy_monetization.md §2.1.
    [CreateAssetMenu(menuName = "Cafe/Recipe Def", fileName = "SO_Recipe_New")]
    public class RecipeDef : ScriptableObject
    {
        public string id;
        public string displayName;
        [Tooltip("Giá bán ở chất lượng 2★ (Xu)")]
        public int price;
        public int ingredientCost;
        [Tooltip("Thời gian pha chuẩn (giây thực)")]
        public float brewSeconds;
        public BrewStep[] steps;
        public int unlockTier = 1;
        [Tooltip("Ngày in-game mở khóa (0 = có từ đầu)")]
        public int unlockDay;
        [Tooltip("Tỉ trọng khách gọi món này")]
        public float orderWeight = 1f;

        [Header("Giai đoạn 2 (công thức ẩn)")]
        [Tooltip("Món ẩn: chỉ có trong Sổ công thức khi khám phá được (thử pha tự do hoặc mua gợi ý)")]
        public bool hidden;
        [TextArea]
        [Tooltip("Gợi ý hiện khi chưa mở khóa, vd. 'Một vị mặn bất ngờ từ Huế...'")]
        public string hint;
        [Tooltip("Dụng cụ cần (id: phin, may_danh_trung, binh_lac). Món ẩn cần mọi dụng cụ cấp >= 2 mới được 3 sao")]
        public string[] requiredTools;
        [Tooltip("Giá gợi ý (Hạt cà phê vàng)")]
        public int hintCostGems = 30;
    }
}
