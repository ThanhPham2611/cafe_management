using System.Runtime.InteropServices;
using UnityEngine;

namespace Cafe
{
    /// Rung nhẹ theo kiểu (Light / Medium / Success). Android: VibrationEffect qua JNI; iOS: plugin Plugins/iOS/CafeHaptics.mm;
    /// máy khác: không làm gì. Tắt được trong Cài đặt; cách nhau tối thiểu MinGap giây.
    public static class Haptics
    {
        private const float MinGap = 0.05f;
        private static float last = -10f;

        /// Chỉ thiết bị di động mới có rung (Cài đặt ẩn hàng "Rung" ở nơi khác).
        public static bool Supported { get { return Application.isMobilePlatform; } }

        public static void Play(HapticKind kind)
        {
            if (kind == HapticKind.None || !AudioPrefs.Haptics || !Supported) return;
            float now = Time.realtimeSinceStartup;
            if (now - last < MinGap) return;
            last = now;
            Vibrate(kind);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static AndroidJavaClass effectClass;
        private static bool hasAmplitude;
        private static bool inited, failed;
        // Không bao giờ true: chỉ để Unity thấy lời gọi Handheld.Vibrate và tự thêm quyền VIBRATE vào AndroidManifest.
        private static bool addVibratePermission;

        private static void Init()
        {
            inited = true;
            if (addVibratePermission) Handheld.Vibrate();
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                hasAmplitude = vibrator != null && vibrator.Call<bool>("hasAmplitudeControl");
            }
            catch (System.Exception) { failed = true; }
        }

        private static void Vibrate(HapticKind kind)
        {
            if (!inited) Init();
            if (failed || vibrator == null) return;
            try
            {
                AndroidJavaObject effect;
                switch (kind)
                {
                    case HapticKind.Light:
                        effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", 15L, hasAmplitude ? 60 : -1);
                        break;
                    case HapticKind.Medium:
                        effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", 25L, hasAmplitude ? 120 : -1);
                        break;
                    default:
                        effect = effectClass.CallStatic<AndroidJavaObject>("createWaveform",
                            new long[] { 0, 20, 60, 30 }, new int[] { 0, 100, 0, 200 }, -1);
                        break;
                }
                vibrator.Call("vibrate", effect);
            }
            catch (System.Exception) { failed = true; }
        }
#elif UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void _CafeHaptic(int kind);

        private static void Vibrate(HapticKind kind) { _CafeHaptic((int)kind); }
#else
        private static void Vibrate(HapticKind kind) { }
#endif
    }
}
