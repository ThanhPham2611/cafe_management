namespace Cafe
{
    /// Một đơn gọi món của khách đang chờ.
    public class Order
    {
        public int id;
        public Customer customer;
        public RecipeDef recipe;
        public float timeOrdered;
    }
}
