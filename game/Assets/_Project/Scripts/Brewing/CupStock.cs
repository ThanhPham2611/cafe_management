namespace Cafe
{
    /// Kho ly sạch. Ly đi theo khách, về lại khi thu ly để lại.
    public class CupStock
    {
        public int Count { get; private set; }
        public event System.Action Changed;

        public void Init(int count)
        {
            Count = count;
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
