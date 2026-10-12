#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Công cụ chạy thử tự động / đo hiệu năng: chỉ có trong Editor và bản Development, không vào bản release.
using System;
using Unity.Profiling;
using UnityEngine;

namespace Cafe
{
    /// Đo hiệu năng CPU / bộ nhớ khi chạy autoplay (cờ -perf), in một dòng "[Perf]" mỗi 30 giây thực.
    /// Chạy -batchmode -nographics không giới hạn FPS thì thời gian khung ≈ chi phí script mỗi khung (không gồm GPU).
    /// Mục tiêu mobile 60 FPS = 16,6 ms/khung; GC gen0 càng ít càng tốt (giật hình khi GC).
    /// Cấp phát đọc từ marker "GC Allocated In Frame" của Profiler (GC.GetAllocatedBytesForCurrentThread trả 0 trên Mono của Unity).
    public sealed class DebugPerfProbe : MonoBehaviour
    {
        private const float WindowSeconds = 30f;
        private const int MaxSamples = 1 << 16;

        private sealed class Stat { public int count; public double sum; public double max; }

        private static readonly System.Collections.Generic.Dictionary<string, Stat> stats = new System.Collections.Generic.Dictionary<string, Stat>();
        private readonly float[] frameMs = new float[MaxSamples];
        private readonly System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
        private int count;
        private ProfilerRecorder allocRecorder;
        private ProfilerRecorder allocCountRecorder;
        private long allocBytes;
        private long allocCalls;
        private long worstFrameAlloc;
        private int gc0Start;
        private float windowStart;
        private float worstMs;

        /// Ghi một lần đo của PerfNote (gộp theo tên, in ở Report).
        public static void Record(string tag, double ms)
        {
            Stat st;
            if (!stats.TryGetValue(tag, out st)) { st = new Stat(); stats[tag] = st; }
            st.count++;
            st.sum += ms;
            if (ms > st.max) st.max = ms;
        }

        public static void Attach()
        {
            var go = new GameObject("DebugPerfProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<DebugPerfProbe>();
        }

        private void OnEnable()
        {
            allocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            allocCountRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
        }

        private void OnDisable()
        {
            allocRecorder.Dispose();
            allocCountRecorder.Dispose();
        }

        private void Start()
        {
            windowStart = Time.realtimeSinceStartup;
            gc0Start = GC.CollectionCount(0);
        }

        private void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            if (count < MaxSamples) frameMs[count++] = ms;
            if (ms > worstMs) worstMs = ms;

            if (allocRecorder.Valid)
            {
                long b = allocRecorder.LastValue;
                allocBytes += b;
                if (b > worstFrameAlloc) worstFrameAlloc = b;
            }
            if (allocCountRecorder.Valid) allocCalls += allocCountRecorder.LastValue;

            if (Time.realtimeSinceStartup - lastSceneLog >= 120f) { lastSceneLog = Time.realtimeSinceStartup; LogSceneStats(); }
            if (Time.realtimeSinceStartup - windowStart >= WindowSeconds) Report();
        }

        private float lastSceneLog = -999f;

        /// Thống kê dựng hình tĩnh (không cần GPU): tam giác của mọi renderer đang bật, vật liệu khác nhau, canvas, đèn bóng.
        private void LogSceneStats()
        {
            var materials = new System.Collections.Generic.HashSet<Material>();
            long tris = 0;
            int renderers = 0, shadowCasters = 0, skinned = 0;
            foreach (Renderer r in FindObjectsByType<Renderer>())
            {
                if (!r.enabled) continue;
                renderers++;
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadowCasters++;
                foreach (Material m in r.sharedMaterials) if (m != null) materials.Add(m);
                Mesh mesh = null;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) { mesh = smr.sharedMesh; skinned++; }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) mesh = mf.sharedMesh;
                }
                if (mesh == null) continue;
                for (int i = 0; i < mesh.subMeshCount; i++) tris += mesh.GetIndexCount(i) / 3;
            }
            int canvases = FindObjectsByType<Canvas>().Length;
            int graphics = FindObjectsByType<UnityEngine.UI.Graphic>().Length;
            Debug.Log($"[Perf] cảnh: renderer={renderers} (skinned {skinned}, đổ bóng {shadowCasters}) tam giác={tris / 1000f:0.0}k " +
                      $"vật liệu={materials.Count} canvas={canvases} uGUI graphic={graphics}");
        }

        private void Report()
        {
            if (count > 0)
            {
                float sum = 0f;
                for (int i = 0; i < count; i++) sum += frameMs[i];
                Array.Sort(frameMs, 0, count);
                float avg = sum / count;
                float p95 = frameMs[Mathf.Min(count - 1, (int)(count * 0.95f))];
                float p99 = frameMs[Mathf.Min(count - 1, (int)(count * 0.99f))];
                float elapsed = Time.realtimeSinceStartup - windowStart;
                int gc0 = GC.CollectionCount(0) - gc0Start;
                GameBootstrap g = GameBootstrap.Instance;
                int customers = g != null && g.Spawner != null ? g.Spawner.ActiveCount : 0;
                Debug.Log($"[Perf] khung={count} avg={avg:0.00}ms p95={p95:0.00}ms p99={p99:0.00}ms max={worstMs:0.0}ms " +
                          $"cấp phát={allocBytes / (float)count:0}B/khung ({allocCalls / (float)count:0.0} lần/khung, khung tệ nhất {worstFrameAlloc / 1024f:0.0}KB) " +
                          $"GC0={gc0} ({gc0 * 60f / elapsed:0.0}/phút) khách={customers} " +
                          $"bộ nhớ mono={UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576f:0.0}MB");
                sb.Length = 0;
                foreach (var kv in stats)
                {
                    if (kv.Value.count == 0) continue;
                    sb.Append(kv.Key).Append(" n=").Append(kv.Value.count).Append(" avg=").Append((kv.Value.sum / kv.Value.count).ToString("0.00"))
                      .Append("ms max=").Append(kv.Value.max.ToString("0.00")).Append("ms; ");
                    kv.Value.count = 0; kv.Value.sum = 0; kv.Value.max = 0;
                }
                if (sb.Length > 0) Debug.Log("[Perf] việc nặng: " + sb);
            }
            count = 0;
            allocBytes = 0;
            allocCalls = 0;
            worstFrameAlloc = 0;
            worstMs = 0f;
            windowStart = Time.realtimeSinceStartup;
            gc0Start = GC.CollectionCount(0);
        }
    }
}
#endif
