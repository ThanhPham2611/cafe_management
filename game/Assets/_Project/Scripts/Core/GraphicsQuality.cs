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
        private const string FpsKey = "cafe.fps";
        private const string ReduceMotionKey = "cafe.reducemotion";

        private static int fpsPreference = 60;
        private static int[] fpsChoices = { 60 };
        private static int fakeHz;   // chỉ bản Development: -fakehz <Hz> giả lập điện thoại màn hình quét nhanh, để thử Cài đặt trên máy tính

        public static GraphicsLevel Current { get; private set; }
        /// Tần số quét cao nhất của màn hình (Hz) ở độ phân giải hiện tại, đọc một lần lúc khởi động (chỉ để ghi log).
        public static int DisplayHz { get; private set; }
        /// Giới hạn FPS đang áp dụng trên điện thoại.
        public static int TargetFps { get { return GraphicsPresets.TargetFps(Current, fpsPreference, fpsChoices); } }
        public static int[] FpsChoices { get { return fpsChoices; } }
        /// Chỉ điện thoại mới giới hạn FPS, và chỉ có gì để chọn khi màn hình quét nhanh hơn 60 Hz.
        public static bool FpsSelectable { get { return IsMobile && fpsChoices.Length > 1; } }
        /// "Giảm chuyển động": camera không quán tính, xoay chậm và dính góc 45 độ, không nghiêng, không xoay bằng 2 ngón.
        public static bool ReduceMotion { get; private set; }
        /// Mức máy này được gợi ý (để hiện "Gợi ý cho máy bạn").
        public static GraphicsLevel Recommended { get; private set; }
        public static event Action Changed;

        public static int DayNightHz { get { return GraphicsPresets.DayNightHz(Current); } }

        public static bool UiOutlines { get { return GraphicsPresets.UiOutlines(Current); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = GraphicsLevel.High;
            Recommended = GraphicsLevel.High;
            DisplayHz = 60;
            fpsPreference = 60;
            fpsChoices = new[] { 60 };
            fakeHz = 0;
            ReduceMotion = false;
            Changed = null;
        }

        /// Gọi một lần khi game khởi động (GameBootstrap.Awake).
        public static void Init()
        {
            Recommended = GraphicsPresets.Recommend(SystemInfo.systemMemorySize, SystemInfo.processorCount, IsMobile);
            fakeHz = ReadFakeHz();
            int[] rates = ReadRefreshRates();
            DisplayHz = Mathf.Max(rates);
            fpsChoices = GraphicsPresets.FpsChoices(rates, Application.platform == RuntimePlatform.IPhonePlayer);
            fpsPreference = PlayerPrefs.GetInt(FpsKey, 60);
            ReduceMotion = PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
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

        /// Người chơi chọn FPS (60 / 90 / 120): áp dụng ngay và nhớ cho lần sau. Mức Thấp vẫn cố định 30.
        public static void SetFps(int fps)
        {
            if (fps == fpsPreference) return;
            fpsPreference = fps;
            PlayerPrefs.SetInt(FpsKey, fps);
            PlayerPrefs.Save();
            ApplyFps();
            Debug.Log("[Gfx] FPS tối đa " + TargetFps + " (màn hình " + DisplayHz + " Hz)");
            if (Changed != null) Changed();
        }

        /// Bật / tắt "Giảm chuyển động" trong Cài đặt: có hiệu lực ngay và được nhớ cho lần sau.
        public static void SetReduceMotion(bool on)
        {
            if (on == ReduceMotion) return;
            ReduceMotion = on;
            PlayerPrefs.SetInt(ReduceMotionKey, on ? 1 : 0);
            PlayerPrefs.Save();
            if (Changed != null) Changed();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// Chỉ cho autoplay (-gfxcycle): đổi mức giữa chừng như người chơi nhưng KHÔNG ghi PlayerPrefs.
        public static void DebugApply(GraphicsLevel level) { Apply(level); }
#endif

        private static bool IsMobile
        {
            get { return Application.isMobilePlatform || fakeHz > 0; }
        }

        private static void Apply(GraphicsLevel level)
        {
            Current = level;
            int index = Mathf.Clamp((int)level, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(index, true);
            ApplyFps();
            ApplyUiOutlines();
            var rp = GraphicsSettings.currentRenderPipeline;
            Debug.Log("[Gfx] mức " + level + " (chất lượng " + index + ", URP=" + (rp != null ? rp.name : "không") + ", FPS tối đa " +
                      (IsMobile ? TargetFps.ToString() : "không giới hạn") + ", màn hình " + DisplayHz + " Hz, viền chữ " + UiOutlines + ")");
            if (Changed != null) Changed();
        }

        private static void ApplyFps()
        {
            if (!IsMobile) return;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps;
        }

        /// Các tần số quét (Hz) màn hình hỗ trợ ở độ phân giải hiện tại. Đọc cả danh sách chế độ vì máy 120 Hz có lúc đang hạ
        /// xuống 60 Hz để tiết kiệm pin; chỉ lấy chế độ cùng độ phân giải vì nhiều máy chỉ cho 120 Hz ở FHD+ còn QHD+ khóa 60 Hz.
        private static int[] ReadRefreshRates()
        {
            if (fakeHz > 0) return new[] { fakeHz };
            Resolution cur = Screen.currentResolution;
            var rates = new System.Collections.Generic.List<int> { Mathf.RoundToInt((float)cur.refreshRateRatio.value) };
            Resolution[] modes = Screen.resolutions;
            int before = rates.Count;
            for (int i = 0; i < modes.Length; i++)
                if (modes[i].width == cur.width && modes[i].height == cur.height)
                    rates.Add(Mathf.RoundToInt((float)modes[i].refreshRateRatio.value));
            // ponytail: độ phân giải hiện tại không khớp chế độ nào (đang giảm độ nét) -> dùng mọi chế độ; kiểm lại trên máy thật
            if (rates.Count == before)
                for (int i = 0; i < modes.Length; i++) rates.Add(Mathf.RoundToInt((float)modes[i].refreshRateRatio.value));
            return rates.ToArray();
        }

        /// Bật / tắt mọi viền chữ UI đã tạo (HudBuilder.AddText cũng đọc UiOutlines cho chữ tạo sau).
        public static void ApplyUiOutlines()
        {
            bool on = UiOutlines;
            Outline[] all = UnityEngine.Object.FindObjectsByType<Outline>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++) all[i].enabled = on;
        }

        private static int ReadFakeHz()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-fakehz");
            int hz;
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out hz)) return Mathf.Clamp(hz, 60, 240);
#endif
            return 0;
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
