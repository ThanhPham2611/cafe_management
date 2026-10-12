using System;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Diagnostics;
#endif

namespace Cafe
{
    /// Đo thời gian một đoạn code nặng: <c>using (PerfNote.Measure("spawn")) { ... }</c>. Kết quả gộp theo tên và in
    /// trong dòng [Perf] của DebugPerfProbe (cờ -perf). Bản release: struct rỗng, không tốn gì.
    public readonly struct PerfNote : IDisposable
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly string tag;
        private readonly long start;

        private PerfNote(string tag)
        {
            this.tag = tag;
            start = Stopwatch.GetTimestamp();
        }

        public static PerfNote Measure(string tag)
        {
            return new PerfNote(tag);
        }

        public void Dispose()
        {
            DebugPerfProbe.Record(tag, (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }
#else
        public static PerfNote Measure(string tag)
        {
            return default;
        }

        public void Dispose() { }
#endif
    }
}
