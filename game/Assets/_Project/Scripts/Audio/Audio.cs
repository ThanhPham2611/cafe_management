using UnityEngine;

namespace Cafe
{
    /// Cửa vào duy nhất của âm thanh: Audio.Play(Sfx.X). Không có host (bot / batchmode) hoặc thiếu clip thì là no-op.
    public static class Audio
    {
        public static bool Enabled { get { return AudioHost.Instance != null; } }

        /// Phát hiệu ứng (kèm rung theo AudioRules.HapticFor khi haptic = true).
        public static void Play(Sfx id, float volumeScale = 1f, bool haptic = true)
        {
            AudioHost host = AudioHost.Instance;
            if (host == null) return;
            if (haptic) Haptics.Play(AudioRules.HapticFor(id));
            host.PlaySfx(id, volumeScale);
        }

        public static void PlayMusic(Bgm id)
        {
            AudioHost host = AudioHost.Instance;
            if (host != null) host.PlayMusic(id);
        }

        /// Nhạc nhỏ đi 4 dB (tổng kết, popup). Là cờ bật / tắt, không đếm tham chiếu nên không thể kẹt.
        public static void SetDuck(bool on)
        {
            AudioHost host = AudioHost.Instance;
            if (host != null) host.SetDuck(on);
        }

        /// Tắt / bật toàn bộ (khi xem quảng cáo).
        public static void MuteAll(bool on)
        {
            AudioListener.volume = on ? 0f : 1f;
        }

        public static void UiTap() { Play(Sfx.UiTap); }
        public static void UiBack() { Play(Sfx.UiBack); }
    }
}
