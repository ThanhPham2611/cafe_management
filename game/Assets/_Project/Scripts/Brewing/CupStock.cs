namespace Cafe
{
    /// Kho ly sạch. Ly đi theo khách, về lại khi thu ly để lại.
    public class CupStock
    {
        public int Count { get; private set; }
        /// Tổng số ly của quán (trong kho + đang được dùng / chờ rửa). Chỉ tăng.
        public int Total { get; private set; }
        public event System.Action Changed;

        public void Init(int count)
        {
            Count = count;
            Total = count;
            if (Changed != null) Changed();
        }

        /// Mua / bổ sung thêm ly cho đủ total (không bao giờ bớt ly).
        public void EnsureTotal(int total)
        {
            if (total <= Total) return;
            int add = total - Total;
            Total = total;
            Count += add;
            if (Changed != null) Changed();
        }

        public bool TryTake()
        {
            if (Count <= 0) return false;
            Count--;
            if (Changed != null) Changed();
            return true;
        }

        public void Return()
        {
            Count++;
            if (Changed != null) Changed();
        }
    }
}
