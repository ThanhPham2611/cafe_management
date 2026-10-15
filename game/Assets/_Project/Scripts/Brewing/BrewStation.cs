using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Logic pha chế: chọn đơn -> làm các bước đúng thứ tự -> ly sẵn sàng -> phục vụ khách.
    /// Bước "làm" đến từ BrewPanel qua TapStep (chạm / giữ đủ lâu / vuốt / xoay) hoặc trực tiếp từ autopilot.
    /// Chất lượng: không sai bước + nhanh -> 3 sao (món ẩn còn cần dụng cụ cấp >= 2, hạn x1,2); không sai nhưng chậm 2 sao;
    /// sai 1 bước 1 sao; sai >= 2 hỏng.
    /// Chế độ Experiment ("Thử pha tự do", Tier 2): nhận mọi chuỗi bước, 20 Xu/nguyên liệu, bấm Xong để so khớp
    /// với món ẩn (khớp -> khám phá; không khớp -> "món lỗi" bán 40% giá TB).
    public class BrewStation
    {
        public enum Phase { Idle, Brewing, Ready }
        // NoMoney thêm cuối để không đổi giá trị cũ.
        public enum TapResult { Ok, Wrong, Ruined, Done, NoOrder, NoCup, HoldingDrink, OwnerAway, NoMoney }

        private readonly GameBootstrap game;
        private readonly List<BrewStep> expSteps = new List<BrewStep>(RecipeBalance.MaxExperimentSteps);

        public Phase State { get; private set; }
        public Order Selected { get; private set; }
        public RecipeDef Recipe { get; private set; }
        public int NextIndex { get; private set; }
        public int Wrong { get; private set; }
        public int HeldQuality { get; private set; }
        public float StartTime { get; private set; }

        /// Đang ở chế độ thử pha tự do (State vẫn là Idle khi chưa có nguyên liệu nào, Brewing khi đã thêm).
        public bool Experiment { get; private set; }

        private string NoCupMessage()
        {
            int waiting = game.WaitingCupCount();
            return waiting > 0 ? string.Format(Strings.NoCupWaitingFmt, waiting) : Strings.NoCup;
        }
        public IReadOnlyList<BrewStep> ExperimentSteps { get { return expSteps; } }

        public event System.Action Changed;

        public float BrewElapsed
        {
            get { return State == Phase.Brewing ? Time.time - StartTime : 0f; }
        }

        /// Hạn (giây) để được "pha hoàn hảo" của công thức đang làm.
        public float PerfectSeconds
        {
            get
            {
                if (Recipe == null) return 0f;
                return game.Recipes != null ? game.Recipes.PerfectSeconds(Recipe) : Recipe.brewSeconds;
            }
        }

        /// Công thức đang làm có thể đạt 3 sao không (dụng cụ đủ cấp cho món ẩn).
        public bool ThreeStarPossible
        {
            get { return game.Recipes == null || game.Recipes.ToolAllowsThreeStars(Recipe); }
        }

        public BrewStation(GameBootstrap game)
        {
            this.game = game;
            GameEvents.OrderRemoved += OnOrderRemoved;
        }

        public void Dispose()
        {
            GameEvents.OrderRemoved -= OnOrderRemoved;
        }

        /// Chọn đơn để pha. Không được khi đang pha dở hoặc đang cầm ly.
        public bool Select(Order o)
        {
            if (State == Phase.Brewing)
            {
                GameEvents.RaiseToast(Strings.BrewingInProgress);
                return false;
            }
            if (State == Phase.Ready)
            {
                GameEvents.RaiseToast(Strings.HoldingDrink);
                return false;
            }
            if (o != null && o.IsClaimed)
            {
                GameEvents.RaiseToast(Strings.OrderTakenByBrewer);
                return false;
            }
            // Chọn đơn khi đang thử pha (chưa có nguyên liệu) = thoát chế độ thử
            if (Experiment) LeaveExperiment();
            Selected = o;
            Recipe = o != null ? o.recipe : null;
            // Chủ quán đang đi xa: tự về quầy trước khi pha
            if (o != null && game.Owner != null && !game.Owner.IsAtCart) game.Owner.RequestReturn();
            Raise();
            return true;
        }

        public TapResult TapStep(BrewStep step)
        {
            if (State == Phase.Ready)
            {
                GameEvents.RaiseToast(Strings.HoldingDrink);
                return TapResult.HoldingDrink;
            }
            if (game.Owner != null && !game.Owner.IsAtCart)
            {
                game.Owner.RequestReturn();
                GameEvents.RaiseToast(Strings.OwnerComing);
                return TapResult.OwnerAway;
            }
            if (Experiment) return TapExperimentStep(step);

            if (State == Phase.Idle)
            {
                if (Selected == null || Recipe == null || Recipe.steps == null || Recipe.steps.Length == 0)
                {
                    GameEvents.RaiseToast(Strings.NoOrderSelected);
                    return TapResult.NoOrder;
                }
                if (!game.Stock.TryTake())
                {
                    GameEvents.RaiseToast(NoCupMessage());
                    return TapResult.NoCup;
                }
                // Bắt đầu pha: trừ nguyên liệu ngay
                game.Wallet.Spend(Recipe.ingredientCost);
                if (game.Day.Summary != null) game.Day.Summary.ingredientCost += Recipe.ingredientCost;
                State = Phase.Brewing;
                NextIndex = 0;
                Wrong = 0;
                StartTime = Time.time;
            }

            if (step == Recipe.steps[NextIndex])
            {
                NextIndex++;
                if (NextIndex >= Recipe.steps.Length)
                {
                    Finish();
                    return TapResult.Done;
                }
                Raise();
                return TapResult.Ok;
            }

            return RegisterWrong();
        }

        /// Bước kiểu Giữ bị nhả tay sớm = bước sai (nếu ly đang pha dở). Chưa bắt đầu pha thì chỉ nhắc, không phạt.
        public TapResult FailStep(BrewStep step)
        {
            if (Experiment || State != Phase.Brewing || Recipe == null)
            {
                GameEvents.RaiseToast(RecipeStrings.HoldTooShort);
                return TapResult.Ok;
            }
            return RegisterWrong();
        }

        private TapResult RegisterWrong()
        {
            if (TutorialGate.NoPenalty) // hướng dẫn: sai bước chỉ nhắc, không tính lỗi
            {
                GameEvents.RaiseToast(TutorialStrings.WrongHint);
                return TapResult.Wrong;
            }
            Wrong++;
            if (Wrong >= 2)
            {
                Ruin();
                return TapResult.Ruined;
            }
            GameEvents.RaiseToast(Strings.Wrong);
            Raise();
            return TapResult.Wrong;
        }

        private void Finish()
        {
            float elapsed = Time.time - StartTime;
            if (Wrong == 0)
            {
                bool perfect = game.Recipes != null
                    ? game.Recipes.IsPerfect(Recipe, elapsed)
                    : elapsed <= Recipe.brewSeconds;
                HeldQuality = perfect ? 3 : 2;
            }
            else
            {
                HeldQuality = 1;
            }
            State = Phase.Ready;
            Selected = null;
            GameEvents.RaiseToast(Strings.BrewDone);
            Raise();
        }

        private void Ruin()
        {
            // Mất nguyên liệu, ly sạch được giữ lại
            game.Stock.Return();
            GameEvents.RaiseToast(Strings.Ruined);
            State = Phase.Idle;
            NextIndex = 0;
            Wrong = 0;
            expSteps.Clear();
            if (Selected == null) Recipe = null;
            Raise();
        }

        /// Hủy đang pha dở (mất nguyên liệu) hoặc đổ ly đã pha xong. Trong chế độ thử: hủy lần thử, bấm lại để thoát.
        public void CancelOrDiscard()
        {
            if (Experiment)
            {
                if (State == Phase.Brewing)
                {
                    game.Stock.Return();
                    expSteps.Clear();
                    State = Phase.Idle;
                    NextIndex = 0;
                }
                else
                {
                    Experiment = false;
                }
                Raise();
                return;
            }
            if (State == Phase.Brewing)
            {
                Ruin();
            }
            else if (State == Phase.Ready)
            {
                game.Stock.Return();
                State = Phase.Idle;
                Recipe = null;
                Selected = null;
                Raise();
            }
            else if (Selected != null)
            {
                Selected = null;
                Recipe = null;
                Raise();
            }
        }

        // ------------------------------------------------------------------ thử pha tự do

        /// Vào chế độ thử pha tự do (từ Sổ công thức). Chỉ khi rảnh tay; bỏ chọn đơn đang chọn.
        public bool BeginExperiment()
        {
            if (game.Recipes == null || !game.Recipes.CanExperiment)
            {
                GameEvents.RaiseToast(RecipeStrings.ExperimentLockedT2);
                return false;
            }
            if (State != Phase.Idle)
            {
                GameEvents.RaiseToast(RecipeStrings.NeedIdleToExperiment);
                return false;
            }
            Selected = null;
            Recipe = null;
            Experiment = true;
            expSteps.Clear();
            NextIndex = 0;
            Wrong = 0;
            if (game.Owner != null && !game.Owner.IsAtCart) game.Owner.RequestReturn();
            GameEvents.RaiseToast(RecipeStrings.ExperimentOn);
            Raise();
            return true;
        }

        private void LeaveExperiment()
        {
            Experiment = false;
            expSteps.Clear();
        }

        private TapResult TapExperimentStep(BrewStep step)
        {
            if (game.Wallet.Xu < RecipeBalance.ExperimentIngredientXu)
            {
                GameEvents.RaiseToast(Strings.NotEnoughXu);
                return TapResult.NoMoney;
            }
            if (State == Phase.Idle)
            {
                if (!game.Stock.TryTake())
                {
                    GameEvents.RaiseToast(NoCupMessage());
                    return TapResult.NoCup;
                }
                State = Phase.Brewing;
                StartTime = Time.time;
                expSteps.Clear();
                Wrong = 0;
            }

            game.Wallet.Spend(RecipeBalance.ExperimentIngredientXu);
            if (game.Day.Summary != null) game.Day.Summary.ingredientCost += RecipeBalance.ExperimentIngredientXu;
            expSteps.Add(step);
            NextIndex = expSteps.Count;

            // Tự chốt khi đủ số nguyên liệu tối đa, hoặc khớp đúng một món ẩn chưa biết và không còn món dài hơn cùng đầu chuỗi
            bool auto = expSteps.Count >= RecipeBalance.MaxExperimentSteps;
            if (!auto && game.Recipes != null)
            {
                RecipeDef m = game.Recipes.FindMatch(expSteps, true);
                auto = m != null && !game.Recipes.IsKnown(m) && !game.Recipes.HasLongerHiddenWithPrefix(expSteps);
            }
            if (auto)
            {
                FinishExperiment();
                return TapResult.Done;
            }
            Raise();
            return TapResult.Ok;
        }

        /// Nút "Xong": chốt chuỗi nguyên liệu đã thêm, so khớp với công thức ẩn. Trả true nếu đã chốt.
        public bool FinishExperiment()
        {
            if (!Experiment || State != Phase.Brewing || expSteps.Count == 0) return false;
            RecipeBook rb = game.Recipes;
            BrewStep[] seq = expSteps.ToArray();
            int spent = seq.Length * RecipeBalance.ExperimentIngredientXu;

            int gain;
            string message;
            RecipeDef hiddenMatch = rb != null ? rb.FindMatch(seq, true) : null;
            if (hiddenMatch != null && !rb.IsKnown(hiddenMatch))
            {
                // Đúng công thức ẩn: ăn mừng (UiFx + toast + vào menu do RecipeBook), bán thử ly đầu tiên đúng giá
                rb.TryDiscoverFromSequence(seq);
                gain = hiddenMatch.price;
                message = string.Format(RecipeStrings.FirstSaleFmt, gain);
            }
            else
            {
                // Món lỗi (hoặc món đã biết): 40% giá TB, không bao giờ lãi hơn tiền nguyên liệu (tránh vòng lặp kiếm tiền)
                RecipeDef known = rb != null ? rb.FindMatch(seq, false) : null;
                int avg = rb != null ? rb.AverageKnownPrice() : 80;
                gain = Mathf.Min(Mathf.RoundToInt(avg * RecipeBalance.FailedDrinkFraction), Mathf.RoundToInt(spent * 0.75f));
                message = (known != null && rb.IsKnown(known))
                    ? string.Format(RecipeStrings.KnownDrinkFmt, Strings.FullName(known))
                    : string.Format(RecipeStrings.FailedDrinkFmt, gain);
            }

            if (gain > 0)
            {
                game.Wallet.Earn(gain);
                if (game.Day.Summary != null) game.Day.Summary.revenue += gain;
            }
            game.Stock.Return(); // ly được rửa/thu lại ngay (quán chưa có bồn rửa ở bước này)
            expSteps.Clear();
            State = Phase.Idle;
            NextIndex = 0;
            Wrong = 0;
            GameEvents.RaiseToast(message);
            Raise();
            return true;
        }

        /// Khách bỏ đi khi chủ quán đang mang ly: đổ ly, nhưng ly sạch chỉ về kho khi chủ quán về quầy
        /// (chủ quán giữ ly trong tay). Trả true nếu có ly để đổ.
        public bool DiscardKeepCup()
        {
            if (State != Phase.Ready) return false;
            State = Phase.Idle;
            Recipe = null;
            Selected = null;
            HeldQuality = 0;
            Raise();
            return true;
        }

        /// Nhân viên Phục vụ cầm ly đã pha đi giao: quầy rảnh ngay để pha ly kế (không phải chờ ly tới tay khách).
        /// Trả chất lượng ly, 0 nếu quầy không có đúng ly đó.
        public int TakeReadyDrink(RecipeDef expected)
        {
            if (State != Phase.Ready || Recipe != expected) return 0;
            int quality = HeldQuality;
            State = Phase.Idle;
            Recipe = null;
            Selected = null;
            HeldQuality = 0;
            Raise();
            return quality;
        }

        /// Chạm khách: phục vụ nếu đúng món đang cầm.
        public bool TryServe(Customer c)
        {
            if (State != Phase.Ready || c == null) return false;
            Order o = c.CurrentOrder;
            if (o == null || o.recipe != Recipe || o.IsClaimed) return false; // đơn Pha chế đang làm thì không giao ly này
            if (!c.Serve(HeldQuality)) return false;
            State = Phase.Idle;
            Recipe = null;
            Selected = null;
            HeldQuality = 0;
            Raise();
            return true;
        }

        public void ResetAll()
        {
            if (State != Phase.Idle) game.Stock.Return();
            State = Phase.Idle;
            Selected = null;
            Recipe = null;
            NextIndex = 0;
            Wrong = 0;
            HeldQuality = 0;
            Experiment = false;
            expSteps.Clear();
            Raise();
        }

        private void OnOrderRemoved(Order o)
        {
            if (Selected == o)
            {
                Selected = null;
                if (State == Phase.Idle) Recipe = null; // đang pha dở thì giữ công thức để pha nốt
                Raise();
            }
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }
    }
}
