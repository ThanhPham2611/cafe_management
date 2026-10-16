using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    public class OrderQueue
    {
        private readonly List<Order> orders = new List<Order>();
        private int nextId = 1;

        public IReadOnlyList<Order> Items { get { return orders; } }
        public int Count { get { return orders.Count; } }

        public Order Add(Customer customer, RecipeDef recipe, bool special = false)
        {
            var o = new Order { id = nextId++, customer = customer, recipe = recipe, timeOrdered = Time.time };
            if (special)
            {
                o.ownerOnly = true;
                o.rewardMult = SpecialOrders.RewardMult;
            }
            orders.Add(o);
            GameEvents.RaiseOrderAdded(o);
            return o;
        }

        public void Remove(Order o)
        {
            if (o != null && orders.Remove(o)) GameEvents.RaiseOrderRemoved(o);
        }

        public void Clear()
        {
            while (orders.Count > 0) Remove(orders[orders.Count - 1]);
        }
    }
}
