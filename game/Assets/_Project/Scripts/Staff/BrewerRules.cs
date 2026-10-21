using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Luật của nhân viên Pha chế (economy §6): tốc độ và chất lượng theo cấp kỹ năng.
    public static class BrewerRules
    {
        /// Hệ số thời gian pha so với thời gian chuẩn của công thức, theo cấp 1..5.
        private static readonly float[] TimeFactors = { 1.8f, 1.5f, 1.25f, 1.1f, 1.0f };

        /// Giây dành cho mang ly tới khách và nhận ly sau khi pha xong.
        public const float DeliveryMargin = 5f;

        /// Đơn còn kịp không: khách còn đủ kiên nhẫn cho thời gian pha + giao. Pha cho khách sắp bỏ về là mất nguyên liệu vô ích.
        public static bool CanFinish(float patienceSecondsLeft, float brewSeconds)
        {
            return patienceSecondsLeft > brewSeconds + DeliveryMargin;
        }

        /// Cấp tối thiểu để pha 3★ (economy §2.3: nhân viên pha chế Lv ≥ 3 cho 3★ không tốn thêm thời gian).
        public const int ThreeStarLevel = 3;

        /// machineFactor: hệ số của máy móc cấp 3 (MachineRules.BrewTimeFactor), 1 = không có máy.
        public static float BrewSeconds(float recipeSeconds, int level, float machineFactor = 1f)
        {
            float factor = TimeFactors[Mathf.Clamp(level, 1, TimeFactors.Length) - 1];
            return Mathf.Max(1f, recipeSeconds * factor * machineFactor);
        }

        /// 2★ khi đúng công thức; 3★ khi cấp đủ và dụng cụ cho phép (món ẩn cần dụng cụ cấp ≥ 2).
        public static int Quality(int level, bool toolsAllowThreeStars)
        {
            return level >= ThreeStarLevel && toolsAllowThreeStars ? 3 : 2;
        }
    }

    /// Chọn đơn cho nhân viên. Thuần logic (không đụng thế giới) để kiểm thử được; đơn xếp theo thứ tự cũ → mới.
    public static class OrderPicker
    {
        /// Đơn cũ nhất mà nhân viên Pha chế có thể nhận: khách còn chờ, chưa ai giữ,
        /// không phải đơn chủ quán đang chọn, và chủ quán không có sẵn ly đúng món đó trên tay.
        public static Order NextForBrewer(IReadOnlyList<Order> orders, Order ownerSelected, RecipeDef ownerReadyRecipe,
                                          Func<Order, bool> isWaiting, Func<Order, bool> canFinish = null)
        {
            if (orders == null) return null;
            for (int i = 0; i < orders.Count; i++)
            {
                Order o = orders[i];
                if (o == null || o.IsClaimed || o.ownerOnly || ReferenceEquals(o, ownerSelected)) continue; // đơn đặc biệt: chỉ chủ quán
                if (!ReferenceEquals(ownerReadyRecipe, null) && ReferenceEquals(o.recipe, ownerReadyRecipe)) continue;
                if (isWaiting != null && !isWaiting(o)) continue;
                if (canFinish != null && !canFinish(o)) continue; // sắp hết kiên nhẫn: bỏ qua, nhận đơn kế còn kịp
                return o;
            }
            return null;
        }

        /// Đơn cũ nhất còn chờ, chưa ai giữ, cùng món: nhận lại ly đã pha cho khách vừa bỏ đi thay vì đổ (đơn đặc biệt chỉ chủ quán pha nên bỏ qua).
        public static Order SameRecipeWaiting(IReadOnlyList<Order> orders, RecipeDef recipe, Func<Order, bool> isWaiting)
        {
            if (orders == null || ReferenceEquals(recipe, null)) return null;
            for (int i = 0; i < orders.Count; i++)
            {
                Order o = orders[i];
                if (o == null || o.IsClaimed || o.ownerOnly || !ReferenceEquals(o.recipe, recipe)) continue;
                if (isWaiting != null && !isWaiting(o)) continue;
                return o;
            }
            return null;
        }

        /// Đơn khớp ly chủ quán đã pha xong để nhân viên Phục vụ mang đi: đơn đặc biệt (x2) trước, rồi đơn cũ nhất. Ly khớp theo MÓN,
        /// nên nếu ưu tiên đơn cũ thì ly chủ quán pha cho đơn đặc biệt có thể bị mang cho đơn thường cùng món.
        public static Order NextForServer(IReadOnlyList<Order> orders, RecipeDef readyRecipe, Func<Order, bool> isWaiting)
        {
            if (orders == null || ReferenceEquals(readyRecipe, null)) return null;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < orders.Count; i++)
                {
                    Order o = orders[i];
                    if (o == null || o.IsClaimed || !ReferenceEquals(o.recipe, readyRecipe)) continue;
                    if (pass == 0 && !o.ownerOnly) continue;
                    if (isWaiting != null && !isWaiting(o)) continue;
                    return o;
                }
            }
            return null;
        }
    }
}
