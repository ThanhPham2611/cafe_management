using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Cafe
{
    /// Ghế ngồi. Mỗi nội thất kind = Seat được gắn Seat.
    public class Seat : MonoBehaviour
    {
        public PlacedFurniture Furniture { get; private set; }
        public FurnitureDef Def { get { return Furniture.Def; } }
        public Customer Occupant;
        public LeftoverCup Cup;
        public bool Usable { get; private set; }

        private readonly List<Vector3> approach = new List<Vector3>(8);
        private float unreachableUntil;

        public Vector3 SitPoint { get { return transform.position + Vector3.up * Furniture.Def.seatHeight; } }

        public void Init(PlacedFurniture furniture)
        {
            Furniture = furniture;
            Usable = true;
        }

        /// Bàn gần nhất (trong SeatTableLinkMeters) mà ghế này thuộc về; null nếu ghế đứng riêng.
        public TableDirt Table
        {
            get
            {
                GameBootstrap g = GameBootstrap.Instance;
                if (g == null || g.Grid == null) return null;
                PlacedFurniture t;
                Vector3 closest;
                if (g.Grid.FindNearestTable(transform.position, GameBalance.SeatTableLinkMeters, out t, out closest))
                    return t.Dirt;
                return null;
            }
        }

        /// Ghế bị chặn khi ly để lại đã quá 30 giây in-game mà chưa thu (T1 / ghế không có bàn),
        /// hoặc (T2+, CleaningRules.Enabled) khi bàn của ghế đang bẩn.
        public bool IsBlocked(float inGameNow)
        {
            if (CleaningRules.Enabled)
            {
                TableDirt td = Table;
                if (td != null && td.BlocksSeats) return true;
            }
            return Cup != null && Cup.IsCollectable
                && inGameNow - Cup.CollectableSince > GameBalance.LeftoverBlockInGameSeconds;
        }

        public bool IsAvailable(float inGameNow)
        {
            return Usable && Occupant == null && Time.time >= unreachableUntil && !IsBlocked(inGameNow);
        }

        public void MarkUnreachable()
        {
            unreachableUntil = Time.time + 20f;
        }

        /// Gọi sau khi NavMesh đã bake: lấy các điểm đứng quanh ghế nằm trên NavMesh.
        public void PrepareApproach()
        {
            approach.Clear();
            Vector3 c = transform.position;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector3 p = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.6f;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(p, out hit, 0.25f, NavMesh.AllAreas))
                {
                    Vector3 d = hit.position - c;
                    d.y = 0f;
                    if (d.magnitude > 0.4f) approach.Add(hit.position);
                }
            }
            Usable = approach.Count > 0;
        }

        /// Có điểm đứng nào của ghế đi tới được từ from (đường NavMesh đầy đủ).
        public bool IsReachableFrom(Vector3 from, NavMeshPath path)
        {
            for (int i = 0; i < approach.Count; i++)
            {
                if (NavMesh.CalculatePath(from, approach[i], NavMesh.AllAreas, path)
                    && path.status == NavMeshPathStatus.PathComplete) return true;
            }
            return false;
        }

        /// Điểm đứng gần vị trí from nhất.
        public bool TryGetApproachPoint(Vector3 from, out Vector3 point)
        {
            point = transform.position;
            if (approach.Count == 0) return false;
            float best = float.MaxValue;
            for (int i = 0; i < approach.Count; i++)
            {
                float d = (approach[i] - from).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    point = approach[i];
                }
            }
            return true;
        }

        /// Chỗ đặt ly trên đất khi không có bàn gần.
        public Vector3 GroundCupPoint
        {
            get
            {
                Vector3 c = transform.position;
                if (approach.Count == 0) return c + new Vector3(0.35f, 0f, 0f);
                Vector3 d = approach[0] - c;
                d.y = 0f;
                if (d.sqrMagnitude < 0.0001f) d = Vector3.right;
                return c + d.normalized * 0.4f;
            }
        }
    }
}
