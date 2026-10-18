using System;
using UnityEngine;

namespace Cafe
{
    /// Lựa chọn âm thanh của người chơi (PlayerPrefs, theo máy, không đi theo save game; cùng khuôn với GraphicsQuality).
    /// Không đặt tên AudioSettings vì trùng UnityEngine.AudioSettings.
    public static class AudioPrefs
    {
        private const string MusicKey = "cafe.music";
        private const string SfxKey = "cafe.sfx";
        private const string HapticsKey = "cafe.haptics";

        public const int DefaultMusicStep = 2;   // Vừa
        public const int DefaultSfxStep = 3;     // To

        public static int MusicStep { get; private set; } = DefaultMusicStep;
        public static int SfxStep { get; private set; } = DefaultSfxStep;
        public static bool Haptics { get; private set; } = true;

        public static float MusicGain { get { return AudioRules.StepGain(MusicStep); } }
        public static float SfxGain { get { return AudioRules.StepGain(SfxStep); } }

        public static event Action Changed;

        public static void Init()
        {
            MusicStep = AudioRules.ClampStep(PlayerPrefs.GetInt(MusicKey, DefaultMusicStep));
            SfxStep = AudioRules.ClampStep(PlayerPrefs.GetInt(SfxKey, DefaultSfxStep));
            Haptics = PlayerPrefs.GetInt(HapticsKey, 1) != 0;
        }

        public static void SetMusic(int step)
        {
            MusicStep = AudioRules.ClampStep(step);
            PlayerPrefs.SetInt(MusicKey, MusicStep);
            Commit();
        }

        public static void SetSfx(int step)
        {
            SfxStep = AudioRules.ClampStep(step);
            PlayerPrefs.SetInt(SfxKey, SfxStep);
            Commit();
        }

        public static void SetHaptics(bool on)
        {
            Haptics = on;
            PlayerPrefs.SetInt(HapticsKey, on ? 1 : 0);
            Commit();
        }

        private static void Commit()
        {
            PlayerPrefs.Save();
            if (Changed != null) Changed();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            MusicStep = DefaultMusicStep;
            SfxStep = DefaultSfxStep;
            Haptics = true;
            Changed = null;
        }
    }
}
