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
        bool cheatXu, cheatRp, autoUpgrade, autoStaff;
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
            var tg = GameBootstrap.Instance;
            int tierNow = tg != null && tg.Tier != null ? tg.Tier.Current : 1;
            Debug.Log($"[AutoPilot] TIER={tierNow}");
            if (tg != null && tg.Staff != null && tg.Staff.Count > 0)
                Debug.Log($"[AutoPilot] STAFF count={tg.Staff.Count}, lương trả hôm nay={s.staffWages}");
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

        /// -autostaff: thuê 1 dọn dẹp + 1 phục vụ ngay sau khi lên Cấp 2 (đi qua đúng StaffRoster.Hire).
        void HireStaff(GameBootstrap g)
        {
            bool a = g.Staff.Hire(StaffRole.Cleaner, 0, null);
            bool b = g.Staff.Hire(StaffRole.Server, 2, null);
            Debug.Log($"[AutoPilot] STAFF thuê dọn dẹp={a}, phục vụ={b}, tổng={g.Staff.Count}, Xu={g.Wallet.Xu}");
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
                          $"orders={g.Orders.Count} cups={g.LeftoverCups.Count} khách={g.Spawner.ActiveCount}");
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

                Order o = g.Orders.Items[0];
                if (!g.Station.Select(o)) continue;
                float t0 = Time.time;
                while (!g.Owner.IsAtCart && Time.time - t0 < 30f) yield return tap;
                if (!g.Owner.IsAtCart || g.Station.Selected != o) continue;

                foreach (BrewStep step in o.recipe.steps)
                {
                    yield return tap;
                    g.Station.TapStep(step);
                    if (g.Station.State == BrewStation.Phase.Idle) break; // hỏng ly
                }
                yield return tap;
                if (g.Station.State == BrewStation.Phase.Ready && o.customer != null)
                {
                    o.customer.OnTap(); // cùng đường vào với người chơi thật: xếp việc giao ly cho chủ quán
                    t0 = Time.time;
                    while (g.Station.State == BrewStation.Phase.Ready && Time.time - t0 < 45f) yield return tap;
                    if (g.Station.State == BrewStation.Phase.Ready) g.Station.CancelOrDiscard(); // kẹt: đổ ly, pha lại
                }
            }
        }
    }
}
