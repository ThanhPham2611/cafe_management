using System;
using UnityEngine;

namespace Cafe
{
    /// Một món nội thất đã đặt trên lưới. Đây cũng là định dạng lưu bố cục quán.
    [Serializable]
    public struct LayoutEntry
    {
        public string furnitureId;
        public Vector2Int cell;
        [Range(0, 3)] public int rotation; // ×90° quanh trục Y

        public LayoutEntry(string furnitureId, Vector2Int cell, int rotation = 0)
        {
            this.furnitureId = furnitureId;
            this.cell = cell;
            this.rotation = rotation;
        }
    }
}
