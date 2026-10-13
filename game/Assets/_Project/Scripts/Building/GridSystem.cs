using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Lưới bày trí ô 0.5 m. Giữ occupancy, đặt/gỡ nội thất, danh sách ghế/bàn.
    public class GridSystem
    {
        public const float Cell = GameBalance.CellSize;

        public Vector3 Origin { get; private set; }
        public Vector2Int Size { get; private set; }
        public readonly List<PlacedFurniture> Placed = new List<PlacedFurniture>();
        public readonly List<Seat> Seats = new List<Seat>();
        public readonly List<PlacedFurniture> Tables = new List<PlacedFurniture>();
        public PlacedFurniture BrewStationFurniture { get; private set; }

        public int ComfortStyleTotal { get; private set; }
        /// F_ref theo cấp quán (T1 20, T2 60): TierManager đặt khi tạo/đổi lưới.
        public float FRef = GameBalance.FRefTier1;
        public float FurnitureScore
        {
            get { return Mathf.Clamp01(ComfortStyleTotal / Mathf.Max(1f, FRef)); }
        }

        public event System.Action LayoutChanged;

        private PlacedFurniture[] cells;
        private readonly Transform root;
        private readonly List<Seat> scratch = new List<Seat>();
        private bool batching;

        public GridSystem(Vector3 origin, Vector2Int size, Transform root)
        {
            Origin = origin;
            Size = size;
            this.root = root;
            cells = new PlacedFurniture[Mathf.Max(1, size.x * size.y)];
        }

        public static int NormalizeRotation(int rotation)
        {
            return ((rotation % 4) + 4) % 4;
        }

        /// Rotation lẻ (1, 3) hoán đổi x/y của footprint.
        public static Vector2Int RotatedFootprint(Vector2Int fp, int rotation)
        {
            return (NormalizeRotation(rotation) & 1) == 1 ? new Vector2Int(fp.y, fp.x) : fp;
        }

        /// Dù che nắng lớn: nằm phía trên, không chiếm ô.
        public static bool IsOverhead(FurnitureDef def)
        {
            return def.overhead || (def.kind == FurnitureKind.Decor && def.footprint.x * def.footprint.y >= 9);
        }

        /// Quán đang đặt máy có id này không (máy trong kho chưa đặt thì chưa có tác dụng).
        public bool HasMachine(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return false;
            for (int i = 0; i < Placed.Count; i++)
            {
                FurnitureDef d = Placed[i].Def;
                if (d != null && d.machineId == machineId) return true;
            }
            return false;
        }

        public bool InBounds(Vector2Int c)
        {
            return c.x >= 0 && c.y >= 0 && c.x < Size.x && c.y < Size.y;
        }

        public Bounds WorldBounds
        {
            get
            {
                Vector3 size = new Vector3(Size.x * Cell, 0.1f, Size.y * Cell);
                return new Bounds(Origin + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f), size);
            }
        }

        /// Tâm một ô ở mặt đất.
        public Vector3 CellToWorld(Vector2Int c)
        {
            return Origin + new Vector3((c.x + 0.5f) * Cell, 0f, (c.y + 0.5f) * Cell);
        }

        /// Tâm footprint (đã xoay) của món đặt tại ô cell (ô góc dưới-trái).
        public Vector3 FootprintCenter(FurnitureDef def, Vector2Int cell, int rotation)
        {
            Vector2Int fp = RotatedFootprint(def.footprint, rotation);
            return Origin + new Vector3((cell.x + fp.x * 0.5f) * Cell, 0f, (cell.y + fp.y * 0.5f) * Cell);
        }

        public bool WorldToCell(Vector3 p, out Vector2Int cell)
        {
            cell = new Vector2Int(Mathf.FloorToInt((p.x - Origin.x) / Cell), Mathf.FloorToInt((p.z - Origin.z) / Cell));
            return InBounds(cell);
        }

        public bool CanPlace(FurnitureDef def, Vector2Int cell, int rotation)
        {
            if (def == null) return false;
            Vector2Int fp = RotatedFootprint(def.footprint, rotation);
            bool overhead = IsOverhead(def);
            for (int y = 0; y < fp.y; y++)
            {
                for (int x = 0; x < fp.x; x++)
                {
                    var c = new Vector2Int(cell.x + x, cell.y + y);
                    if (!InBounds(c)) return false;
                    if (!overhead && cells[c.y * Size.x + c.x] != null) return false;
                }
            }
            return true;
        }

        public PlacedFurniture Place(FurnitureDef def, Vector2Int cell, int rotation)
        {
            if (def == null || def.prefab == null) return null;
            rotation = NormalizeRotation(rotation);
            if (!CanPlace(def, cell, rotation)) return null;

            Vector2Int fp = RotatedFootprint(def.footprint, rotation);
            bool overhead = IsOverhead(def);
            Vector3 pos = FootprintCenter(def, cell, rotation);
            GameObject go = Object.Instantiate(def.prefab, pos, Quaternion.Euler(0f, 90f * rotation, 0f), root);
            go.name = string.IsNullOrEmpty(def.id) ? def.name : def.id;

            var pf = go.AddComponent<PlacedFurniture>();
            pf.Init(def, cell, rotation, fp, overhead);

            if (overhead)
            {
                Collider[] cols = go.GetComponentsInChildren<Collider>();
                for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
            }
            else
            {
                for (int y = 0; y < fp.y; y++)
                    for (int x = 0; x < fp.x; x++)
                        cells[(cell.y + y) * Size.x + (cell.x + x)] = pf;
            }

            Placed.Add(pf);
            ComfortStyleTotal += def.comfort + def.style;
            switch (def.kind)
            {
                case FurnitureKind.Seat:
                    var seat = go.AddComponent<Seat>();
                    seat.Init(pf);
                    Seats.Add(seat);
                    break;
                case FurnitureKind.Table:
                    Tables.Add(pf);
                    break;
                case FurnitureKind.BrewStation:
                    BrewStationFurniture = pf;
                    break;
            }

            if (LayoutChanged != null) LayoutChanged();
            return pf;
        }

        public bool Remove(PlacedFurniture pf)
        {
            if (pf == null || !Placed.Remove(pf)) return false;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i] == pf) cells[i] = null;

            ComfortStyleTotal -= pf.Def.comfort + pf.Def.style;
            Seat seat = pf.GetComponent<Seat>();
            if (seat != null) Seats.Remove(seat);
            Tables.Remove(pf);
            if (BrewStationFurniture == pf) BrewStationFurniture = null;
            pf.gameObject.SetActive(false); // collider không còn được NavMeshBuilder thu thập ngay trong frame này
            Object.Destroy(pf.gameObject);

            if (!batching && LayoutChanged != null) LayoutChanged();
            return true;
        }

        /// Gỡ toàn bộ nội thất (chuyển cấp quán). Chỉ báo LayoutChanged một lần.
        public void RemoveAll()
        {
            batching = true;
            try
            {
                for (int i = Placed.Count - 1; i >= 0; i--) Remove(Placed[i]);
            }
            finally
            {
                batching = false;
            }
            if (LayoutChanged != null) LayoutChanged();
        }

        /// Đổi gốc + kích thước lưới (chuyển cấp quán: phòng thuê 12 x 16 ô). Gỡ hết nội thất trước. Giữ nguyên instance
        /// nên mọi hệ thống đã đăng ký LayoutChanged vẫn nhận sự kiện.
        public void Reconfigure(Vector3 origin, Vector2Int size)
        {
            RemoveAll();
            Origin = origin;
            Size = size;
            cells = new PlacedFurniture[Mathf.Max(1, size.x * size.y)];
            ComfortStyleTotal = 0;
            if (LayoutChanged != null) LayoutChanged();
        }

        /// Món đang chiếm ô c (null nếu trống / ngoài lưới / món "trên cao").
        public PlacedFurniture GetAt(Vector2Int c)
        {
            return InBounds(c) ? cells[c.y * Size.x + c.x] : null;
        }

        /// Số món đã đặt theo id (trừ bàn pha) để lưu vào SaveData.purchases.
        public PurchaseCount[] ExportPurchases()
        {
            var list = new List<PurchaseCount>();
            for (int i = 0; i < Placed.Count; i++)
            {
                FurnitureDef d = Placed[i].Def;
                if (d == null || d.kind == FurnitureKind.BrewStation) continue;
                int idx = -1;
                for (int k = 0; k < list.Count; k++)
                    if (list[k].furnitureId == d.id) { idx = k; break; }
                if (idx < 0) list.Add(new PurchaseCount { furnitureId = d.id, count = 1 });
                else list[idx] = new PurchaseCount { furnitureId = d.id, count = list[idx].count + 1 };
            }
            return list.ToArray();
        }

        public List<LayoutEntry> ExportLayout()
        {
            var list = new List<LayoutEntry>(Placed.Count);
            for (int i = 0; i < Placed.Count; i++)
            {
                PlacedFurniture p = Placed[i];
                list.Add(new LayoutEntry(p.Def.id, p.Cell, p.Rotation));
            }
            return list;
        }

        /// Chọn ngẫu nhiên một ghế trống, sạch, đến được; giữ chỗ cho khách.
        public bool TryReserveSeat(Customer customer, float inGameNow, out Seat result)
        {
            scratch.Clear();
            for (int i = 0; i < Seats.Count; i++)
                if (Seats[i].IsAvailable(inGameNow)) scratch.Add(Seats[i]);

            if (scratch.Count == 0)
            {
                result = null;
                return false;
            }
            result = scratch[UnityEngine.Random.Range(0, scratch.Count)];
            result.Occupant = customer;
            return true;
        }

        /// Bàn gần nhất (theo khoảng cách ngang tới hộp bao) trong maxDist.
        public bool FindNearestTable(Vector3 p, float maxDist, out PlacedFurniture table, out Vector3 closest)
        {
            table = null;
            closest = p;
            float best = maxDist;
            for (int i = 0; i < Tables.Count; i++)
            {
                Bounds b = Tables[i].WorldBounds;
                Vector3 q = b.ClosestPoint(new Vector3(p.x, b.center.y, p.z));
                float d = new Vector2(q.x - p.x, q.z - p.z).magnitude;
                if (d <= best)
                {
                    best = d;
                    table = Tables[i];
                    closest = q;
                }
            }
            return table != null;
        }

        /// Chỗ đặt ly: trên mặt bàn gần ghế (<= 1.2 m), nếu không có thì trên đất cạnh ghế.
        public Vector3 GetCupPoint(Seat seat)
        {
            Vector3 sp = seat.transform.position;
            PlacedFurniture table;
            Vector3 closest;
            if (FindNearestTable(sp, 1.2f, out table, out closest))
            {
                Bounds b = table.WorldBounds;
                Vector3 toCenter = new Vector3(b.center.x - closest.x, 0f, b.center.z - closest.z);
                Vector3 p = closest;
                if (toCenter.sqrMagnitude > 0.0001f)
                    p += toCenter.normalized * Mathf.Min(0.12f, toCenter.magnitude);
                p.y = b.max.y;
                return p;
            }
            return seat.GroundCupPoint;
        }
    }
}
