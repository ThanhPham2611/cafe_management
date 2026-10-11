using UnityEngine;

namespace Cafe
{
    /// Vùng chạm trên mặt bàn bẩn (collider con của bàn, chỉ bật khi bàn đang bẩn).
    /// Tách khỏi TableDirt để bàn sạch KHÔNG chặn tia chạm tới khách / ly phía sau.
    public class TableTapTarget : MonoBehaviour, ITappable
    {
        [System.NonSerialized] public TableDirt table;

        public void OnTap()
        {
            if (table != null) table.OnTap();
        }
    }
}
