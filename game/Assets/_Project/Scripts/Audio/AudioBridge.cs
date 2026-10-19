using System;
using UnityEngine;

namespace Cafe
{
    /// Nối GameEvents với âm thanh. Mọi handler bọc try/catch: một lỗi âm thanh không được làm đứt chuỗi thanh toán / cuối ngày.
    /// Không gắn MoneyChanged (bắn cả khi chi tiêu) và Toast (chuỗi, không có loại).
    public sealed class AudioBridge
    {
        public void Attach()
        {
            GameEvents.OrderAdded += OnOrderAdded;
            GameEvents.CustomerServed += OnCustomerServed;
            GameEvents.DayStarted += OnDayStarted;
            GameEvents.DayClosing += OnDayClosing;
            GameEvents.DayEnded += OnDayEnded;
            GameEvents.LevelUp += OnLevelUp;
            GameEvents.LevelsClaimed += OnLevelsClaimed;
            GameEvents.FundStage += OnFundStage;
            GameEvents.WeeklyDone += OnWeeklyDone;
        }

        public void Detach()
        {
            GameEvents.OrderAdded -= OnOrderAdded;
            GameEvents.CustomerServed -= OnCustomerServed;
            GameEvents.DayStarted -= OnDayStarted;
            GameEvents.DayClosing -= OnDayClosing;
            GameEvents.DayEnded -= OnDayEnded;
            GameEvents.LevelUp -= OnLevelUp;
            GameEvents.LevelsClaimed -= OnLevelsClaimed;
            GameEvents.FundStage -= OnFundStage;
            GameEvents.WeeklyDone -= OnWeeklyDone;
        }

        private static void Run(Action a)
        {
            try { a(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void OnOrderAdded(Order o) { Run(() => Audio.Play(Sfx.OrderNew)); }
        private void OnCustomerServed(Customer c) { Run(() => Audio.Play(Sfx.CoinPay)); }
        private void OnDayStarted(int day)
        {
            Run(() =>
            {
                Audio.SetDuck(false);
                Audio.Play(Sfx.DayOpen);
                Audio.PlayMusic(Bgm.Day);
            });
        }
        private void OnDayClosing() { Run(() => Audio.Play(Sfx.DayClose)); }
        private void OnDayEnded(DaySummary s) { Run(() => Audio.PlayMusic(Bgm.Calm)); }
        private void OnLevelUp(int level) { Run(() => Audio.Play(Sfx.LevelUp)); }
        private void OnLevelsClaimed(int from, int to, int xu, int gems) { Run(() => Audio.Play(Sfx.Claim)); }
        private void OnFundStage(int tier, int stage, int gems) { Run(() => Audio.Play(Sfx.Claim)); }
        private void OnWeeklyDone(int xu, int gems) { Run(() => Audio.Play(Sfx.Fanfare)); }
    }
}
