using UnityEngine;

namespace Cafe
{
    /// Luật âm thanh thuần (không đụng Unity runtime) để kiểm thử được: âm lượng theo bậc, ducking, cooldown, chọn biến thể,
    /// chọn voice, rung theo âm, và khi nào bật âm thanh.
    public static class AudioRules
    {
        public const float DuckDb = -4f;          // art_bible §11: nhạc nền nhỏ đi 4 dB khi có popup
        public const int MaxStep = 3;             // 0 Tắt, 1 Nhỏ, 2 Vừa, 3 To

        public static float DbToGain(float db) { return Mathf.Pow(10f, db / 20f); }

        public static int ClampStep(int step) { return Mathf.Clamp(step, 0, MaxStep); }

        /// Bậc -> hệ số âm lượng: Tắt 0, Nhỏ -14 dB, Vừa -7 dB, To 0 dB.
        public static float StepGain(int step)
        {
            step = ClampStep(step);
            return step == 0 ? 0f : DbToGain(-7f * (MaxStep - step));
        }

        /// Hệ số ducking của nhạc: -4 dB khi có popup / tổng kết (duck) hoặc khi game đứng thời gian (timeFrozen: hội thoại truyện).
        public static float DuckFactor(bool duck, bool timeFrozen)
        {
            return (duck || timeFrozen) ? DbToGain(DuckDb) : 1f;
        }

        public static bool CooldownReady(float now, float last, float cooldown)
        {
            return cooldown <= 0f || now - last >= cooldown;
        }

        public static float Pitch(float min, float max, float r01)
        {
            return Mathf.Lerp(min, max, Mathf.Clamp01(r01));
        }

        /// Chọn biến thể (clip) không trùng clip vừa phát khi có từ 2 clip trở lên. count <= 0 -> -1.
        public static int NextVariant(int count, int last, float r01)
        {
            if (count <= 0) return -1;
            if (count == 1) return 0;
            if (last < 0 || last >= count) return Mathf.Min(count - 1, Mathf.FloorToInt(Mathf.Clamp01(r01) * count));
            int i = Mathf.Min(count - 2, Mathf.FloorToInt(Mathf.Clamp01(r01) * (count - 1)));
            if (i >= last) i++;
            return i;
        }

        public static float Fade(float current, float target, float dt, float seconds)
        {
            if (seconds <= 0f) return target;
            return Mathf.MoveTowards(current, target, dt / seconds);
        }

        /// Chọn voice cho âm mới. priority nhỏ = quan trọng hơn (0 = âm chủ đạo). Còn voice rảnh thì dùng; hết thì cướp voice kém
        /// quan trọng nhất (priority lớn nhất, hòa thì phát lâu nhất) với điều kiện không quan trọng hơn âm mới; không có thì -1.
        public static int PickVoice(bool[] playing, int[] priority, float[] startTime, int incomingPriority)
        {
            for (int i = 0; i < playing.Length; i++) if (!playing[i]) return i;
            int best = -1;
            for (int i = 0; i < playing.Length; i++)
            {
                if (priority[i] < incomingPriority) continue;
                if (best < 0 || priority[i] > priority[best] || (priority[i] == priority[best] && startTime[i] < startTime[best])) best = i;
            }
            return best;
        }

        /// Kiểu rung đi kèm âm. Âm do nhân viên làm (Serve) không rung.
        public static HapticKind HapticFor(Sfx s)
        {
            switch (s)
            {
                case Sfx.UiTap:
                case Sfx.UiBack:
                case Sfx.OrderSelect:
                case Sfx.CustomerTap:
                case Sfx.StepOk:
                case Sfx.BrewDone:
                case Sfx.CupPick:
                case Sfx.WipeDone:
                case Sfx.BuildPlace:
                case Sfx.Sell:
                case Sfx.Purchase:
                case Sfx.Claim:
                case Sfx.Chest:
                    return HapticKind.Light;
                case Sfx.StepWrong:
                case Sfx.Ruin:
                case Sfx.LevelUp:
                    return HapticKind.Medium;
                case Sfx.Fanfare:
                    return HapticKind.Success;
                default:
                    return HapticKind.None;
            }
        }

        /// Có tạo hệ thống âm thanh không: bot / batchmode không cần (BootRules.ShouldSkipTitle gom các cờ chạy tự động).
        /// "-audio" ép bật (nghe thử khi chạy tự động).
        public static bool EnabledForRun(string[] args, bool batchMode)
        {
            if (args != null)
                for (int i = 0; i < args.Length; i++)
                    if (args[i] == "-audio") return true;
            return !BootRules.ShouldSkipTitle(args, batchMode);
        }
    }
}
