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

        // GPU / draw call: FrameTimingManager + ProfilerRecorder (cần PlayerSettings.enableFrameTimingStats, BuildTools bật cho bản Development).
        private ProfilerRecorder rSetPass, rDraw, rTris, rShadow, rTex;
        private readonly FrameTiming[] ft = new FrameTiming[1];
        private readonly System.Collections.Generic.List<float> gpuList = new System.Collections.Generic.List<float>();
        private readonly System.Collections.Generic.List<float> mainList = new System.Collections.Generic.List<float>();
        private double sSetPass, sDraw, sTris, sShadow, sTexMax, sMain, sRender, sGpu;
        private int nStat, nGpu;

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
            rSetPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            rDraw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            rTris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            rShadow = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count");
            rTex = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory");
        }

        private void OnDisable()
        {
            allocRecorder.Dispose();
            allocCountRecorder.Dispose();
            rSetPass.Dispose(); rDraw.Dispose(); rTris.Dispose(); rShadow.Dispose(); rTex.Dispose();
        }

        private void Start()
        {
            windowStart = Time.realtimeSinceStartup;
            gc0Start = GC.CollectionCount(0);
            Debug.Log($"[Perf] máy: gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType} màn={Screen.currentResolution} " +
                      $"fps mục tiêu={Application.targetFrameRate} mức={GraphicsQuality.Current} frameTiming={FrameTimingManager.IsFeatureEnabled()}");
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
            if (rDraw.Valid)
            {
                nStat++; sSetPass += rSetPass.LastValue; sDraw += rDraw.LastValue; sTris += rTris.LastValue; sShadow += rShadow.LastValue;
                if (rTex.Valid && rTex.LastValue > sTexMax) sTexMax = rTex.LastValue;
            }
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, ft) > 0)
            {
                nGpu++; sMain += ft[0].cpuMainThreadFrameTime; sRender += ft[0].cpuRenderThreadFrameTime; sGpu += ft[0].gpuFrameTime;
                if (gpuList.Count < MaxSamples) { gpuList.Add((float)ft[0].gpuFrameTime); mainList.Add((float)ft[0].cpuMainThreadFrameTime); }
            }

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
            int[] catAll = new int[CasterCategories.Length], catCast = new int[CasterCategories.Length];
            foreach (Renderer r in FindObjectsByType<Renderer>())
            {
                if (!r.enabled) continue;
                renderers++;
                int cat = CasterCategory(r);
                catAll[cat]++;
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) { shadowCasters++; catCast[cat]++; }
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
            sb.Length = 0;
            for (int i = 0; i < catAll.Length; i++) sb.Append(CasterCategories[i]).Append(' ').Append(catCast[i]).Append('/').Append(catAll[i]).Append("; ");
            GameBootstrap g = GameBootstrap.Instance;
            int customers = g != null && g.Spawner != null ? g.Spawner.ActiveCount : 0;
            Debug.Log($"[Perf] đổ bóng theo loại (đổ bóng/renderer): {sb} khách={customers} renderer/khách={(customers > 0 ? catAll[0] / (float)customers : 0f):0.0}");
        }

        private static readonly string[] CasterCategories = { "Customer", "StaffWorker", "Owner", "Furniture<0.5m", "Furniture>=0.5m", "Môi trường/khác" };

        /// Phân loại renderer theo cha: khách, nhân viên, chủ quán, đồ đặt (nhỏ/lớn theo chiều cao bounds), còn lại.
        private static int CasterCategory(Renderer r)
        {
            if (r.GetComponentInParent<Customer>() != null) return 0;
            if (r.GetComponentInParent<StaffWorker>() != null) return 1;
            if (r.GetComponentInParent<OwnerController>() != null) return 2;
            if (r.GetComponentInParent<PlacedFurniture>() != null) return r.bounds.size.y < 0.5f ? 3 : 4;
            return 5;
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
                if (nStat > 0)
                {
                    gpuList.Sort(); mainList.Sort();
                    float gp95 = gpuList.Count > 0 ? gpuList[Mathf.Min(gpuList.Count - 1, (int)(gpuList.Count * 0.95f))] : 0f;
                    float mp95 = mainList.Count > 0 ? mainList[Mathf.Min(mainList.Count - 1, (int)(mainList.Count * 0.95f))] : 0f;
                    Debug.Log($"[PerfX] lvl={GraphicsQuality.Current} draw={sDraw / nStat:0} setpass={sSetPass / nStat:0} tris={sTris / nStat / 1000f:0.0}k shadowCasters={sShadow / nStat:0} " +
                              $"mainCPU={(nGpu > 0 ? sMain / nGpu : 0):0.00}ms(p95 {mp95:0.00}) renderCPU={(nGpu > 0 ? sRender / nGpu : 0):0.00}ms GPU={(nGpu > 0 ? sGpu / nGpu : 0):0.00}ms(p95 {gp95:0.00}) " +
                              $"texMem={sTexMax / 1048576f:0.0}MB totalAlloc={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f:0.0}MB " +
                              $"gfxDriver={UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576f:0.0}MB night={DayNightController.Night:0.00}");
                    nStat = nGpu = 0; sSetPass = sDraw = sTris = sShadow = sTexMax = sMain = sRender = sGpu = 0; gpuList.Clear(); mainList.Clear();
                }
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
