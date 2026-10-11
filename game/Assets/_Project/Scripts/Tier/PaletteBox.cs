using UnityEngine;

namespace Cafe
{
    /// Hộp đơn sắc theo ô palette dựng lúc chạy (bản runtime của PaletteMesh.Box trong Editor).
    /// Mọi UV dồn vào tâm 1 ô palette, dùng GameBootstrap.toonMaterial (M_Toon_Palette).
    public static class PaletteBox
    {
        /// Mesh hộp kích thước size, tâm ở gốc.
        public static Mesh Box(Vector3 size, int swatch)
        {
            var b = new PaletteMeshBuilder();
            b.AddBox(Vector3.zero, size, swatch);
            return b.ToMesh("PaletteBox_" + swatch, false);
        }

        /// GameObject hộp (MeshFilter + MeshRenderer) tại center (toạ độ local của parent).
        public static GameObject Make(string name, Vector3 size, Vector3 center, int swatch, Material mat, Transform parent)
        {
            return MakeFromMesh(name, Box(size, swatch), mat, parent, center);
        }

        public static GameObject MakeFromMesh(string name, Mesh mesh, Material mat, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }
    }
}
