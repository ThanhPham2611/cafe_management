using UnityEngine;

namespace Cafe
{
    /// Một ngày: 210 giây thực (cấp 1-2) hoặc 300 giây (nhà hàng cấp 3) = 06:00 -> 22:00. Hết giờ -> đóng cửa, khi khách ra hết thì tổng kết.
    public class DayCycle : MonoBehaviour
    {
        /// Prep: sau tổng kết, trước khi mở cửa ngày kế tiếp; đồng hồ đứng, không có khách (trang trí, mua đồ, nâng cấp quán).
        public enum Phase { Idle, Open, Closing, Summary, Prep }

        private GameBootstrap game;
        private float length = GameBalance.DayRealSeconds;

        /// Độ dài ngày đang chạy (giây thực), đặt lúc bắt đầu ngày theo cấp quán.
        public float Length { get { return length; } }
        /// Giây in-game trôi qua mỗi giây thực của ngày đang chạy.
        public float InGameRate { get { return GameBalance.InGameSecondsPerRealSecondForLength(length); } }

        public Phase CurrentPhase { get; private set; }
        public int Day { get; private set; }
        /// Giây thực kể từ lúc mở cửa.
        public float Elapsed { get; private set; }
        /// Đồng hồ in-game (giây), tăng đơn điệu qua các ngày; dùng cho ly để lại.
        public float InGameSeconds { get; private set; }
        public float ClosingSeconds { get; private set; }
        public DaySummary Summary { get; private set; }

        public bool IsOpen { get { return CurrentPhase == Phase.Open; } }

        public float Normalized
        {
            get { return Mathf.Clamp01(Elapsed / length); }
        }

        /// Giờ trong ngày (6.0 .. 22.0).
        public float ClockHours
        {
            get { return GameBalance.OpenHour + Normalized * (GameBalance.CloseHour - GameBalance.OpenHour); }
        }

        public void Init(GameBootstrap g)
        {
            game = g;
        }

        public void Begin(int day)
        {
            Day = day;
            length = GameBalance.DayRealSecondsFor(game != null && game.Tier != null ? game.Tier.Current : 1);
            Elapsed = 0f;
            ClosingSeconds = 0f;
            Summary = new DaySummary { day = day };
            CurrentPhase = Phase.Open;
        }

        /// Từ màn tổng kết sang pha chuẩn bị. Chưa tăng số ngày: ngày mới chỉ bắt đầu ở Begin().
        public void EnterPrep()
        {
            if (CurrentPhase == Phase.Summary) CurrentPhase = Phase.Prep;
        }

        /// Lên cấp giữa ngày: đổi độ dài ngày mà giữ nguyên giờ trên đồng hồ (Elapsed co giãn theo tỉ lệ).
        /// Trả về tỉ lệ co giãn (mới / cũ; 1 = không đổi) để lịch sinh khách còn lại co giãn theo.
        public float OnTierChanged()
        {
            float newLength = GameBalance.DayRealSecondsFor(game != null && game.Tier != null ? game.Tier.Current : 1);
            if (Mathf.Approximately(newLength, length)) return 1f;
            float ratio = newLength / length;
            Elapsed *= ratio;
            length = newLength;
            return ratio;
        }

        private void Update()
        {
            if (game != null && game.Tier != null && game.Tier.Transitioning) return; // đang chuyển cấp quán: đồng hồ ngày đứng
            if (CurrentPhase == Phase.Open && TutorialGate.ClockHold) return; // hướng dẫn ngày 1: đồng hồ đứng tới khi xong các bước đầu
            float dt = Time.deltaTime;
            switch (CurrentPhase)
            {
                case Phase.Open:
                    Elapsed += dt;
                    InGameSeconds += dt * InGameRate;
                    if (Elapsed >= length)
                    {
                        Elapsed = length;
                        CurrentPhase = Phase.Closing;
                        ClosingSeconds = 0f;
                        GameEvents.RaiseDayClosing();
                    }
                    break;

                case Phase.Closing:
                    ClosingSeconds += dt;
                    InGameSeconds += dt * InGameRate;
                    if (game.Spawner.ActiveCount == 0) EndDay();
                    break;
            }
        }

        private void EndDay()
        {
            CurrentPhase = Phase.Summary;
            game.Orders.Clear();
            game.Station.ResetAll();

            // Phí cố định theo cấp: vỉa hè 60, thuê mặt bằng 150 (điện nước, vận hành)
            int fixedCost = game.Tier != null ? game.Tier.DailyFixedCost : GameBalance.SidewalkFee;
            Summary.fee = fixedCost;
            game.Wallet.Spend(fixedCost);
            if (game.Staff != null) game.Staff.PayDailyWages(Summary);
            if (game.Tier != null) game.Tier.OnDayEnd(Summary); // đếm ngày thuê + trả nợ 30% doanh thu (cấp 2)
            Summary.stars = game.Reputation.Stars;

            game.SaveSnapshot();
            GameEvents.RaiseDayEnded(Summary);
        }
    }
}
