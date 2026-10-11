using UnityEngine;

namespace Cafe
{
    /// CleanTable / WashCups / MopFloor chỉ dùng khi CleaningRules.Enabled (T2+).
    public enum OwnerTaskType { ReturnToCart, Deliver, CollectCup, CleanTable, WashCups, MopFloor }

    /// Một việc trong hàng đợi của chủ quán (tối đa 3 việc do người chơi giao; ReturnToCart và WashCups tự thêm không tính).
    public class OwnerTask
    {
        public OwnerTaskType type;
        public Customer customer;   // Deliver
        public Order order;         // Deliver: đơn của khách lúc giao việc
        public RecipeDef recipe;    // Deliver: món đang cầm (cho icon)
        public LeftoverCup cup;     // CollectCup
        public TableDirt table;     // CleanTable: bàn cần lau
        public WorkJob job;         // CleanTable / MopFloor: job trong WorkQueue; WashCups: ly đang rửa

        public bool started;        // đã đặt đích đi cho việc này
        public bool arrived;        // đã tới nơi (đang quay mặt/làm nốt)
        public float timer;         // giây kể từ lúc bắt đầu (chống kẹt)
        public float arriveTimer;   // giây kể từ lúc tới nơi
        public float work;          // WashCups: giây đã rửa ly hiện tại
        public bool dropped;        // WashCups: đã đặt ly bẩn xuống bồn
        public Vector3 dest;
    }
}
