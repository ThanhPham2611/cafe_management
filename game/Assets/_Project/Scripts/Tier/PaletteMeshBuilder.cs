using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Gộp nhiều hộp / ô phẳng thành MỘT mesh; mỗi hình dùng đúng 1 ô palette (mọi UV của hình dồn vào tâm ô,
    /// u = (col + .5)/8, v = 1 - (row + .5)/8) nên chỉ cần M_Toon_Palette. Dùng lúc chạy (bản runtime của PaletteMesh trong Editor).
    public sealed class PaletteMeshBuilder
    {
        private static readonly Vector3[] FaceNormals =
            { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        private readonly List<Vector3> verts = new List<Vector3>(512);
        private readonly List<Vector3> norms = new List<Vector3>(512);
        private readonly List<Vector2> uvs = new List<Vector2>(512);
        private readonly List<int> tris = new List<int>(1024);

        public int VertexCount { get { return verts.Count; } }

        /// Tâm ô palette (index = row*8 + col, row 0 ở trên cùng).
        public static Vector2 CellUv(int swatch)
        {
            int row = swatch / 8, col = swatch % 8;
            return new Vector2((col + 0.5f) / 8f, 1f - (row + 0.5f) / 8f);
        }

        /// Hộp 6 mặt, tâm center, kích thước size.
        public void AddBox(Vector3 center, Vector3 size, int swatch)
        {
            Vector2 uv = CellUv(swatch);
            Vector3 h = size * 0.5f;
            for (int f = 0; f < 6; f++)
            {
                Vector3 n = FaceNormals[f];
                Vector3 a = n.y != 0f ? Vector3.right : Vector3.up;
                Vector3 u = Vector3.Cross(n, a).normalized;
                Vector3 v = Vector3.Cross(n, u);
                int b = verts.Count;
                for (int k = 0; k < 4; k++)
                {
                    float su = (k == 0 || k == 3) ? -1f : 1f;
                    float sv = (k < 2) ? -1f : 1f;
                    Vector3 p = n + u * su + v * sv;
                    verts.Add(center + Vector3.Scale(p, h));
                    norms.Add(n);
                    uvs.Add(uv);
                }
                // Unity: mặt trước khi Cross(p1 - p0, p2 - p0) cùng hướng normal
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
        }

        /// Ô phẳng hướng lên (+Y) ở độ cao y, tâm (cx, cz), kích thước (sx, sz).
        public void AddUpQuad(float cx, float cz, float y, float sx, float sz, int swatch)
        {
            float x0 = cx - sx * 0.5f, x1 = cx + sx * 0.5f;
            float z0 = cz - sz * 0.5f, z1 = cz + sz * 0.5f;
            AddUp(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), swatch);
        }

        /// Hình thoi hướng lên (hoa văn gạch bông): nửa đường chéo = half.
        public void AddUpDiamond(float cx, float cz, float y, float half, int swatch)
        {
            AddUp(new Vector3(cx - half, y, cz), new Vector3(cx, y, cz + half),
                  new Vector3(cx + half, y, cz), new Vector3(cx, y, cz - half), swatch);
        }

        private void AddUp(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int swatch)
        {
            Vector2 uv = CellUv(swatch);
            int b = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
            for (int i = 0; i < 4; i++)
            {
                norms.Add(Vector3.up);
                uvs.Add(uv);
            }
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
        }

        /// Dựng mesh. release = true: giải phóng bản CPU (mesh không cần đọc lại, tiết kiệm RAM).
        public Mesh ToMesh(string name, bool release)
        {
            var m = new Mesh();
            m.name = name;
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            if (release) m.UploadMeshData(true);
            return m;
        }
    }
}
