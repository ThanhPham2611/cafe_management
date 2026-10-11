using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    public enum TableState { Clean, Occupied, Dirty, Cleaning }

    /// Trạng thái bẩn của một bàn (PlacedFurniture kind = Table): Sạch -> Có khách -> Bẩn -> Đang dọn -> Sạch.
    /// Chỉ hoạt động khi CleaningRules.Enabled (T2+). Khi khách uống xong rời ghế, Customer gọi AddDish():
    /// ly + đĩa/giấy (1-3 vật, dựng bằng primitive toon + _SwatchIndex) nằm lại trên bàn, bàn chặn các ghế gắn với nó,
    /// và một job CleanTable được đăng lên WorkQueue.
    public class TableDirt : MonoBehaviour
    {
        private const int SwatchPlate = 44;    // gạch men trắng
        private const int SwatchNapkin = 15;   // giấy/menu
        private const int SwatchStain = 4;     // vệt cà phê nhạt
        private const int MaxDishes = 6;

        private static readonly int SwatchId = Shader.PropertyToID("_SwatchIndex");
        private static MaterialPropertyBlock mpb;

        private class Dish
        {
            public GameObject cup;
            public GameObject extra1;
            public GameObject extra2;

            public void DestroyAll()
            {
                if (cup != null) Object.Destroy(cup);
                if (extra1 != null) Object.Destroy(extra1);
                if (extra2 != null) Object.Destroy(extra2);
            }
        }

        private PlacedFurniture furniture;
        private readonly List<Dish> dishes = new List<Dish>(4);
        private Transform dishRoot;
        private GameObject tapBox;
        private float dirtySince;
        private float cleaningStamp = -10f;
        private WorkJob job;

        public PlacedFurniture Furniture { get { return furniture; } }

        /// Số ly bẩn đang nằm trên bàn (mỗi khách rời đi để lại 1 ly).
        public int DirtyCups { get { return dishes.Count; } }
        public bool IsDirty { get { return dishes.Count > 0; } }
        /// Bàn bẩn chặn mọi ghế gắn với nó.
        public bool BlocksSeats { get { return dishes.Count > 0; } }
        public WorkJob Job { get { return job; } }

        /// Giây thực (Time.time) kể từ khi bàn bắt đầu bẩn.
        public float DirtyAge { get { return dishes.Count > 0 ? Time.time - dirtySince : 0f; } }
        /// Bẩn quá lâu (> 26 s thực): -5 CI, có ruồi bay quanh.
        public bool IsOld { get { return dishes.Count > 0 && Time.time - dirtySince > GameBalance.OldTableRealSeconds; } }
        /// Chủ quán/nhân viên đang lau (MarkCleaning gọi mỗi frame trong lúc lau).
        public bool IsCleaning { get { return Time.time - cleaningStamp < 0.35f; } }

        public TableState State
        {
            get
            {
                if (dishes.Count > 0) return IsCleaning ? TableState.Cleaning : TableState.Dirty;
                return HasSeatedCustomer() ? TableState.Occupied : TableState.Clean;
            }
        }

        /// Điểm trên mặt bàn (giữa) - icon / vòng tiến độ bám theo.
        public Vector3 TopPoint
        {
            get
            {
                Bounds b = furniture != null ? furniture.WorldBounds : new Bounds(transform.position, Vector3.one * 0.5f);
                return new Vector3(b.center.x, b.max.y, b.center.z);
            }
        }

        public void Init(PlacedFurniture pf)
        {
            furniture = pf;
        }

        public void MarkCleaning()
        {
            cleaningStamp = Time.time;
        }

        // ------------------------------------------------------------------ khách

        /// Có khách đang ngồi / giữ chỗ ở ghế nào gắn với bàn này (chưa lau được).
        public bool HasSeatedCustomer()
        {
            GameBootstrap g = GameBootstrap.Instance;
            if (g == null || g.Grid == null) return false;
            var seats = g.Grid.Seats;
            for (int i = 0; i < seats.Count; i++)
            {
                Seat s = seats[i];
                if (s != null && s.Occupant != null && s.Table == this) return true;
            }
            return false;
        }

        /// Khách rời ghế: để lại ly (đã trả ly cho bàn), thêm đĩa/giấy ngẫu nhiên, đăng job lau bàn.
        public void AddDish(Vector3 cupPos)
        {
            GameBootstrap g = GameBootstrap.Instance;
            if (g == null) return;

            if (dishes.Count == 0) dirtySince = Time.time;
            if (dishRoot == null)
            {
                dishRoot = new GameObject("Dishes").transform;
                dishRoot.SetParent(transform, false);
            }

            var d = new Dish();
            if (dishes.Count < MaxDishes)
            {
                // Ly: dùng đúng prefab ly của quán
                Quaternion yaw = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                if (g.CupPrefab != null)
                {
                    d.cup = Instantiate(g.CupPrefab, cupPos, yaw, dishRoot);
                    Collider[] cols = d.cup.GetComponentsInChildren<Collider>();
                    for (int i = 0; i < cols.Length; i++) Destroy(cols[i]);
                }
                else
                {
                    d.cup = MakePrimitive(PrimitiveType.Cylinder, g.toonMaterial, 1, cupPos + Vector3.up * 0.03f,
                        new Vector3(0.07f, 0.03f, 0.07f), Quaternion.identity);
                }
                d.cup.name = "DirtyCup";

                // Đĩa lót / giấy / vệt nước: 0-2 món thêm (tổng 1-3 vật mỗi khách)
                if (Random.value < 0.55f)
                {
                    d.extra1 = MakePrimitive(PrimitiveType.Cylinder, g.toonMaterial, SwatchPlate,
                        cupPos + Vector3.up * 0.006f, new Vector3(0.2f, 0.006f, 0.2f), Quaternion.identity);
                    d.extra1.name = "DirtyPlate";
                }
                if (Random.value < 0.55f)
                {
                    Vector2 o = Random.insideUnitCircle.normalized * Random.Range(0.1f, 0.16f);
                    bool napkin = Random.value < 0.6f;
                    d.extra2 = napkin
                        ? MakePrimitive(PrimitiveType.Cube, g.toonMaterial, SwatchNapkin,
                            cupPos + new Vector3(o.x, 0.003f, o.y), new Vector3(0.12f, 0.004f, 0.1f), Quaternion.Euler(0f, Random.Range(0f, 180f), 0f))
                        : MakePrimitive(PrimitiveType.Cylinder, g.toonMaterial, SwatchStain,
                            cupPos + new Vector3(o.x, 0.002f, o.y), new Vector3(0.15f, 0.002f, 0.15f), Quaternion.identity);
                    d.extra2.name = napkin ? "DirtyNapkin" : "DirtyStain";
                }
            }
            dishes.Add(d);

            EnsureTapBox();
            tapBox.SetActive(true);
            job = WorkQueue.PostCleanTable(this);
        }

        private GameObject MakePrimitive(PrimitiveType type, Material mat, int swatch, Vector3 pos, Vector3 scale, Quaternion rot)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Transform t = go.transform;
            t.SetParent(dishRoot, false);
            t.position = pos;
            t.rotation = rot;
            t.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mpb.SetFloat(SwatchId, swatch);
            r.SetPropertyBlock(mpb);
            return go;
        }

        // ------------------------------------------------------------------ lau

        /// Lau xong: lấy tối đa maxCups ly bẩn, gỡ đĩa/giấy tương ứng. Trả về số ly lấy được.
        /// Hết ly -> bàn Sạch, job CleanTable hoàn thành. Còn dư (hiếm: tay chủ quán đầy) -> bàn vẫn bẩn.
        public int FinishClean(int maxCups)
        {
            int n = Mathf.Clamp(maxCups, 0, dishes.Count);
            for (int i = 0; i < n; i++)
            {
                Dish d = dishes[dishes.Count - 1];
                dishes.RemoveAt(dishes.Count - 1);
                d.DestroyAll();
            }
            if (dishes.Count == 0)
            {
                if (tapBox != null) tapBox.SetActive(false);
                WorkQueue.Complete(job);
                job = null;
                cleaningStamp = -10f;
            }
            return n;
        }

        /// Qua đêm / bán bàn: dọn sạch ngay, ly bẩn về thẳng kho (như thu ly qua đêm của T1).
        public void ResetSilently()
        {
            GameBootstrap g = GameBootstrap.Instance;
            int n = dishes.Count;
            for (int i = 0; i < dishes.Count; i++) dishes[i].DestroyAll();
            dishes.Clear();
            if (tapBox != null) tapBox.SetActive(false);
            if (job != null) WorkQueue.Discard(job);
            job = null;
            cleaningStamp = -10f;
            if (g != null && g.Stock != null)
                for (int i = 0; i < n; i++) g.Stock.Return();
        }

        private void OnDestroy()
        {
            // Bàn bị bán / di chuyển giữa chừng: ly bẩn không biến mất khỏi kho
            if (dishes.Count > 0 || job != null) ResetSilently();
        }

        // ------------------------------------------------------------------ chạm

        /// Người chơi chạm bàn bẩn (qua TableTapTarget) hoặc bot gọi cùng đường vào.
        public void OnTap()
        {
            if (!CleaningRules.Enabled || !IsDirty) return;
            GameBootstrap g = GameBootstrap.Instance;
            if (g == null || g.Owner == null) return;
            g.Owner.QueueCleanTable(this);
        }

        private void EnsureTapBox()
        {
            if (tapBox != null) return;
            tapBox = new GameObject("DirtTap");
            Bounds b = furniture != null ? furniture.WorldBounds : new Bounds(transform.position, Vector3.one * 0.8f);
            tapBox.transform.SetParent(transform, true);
            tapBox.transform.position = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
            tapBox.transform.rotation = Quaternion.identity;
            var box = tapBox.AddComponent<BoxCollider>();
            box.size = new Vector3(Mathf.Clamp(b.size.x, 0.4f, 0.8f), 0.35f, Mathf.Clamp(b.size.z, 0.4f, 0.8f));
            var tgt = tapBox.AddComponent<TableTapTarget>();
            tgt.table = this;
            tapBox.SetActive(false);
        }

        // ------------------------------------------------------------------ đường đi

        /// Điểm đứng lau cạnh bàn (trên NavMesh, tới được), gần `from` nhất.
        public bool TryGetApproachPoint(Vector3 from, out Vector3 point)
        {
            Bounds b = furniture != null ? furniture.WorldBounds : new Bounds(transform.position, Vector3.one);
            return CleaningNav.TryGetApproach(b, from, out point);
        }
    }
}
