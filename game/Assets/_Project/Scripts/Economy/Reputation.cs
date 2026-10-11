using UnityEngine;

namespace Cafe
{
    /// Điểm uy tín (RP) và hạng sao. RP không tụt dưới ngưỡng ★ đã đạt (sàn mềm).
    public class Reputation
    {
        public float RP { get; private set; }

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

        public void Init(float rp)
        {
            RP = Mathf.Max(0f, rp);
            GameEvents.RaiseReputationChanged(RP, Stars);
        }

        /// Cộng/trừ RP; trả về thay đổi thực tế (sau khi áp sàn).
        public float Add(float delta)
        {
            float old = RP;
            int floorRp = GameBalance.StarThresholds[Stars - 1];
            RP = Mathf.Max(floorRp, RP + delta);
            GameEvents.RaiseReputationChanged(RP, Stars);
            return RP - old;
        }
    }
}
