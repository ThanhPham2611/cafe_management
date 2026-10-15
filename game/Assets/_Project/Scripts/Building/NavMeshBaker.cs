using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Cafe
{
    /// Bake NavMesh lúc chạy bằng NavMeshBuilder: mặt đất đi được, nội thất là vùng Not Walkable (area 1).
    public static class NavMeshBaker
    {
        private static NavMeshData data;
        private static NavMeshDataInstance instance;

        public const float AgentRadius = 0.18f;

        private static bool IsDynamic(Collider c)
        {
            return c is CharacterController
                || c.GetComponentInParent<NavMeshAgent>() != null
                || c.GetComponentInParent<Customer>() != null
                || c.GetComponentInParent<LeftoverCup>() != null;
        }

        /// Bake lại toàn cảnh với cùng bounds như GameBootstrap.Build.
        public static bool BakeFor(GameBootstrap g, IList<Transform> ignoreRoots)
        {
            Bounds nb = g.Grid.WorldBounds;
            nb.Encapsulate(g.SpawnPosition);
            nb.Encapsulate(g.ExitPosition);
            nb.Expand(new Vector3(6f, 6f, 6f));
            return Bake(nb, g.Grid.Placed, ignoreRoots);
        }

        /// Sau khi NavMesh được bake lại: khách đang đi bị mất mesh thì đặt lại lên điểm gần nhất trên mesh mới.
        public static void RepairCustomers(IList<Customer> customers)
        {
            if (customers == null) return;
            for (int i = 0; i < customers.Count; i++)
            {
                if (customers[i] == null) continue;
                NavMeshAgent a = customers[i].GetComponent<NavMeshAgent>();
                if (a == null || !a.enabled || a.isOnNavMesh) continue;
                NavMeshHit hit;
                if (NavMesh.SamplePosition(a.transform.position, out hit, 1.5f, NavMesh.AllAreas)) a.Warp(hit.position);
            }
        }

        public static bool Bake(Bounds bounds, IList<PlacedFurniture> furniture, IList<Transform> ignoreRoots)
        {
            using var perf = PerfNote.Measure("bake NavMesh");
            Physics.SyncTransforms(); // collider vừa Instantiate phải đồng bộ trước khi thu thập

            var markups = new List<NavMeshBuildMarkup>();
            if (furniture != null)
            {
                for (int i = 0; i < furniture.Count; i++)
                {
                    PlacedFurniture f = furniture[i];
                    if (f == null) continue;
                    var m = new NavMeshBuildMarkup();
                    m.root = f.transform;
                    m.area = 1; // Not Walkable
                    m.overrideArea = !f.Overhead;
                    m.ignoreFromBuild = f.Overhead;
                    m.applyToChildren = true;
                    markups.Add(m);
                }
            }
            if (ignoreRoots != null)
            {
                for (int i = 0; i < ignoreRoots.Count; i++)
                {
                    if (ignoreRoots[i] == null) continue;
                    var m = new NavMeshBuildMarkup();
                    m.root = ignoreRoots[i];
                    m.ignoreFromBuild = true;
                    m.applyToChildren = true;
                    markups.Add(m);
                }
            }

            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            // Bake lúc đang chơi: bỏ collider của khách / chủ quán / ly để lại (vật thể động)
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                var col = sources[i].component as Collider;
                if (col != null && IsDynamic(col)) sources.RemoveAt(i);
            }
            if (sources.Count == 0)
            {
                Debug.LogWarning("[NavMeshBaker] Không thu được geometry nào (Ground có collider chưa?).");
                return false;
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = AgentRadius;
            settings.agentHeight = 1.2f;
            settings.agentClimb = 0.12f;
            settings.agentSlope = 35f;
            settings.minRegionArea = 0.25f;

            NavMeshData built = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (built == null) return false;

            if (instance.valid) instance.Remove();
            data = built;
            instance = NavMesh.AddNavMeshData(data);
            return true;
        }
    }
}
