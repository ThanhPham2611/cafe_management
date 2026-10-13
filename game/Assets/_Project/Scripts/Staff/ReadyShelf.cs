using System;
using System.Collections.Generic;

namespace Cafe
{
    /// Quầy giao: nhân viên Pha chế đặt ly đã pha xong lên đây để nhân viên Phục vụ mang ra khách, nên Pha chế không phải
    /// đi bộ qua cả sảnh (ở nhà hàng lớn, đi giao chiếm phần lớn thời gian của Pha chế). Mỗi ly giữ một đơn (claim) cho tới khi
    /// có người mang đi. Thuần logic (không đụng thế giới) để kiểm thử được; ly bị bỏ (khách đã đi) được báo qua `discard`
    /// để trả ly sạch về kho.
    public sealed class ReadyShelf
    {
        public struct Drink
        {
            public Order order;
            public int quality;
        }

        public const int Capacity = 6;

        private readonly List<Drink> items = new List<Drink>(Capacity);
        private readonly Action discard;

        /// discard: gọi một lần cho mỗi ly bị bỏ (khách đã đi / hết ngày) để trả ly sạch về kho.
        public ReadyShelf(Action discard)
        {
            this.discard = discard;
        }

        public int Count { get { return items.Count; } }
        public bool HasRoom { get { return items.Count < Capacity; } }
        public IReadOnlyList<Drink> Items { get { return items; } }

        /// Đặt ly lên quầy. `from` là người đang giữ đơn (Pha chế): đơn chuyển sang quầy giao giữ. false nếu đầy / không giữ được.
        public bool TryAdd(Order order, int quality, object from)
        {
            if (order == null || quality <= 0 || !HasRoom) return false;
            order.Release(from);
            if (!order.TryClaim(this)) return false;
            items.Add(new Drink { order = order, quality = quality });
            return true;
        }

        /// Bỏ các ly mà khách không còn chờ (isWaiting = false). Trả về số ly bỏ.
        public int Prune(Func<Order, bool> isWaiting)
        {
            int removed = 0;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (isWaiting != null && isWaiting(items[i].order)) continue;
                items[i].order.Release(this);
                items.RemoveAt(i);
                removed++;
                if (discard != null) discard();
            }
            return removed;
        }

        /// Lấy ly cũ nhất mà `isServable` (khách còn chờ và tới được) cho `taker`: đơn chuyển sang taker. false nếu không có.
        public bool TryTake(object taker, Func<Order, bool> isServable, out Drink drink)
        {
            for (int i = 0; i < items.Count; i++)
            {
                Drink d = items[i];
                if (isServable != null && !isServable(d.order)) continue;
                d.order.Release(this);
                if (!d.order.TryClaim(taker))
                {
                    d.order.TryClaim(this); // taker không nhận được: trả lại quầy
                    continue;
                }
                items.RemoveAt(i);
                drink = d;
                return true;
            }
            drink = default(Drink);
            return false;
        }

        /// Hết ngày / đổi cấp: bỏ mọi ly còn lại. Trả về số ly bỏ.
        public int Clear()
        {
            return Prune(null);
        }
    }
}
