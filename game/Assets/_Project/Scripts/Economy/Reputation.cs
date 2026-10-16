using UnityEngine;

namespace Cafe
{
    /// Điểm uy tín (RP), hạng sao và level (LevelRules). RP không tụt dưới ngưỡng level đã đạt (sàn mềm; mốc sao cũng là mốc level).
    public class Reputation
    {
        public float RP { get; private set; }

        /// Level hiện tại theo RP (LevelRules).
        public int Level { get { return LevelRules.LevelFor(RP); } }

        /// Level cao nhất đã nhận thưởng (có lưu). Cao hơn Level khi vào lại giữa ngày: RP về mốc đầu ngày nhưng thưởng đã nhận rồi.
        public int RewardedLevel { get; private set; }

        public int Stars
        {
            get
            {
                int[] th = GameBalance.StarThresholds;
                int s = 1;
                for (int i = 1; i < th.Length; i++)
                    if (RP >= th[i]) s = i + 1;
                return s;
            }
        }

        /// RP đầu hạng sao hiện tại và RP cần cho hạng kế (-1 nếu đã tối đa).
        public void GetRange(out int current, out int next)
        {
            int[] th = GameBalance.StarThresholds;
            int s = Stars;
            current = th[s - 1];
            next = s < th.Length ? th[s] : -1;
        }

        public float ProgressToNext
        {
            get
            {
                int cur, next;
                GetRange(out cur, out next);
                if (next < 0) return 1f;
                return Mathf.Clamp01((RP - cur) / (float)(next - cur));
            }
        }

        /// rewardedLevel: level đã thưởng trong save (0 = save cũ chưa có: không thưởng bù cho level đã đạt).
        public void Init(float rp, int rewardedLevel = 0)
        {
            RP = Mathf.Max(0f, rp);
            RewardedLevel = Mathf.Max(rewardedLevel, Level);
            GameEvents.RaiseReputationChanged(RP, Stars);
        }

        /// Cộng/trừ RP; trả về thay đổi thực tế (sau khi áp sàn).
        public float Add(float delta)
        {
            float old = RP;
            int floorRp = LevelRules.RpFor(Level);
            RP = Mathf.Max(floorRp, RP + delta);
            GameEvents.RaiseReputationChanged(RP, Stars);
            // Lên một hay nhiều level (vd. chương truyện +100 RP): mỗi level thưởng đúng một lần
            while (RewardedLevel < Level)
            {
                RewardedLevel++;
                GameEvents.RaiseLevelUp(RewardedLevel);
            }
            return RP - old;
        }
    }
}
