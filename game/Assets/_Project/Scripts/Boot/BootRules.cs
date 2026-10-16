using UnityEngine;

namespace Cafe
{
    /// Logic thuần của màn tải (có test EditMode): thời gian tối thiểu, ánh xạ tiến độ, chọn mẹo.
    public static class BootRules
    {
        /// Màn tải hiện tối thiểu bấy nhiêu giây để logo kịp hiện (onboarding_spec §4.2).
        public const float MinDisplaySeconds = 1.5f;

        /// Quá thời gian này mà GameBootstrap vẫn chưa báo xong thì thôi chờ (tránh kẹt vĩnh viễn nếu Build() lỗi).
        public const float MaxWaitSeconds = 45f;

        /// Phần thanh dành cho việc nạp scene (AsyncOperation.progress 0..0.9); phần còn lại là dựng quán (Build).
        private const float SceneShare = 0.8f;

        /// Tiến độ hiển thị 0..1. asyncProgress: AsyncOperation.progress (0..0.9, 1 khi xong);
        /// sceneLoaded: scene đã kích hoạt; buildFinished: GameBootstrap.Build() xong.
        public static float DisplayProgress(float asyncProgress, bool sceneLoaded, bool buildFinished)
        {
            if (buildFinished) return 1f;
            if (sceneLoaded) return SceneShare + 0.1f; // đang dựng lưới / NavMesh: dừng ở 90%
            return Mathf.Clamp01(asyncProgress / 0.9f) * SceneShare;
        }

        /// Làm mượt thanh: chỉ tiến, không lùi, và không vượt target quá nhanh.
        public static float Smooth(float shown, float target, float dt, float speed)
        {
            if (target <= shown) return shown;
            return Mathf.Min(target, shown + speed * Mathf.Max(0f, dt));
        }

        /// Được phép tắt màn tải chưa.
        public static bool CanDismiss(float elapsed, bool buildFinished)
        {
            if (elapsed >= MaxWaitSeconds) return true;
            return buildFinished && elapsed >= MinDisplaySeconds;
        }

        /// Chọn mẹo theo chỉ số ngẫu nhiên (an toàn với mảng rỗng / chỉ số âm).
        public static string PickTip(string[] tips, int randomIndex)
        {
            if (tips == null || tips.Length == 0) return "";
            int i = randomIndex % tips.Length;
            if (i < 0) i += tips.Length;
            return tips[i];
        }

        /// Hệ số pop của logo: 0.8 -> 1.05 (60% thời gian đầu) -> 1.0. t = 0..1.
        public static float LogoPopScale(float t)
        {
            t = Mathf.Clamp01(t);
            const float split = 0.6f;
            if (t < split)
            {
                float k = t / split;
                k = 1f - (1f - k) * (1f - k); // ease-out
                return Mathf.Lerp(0.8f, 1.05f, k);
            }
            float u = (t - split) / (1f - split);
            return Mathf.Lerp(1.05f, 1f, u);
        }

        /// Màn tiêu đề tự bỏ qua khi chạy tự động (batchmode hoặc cờ của DebugAutoPilot: -auto*, -shot*, -cheat*, -perf, -gfxcycle,
        /// -bestmenu, -notitle) để bot / QA không bị chặn. Cờ -title ép hiện (chụp màn hình màn tiêu đề).
        public static bool ShouldSkipTitle(string[] args, bool batchMode)
        {
            if (args != null)
                for (int i = 0; i < args.Length; i++)
                    if (args[i] == "-title") return false;
            if (batchMode) return true;
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.IsNullOrEmpty(a)) continue;
                if (a.StartsWith("-auto") || a.StartsWith("-shot") || a.StartsWith("-cheat")) return true;
                if (a == "-perf" || a == "-gfxcycle" || a == "-bestmenu" || a == "-notitle") return true;
            }
            return false;
        }
    }
}
