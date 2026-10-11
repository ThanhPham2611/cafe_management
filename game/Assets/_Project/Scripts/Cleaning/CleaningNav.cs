using UnityEngine;
using UnityEngine.AI;

namespace Cafe
{
    /// Tìm điểm đứng trên NavMesh quanh một vật chiếm ô (bàn, bồn rửa).
    public static class CleaningNav
    {
        private const float StandOffset = 0.5f;
        private static NavMeshPath scratchPath;

        /// Thử 8 hướng quanh hộp bao b; chọn điểm trên NavMesh, nằm ngoài hộp, đi tới được và gần `from` nhất.
        /// Không điểm nào đi tới chắc chắn được thì trả điểm hợp lệ đầu tiên (để Owner tự xử lý "không tới được").
        public static bool TryGetApproach(Bounds b, Vector3 from, out Vector3 point)
        {
            point = from;
            if (scratchPath == null) scratchPath = new NavMeshPath();

            bool found = false;
            float best = float.MaxValue;
            Vector3 anyPoint = from;
            bool haveAny = false;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                float dx = Mathf.Sin(a), dz = Mathf.Cos(a);
                float rx = Mathf.Abs(dx) > 0.001f ? b.extents.x / Mathf.Abs(dx) : float.MaxValue;
                float rz = Mathf.Abs(dz) > 0.001f ? b.extents.z / Mathf.Abs(dz) : float.MaxValue;
                float reach = Mathf.Min(rx, rz) + StandOffset;
                var p = new Vector3(b.center.x + dx * reach, 0f, b.center.z + dz * reach);
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(p, out hit, 0.3f, NavMesh.AllAreas)) continue;

                // điểm phải nằm ngoài hộp (không bị snap vào trong vật)
                float ox = Mathf.Max(Mathf.Abs(hit.position.x - b.center.x) - b.extents.x, 0f);
                float oz = Mathf.Max(Mathf.Abs(hit.position.z - b.center.z) - b.extents.z, 0f);
                if (ox * ox + oz * oz < 0.12f * 0.12f) continue;

                if (!haveAny)
                {
                    anyPoint = hit.position;
                    haveAny = true;
                }
                if (!NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, scratchPath)
                    || scratchPath.status != NavMeshPathStatus.PathComplete) continue;
                float d = (hit.position - from).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    point = hit.position;
                    found = true;
                }
            }
            if (found) return true;
            if (haveAny)
            {
                point = anyPoint;
                return true;
            }
            NavMeshHit h2;
            if (NavMesh.SamplePosition(b.center, out h2, 2.5f, NavMesh.AllAreas))
            {
                point = h2.position;
                return true;
            }
            return false;
        }
    }
}
