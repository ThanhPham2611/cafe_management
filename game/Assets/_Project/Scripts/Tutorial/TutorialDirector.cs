using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Máy trạng thái hướng dẫn (docs/onboarding_spec.md §2): phần lõi ngày 1 gồm 8 bước + các gợi ý theo tình huống (hiện một lần).
    /// Tạo từ GameBootstrap.Build() sau HUD. Nghe GameEvents + Station, ghi cờ vào TutorialGate (CustomerSpawner, Customer,
    /// BrewStation, DayCycle đọc), vẽ qua TutorialOverlay. Logic quyết định nằm ở TutorialRules (có test).
    public sealed class TutorialDirector : MonoBehaviour
    {
        /// Bot (-autoplay...) mặc định bỏ qua hướng dẫn để số đo kinh tế không đổi; -tutorial tắt cờ này.
        public static bool DisabledForAutomation;
        /// Bot chạy hướng dẫn (-tutorial): ghi cảnh báo khi một bước kẹt quá lâu.
        public static bool BotDriven;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            DisabledForAutomation = false;
            BotDriven = false;
        }

        private struct RecapItem
        {
            public string text;
            public string path;
        }

        private const float TipMinGap = 5f;
        private const float TipTimeout = 14f;
        private const float StoolTimeout = 90f;
        private const float StuckSeconds = 90f;

        private GameBootstrap game;
        private TutorialOverlay overlay;
        private readonly TutorialSeen seen = new TutorialSeen();
        private bool disabled;
        private int step;

        // ---- phần lõi ----
        private Customer tutCustomer;
        private float stepClock;
        private bool promptShown;
        private int lastBrewIndex = -1;
        private bool moneyPaid;
        private float moneyPaidAt;
        private bool cupSeen;
        private bool ringShown;
        private float ringShownAt;
        private bool summaryPending;
        private float summaryAt;
        private bool stuckLogged;
        private float stuckClock;          // giây thật ở bước hiện tại, không tính lúc chờ hết ngày (bước tổng kết chờ DayEnded)
        private float sinceProgress;       // giây từ lần qua bước gần nhất (phần kịch bản), chống kẹt
        private float noCustomerTime;      // giây liên tục không có khách nào dùng được ở bước 2-4
        private int respawns;              // số lần thả thêm khách khi khách đầu bỏ về

        // ---- gợi ý theo tình huống ----
        private string activeTip;
        private float tipStart;
        private float nextTipAt;
        private float nextEval;
        private bool lostPending;
        private int stoolStage = -1;
        private int stoolStartSeats;
        private int startSeats;
        private int recapIndex = -1;
        private bool replayArmed;          // "HƯỚNG DẪN" bấm ngoài giờ mở cửa: chạy lại khi ngày kế tiếp bắt đầu
        private static readonly RecapItem[] Recap =
        {
            new RecapItem { text = TutorialStrings.Recap1, path = "OrderColumn" },
            new RecapItem { text = TutorialStrings.Recap2, path = "BrewPanel" },
            new RecapItem { text = TutorialStrings.Recap3, path = "CoinChip" },
            new RecapItem { text = TutorialStrings.Recap4, path = "CupChip" },
        };

        private readonly Dictionary<string, RectTransform> uiCache = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, float> uiNextScan = new Dictionary<string, float>();

        public int Step { get { return step; } }
        public bool IsCoreDone { get { return TutorialRules.IsDone(step); } }
        public string ActiveTipId { get { return activeTip; } }
        public bool WaitingForNext { get { return overlay != null && overlay.NextVisible; } }
        /// Thẻ hướng dẫn đang hiện (thông báo chung giữa-trên sẽ bị thẻ che nên HUD tạm bỏ qua).
        public bool IsShowingCard { get { return overlay != null && overlay.IsShowing; } }

        // ------------------------------------------------------------------ vòng đời

        public static TutorialDirector Create(GameBootstrap g, SaveData save)
        {
            var go = new GameObject("TutorialDirector");
            var d = go.AddComponent<TutorialDirector>();
            d.Init(g, save);
            return d;
        }

        private void Init(GameBootstrap g, SaveData save)
        {
            game = g;
            seen.Load(save != null ? save.tutorialSeen : null);
            step = TutorialRules.InitialStep(save != null, save != null ? save.tutorialStep : 0,
                save != null ? save.day : 1, save != null ? save.tier : 1);
            startSeats = g.Grid != null ? g.Grid.Seats.Count : 0;
            // Save cũ chưa từng qua hướng dẫn: người chơi kỳ cựu, khỏi nhắc những thứ đã quen (gợi ý cấp <= cấp hiện tại)
            if (TutorialRules.IsVeteranMigration(save != null, save != null ? save.tutorialStep : 0, step)
                && (save.tutorialSeen == null || save.tutorialSeen.Length == 0))
                seen.AddKnownForTier(save.tier > 0 ? save.tier : 1);

            if (DisabledForAutomation)
            {
                disabled = true;
                step = TutorialStep.Done;
                TutorialGate.Reset();
                return;
            }

            overlay = TutorialOverlay.Create(g.Hud != null ? g.Hud.transform : transform, OnSkipConfirmed);
            TutorialGate.Reset();
            if (!TutorialRules.IsDone(step))
            {
                // Chuẩn bị cờ TRƯỚC khi ngày 1 bắt đầu (CustomerSpawner.BeginDay đọc Active để không xếp khách quen)
                TutorialGate.Active = true;
                TutorialGate.Step = step;
                ApplyGate();
            }

            GameEvents.DayStarted += OnDayStarted;
            GameEvents.DayEnded += OnDayEnded;
            GameEvents.CustomerLeft += OnCustomerLeft;
        }

        private void OnDestroy()
        {
            GameEvents.DayStarted -= OnDayStarted;
            GameEvents.DayEnded -= OnDayEnded;
            GameEvents.CustomerLeft -= OnCustomerLeft;
            TutorialGate.Reset();
        }

        /// Ghi bước + các gợi ý đã xem vào save.
        public void WriteToSave(SaveData data)
        {
            data.tutorialStep = step;
            data.tutorialSeen = seen.ToArray();
        }

        // ------------------------------------------------------------------ sự kiện

        private void OnDayStarted(int day)
        {
            if (disabled) return;
            if (!IsCoreDone)
            {
                if (day <= 1)
                {
                    if (step == TutorialStep.NotStarted)
                    {
                        // Không có ghế thì khách không vào được: kịch bản sẽ kẹt, bỏ luôn (không đánh dấu đã xem gợi ý)
                        if (!TutorialRules.CanRunScripted(game.Grid != null ? game.Grid.Seats.Count : 0)) FinishCore("fallback");
                        else BeginStep(TutorialStep.Welcome);
                    }
                }
                else
                {
                    bool natural = step >= TutorialStep.Summary;
                    if (natural) LogStep(step, "done");
                    FinishCore(natural ? "done" : "skipped");
                }
            }
            lostPending = false;
            if (replayArmed && IsCoreDone)
            {
                replayArmed = false;
                recapIndex = 0;
                ShowRecap();
            }
        }

        private void OnDayEnded(DaySummary s)
        {
            if (disabled) return;
            if (!IsCoreDone)
            {
                if (step >= TutorialStep.PatienceRing && step < TutorialStep.Summary)
                {
                    LogStep(step, "done");
                    BeginStep(TutorialStep.Summary);
                }
                if (step == TutorialStep.Summary)
                {
                    summaryPending = true;
                    summaryAt = stepClock;
                    stuckClock = 0f; // từ giờ mới tính "kẹt" ở bước tổng kết
                }
            }
            AbortTip(); // cả gợi ý lẫn bản nhắc lại (Replay) không được đè lên bảng tổng kết
        }

        private void OnCustomerLeft(Customer c)
        {
            if (disabled || !IsCoreDone || c == null || c.WasServed) return;
            if (game.Day != null && game.Day.CurrentPhase == DayCycle.Phase.Open) lostPending = true;
        }

        // ------------------------------------------------------------------ cập nhật

        private void Update()
        {
            if (disabled || game == null || game.Day == null) return;

            bool busy = (game.Hud != null && game.Hud.StoryOpen) || (game.Tier != null && game.Tier.Transitioning)
                        || (game.Creator != null && game.Creator.IsOpen);
            overlay.SetSuppressed(busy);

            if (!IsCoreDone)
            {
                stepClock += Time.unscaledDeltaTime;
                bool waitingDayEnd = step == TutorialStep.Summary && !summaryPending; // chờ hết ngày: không phải kẹt
                if (!busy && !waitingDayEnd) stuckClock += Time.unscaledDeltaTime;
                int spawned = game.Spawner != null ? game.Spawner.SpawnedCount : 0;
                int planned = game.Spawner != null ? game.Spawner.PlannedCount : 0;
                TutorialGate.ClockHold = TutorialRules.ClockHold(step, TutorialRules.EffectiveSpawned(spawned, planned));
                TutorialGate.Spawned = spawned;
                if (!busy && !CheckStall(spawned)) UpdateCore();
                if (IsCoreDone) return;
                if (BotDriven && !stuckLogged && step > TutorialStep.NotStarted && stuckClock > StuckSeconds)
                {
                    stuckLogged = true;
                    Debug.LogWarning("[Tutorial] KẸT ở bước " + TutorialRules.StepId(step) + " quá " + StuckSeconds + " giây");
                }
            }
            else if (!busy)
            {
                UpdateTips();
            }
        }

        // ------------------------------------------------------------------ phần lõi

        private void ApplyGate()
        {
            TutorialGate.PatiencePaused = TutorialRules.PatiencePaused(step);
            TutorialGate.NoPenalty = TutorialRules.NoPenalty(step);
            TutorialGate.AllowedSpawns = TutorialRules.AllowedSpawns(step);
            TutorialGate.ClockHold = TutorialRules.ClockHold(step, 0);
            TutorialGate.Step = step;
        }

        private void LogStep(int s, string what)
        {
            Debug.Log("[Tutorial] step " + TutorialRules.StepId(s) + " " + what);
        }

        private void BeginStep(int s)
        {
            step = s;
            stepClock = 0f;
            stuckClock = 0f;
            promptShown = false;
            lastBrewIndex = -1;
            stuckLogged = false;
            ApplyGate();
            LogStep(s, "start");
            overlay.Hide();

            switch (s)
            {
                case TutorialStep.Welcome:
                    Present(new TutorialPrompt
                    {
                        text = TutorialStrings.Welcome,
                        modal = true,
                        block = true,
                        hand = HandMode.None,
                        showNext = true,
                        showSkip = true,
                        onNext = () => CompleteStep(TutorialStep.Welcome)
                    });
                    break;
                case TutorialStep.Money:
                    moneyPaid = false;
                    break;
                case TutorialStep.CollectCup:
                    cupSeen = false;
                    break;
                case TutorialStep.PatienceRing:
                    ringShown = false;
                    break;
                case TutorialStep.Summary:
                    summaryPending = false;
                    break;
            }
        }

        private void CompleteStep(int s)
        {
            if (step != s) return;
            LogStep(s, "done");
            sinceProgress = 0f;
            respawns = 0;
            int next = TutorialRules.Next(s);
            if (TutorialRules.IsDone(next)) FinishCore("done");
            else BeginStep(next);
        }

        private void RegressTo(int s)
        {
            Debug.Log("[Tutorial] step " + TutorialRules.StepId(step) + " -> quay lại " + TutorialRules.StepId(s));
            BeginStep(s);
        }

        private void Present(TutorialPrompt p)
        {
            p.showSkip = true; // "Bỏ qua hướng dẫn" luôn có ở mọi lời nhắc của phần lõi
            overlay.Show(p);
            promptShown = true;
        }

        private void HidePrompt()
        {
            if (!promptShown) return;
            overlay.Hide();
            promptShown = false;
        }

        private void FinishCore(string how)
        {
            bool skipped = how == "skipped";
            step = TutorialStep.Done;
            replayArmed = false;
            if (skipped) seen.AddAllTips();
            TutorialGate.Reset();
            overlay.Hide();
            Debug.Log("[Tutorial] step done (" + how + ")");
            game.SaveSnapshot();
        }

        /// Người chơi xác nhận "Bỏ qua hướng dẫn".
        private void OnSkipConfirmed()
        {
            if (disabled || IsCoreDone) return;
            FinishCore("skipped");
        }

        private Customer FindWaiting(Customer except)
        {
            if (game.Spawner == null) return null;
            var list = game.Spawner.Active;
            for (int i = 0; i < list.Count; i++)
            {
                Customer c = list[i];
                if (c == null || c == except || !c.IsWaitingForDrink) continue;
                if (c.CurrentOrder != null && c.CurrentOrder.IsClaimed) continue;
                return c;
            }
            return null;
        }

        private LeftoverCup FindCollectableCup()
        {
            var cups = game.LeftoverCups;
            for (int i = 0; i < cups.Count; i++)
                if (cups[i] != null && cups[i].IsCollectable) return cups[i];
            return null;
        }

        private TutorialTarget CustomerTarget(Customer c)
        {
            return TutorialTarget.At(() => c != null ? c.transform : null, new Vector3(0f, 0.7f, 0f), new Vector2(230f, 300f));
        }

        /// Có khách nào còn đang vào / chờ món (chưa rời) không.
        private bool HasViableCustomer()
        {
            if (game.Spawner == null) return false;
            var list = game.Spawner.Active;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].State < CustomerState.Leaving) return true;
            return false;
        }

        /// Chống kẹt phần kịch bản: khách đầu bỏ về (hết chỗ ngồi, không có món...) thì thả thêm khách; quá nhiều lần / quá lâu thì
        /// bỏ kịch bản để chơi bình thường. Trả true nếu đã kết thúc phần lõi.
        private bool CheckStall(int spawned)
        {
            if (!TutorialRules.IsScripted(step)) return false;
            sinceProgress += Time.unscaledDeltaTime;
            if (TutorialRules.ShouldFallback(respawns, sinceProgress))
            {
                Debug.LogWarning("[Tutorial] kẹt ở bước " + TutorialRules.StepId(step) + ", chuyển sang chơi bình thường");
                FinishCore("fallback");
                return true;
            }
            if (step > TutorialStep.Serve || game.Spawner == null) return false;
            if (HasViableCustomer()) { noCustomerTime = 0f; return false; }
            noCustomerTime += Time.unscaledDeltaTime;
            if (noCustomerTime >= TutorialRules.RespawnDelay
                && TutorialRules.NeedExtraSpawn(step, spawned, TutorialGate.AllowedSpawns, false))
            {
                respawns++;
                noCustomerTime = 0f;
                tutCustomer = null;
                Debug.Log("[Tutorial] khách đã bỏ về, thả khách mới (lần " + respawns + ")");
                game.Spawner.SpawnExtra();
                if (step != TutorialStep.TapCustomer) RegressTo(TutorialStep.TapCustomer);
            }
            return false;
        }

        private void UpdateCore()
        {
            BrewStation st = game.Station;
            switch (step)
            {
                case TutorialStep.TapCustomer:
                    if (tutCustomer == null || !tutCustomer.IsWaitingForDrink) tutCustomer = FindWaiting(null);
                    if (tutCustomer == null) { HidePrompt(); break; }
                    if (st.Selected != null && st.Selected == tutCustomer.CurrentOrder) { CompleteStep(step); break; }
                    if (!promptShown)
                    {
                        Present(new TutorialPrompt
                        {
                            text = TutorialStrings.TapCustomer,
                            target = CustomerTarget(tutCustomer),
                            altTarget = TutorialTarget.Ui(() => FindUi("OrderColumn/Card0")), // chạm thẻ đơn cũng nhận đơn
                            block = true,
                            hand = HandMode.Tap
                        });
                    }
                    break;

                case TutorialStep.Brew:
                    if (st.State == BrewStation.Phase.Ready) { CompleteStep(step); break; }
                    if (st.Selected == null && st.State == BrewStation.Phase.Idle) { RegressTo(TutorialStep.TapCustomer); break; }
                    UpdateBrewPrompt(st);
                    break;

                case TutorialStep.Serve:
                    if (tutCustomer == null || tutCustomer.WasServed || tutCustomer.State == CustomerState.Drink
                        || tutCustomer.State == CustomerState.Leaving || tutCustomer.State == CustomerState.Done)
                    { CompleteStep(step); break; }
                    bool delivering = game.Owner != null && game.Owner.IsDelivering;
                    if (st.State != BrewStation.Phase.Ready && !delivering) { RegressTo(TutorialStep.TapCustomer); break; }
                    if (delivering) HidePrompt();
                    else if (!promptShown)
                    {
                        Present(new TutorialPrompt
                        {
                            text = TutorialStrings.Serve,
                            target = CustomerTarget(tutCustomer),
                            block = true,
                            hand = HandMode.Tap
                        });
                    }
                    break;

                case TutorialStep.Money:
                    if (!moneyPaid)
                    {
                        if (tutCustomer != null && !tutCustomer.WasServed && tutCustomer.State < CustomerState.Leaving) break; // còn đang uống, chưa trả tiền
                        moneyPaid = true;
                        moneyPaidAt = stepClock;
                        Present(new TutorialPrompt
                        {
                            text = TutorialStrings.Money,
                            target = TutorialTarget.Ui(() => FindUi("CoinChip")),
                            hand = HandMode.Tap
                        });
                    }
                    else if (stepClock - moneyPaidAt > 2.5f)
                    {
                        CompleteStep(step);
                    }
                    break;

                case TutorialStep.CollectCup:
                    LeftoverCup cup = FindCollectableCup();
                    if (cup != null)
                    {
                        cupSeen = true;
                        if (!promptShown)
                        {
                            LeftoverCup target = cup;
                            Present(new TutorialPrompt
                            {
                                text = TutorialStrings.CollectCup,
                                target = TutorialTarget.At(() => target != null ? target.transform : null, new Vector3(0f, 0.15f, 0f), new Vector2(210f, 210f)),
                                block = true,
                                hand = HandMode.Tap
                            });
                        }
                    }
                    else if (cupSeen || stepClock > 6f)
                    {
                        CompleteStep(step); // ly đã thu (hoặc không có ly để thu)
                    }
                    break;

                case TutorialStep.PatienceRing:
                    if (!ringShown)
                    {
                        if (FindWaiting(tutCustomer) != null || stepClock > 40f)
                        {
                            ringShown = true;
                            ringShownAt = stepClock;
                            Present(new TutorialPrompt
                            {
                                text = TutorialStrings.Patience,
                                target = TutorialTarget.Ui(() => FindUi("OrderColumn/Card0")),
                                hand = HandMode.None
                            });
                        }
                    }
                    else if (stepClock - ringShownAt > 5f)
                    {
                        CompleteStep(step);
                    }
                    break;

                case TutorialStep.Summary:
                    if (summaryPending && !promptShown && stepClock - summaryAt > 1.2f)
                    {
                        Present(new TutorialPrompt
                        {
                            text = TutorialStrings.Summary,
                            target = TutorialTarget.Ui(() => FindUi("NextDay")),
                            avoid = () => FindUi("SummaryPanel/Card"), // thẻ không được che bảng tổng kết
                            hand = HandMode.Tap
                        });
                    }
                    break;
            }
        }

        private void UpdateBrewPrompt(BrewStation st)
        {
            RecipeDef r = st.Recipe;
            if (r == null || r.steps == null || r.steps.Length == 0) return;
            int idx = st.State == BrewStation.Phase.Brewing ? Mathf.Min(st.NextIndex, r.steps.Length - 1) : 0;
            if (promptShown && idx == lastBrewIndex) return;
            lastBrewIndex = idx;

            BrewStep bs = r.steps[idx];
            StepAction act = StepActions.For(bs);
            string name = Strings.StepName(bs);
            string text = act == StepAction.Hold ? string.Format(TutorialStrings.BrewHoldFmt, name)
                        : act == StepAction.Tap ? string.Format(TutorialStrings.BrewTapFmt, name)
                        : TutorialStrings.BrewOther;
            if (idx > 0 && act == StepAction.Tap) text = string.Format(TutorialStrings.BrewTapFmt, name);
            string key = "Step_" + bs;
            Present(new TutorialPrompt
            {
                text = text,
                target = TutorialTarget.Ui(() => FindUi(key)),
                block = true,
                hand = act == StepAction.Hold ? HandMode.Hold : HandMode.Tap
            });
        }

        // ------------------------------------------------------------------ gợi ý theo tình huống

        private bool CanShowTip()
        {
            if (game.Day.CurrentPhase != DayCycle.Phase.Open) return false;
            if (game.Tier != null && game.Tier.Transitioning) return false;
            if (game.Hud == null || game.Hud.StoryOpen) return false;
            if (overlay.ConfirmOpen) return false;
            if (game.Builder != null && game.Builder.IsOpen) return false;
            return true;
        }

        private void UpdateTips()
        {
            if (recapIndex >= 0) return;
            if (activeTip != null) { TickTip(); return; }
            if (Time.unscaledTime < nextTipAt || Time.unscaledTime < nextEval) return;
            nextEval = Time.unscaledTime + 0.4f;
            if (!CanShowTip()) return;

            // Đã tự mua ghế trước khi tới ngày 2 thì khỏi dạy mua ghế
            if (!seen.Has(TutorialTips.Stool) && game.Grid != null && game.Grid.Seats.Count > startSeats) seen.Add(TutorialTips.Stool);

            string id = TutorialRules.PickTip(BuildContext(), seen);
            if (id != null) StartTip(id);
        }

        private TipContext BuildContext()
        {
            var c = new TipContext();
            c.day = game.Day.Day;
            c.tier = game.Tier != null ? game.Tier.Current : 1;
            c.lostCustomer = lostPending;
            c.cupsEmpty = game.Stock != null && game.Stock.Count == 0;
            c.missionClaimable = game.Missions != null && game.Missions.HasClaimable;
            c.canUpgrade = game.Tier != null && game.Tier.CanUpgrade;

            if (game.recipes != null)
            {
                for (int i = 0; i < game.recipes.Length; i++)
                {
                    RecipeDef r = game.recipes[i];
                    if (r != null && r.id == "bac_xiu")
                    {
                        c.bacXiuNew = r.unlockDay > 0 && c.day >= r.unlockDay;
                        break;
                    }
                }
            }

            FurnitureDef stool = game.GetDef("Stool_Plastic_Red");
            if (stool != null && game.Grid != null)
                c.canAffordStool = !ShopRules.AtMax(game.Grid, stool) && game.Wallet.Xu >= ShopRules.Price(game.Grid, stool);

            if (CleaningRules.Enabled && game.Grid != null)
            {
                c.dirtyTable = FirstDirtyTable() != null;
                CleaningSystem cs = CleaningSystem.Instance;
                c.wantsMop = cs != null && cs.Model != null && cs.Model.WantsMop;
            }
            c.dirtyCupsCarried = game.Owner != null && game.Owner.DirtyCupsCarried > 0;
            c.canHireCleaner = game.Staff != null && !game.Staff.HasRole(StaffRole.Cleaner) && game.Staff.CanHire(StaffRole.Cleaner)
                               && game.Wallet.Xu >= GameBalance.StaffHireFee(StaffRole.Cleaner);
            c.canExperiment = game.Recipes != null && game.Recipes.CanExperiment && game.Station.State == BrewStation.Phase.Idle;
            if (c.tier >= 3 && game.Tier != null)
            {
                for (int i = 0; i < ZoneInfo.Buyable.Length; i++)
                {
                    ZoneId z = ZoneInfo.Buyable[i];
                    if (!game.Tier.IsZoneOpen(z) && game.Wallet.Xu >= ZoneInfo.Cost(z)) { c.canAffordZone = true; break; }
                }
            }
            return c;
        }

        private PlacedFurniture FirstDirtyTable()
        {
            var tables = game.Grid.Tables;
            for (int i = 0; i < tables.Count; i++)
                if (tables[i] != null && tables[i].Dirt != null && tables[i].Dirt.IsDirty) return tables[i];
            return null;
        }

        private void StartTip(string id)
        {
            activeTip = id;
            tipStart = Time.unscaledTime;
            seen.Add(id);
            Debug.Log("[Tutorial] tip " + id + " start");
            if (id == TutorialTips.Stool)
            {
                stoolStage = -1;
                stoolStartSeats = game.Grid.Seats.Count;
                TickStool();
                return;
            }
            overlay.Show(MakeTipPrompt(id));
        }

        private void EndTip(string how)
        {
            if (activeTip == null) return;
            Debug.Log("[Tutorial] tip " + activeTip + " " + how);
            if (activeTip == TutorialTips.Lost) lostPending = false;
            activeTip = null;
            nextTipAt = Time.unscaledTime + TipMinGap;
            overlay.Hide();
            game.SaveSnapshot();
        }

        private void AbortTip()
        {
            if (activeTip != null) EndTip("done");
            if (recapIndex >= 0) { recapIndex = -1; overlay.Hide(); }
        }

        private void TickTip()
        {
            float age = Time.unscaledTime - tipStart;
            if (activeTip == TutorialTips.Stool)
            {
                TickStool();
                return;
            }
            bool done = false;
            switch (activeTip)
            {
                case TutorialTips.NoCup: done = game.Stock.Count > 0; break;
                case TutorialTips.Mission: done = !game.Missions.HasClaimable; break;
                case TutorialTips.Upgrade1:
                case TutorialTips.Upgrade2: done = game.Tier.Transitioning || !game.Tier.CanUpgrade; break;
                case TutorialTips.T2Table: done = FirstDirtyTable() == null; break;
                case TutorialTips.T2Mop:
                    done = CleaningSystem.Instance == null || CleaningSystem.Instance.Model == null || !CleaningSystem.Instance.Model.WantsMop;
                    break;
            }
            if (done || age > TipTimeout || game.Day.CurrentPhase != DayCycle.Phase.Open) EndTip("done");
        }

        private void TickStool()
        {
            if (game.Grid.Seats.Count > stoolStartSeats) { EndTip("done"); return; }
            if (Time.unscaledTime - tipStart > StoolTimeout || game.Day.CurrentPhase != DayCycle.Phase.Open) { EndTip("timeout"); return; }

            BuildModeController b = game.Builder;
            int stage = b.IsPlacing ? 2 : (b.IsOpen ? 1 : 0);
            if (stage == stoolStage) return;
            stoolStage = stage;
            if (stage == 0)
            {
                overlay.Show(new TutorialPrompt { text = TutorialStrings.StoolShop, target = TutorialTarget.Ui(() => FindUi("ShopButton")), hand = HandMode.Tap });
            }
            else if (stage == 1)
            {
                overlay.Show(new TutorialPrompt
                {
                    text = TutorialStrings.StoolPick,
                    target = TutorialTarget.Ui(() => game.Hud != null ? game.Hud.ShopCardRect("Stool_Plastic_Red") : null),
                    hand = HandMode.Tap
                });
            }
            else
            {
                overlay.Show(new TutorialPrompt { text = TutorialStrings.StoolPlace, target = TutorialTarget.Ui(() => game.Builder.PlaceConfirmRect), hand = HandMode.Tap });
            }
        }

        private TutorialPrompt MakeTipPrompt(string id)
        {
            string text = "";
            TutorialTarget target = null;
            switch (id)
            {
                case TutorialTips.NoCup: text = TutorialStrings.NoCup; target = UiTarget("CupChip"); break;
                case TutorialTips.Lost: text = TutorialStrings.Lost; target = UiTarget("ShopButton"); break;
                case TutorialTips.Upgrade1: text = TutorialStrings.Upgrade1; target = TutorialTarget.Ui(() => FindUi("TierCta") ?? FindUi("TierCard")); break;
                case TutorialTips.Upgrade2: text = TutorialStrings.Upgrade2; target = TutorialTarget.Ui(() => FindUi("TierCta") ?? FindUi("TierCard")); break;
                case TutorialTips.Mission: text = TutorialStrings.Mission; target = UiTarget("MissionButton"); break;
                case TutorialTips.BacXiu: text = TutorialStrings.BacXiu; target = UiTarget("RecipeBookButton"); break;
                case TutorialTips.T2Table:
                    text = TutorialStrings.T2Table;
                    target = TutorialTarget.At(() => { PlacedFurniture t = FirstDirtyTable(); return t != null ? t.transform : null; },
                        Vector3.up * 0.3f, new Vector2(320f, 240f));
                    break;
                case TutorialTips.T2Mop: text = TutorialStrings.T2Mop; target = UiTarget("MopBtn"); break;
                case TutorialTips.T2Sink: text = TutorialStrings.T2Sink; break;
                case TutorialTips.T2Cleaner: text = TutorialStrings.T2Cleaner; target = UiTarget("StaffButton"); break;
                case TutorialTips.T2Experiment: text = TutorialStrings.T2Experiment; target = UiTarget("RecipeBookButton"); break;
                case TutorialTips.T3Machines: text = TutorialStrings.T3Machines; target = UiTarget("ShopButton"); break;
                case TutorialTips.T3Staff: text = TutorialStrings.T3Staff; target = UiTarget("StaffButton"); break;
                case TutorialTips.T3Zone: text = TutorialStrings.T3Zone; target = UiTarget("ZonesButton"); break;
            }
            return new TutorialPrompt
            {
                text = text,
                target = target,
                hand = target != null ? HandMode.Tap : HandMode.None,
                showNext = true,
                nextLabel = TutorialStrings.GotIt,
                onNext = () => EndTip("done")
            };
        }

        private TutorialTarget UiTarget(string path)
        {
            // Nút phụ nằm trong popup MENU (đang đóng thì không thấy): chỉ vào nút MENU
            bool inMenu = path == "StaffButton" || path == "ZonesButton" || path == "MissionButton" || path == "RegularsButton" || path == "GraphicsButton";
            return TutorialTarget.Ui(() => FindUi(path) ?? (inMenu ? FindUi("MenuButton") : null));
        }

        // ------------------------------------------------------------------ xem lại / bỏ qua

        /// Nút "HƯỚNG DẪN" trong Cài đặt: gợi ý tình huống hiện lại khi gặp, và chạy ngay bản nhắc lại thao tác cơ bản.
        public void Replay()
        {
            if (disabled || !IsCoreDone) return;
            seen.Clear();
            lostPending = false;
            if (activeTip != null) EndTip("done");
            if (game.Day.CurrentPhase != DayCycle.Phase.Open)
            {
                replayArmed = true; // đang ở màn tiêu đề / tổng kết: chạy bản nhắc lại khi ngày kế tiếp bắt đầu
                return;
            }
            recapIndex = 0;
            ShowRecap();
        }

        private void ShowRecap()
        {
            if (recapIndex < 0 || recapIndex >= Recap.Length)
            {
                recapIndex = -1;
                overlay.Hide();
                nextTipAt = Time.unscaledTime + TipMinGap;
                game.SaveSnapshot();
                return;
            }
            RecapItem it = Recap[recapIndex];
            overlay.Show(new TutorialPrompt
            {
                text = it.text,
                target = UiTarget(it.path),
                hand = HandMode.None,
                showNext = true,
                showSkip = false,
                onNext = () => { recapIndex++; ShowRecap(); }
            });
        }

        // ------------------------------------------------------------------ tìm UI theo tên

        /// Tìm RectTransform đang hiện trong cây HUD theo đường dẫn tên ("Tên" hoặc "Cha/Con"). Có đệm, quét lại tối đa ~3 lần/giây.
        private RectTransform FindUi(string path)
        {
            RectTransform cached;
            if (uiCache.TryGetValue(path, out cached) && cached != null && cached.gameObject.activeInHierarchy) return cached;
            float next;
            if (uiNextScan.TryGetValue(path, out next) && Time.unscaledTime < next) return null;
            uiNextScan[path] = Time.unscaledTime + 0.3f;
            if (game.Hud == null) return null;

            string[] parts = path.Split('/');
            RectTransform found = Search(game.Hud.transform, parts);
            uiCache[path] = found;
            return found;
        }

        private static RectTransform Search(Transform root, string[] parts)
        {
            string last = parts[parts.Length - 1];
            string parent = parts.Length > 1 ? parts[parts.Length - 2] : null;
            var stack = new Stack<Transform>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                Transform t = stack.Pop();
                for (int i = 0; i < t.childCount; i++)
                {
                    Transform ch = t.GetChild(i);
                    if (!ch.gameObject.activeInHierarchy) continue;
                    RectTransform rt = ch as RectTransform;
                    if (rt != null && ch.name == last && (parent == null || ch.parent.name == parent)) return rt;
                    stack.Push(ch);
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ bot

        /// Chỉ cho DebugAutoPilot (-tutorial): bấm TIẾP / Đã hiểu nếu đang hiện.
        public bool DebugAdvance()
        {
            return overlay != null && overlay.DebugPressNext();
        }
    }
}
