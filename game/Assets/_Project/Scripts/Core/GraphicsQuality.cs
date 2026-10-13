using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Cafe
{
    /// Mức đồ họa đang dùng: đọc / lưu lựa chọn của người chơi (PlayerPrefs, theo máy — không đi theo save game), áp dụng
    /// cho Unity (mức chất lượng => URP asset tương ứng), giới hạn FPS trên điện thoại và bật / tắt viền chữ UI.
    /// Lần chạy đầu chưa có lựa chọn thì tự chọn theo cấu hình máy (GraphicsPresets.Recommend).
    public static class GraphicsQuality
    {
        private const string PrefKey = "cafe.graphics";

        public static GraphicsLevel Current { get; private set; }
        /// Mức máy này được gợi ý (để hiện "Gợi ý cho máy bạn").
        public static GraphicsLevel Recommended { get; private set; }
        public static event Action Changed;

        public static bool UiOutlines { get { return GraphicsPresets.UiOutlines(Current); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = GraphicsLevel.High;
            Recommended = GraphicsLevel.High;
            Changed = null;
        }

        /// Gọi một lần khi game khởi động (GameBootstrap.Awake).
        public static void Init()
        {
            Recommended = GraphicsPresets.Recommend(SystemInfo.systemMemorySize, SystemInfo.processorCount, IsMobile);
            GraphicsLevel level = Recommended;
            if (PlayerPrefs.HasKey(PrefKey)) level = GraphicsPresets.Clamp(PlayerPrefs.GetInt(PrefKey));
            GraphicsLevel forced;
            if (TryCommandLineLevel(out forced)) level = forced; // chỉ để chạy thử tự động (-gfx low|medium|high)
            Apply(level);
        }

        /// Người chơi chọn mức mới trong Cài đặt: áp dụng ngay và nhớ cho lần sau.
        public static void Set(GraphicsLevel level)
        {
            if (level == Current) return;
            Apply(level);
            PlayerPrefs.SetInt(PrefKey, (int)level);
            PlayerPrefs.Save();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// Chỉ cho autoplay (-gfxcycle): đổi mức giữa chừng như người chơi nhưng KHÔNG ghi PlayerPrefs.
        public static void DebugApply(GraphicsLevel level) { Apply(level); }
#endif

        private static bool IsMobile
        {
            get { return Application.isMobilePlatform; }
        }

        private static void Apply(GraphicsLevel level)
        {
            Current = level;
            int index = Mathf.Clamp((int)level, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(index, true);
            if (IsMobile)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = GraphicsPresets.TargetFps(level);
            }
            ApplyUiOutlines();
            var rp = GraphicsSettings.currentRenderPipeline;
            Debug.Log("[Gfx] mức " + level + " (chất lượng " + index + ", URP=" + (rp != null ? rp.name : "không") + ", FPS tối đa " +
                      (IsMobile ? GraphicsPresets.TargetFps(level).ToString() : "không giới hạn") + ", viền chữ " + UiOutlines + ")");
            if (Changed != null) Changed();
        }

        /// Bật / tắt mọi viền chữ UI đã tạo (HudBuilder.AddText cũng đọc UiOutlines cho chữ tạo sau).
        public static void ApplyUiOutlines()
        {
            bool on = UiOutlines;
            Outline[] all = UnityEngine.Object.FindObjectsByType<Outline>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++) all[i].enabled = on;
        }

        private static bool TryCommandLineLevel(out GraphicsLevel level)
        {
            level = GraphicsLevel.High;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-gfx");
            if (i < 0 || i + 1 >= args.Length) return false;
            switch (args[i + 1].ToLowerInvariant())
            {
                case "low": level = GraphicsLevel.Low; return true;
                case "medium": level = GraphicsLevel.Medium; return true;
                case "high": level = GraphicsLevel.High; return true;
            }
#endif
            return false;
        }
    }
}
