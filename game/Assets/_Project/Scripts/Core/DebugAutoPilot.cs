using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Cafe
{
    /// Công cụ test tự động, chỉ bật khi chạy bản build với tham số dòng lệnh:
    ///   -autoshot <thư mục>   chụp màn hình định kỳ + khi tổng kết ngày, rồi thoát
    ///   -autoplay             tự đóng vai người chơi: nhận order, pha đúng bước, phục vụ, thu ly
    ///   -timescale <x>        tua nhanh (mặc định 1)
    ///   -freshsave            xóa save trước khi chơi
    ///   -forceclean           bật luật dọn dẹp T2 (bàn bẩn, bồn rửa, CI) ngay trong cảnh T1; bot tự lau bàn / rửa ly / lau sàn
    ///   -cheatxu / -cheatrp   (chỉ bản Development) +30000 Xu / đặt RP = 400 lúc bắt đầu, đủ điều kiện thuê mặt bằng
    ///   -autoupgrade          bot thuê mặt bằng (cấp 2) ngay khi đủ điều kiện, rồi chơi tiếp; mặc định chơi 4 ngày
    ///   -days <n>             chơi n ngày rồi thoát (mặc định 1; 4 khi có -autoupgrade). Cuối ngày in "[AutoPilot] TIER=<n>"
    /// Không chạy trong bản phát hành thường vì không ai truyền các tham số này.
    public class DebugAutoPilot : MonoBehaviour
    {
        string shotDir;
        bool autoplay;
        bool autoshop;
        bool autorecipes;
        bool cheatXu, cheatRp, autoUpgrade, autoStaff, noBrewer, autoBuy, autoHidden, upgradeStaff;
        int targetDays = 1;
        int daysPlayed;
        float timeScale = 1f;
        float nextShot = 5f;
        int shotIndex;
        DaySummary summary;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Has(args, "-forceclean"))
            {
                CleaningRules.ForceEnabled = true;
                CleaningRules.TierProvider = () => 2;
            }
            string dir = Arg(args, "-autoshot");
            bool play = Has(args, "-autoplay");
            bool recipesTest = Has(args, "-autorecipes");
            if (recipesTest) RecipeBook.DebugIgnoreTier = true; // món ẩn Tier 2 được gọi/menu hoạt động ngay ở Tier 1
            bool tierTest = Has(args, "-cheatxu") || Has(args, "-cheatrp") || Has(args, "-autoupgrade");
            if (dir == null && !play && !recipesTest && !tierTest) return;

            if (Has(args, "-freshsave")) SaveSystem.Delete();
            string ts = Arg(args, "-timescale");
            if (ts != null && float.TryParse(ts, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float scale))
                scale = Mathf.Clamp(scale, 0.1f, 8f);
            else
                scale = 1f;

            var go = new GameObject("DebugAutoPilot");
            DontDestroyOnLoad(go);
            var p = go.AddComponent<DebugAutoPilot>();
            p.shotDir = dir;
            p.autoplay = play;
            p.autoshop = Has(args, "-autoshop");
            p.autorecipes = recipesTest;
            p.timeScale = scale;
            // Cheat chỉ trong bản Development / Editor
            p.cheatXu = Has(args, "-cheatxu") && Debug.isDebugBuild;
            p.cheatRp = Has(args, "-cheatrp") && Debug.isDebugBuild;
            p.autoUpgrade = Has(args, "-autoupgrade");
            p.autoStaff = Has(args, "-autostaff");
            p.noBrewer = Has(args, "-nobrewer");
            p.autoBuy = Has(args, "-autobuy");
            p.autoHidden = Has(args, "-autohidden");
            p.upgradeStaff = Has(args, "-upgradestaff");
            string days = Arg(args, "-days");
            int nd;
            if (days != null && int.TryParse(days, out nd)) p.targetDays = Mathf.Clamp(nd, 1, 60);
            else p.targetDays = p.autoUpgrade ? 4 : 1;
            if (dir != null) Directory.CreateDirectory(dir);
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Exception || type == LogType.Error)
                    Debug.Log("[AutoPilot] LỖI: " + msg + "\n" + stack);
            };
        }

        static bool Has(string[] args, string key) => Array.IndexOf(args, key) >= 0;

        static string Arg(string[] args, string key)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        void OnEnable() => GameEvents.DayEnded += OnDayEnded;
        void OnDisable() => GameEvents.DayEnded -= OnDayEnded;

        void Start()
        {
            Time.timeScale = timeScale; // đặt sau khi scene load để không bị reset
            if (autoplay) { StartCoroutine(PlayLoop()); StartCoroutine(StoryAdvanceLoop()); }
            if (autoshop) StartCoroutine(ShopTest());
            if (autorecipes) StartCoroutine(RecipesTest());
            if (cheatXu || cheatRp) StartCoroutine(CheatLoop());
            if (autoUpgrade) StartCoroutine(UpgradeLoop());
            if (autoBuy) StartCoroutine(BuyLoop());
            if (autoHidden) StartCoroutine(HiddenMenuLoop());
            StartCoroutine(StatsLoop());
        }

        void Update()
        {
            if (shotDir != null && Time.time >= nextShot)
            {
                nextShot += 20f;
                Shot("t" + Mathf.RoundToInt(Time.time).ToString("000"));
            }
        }

        void OnDayEnded(DaySummary s)
        {
            summary = s;
            Debug.Log($"[AutoPilot] KẾT THÚC NGÀY {s.day}: phục vụ {s.served}, mất {s.lost}, doanh thu {s.revenue}, " +
                      $"tip {s.tips}, nguyên liệu {s.ingredientCost}, phí {s.fee}, lãi {s.NetProfit}, " +
                      $"RP +{s.rpGained:0.0}, hài lòng TB {s.AverageSatisfaction:0.00}, ★{s.stars}");
            Debug.Log($"[AutoPilot] MẤT KHÁCH ngày {s.day}: hết chỗ {s.lostNoSeat}, hết kiên nhẫn {s.lostPatience}, quán bẩn {s.lostDirty}, khác {s.lost - s.lostNoSeat - s.lostPatience - s.lostDirty}");
            var tg = GameBootstrap.Instance;
            int tierNow = tg != null && tg.Tier != null ? tg.Tier.Current : 1;
            Debug.Log($"[AutoPilot] TIER={tierNow}");
            if (tg != null && tg.Staff != null && tg.Staff.Count > 0)
                Debug.Log($"[AutoPilot] STAFF count={tg.Staff.Count}, lương trả hôm nay={s.staffWages}, việc đã làm: {StaffWorkSummary(tg)}");
            if (tg != null && tg.Missions != null) StartCoroutine(MissionsNextFrame(tg)); // sau khi GameBootstrap ghi nhận nhiệm vụ cuối ngày
            if (tg != null && tg.Regulars != null)
                foreach (var d in tg.Regulars.Definitions)
                {
                    var pr = tg.Regulars.GetProgress(d.id);
                    if (pr != null) Debug.Log($"[AutoPilot] REGULAR {d.id}: bond={pr.Bond}, chương đã đọc={pr.ChaptersDone}, lần ghé cuối=ngày {pr.LastVisitDay}");
                }
            if (tierNow >= 2)
                Debug.Log($"[AutoPilot] THUÊ NHÀ ngày {s.day}: thuê {s.rent}, trả nợ {s.debtPaid}, nợ còn {s.debt}, " +
                          $"cọc {tg.Tier.Deposit}, kho {tg.Tier.InventoryTotal}, ghế {tg.Grid.Seats.Count}");
            if (CleaningRules.Enabled)
                Debug.Log($"[AutoPilot] CI cuối ngày {s.day}: TB={s.cleanliness:0.0} hiện tại={CleaningRules.CurrentCI:0.0}");
            if (autorecipes) LogRecipes();
            StartCoroutine(FinishAfterSummary());
        }

        void LogRecipes()
        {
            var g = GameBootstrap.Instance;
            if (g == null || g.Recipes == null) return;
            Debug.Log($"[AutoPilot] RECIPES known={g.Recipes.KnownCount} menu={g.Recipes.MenuCount}");
        }

        /// -autorecipes: ~t=20 s tự "khám phá" Cà phê muối và bật vào menu (bot -autoplay sẽ pha nó qua TapStep như món khác);
        /// rồi thử pha tự do ra Cà phê trứng (đi qua BrewStation.Experiment + RecipeBook.Discover).
        IEnumerator RecipesTest()
        {
            yield return new WaitForSeconds(20f);
            var g = GameBootstrap.Instance;
            while (g == null || g.Recipes == null) { yield return new WaitForSeconds(1f); g = GameBootstrap.Instance; }
            g.Recipes.Discover("ca_phe_muoi");
            g.Recipes.SetOnMenu("ca_phe_muoi", true);
            LogRecipes();

            // Thử pha tự do: chạy gọn trong một frame khi quầy rảnh để không chen vào vòng pha của bot
            for (int attempt = 0; attempt < 40; attempt++)
            {
                yield return new WaitForSeconds(3f);
                var st = g.Station;
                if (st == null || st.State != BrewStation.Phase.Idle || st.Selected != null || st.Experiment) continue;
                if (g.Owner != null && !g.Owner.IsAtCart) continue;
                RecipeDef egg = g.Recipes.Get("ca_phe_trung");
                if (egg == null || g.Recipes.IsKnown(egg)) break;
                if (!st.BeginExperiment()) continue;
                for (int i = 0; i < egg.steps.Length; i++) st.TapStep(egg.steps[i]); // khớp đúng -> tự chốt + khám phá
                if (st.Experiment && st.State == BrewStation.Phase.Brewing) st.FinishExperiment();
                for (int k = 0; k < 2 && st.Experiment; k++) st.CancelOrDiscard(); // thoát chế độ thử
                Debug.Log($"[AutoPilot] RECIPES experiment ca_phe_trung -> {(g.Recipes.IsKnown(egg) ? "đã khám phá" : "KHÔNG khớp")}");
                LogRecipes();
                break;
            }
        }

        IEnumerator FinishAfterSummary()
        {
            yield return new WaitForSecondsRealtime(1.5f);
            if (shotDir != null) Shot("summary_day" + summary.day);
            yield return new WaitForSecondsRealtime(0.5f);
            daysPlayed++;
            var g = GameBootstrap.Instance;
            if (daysPlayed < targetDays && g != null)
            {
                Debug.Log($"[AutoPilot] sang ngày kế ({daysPlayed}/{targetDays} đã chơi)");
                g.StartNextDay();   // như bấm "Ngày mới" (HUD tự ẩn bảng tổng kết khi ngày bắt đầu)
                yield break;
            }
            Application.Quit();
        }

        /// -cheatxu / -cheatrp: cộng Xu / nâng RP ngay khi game dựng xong.
        IEnumerator CheatLoop()
        {
            GameBootstrap g;
            do
            {
                yield return null;
                g = GameBootstrap.Instance;
            } while (g == null || g.Wallet == null || g.Reputation == null || g.Tier == null);

            if (cheatXu)
            {
                g.Wallet.Earn(30000);
                Debug.Log($"[AutoPilot] CHEAT +30000 Xu -> {g.Wallet.Xu}");
            }
            if (cheatRp && g.Reputation.RP < 400f)
            {
                g.Reputation.Add(400f - g.Reputation.RP);
                Debug.Log($"[AutoPilot] CHEAT RP -> {g.Reputation.RP:0}");
            }
        }

        /// -autoupgrade: khi đủ điều kiện (RP >= 300, Xu >= 18.000) và đang trong ngày, thuê mặt bằng ngay (bỏ qua hộp xác nhận).
        IEnumerator UpgradeLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);
                var g = GameBootstrap.Instance;
                if (g == null || g.Tier == null || g.Day == null) continue;
                if (g.Tier.Current >= 2) yield break;
                if (g.Creator != null && g.Creator.IsOpen) continue;
                if (g.Day.CurrentPhase != DayCycle.Phase.Open || !g.Tier.CanUpgradeToT2) continue;

                Debug.Log($"[AutoPilot] UPGRADE: thuê mặt bằng (Xu={g.Wallet.Xu}, RP={g.Reputation.RP:0}, ghế={g.Grid.Seats.Count})");
                if (!g.Tier.BeginUpgrade(true)) continue;
                float t0 = Time.realtimeSinceStartup;
                while (g.Tier.Transitioning && Time.realtimeSinceStartup - t0 < 30f) yield return null;
                Debug.Log($"[AutoPilot] TIER={g.Tier.Current} sau nâng cấp: ghế={g.Grid.Seats.Count}, đồ={g.Grid.Placed.Count}, " +
                          $"kho={g.Tier.InventoryTotal}, cọc={g.Tier.Deposit}, quầy={(g.Grid.BrewStationFurniture != null ? g.Grid.BrewStationFurniture.Def.id : "KHÔNG CÓ")}");
                if (autoStaff) HireStaff(g);
                yield break;
            }
        }

        /// -autostaff: thuê 1 dọn dẹp + 1 phục vụ + 1 pha chế ngay sau khi lên Cấp 2 (đi qua đúng StaffRoster.Hire).
        void HireStaff(GameBootstrap g)
        {
            bool a = g.Staff.Hire(StaffRole.Cleaner, 0, null);
            bool b = g.Staff.Hire(StaffRole.Server, 2, null);
            bool c = !noBrewer && g.Staff.Hire(StaffRole.Brewer, 1, null); // -nobrewer: đối chứng "trước khi có Pha chế"
            Debug.Log($"[AutoPilot] STAFF thuê dọn dẹp={a}, phục vụ={b}, pha chế={c}, tổng={g.Staff.Count}, Xu={g.Wallet.Xu}");
        }

        IEnumerator MissionsNextFrame(GameBootstrap g)
        {
            yield return null;
            LogAndClaimMissions(g);
        }

        /// Cuối ngày: in tiến độ nhiệm vụ rồi nhận hết (đi qua đúng MissionBook.Claim / ClaimChest như bấm nút).
        static void LogAndClaimMissions(GameBootstrap g)
        {
            MissionBook m = g.Missions;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < m.Items.Count; i++)
            {
                MissionState it = m.Items[i];
                sb.Append(it.id).Append(' ').Append(it.progress).Append('/').Append(it.target).Append(it.claimed ? " (đã nhận)" : "").Append("; ");
            }
            int xu0 = g.Wallet.Xu, gems0 = g.Gems.Gems;
            int claimed = 0;
            for (int i = 0; i < m.Items.Count; i++)
                if (m.Claim(i)) claimed++;
            bool chest = m.ClaimChest();
            Debug.Log($"[AutoPilot] MISSIONS key={m.DayKey}: {sb}→ nhận {claimed}, rương={chest}, +{g.Wallet.Xu - xu0} Xu, +{g.Gems.Gems - gems0} Hạt");
        }

        /// Số việc mỗi vai trò đã làm trong ngày (để chứng minh nhân viên thật sự làm việc).
        static string StaffWorkSummary(GameBootstrap g)
        {
            var sb = new System.Text.StringBuilder();
            foreach (StaffWorker w in g.Staff.Workers)
            {
                if (w == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(StaffRoles.ToId(w.Role)).Append('=').Append(w.JobsDoneToday);
            }
            return sb.ToString();
        }

        /// Truyện khách quen dừng game (timeScale 0): bot bấm TIẾP bằng thời gian thật.
        IEnumerator StoryAdvanceLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.6f);
                var g = GameBootstrap.Instance;
                if (g == null || g.Hud == null || !g.Hud.StoryOpen) continue;
                g.Hud.DebugAdvanceStory();
                Debug.Log("[AutoPilot] STORY bấm TIẾP");
            }
        }

        void Shot(string name)
        {
            string path = Path.Combine(shotDir, $"{shotIndex++:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[AutoPilot] Chụp " + path);
        }

        IEnumerator StatsLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(15f);
                var g = GameBootstrap.Instance;
                if (g == null || g.Wallet == null) continue;
                Debug.Log($"[AutoPilot] t={Time.time:0}s Xu={g.Wallet.Xu} RP={g.Reputation.RP:0.0} " +
                          $"orders={g.Orders.Count} cups={g.LeftoverCups.Count} khách={g.Spawner.ActiveCount} " +
                          $"quầy={g.Station.State}/{(g.Station.Recipe != null ? g.Station.Recipe.id : "-")} chọn={(g.Station.Selected != null)} " +
                          $"chủQuán: hàng={g.Owner.QueueCount} tạiQuầy={g.Owner.IsAtCart} giao={g.Owner.IsDelivering}");
                CleaningSystem cs = CleaningSystem.Instance;
                if (CleaningRules.Enabled && cs != null && cs.Model != null)
                    Debug.Log($"[AutoPilot] CI={cs.Model.CI:0.0} floor={cs.Model.FloorDirt:0.0} " +
                              $"dirtyTables={cs.Model.DirtyTables}/{cs.Model.TotalTables} old={cs.Model.OldTables} " +
                              $"sinkPile={cs.SinkPile} ownerDirtyCups={g.Owner.DirtyCupsCarried} cleanCups={g.Stock.Count}");
            }
        }

        /// Người chơi giả: tốc độ chạm như người thật (0.4 s/bước) để đo nhịp game đúng.
        /// Luồng mới (chủ quán đi bộ): tạo nhân vật mặc định -> thu ly (giao việc cho chủ quán) -> chọn đơn
        /// -> chờ chủ quán về quầy -> chạm các bước -> chạm khách (xếp việc giao ly) -> chờ giao xong.
        /// -autoshop: mua và đặt đồ qua luồng build mode thật (kiểm tra NavMesh rebake, đường đi, tiền, nhu cầu).
        IEnumerator ShopTest()
        {
            yield return new WaitForSeconds(12f);
            var g = GameBootstrap.Instance;
            if (g == null || g.Builder == null) yield break;
            g.Wallet.Earn(5000); // đủ tiền để test luật đặt đồ, không phải luật tiền
            (string id, Vector2Int cell)[] buys =
            {
                ("Stool_Plastic_Green", new Vector2Int(2, 3)),
                ("Plant_Pot_Small", new Vector2Int(0, 9)),
                ("Board_Menu_A", new Vector2Int(14, 1)),
                ("Board_Menu_A", new Vector2Int(15, 1)),   // phải bị từ chối: tối đa 1
                ("Stool_Plastic_Red", new Vector2Int(6, 7)), // phải bị từ chối: trùng xe đẩy
            };
            foreach (var (id, cell) in buys)
            {
                FurnitureDef def = g.GetDef(id);
                int xu = g.Wallet.Xu;
                bool ok = def != null && g.Builder.DebugPlace(def, cell);
                Debug.Log($"[AutoPilot] MUA {id} @ {cell}: {(ok ? "OK" : "từ chối")}, Xu {xu} -> {g.Wallet.Xu}, " +
                          $"ghế={g.Grid.Seats.Count}, khách hôm nay={g.Spawner.PlannedCount}");
                yield return new WaitForSeconds(2f);
            }
        }

        /// -autobuy: "người chơi trung bình" của economy §16 — từ cấp 2, hễ dư tiền là mua thêm một bộ bàn + 2 ghế
        /// qua đúng luồng build mode (Builder.DebugPlace), tới khi hết chỗ trống trong danh sách ô thử.
        IEnumerator BuyLoop()
        {
            Vector2Int[] tableSpots =
            {
                new Vector2Int(1, 8), new Vector2Int(9, 8), new Vector2Int(1, 11), new Vector2Int(9, 11), new Vector2Int(5, 11)
            };
            int next = 0;
            while (true)
            {
                yield return new WaitForSeconds(10f);
                var g = GameBootstrap.Instance;
                if (g == null || g.Builder == null || g.Tier == null || g.Tier.Current < 2) continue;
                if (g.Day.CurrentPhase != DayCycle.Phase.Open) continue;

                // Nâng cấp nhân viên khi dư tiền: dọn dẹp trước (cổ chai đo được ở cấp 2), rồi pha chế, rồi phục vụ
                if (upgradeStaff && g.Wallet.Xu >= 4500)
                {
                    StaffRole[] order = { StaffRole.Cleaner, StaffRole.Brewer, StaffRole.Server };
                    for (int r = 0; r < order.Length; r++)
                    {
                        string rid = StaffRoles.ToId(order[r]);
                        for (int m = 0; m < g.Staff.Members.Count; m++)
                        {
                            if (g.Staff.Members[m].role != rid) continue;
                            int upCost = GameBalance.StaffUpgradeCost(g.Staff.Members[m].level);
                            if (g.Wallet.Xu >= upCost + 3000 && g.Staff.Upgrade(m))
                            {
                                Debug.Log($"[AutoPilot] NÂNG CẤP nhân viên {rid} -> cấp {g.Staff.Members[m].level}, -{upCost} Xu");
                                goto upgraded;
                            }
                        }
                    }
                }
                upgraded:
                if (next >= tableSpots.Length) continue;

                FurnitureDef table = g.GetDef(TierConfig.IdTable) ?? g.GetDef(TierConfig.IdTableFallback);
                FurnitureDef chair = g.GetDef(TierConfig.IdChair) ?? g.GetDef(TierConfig.IdChairFallback);
                if (table == null || chair == null) continue;
                int cost = ShopRules.Price(g.Grid, table) + 2 * ShopRules.Price(g.Grid, chair);
                if (g.Wallet.Xu < cost + 3000) continue; // giữ quỹ dự phòng như economy §16 (reserve)

                Vector2Int c = tableSpots[next++];
                int seats0 = g.Grid.Seats.Count, xu0 = g.Wallet.Xu;
                bool okT = g.Builder.DebugPlace(table, c);
                bool okL = okT && g.Builder.DebugPlace(chair, new Vector2Int(c.x - 1, c.y));
                bool okR = okT && g.Builder.DebugPlace(chair, new Vector2Int(c.x + 2, c.y + 1));
                Debug.Log($"[AutoPilot] MUA bàn @ {c}: bàn={okT} ghế trái={okL} ghế phải={okR}, Xu {xu0} -> {g.Wallet.Xu}, " +
                          $"ghế {seats0} -> {g.Grid.Seats.Count}");
            }
        }

        /// -autohidden: người chơi biết khám phá món ẩn — sang ngày thứ 2 của cấp 2 mở Cà phê muối + cốt dừa
        /// (qua RecipeBook.Discover, cùng đường với thử pha thành công) và tắt món rẻ để menu 6 món ưu tiên giá cao.
        IEnumerator HiddenMenuLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(5f);
                var g = GameBootstrap.Instance;
                if (g == null || g.Recipes == null || g.Tier == null || g.Tier.Current < 2 || g.Day.Day < 2) continue;
                g.Recipes.SetOnMenu("tra_da", false);
                g.Recipes.SetOnMenu("tra_chanh", false);
                g.Recipes.Discover("ca_phe_muoi");
                g.Recipes.Discover("ca_phe_cot_dua");
                Debug.Log($"[AutoPilot] MENU ẩn: {g.Recipes.MenuCount} món, đã biết {g.Recipes.KnownCount}");
                yield break;
            }
        }

        IEnumerator PlayLoop()
        {
            var tap = new WaitForSeconds(0.4f);
            while (true)
            {
                yield return tap;
                var g = GameBootstrap.Instance;
                if (g == null || g.Station == null) continue;

                // Màn tạo nhân vật: điền mặc định và mở quán
                if (g.Creator != null && g.Creator.IsOpen)
                {
                    g.Creator.AutoComplete("Test", "Cà Phê Vỉa Hè");
                    continue;
                }
                if (g.Owner == null || g.Day.CurrentPhase == DayCycle.Phase.Idle) continue;
                if (g.Tier != null && g.Tier.Transitioning) continue; // đang chuyển cấp quán (màn đen)

                // Dọn dẹp (T2+ hoặc -forceclean): đi qua đúng các đường vào của người chơi (chạm bàn, nút Lau sàn)
                if (CleaningRules.Enabled && g.Grid != null)
                {
                    var tables = g.Grid.Tables;
                    int dirtyCount = 0;
                    for (int i = 0; i < tables.Count; i++)
                        if (tables[i] != null && tables[i].Dirt != null && tables[i].Dirt.IsDirty) dirtyCount++;
                    // Ưu tiên pha chế: chỉ dọn khi rảnh đơn, hoặc khi quá nửa số bàn đã bẩn
                    bool wantClean = g.Orders.Count == 0 || dirtyCount * 2 >= tables.Count;
                    // Đã thuê nhân viên Dọn dẹp thì người chơi giao việc dọn / lau sàn / rửa ly cho họ (chủ quán không cướp việc)
                    if (g.Staff != null && g.Staff.HasRole(StaffRole.Cleaner)) wantClean = false;
                    if (wantClean)
                    {
                        for (int i = 0; i < tables.Count; i++)
                        {
                            if (g.Owner.QueueCount >= OwnerController.MaxQueue) break;
                            TableDirt td = tables[i] != null ? tables[i].Dirt : null;
                            if (td == null || !td.IsDirty) continue;
                            if (g.Owner.CarriedCups + td.DirtyCups > OwnerController.MaxCarriedCups) continue;
                            td.OnTap(); // cùng đường vào với chạm thật: TableTapTarget -> TableDirt.OnTap -> Owner.QueueCleanTable
                            yield return tap;
                        }
                        CleaningSystem cs = CleaningSystem.Instance;
                        if (cs != null && cs.Model != null && cs.Model.WantsMop && g.Owner.QueueCount < OwnerController.MaxQueue)
                            g.Owner.QueueMopFloor(); // như bấm nút HUD "Lau sàn"
                        g.Owner.QueueWashCups();      // ly bẩn đang cầm / còn ở bồn
                    }
                }

                // Giao việc thu ly cho chủ quán (tối đa 3 việc, 4 ly)
                for (int i = g.LeftoverCups.Count - 1; i >= 0; i--)
                {
                    if (g.Owner.QueueCount >= OwnerController.MaxQueue) break;
                    var c = g.LeftoverCups[i];
                    if (c != null && c.IsCollectable) { c.OnTap(); yield return tap; }
                }

                // Chủ quán còn đang đi thu ly: đợi về quầy rồi mới pha
                if (!g.Owner.IsAtCart) continue;
                if (g.Station.State != BrewStation.Phase.Idle || g.Orders.Count == 0) continue;

                Order o = null; // đơn cũ nhất mà nhân viên Pha chế chưa nhận
                for (int k = 0; k < g.Orders.Items.Count && o == null; k++)
                    if (!g.Orders.Items[k].IsClaimed) o = g.Orders.Items[k];
                if (o == null || !g.Station.Select(o)) continue;
                float t0 = Time.time;
                while (!g.Owner.IsAtCart && Time.time - t0 < 30f) yield return tap;
                if (!g.Owner.IsAtCart || g.Station.Selected != o) continue;

                foreach (BrewStep step in o.recipe.steps)
                {
                    // Chủ quán tạm rời quầy một nhịp (TapStep báo OwnerAway): người thật sẽ chạm lại bước đó
                    BrewStation.TapResult res;
                    int tries = 0;
                    do
                    {
                        yield return tap;
                        res = g.Station.TapStep(step);
                        tries++;
                    }
                    while (res == BrewStation.TapResult.OwnerAway && tries < 40);
                    if (g.Station.State == BrewStation.Phase.Idle) break; // hỏng ly
                }
                yield return tap;
                // Kẹt giữa chừng (sai bước...): bấm Hủy như người chơi thật rồi pha lại
                if (g.Station.State == BrewStation.Phase.Brewing && !g.Station.Experiment) g.Station.CancelOrDiscard();
                if (g.Station.State == BrewStation.Phase.Ready && o.customer != null)
                {
                    // Có nhân viên Phục vụ thì họ tự mang ly đi; không thì chạm khách như người chơi thật (chủ quán đi giao)
                    if (g.Staff == null || !g.Staff.HasRole(StaffRole.Server)) o.customer.OnTap();
                    t0 = Time.time;
                    while (g.Station.State == BrewStation.Phase.Ready && Time.time - t0 < 45f) yield return tap;
                    if (g.Station.State == BrewStation.Phase.Ready) g.Station.CancelOrDiscard(); // kẹt: đổ ly, pha lại
                }
            }
        }
    }
}
