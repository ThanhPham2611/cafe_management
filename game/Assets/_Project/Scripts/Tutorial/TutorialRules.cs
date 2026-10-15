using System.Collections.Generic;

namespace Cafe
{
    /// Các bước phần lõi (ngày 1) của hướng dẫn. Giá trị = số lưu trong SaveData.tutorialStep.
    /// 0 = chưa bắt đầu; Done = đã xong hoặc đã bỏ qua.
    public static class TutorialStep
    {
        public const int NotStarted = 0;
        public const int Welcome = 1;
        public const int TapCustomer = 2;
        public const int Brew = 3;
        public const int Serve = 4;
        public const int Money = 5;
        public const int CollectCup = 6;
        public const int PatienceRing = 7;
        public const int Summary = 8;
        public const int Done = 9;
    }

    /// Mã các hướng dẫn theo tình huống (hiện một lần). Lưu trong SaveData.tutorialSeen.
    public static class TutorialTips
    {
        public const string NoCup = "nocup";
        public const string Lost = "lost";
        public const string Upgrade1 = "upgrade1";
        public const string Upgrade2 = "upgrade2";
        public const string Mission = "mission";
        public const string Stool = "stool";
        public const string BacXiu = "bacxiu";
        public const string T2Table = "t2_table";
        public const string T2Mop = "t2_mop";
        public const string T2Sink = "t2_sink";
        public const string T2Cleaner = "t2_cleaner";
        public const string T2Experiment = "t2_experiment";
        public const string T3Machines = "t3_machines";
        public const string T3Staff = "t3_staff";
        public const string T3Zone = "t3_zone";

        /// Cấp quán mà gợi ý này "thuộc về" (người đã ở cấp đó coi như biết rồi). Gợi ý lên cấp k thuộc cấp k+1 (đã lên cấp là biết).
        public static int TipTier(string id)
        {
            switch (id)
            {
                case Upgrade1: case T2Table: case T2Mop: case T2Sink: case T2Cleaner: case T2Experiment: return 2;
                case Upgrade2: case T3Machines: case T3Staff: case T3Zone: return 3;
            }
            return 1; // nocup, lost, mission, stool, bacxiu: cơ bản của cấp 1
        }

        /// Thứ tự ưu tiên khi nhiều tình huống cùng đủ điều kiện.
        public static readonly string[] Order =
        {
            NoCup, Lost, Upgrade1, Upgrade2, Mission, Stool, BacXiu,
            T2Table, T2Mop, T2Sink, T2Cleaner, T2Experiment,
            T3Machines, T3Staff, T3Zone
        };
    }

    /// Ảnh chụp tình huống hiện tại để chọn hướng dẫn (Director dựng từ game, ở đây là logic thuần).
    public struct TipContext
    {
        public int day;
        public int tier;
        public bool lostCustomer;       // vừa có khách bỏ về mà chưa nhắc
        public bool cupsEmpty;          // hết ly sạch
        public bool bacXiuNew;          // Bạc xỉu đã mở khóa (ngày mở món)
        public bool missionClaimable;
        public bool canUpgrade;         // đủ điều kiện lên cấp kế
        public bool canAffordStool;     // đủ Xu mua ghế đẩu và còn mua được
        public bool dirtyTable;         // có bàn bẩn (T2+)
        public bool wantsMop;           // sàn bẩn (T2+)
        public bool dirtyCupsCarried;   // chủ quán đang cầm ly bẩn (T2+)
        public bool canHireCleaner;
        public bool canExperiment;
        public bool canAffordZone;
    }

    /// Tập mã hướng dẫn đã xem (lưu/đọc dạng mảng chuỗi).
    public sealed class TutorialSeen
    {
        private readonly HashSet<string> set = new HashSet<string>();

        public int Count { get { return set.Count; } }
        public bool Has(string id) { return !string.IsNullOrEmpty(id) && set.Contains(id); }

        public bool Add(string id)
        {
            return !string.IsNullOrEmpty(id) && set.Add(id);
        }

        public void Clear() { set.Clear(); }

        public void Load(string[] ids)
        {
            set.Clear();
            if (ids == null) return;
            for (int i = 0; i < ids.Length; i++)
                if (!string.IsNullOrEmpty(ids[i])) set.Add(ids[i]);
        }

        public string[] ToArray()
        {
            var arr = new string[set.Count];
            set.CopyTo(arr);
            System.Array.Sort(arr, System.StringComparer.Ordinal);
            return arr;
        }

        /// Save cũ (trước khi có hướng dẫn) coi như đã quen: đánh dấu đã xem mọi gợi ý thuộc cấp <= cấp hiện tại.
        public void AddKnownForTier(int tier)
        {
            for (int i = 0; i < TutorialTips.Order.Length; i++)
                if (TutorialTips.TipTier(TutorialTips.Order[i]) <= tier) set.Add(TutorialTips.Order[i]);
        }

        /// Đánh dấu đã xem mọi hướng dẫn tình huống (khi người chơi chọn "Bỏ qua hướng dẫn").
        public void AddAllTips()
        {
            for (int i = 0; i < TutorialTips.Order.Length; i++) set.Add(TutorialTips.Order[i]);
        }
    }

    /// Logic thuần của hướng dẫn: bước nào, móc gameplay nào bật ở bước nào, hướng dẫn tình huống nào hiện.
    /// Không phụ thuộc Unity nên test EditMode được (TutorialRulesTests).
    public static class TutorialRules
    {
        /// Số khách được thả cùng lúc ở bước 7 (khách thứ 2 ép Cà phê sữa, từ khách 3 ngẫu nhiên).
        public const int BurstCustomers = 3;

        /// Món ép cho hai đơn đầu: ít bước nhất, rồi món nhiều bước hơn để thấy thứ tự khác nhau.
        public static readonly string[] ForcedRecipes = { "ca_phe_den", "ca_phe_sua" };

        public static bool IsDone(int step) { return step >= TutorialStep.Done; }

        /// Bước ban đầu khi nạp game. Save cũ (đã qua ngày 1 hoặc đã lên cấp) coi như xong phần lõi.
        /// Trả NotStarted = phần lõi còn chờ, bắt đầu khi ngày 1 mở cửa.
        public static int InitialStep(bool hasSave, int savedStep, int savedDay, int savedTier)
        {
            if (!hasSave) return TutorialStep.NotStarted;
            if (savedStep >= TutorialStep.Done) return TutorialStep.Done;
            if (savedDay > 1 || savedTier > 1) return TutorialStep.Done;
            return TutorialStep.NotStarted; // thoát giữa ngày 1: ngày 1 làm lại từ đầu nên hướng dẫn cũng vậy
        }

        /// Save cũ chưa từng đi qua hướng dẫn (tutorialStep == 0, trước khi có tutorial) nhưng vào thẳng phần "xong": người chơi kỳ cựu.
        /// Save có tutorialStep 1..8 là người mới đang làm dở (vd thoát ở bảng tổng kết ngày 1): vẫn cần các gợi ý.
        public static bool IsVeteranMigration(bool hasSave, int savedStep, int initialStep)
        {
            return hasSave && savedStep == TutorialStep.NotStarted && IsDone(initialStep);
        }

        // ------------------------------------------------------------------ chống kẹt ngày 1

        /// Số lần thả thêm khách tối đa trước khi bỏ hẳn phần kịch bản.
        public const int MaxRespawns = 3;
        /// Chờ bao lâu (giây) không có khách nào còn dùng được thì thả khách mới.
        public const float RespawnDelay = 1.5f;
        /// Quá lâu (giây) không qua được bước nào ở phần kịch bản thì bỏ phần kịch bản.
        public const float NoProgressSeconds = 150f;

        /// Phần kịch bản (ghế cần có để khách vào).
        public static bool CanRunScripted(int seatCount) { return seatCount > 0; }

        /// Bước 2-4 cần một khách còn sống nhưng không còn ai (đã bỏ về) và hạn mức sinh khách đã hết: thả thêm.
        public static bool NeedExtraSpawn(int step, int spawned, int allowed, bool viableCustomer)
        {
            return step >= TutorialStep.TapCustomer && step <= TutorialStep.Serve && !viableCustomer && spawned >= allowed;
        }

        /// Bỏ phần kịch bản để chơi bình thường: thả thêm quá nhiều lần, hoặc quá lâu không tiến triển.
        public static bool ShouldFallback(int respawns, float secondsWithoutProgress)
        {
            return respawns > MaxRespawns || secondsWithoutProgress > NoProgressSeconds;
        }

        /// Số khách "đã thả" dùng cho ClockHold ở bước 7: lịch hết khách (ít hơn BurstCustomers) thì coi như đã đủ để đồng hồ chạy.
        public static int EffectiveSpawned(int spawned, int planned)
        {
            return spawned >= planned ? System.Math.Max(spawned, BurstCustomers) : spawned;
        }

        public static int Next(int step)
        {
            return step >= TutorialStep.Done ? TutorialStep.Done : step + 1;
        }

        /// Bước có kịch bản: khách không mất kiên nhẫn, sai bước chỉ nhắc (không trừ).
        public static bool IsScripted(int step)
        {
            return step >= TutorialStep.TapCustomer && step <= TutorialStep.CollectCup;
        }

        public static bool PatiencePaused(int step) { return IsScripted(step); }
        public static bool NoPenalty(int step) { return IsScripted(step); }

        /// Số khách tối đa được sinh ra tính từ đầu ngày 1 (int.MaxValue = không giới hạn).
        public static int AllowedSpawns(int step)
        {
            if (step >= TutorialStep.Done) return int.MaxValue;
            if (step <= TutorialStep.Welcome) return 0;
            if (step <= TutorialStep.CollectCup) return 1;
            return int.MaxValue;
        }

        /// Đồng hồ ngày đứng yên: bước 0-6, và bước 7 cho tới khi đủ khách đã được thả.
        public static bool ClockHold(int step, int spawned)
        {
            if (step >= TutorialStep.Done) return false;
            if (step < TutorialStep.PatienceRing) return true;
            if (step == TutorialStep.PatienceRing) return spawned < BurstCustomers;
            return false;
        }

        /// Khi đồng hồ đứng, khách được phép sinh có tới giờ ngay không (khách thứ i, 0 = đầu tiên).
        public static bool SpawnDueWhileHeld(int step, int index)
        {
            return step < TutorialStep.Done && index < AllowedSpawns(step);
        }

        /// Món ép cho đơn thứ n (0 = đầu tiên); null = chọn ngẫu nhiên như thường.
        public static string ForcedRecipeFor(int orderIndex)
        {
            return orderIndex >= 0 && orderIndex < ForcedRecipes.Length ? ForcedRecipes[orderIndex] : null;
        }

        public static string StepId(int step)
        {
            switch (step)
            {
                case TutorialStep.Welcome: return "welcome";
                case TutorialStep.TapCustomer: return "tap_customer";
                case TutorialStep.Brew: return "brew";
                case TutorialStep.Serve: return "serve";
                case TutorialStep.Money: return "money";
                case TutorialStep.CollectCup: return "collect_cup";
                case TutorialStep.PatienceRing: return "patience";
                case TutorialStep.Summary: return "summary";
                case TutorialStep.Done: return "done";
            }
            return "none";
        }

        // ------------------------------------------------------------------ hướng dẫn theo tình huống

        /// Tình huống có đủ điều kiện hiện không (chưa xét đã xem).
        public static bool Eligible(string id, TipContext c)
        {
            switch (id)
            {
                case TutorialTips.NoCup: return c.cupsEmpty;
                case TutorialTips.Lost: return c.lostCustomer;
                case TutorialTips.Upgrade1: return c.tier == 1 && c.canUpgrade;
                case TutorialTips.Upgrade2: return c.tier == 2 && c.canUpgrade;
                case TutorialTips.Mission: return c.missionClaimable;
                case TutorialTips.Stool: return c.tier == 1 && c.day >= 2 && c.canAffordStool;
                case TutorialTips.BacXiu: return c.tier == 1 && c.bacXiuNew;
                case TutorialTips.T2Table: return c.tier >= 2 && c.dirtyTable;
                case TutorialTips.T2Mop: return c.tier >= 2 && c.wantsMop;
                case TutorialTips.T2Sink: return c.tier >= 2 && c.dirtyCupsCarried;
                case TutorialTips.T2Cleaner: return c.tier == 2 && c.canHireCleaner;
                case TutorialTips.T2Experiment: return c.tier >= 2 && c.canExperiment;
                case TutorialTips.T3Machines: return c.tier >= 3;
                case TutorialTips.T3Staff: return c.tier >= 3;
                case TutorialTips.T3Zone: return c.tier >= 3 && c.canAffordZone;
            }
            return false;
        }

        /// Hướng dẫn tình huống kế tiếp cần hiện (theo thứ tự ưu tiên, bỏ qua cái đã xem); null = không có.
        public static string PickTip(TipContext c, TutorialSeen seen)
        {
            for (int i = 0; i < TutorialTips.Order.Length; i++)
            {
                string id = TutorialTips.Order[i];
                if (seen != null && seen.Has(id)) continue;
                if (Eligible(id, c)) return id;
            }
            return null;
        }

        // ------------------------------------------------------------------ bố cục thẻ lời thoại

        public const int SlotTop = 0;
        public const int SlotLower = 1;
        public const int SlotMiddle = 2;

        /// Khoảng trống phía trên cạnh trên màn hình dành cho chip Xu / Sao / Ngày (đơn vị canvas).
        public const float TopInset = 140f;
        /// Đáy thẻ ở ô "thấp": ngay trên bàn pha khi mở rộng (đơn vị canvas, tính từ đáy).
        public const float LowerBottom = 430f;

        /// Khoảng [yMin, yMax] của thẻ (gốc tọa độ dưới-trái) ở ô slot.
        public static void SlotY(int slot, float canvasH, float cardH, out float yMin, out float yMax)
        {
            switch (slot)
            {
                case SlotTop:
                    yMax = canvasH - TopInset;
                    yMin = yMax - cardH;
                    break;
                case SlotLower:
                    yMin = LowerBottom;
                    yMax = yMin + cardH;
                    break;
                default:
                    yMin = (canvasH - cardH) * 0.5f;
                    yMax = yMin + cardH;
                    break;
            }
        }

        /// Chọn ô đặt thẻ lời thoại sao cho không đè lên vùng chỉ (hole, đơn vị canvas, gốc dưới-trái).
        /// Thẻ canh giữa theo chiều ngang nên chỉ cần so khoảng X của thẻ với hole. hasHole = false -> ô trên.
        public static int PickCardSlot(bool hasHole, float holeMinX, float holeMinY, float holeMaxX, float holeMaxY,
            float canvasW, float canvasH, float cardW, float cardH, float margin)
        {
            if (!hasHole) return SlotTop;
            return PickCardSlot(new[] { holeMinX, holeMinY, holeMaxX, holeMaxY }, 1, canvasW, canvasH, cardW, cardH, margin);
        }

        /// Như trên nhưng tránh nhiều vùng cùng lúc (vùng chỉ, thẻ đơn hàng, bảng đang được giải thích...).
        /// rects = dãy phẳng [minX, minY, maxX, maxY] x count. Không ô nào trống -> ô trên.
        public static int PickCardSlot(float[] rects, int count, float canvasW, float canvasH, float cardW, float cardH, float margin)
        {
            if (rects == null || count <= 0) return SlotTop;
            float cx0 = (canvasW - cardW) * 0.5f;
            float cx1 = cx0 + cardW;
            int[] order = { SlotTop, SlotLower, SlotMiddle };
            for (int i = 0; i < order.Length; i++)
            {
                float y0, y1;
                SlotY(order[i], canvasH, cardH, out y0, out y1);
                bool clear = true;
                for (int r = 0; r < count && clear; r++)
                {
                    float minX = rects[r * 4], minY = rects[r * 4 + 1], maxX = rects[r * 4 + 2], maxY = rects[r * 4 + 3];
                    bool xOverlap = maxX + margin > cx0 && minX - margin < cx1;
                    bool yOverlap = maxY + margin > y0 && minY - margin < y1;
                    if (xOverlap && yOverlap) clear = false;
                }
                if (clear) return order[i];
            }
            return SlotTop;
        }

        /// Thẻ gọn đặt cạnh một bảng lớn (bảng tổng kết) thay vì đè lên nó: trả tâm X (canvas) ở dải trống bên trái nếu đủ rộng,
        /// không thì bên phải; false = màn hình quá hẹp, không bên nào đủ chỗ.
        public static bool PickSideCenterX(float avoidMinX, float avoidMaxX, float canvasW, float cardW, float margin, out float centerX)
        {
            float need = cardW + margin * 2f;
            if (avoidMinX >= need) { centerX = avoidMinX * 0.5f; return true; }
            float rightFree = canvasW - avoidMaxX;
            if (rightFree >= need) { centerX = avoidMaxX + rightFree * 0.5f; return true; }
            centerX = canvasW * 0.5f;
            return false;
        }
    }
}
