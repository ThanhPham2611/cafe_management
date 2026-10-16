using UnityEngine;

namespace Cafe
{
    /// Đồ đặt trên mặt quầy: tâm đồ (đã quy về toạ độ cục bộ của quầy) phải nằm trong vùng cho phép của quầy.
    public static class CounterMountRules
    {
        public static bool Contains(Rect area, Vector3 localPoint)
        {
            return area.Contains(new Vector2(localPoint.x, localPoint.z));
        }
    }
}
