using UnityEngine;

namespace Cafe
{
    /// Một món nội thất đã đặt trên lưới (gắn vào root của prefab).
    public class PlacedFurniture : MonoBehaviour
    {
        public FurnitureDef Def { get; private set; }
        public Vector2Int Cell { get; private set; }
        public int Rotation { get; private set; }
        /// Footprint sau khi xoay (ô 0.5 m).
        public Vector2Int Footprint { get; private set; }
        /// Vật thể "trên cao" (dù che): không chiếm ô, không chặn đường/chạm.
        public bool Overhead { get; private set; }
        public Bounds WorldBounds { get; private set; }
        public float TopY { get { return WorldBounds.max.y; } }
        /// Trạng thái bẩn của bàn (chỉ có ở kind = Table; null với đồ khác). Xem Scripts/Cleaning/TableDirt.cs.
        public TableDirt Dirt { get; private set; }

        public void Init(FurnitureDef def, Vector2Int cell, int rotation, Vector2Int footprint, bool overhead)
        {
            Def = def;
            Cell = cell;
            Rotation = rotation;
            Footprint = footprint;
            Overhead = overhead;

            Renderer[] rs = GetComponentsInChildren<Renderer>();
            Bounds b;
            if (rs.Length > 0)
            {
                b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            }
            else
            {
                b = new Bounds(transform.position + Vector3.up * 0.5f,
                    new Vector3(footprint.x * GameBalance.CellSize, 1f, footprint.y * GameBalance.CellSize));
            }
            WorldBounds = b;

            if (def != null && def.kind == FurnitureKind.Table)
            {
                Dirt = GetComponent<TableDirt>();
                if (Dirt == null) Dirt = gameObject.AddComponent<TableDirt>();
                Dirt.Init(this);
            }
        }
    }
}
