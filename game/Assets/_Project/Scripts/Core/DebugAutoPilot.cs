#if UNITY_EDITOR || DEVELOPMENT_BUILD
// Công cụ chạy thử tự động / đo hiệu năng: chỉ có trong Editor và bản Development, không vào bản release.
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
    ///   -autoupgrade3         sau cấp 2, bot mua nhà hàng (cấp 3) ngay khi đủ điều kiện
    ///   -cheatt3              (chỉ bản Development) tới cấp 2 thì cộng đủ Xu + RP để mua nhà hàng
    ///   -autodecor3           như -autodecor nhưng ở nhà hàng (cấp 3): mua và đặt bộ nội thất cấp 3 rồi chụp màn hình
    ///   -nomachines           bot không mua máy móc cấp 3 (đối chứng)
    ///   -t3xu <n>             (chỉ bản Development) cộng thêm n Xu ngay sau khi mua nhà hàng (thử máy móc / nội thất)
    ///   -staffprio <n>        ưu tiên né nhau của NavMeshAgent nhân viên (mặc định 50 = Unity; 90 làm đội nhường đường quá mức); -homerow <n> số người mỗi hàng chỗ đứng chờ (mặc định 9)
    ///   -bestmenu             từ cấp 2, bot chọn menu là các món GIÁ CAO nhất đang gọi được (như người chơi biết tối ưu); in "[AutoPilot] MENU"
    ///   -autozones            ở nhà hàng (cấp 3) bot mở lần lượt sân vườn -> lầu -> VIP khi dư tiền (giữ quỹ dự phòng) và mua bàn ghế trong khu
    ///   -shotzones            mở lần lượt 3 khu bằng đường mua thật và chụp từng khu + bảng Mở rộng (cần -autoshot, -t3xu để đủ Xu)
    ///   -gfxcycle             đổi mức đồ họa giữa chừng (Vừa -> Thấp -> Cao) và chụp từng mức, không lưu lựa chọn
    ///   -shotsettings         mở bảng Cài đặt đồ họa rồi chụp màn hình (cần -autoshot)
    ///   -gfx low|medium|high  ép mức đồ họa (bản Development); log "[Gfx]" cho biết mức đang dùng
    ///   -cleaners <n>         số nhân viên Dọn dẹp bot thuê (mặc định = tối đa của cấp)
    ///   -fullstaff            thuê đủ nhân viên của cấp ngay khi đủ tiền tuyển (không chờ đủ ghế / quỹ dự phòng)
    ///   -autoprep             cuối ngày vào pha Chuẩn bị (mua thử 1 món, mở Cửa hàng, chụp màn hình, kiểm tra đồng hồ đứng / không khách / save) rồi mới MỞ CỬA
    ///   -shotmissions         (cần -autoprep + -autoshot) ở pha Chuẩn bị ép bộ nhiệm vụ cấp 2 có "try_recipe" rồi chụp bảng nhiệm vụ
    ///   -tutorial             bot đi qua hướng dẫn người chơi mới (mặc định bot BỎ QUA hướng dẫn để số đo kinh tế không đổi); bấm TIẾP,
    ///                         làm gợi ý mua ghế; log "[Tutorial] step <id> start/done", cảnh báo nếu một bước kẹt > 90 s. Dùng với -freshsave -days 3
    ///   -days <n>             chơi n ngày rồi thoát (mặc định 1; 4 khi có -autoupgrade; tối đa 200). Cuối ngày in "[AutoPilot] TIER=<n>"
    /// Không chạy trong bản phát hành thường vì không ai truyền các tham số này.
    public class DebugAutoPilot : MonoBehaviour
    {
        string shotDir;
        bool autoplay;
        bool autoshop;
        bool autorecipes;
        bool cheatXu, cheatRp, autoUpgrade, autoUpgrade3, cheatT3, fullStaff, noMachines, autoZones, shotZones, bestMenu, gfxCycle, shotSettings, autoDecor3, autoStaff, noBrewer, autoBuy, autoHidden, upgradeStaff, perf, autoDecor, autoPrep, shotMissions;
        bool prepBought, missionsShot;
        int cleanerLimit = 99;
        int t3ExtraXu;
        int targetDays = 1;
        int daysPlayed;
        float timeScale = 1f;
        float nextShot = float.MaxValue; // đặt lại sau khi GameBootstrap dựng xong (thời gian boot không tính)
        bool showTitle;
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
            bool tierTest = Has(args, "-cheatxu") || Has(args, "-cheatrp") || Has(args, "-autoupgrade") || Has(args, "-autoupgrade3");
            if (dir == null && !play && !recipesTest && !tierTest) return;

            if (Has(args, "-tutorial")) TutorialDirector.BotDriven = true;
            else TutorialDirector.DisabledForAutomation = true;
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
            p.autoUpgrade = Has(args, "-autoupgrade") || Has(args, "-autoupgrade3");
            p.autoUpgrade3 = Has(args, "-autoupgrade3");
            p.cheatT3 = Has(args, "-cheatt3") && Debug.isDebugBuild;
            p.fullStaff = Has(args, "-fullstaff");
            p.noMachines = Has(args, "-nomachines");
            p.shotSettings = Has(args, "-shotsettings");
            p.autoZones = Has(args, "-autozones");
            p.bestMenu = Has(args, "-bestmenu");
            string sp = Arg(args, "-staffprio");
            int spn;
            if (sp != null && int.TryParse(sp, out spn)) StaffWorker.AvoidancePriority = Mathf.Clamp(spn, 0, 99);
            string hr = Arg(args, "-homerow");
            int hrn;
            if (hr != null && int.TryParse(hr, out hrn)) StaffWorker.HomeRowSize = Mathf.Clamp(hrn, 1, 20);
            p.shotZones = Has(args, "-shotzones");
            p.gfxCycle = Has(args, "-gfxcycle");
            p.autoDecor3 = Has(args, "-autodecor3");
            string tx = Arg(args, "-t3xu");
            int txn;
            if (tx != null && int.TryParse(tx, out txn) && Debug.isDebugBuild) p.t3ExtraXu = Mathf.Max(0, txn);
            string cl = Arg(args, "-cleaners");
            int cln;
            if (cl != null && int.TryParse(cl, out cln)) p.cleanerLimit = Mathf.Clamp(cln, 0, 9);
            p.autoStaff = Has(args, "-autostaff");
            p.noBrewer = Has(args, "-nobrewer");
            p.autoBuy = Has(args, "-autobuy");
            p.autoHidden = Has(args, "-autohidden");
            p.upgradeStaff = Has(args, "-upgradestaff");
            p.perf = Has(args, "-perf");
            p.autoPrep = Has(args, "-autoprep");
            p.shotMissions = Has(args, "-shotmissions");
            p.autoDecor = Has(args, "-autodecor") || Has(args, "-autodecor3");
            string days = Arg(args, "-days");
            int nd;
            if (days != null && int.TryParse(days, out nd)) p.targetDays = Mathf.Clamp(nd, 1, 200);
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
            showTitle = Has(Environment.GetCommandLineArgs(), "-title");
            StartCoroutine(BootThenRun());
        }

        /// Chờ GameBootstrap dựng xong (boot + nạp scene không tính vào các bộ đếm cố định của bot). Có -title: chụp màn tiêu đề rồi
        /// tự bấm MỞ QUÁN sau ~4 giây thật (autoshot đầu tiên rơi vào lúc màn tiêu đề còn mở).
        IEnumerator BootThenRun()
        {
            float t0 = Time.realtimeSinceStartup;
            while (!GameBootstrap.IsBuilt && Time.realtimeSinceStartup - t0 < 60f) yield return null;
            if (showTitle && GameBootstrap.IsBuilt)
            {
                nextShot = Time.time + 2f;
                yield return new WaitForSecondsRealtime(4f);
                GameBootstrap g = GameBootstrap.Instance;
                if (g != null && g.Title != null && g.Title.IsOpen)
                {
                    Debug.Log("[AutoPilot] TITLE bấm MỞ QUÁN");
                    g.Title.Dismiss();
                }
                yield return null;
            }
            nextShot = Time.time + 5f;
            RunLoops();
        }

        void RunLoops()
        {
            if (autoplay) { StartCoroutine(PlayLoop()); StartCoroutine(StoryAdvanceLoop()); }
            if (TutorialDirector.BotDriven) StartCoroutine(TutorialLoop());
            if (autoshop) StartCoroutine(ShopTest());
            if (autorecipes) StartCoroutine(RecipesTest());
            if (cheatXu || cheatRp) StartCoroutine(CheatLoop());
            if (autoUpgrade) StartCoroutine(UpgradeLoop());
            if (autoBuy) StartCoroutine(BuyLoop());
            if (autoHidden) StartCoroutine(HiddenMenuLoop());
            if (perf) DebugPerfProbe.Attach();
            if (autoDecor) StartCoroutine(DecorTest());
            if (shotDir != null) GameEvents.LevelUp += OnLevelUpShot;   // chụp hiệu ứng lên level (cần -autoshot)
            if (shotSettings) StartCoroutine(SettingsShot());
            if (shotZones) StartCoroutine(ZoneShots());
            if (bestMenu) StartCoroutine(BestMenuLoop());
            if (gfxCycle) StartCoroutine(GfxCycle());
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
            if (tierNow >= 3 && tg.Grid != null && tg.Grid.Zones != null) Debug.Log("[AutoPilot] KHU " + ZoneSummary(tg));
            if (autorecipes) LogRecipes();
            StartCoroutine(FinishAfterSummary());
        }

        /// Mỗi khu đang mở: số ghế, điểm nội thất F, CI. Dùng để kiểm tra kinh tế / dọn dẹp theo khu.
        static string ZoneSummary(GameBootstrap g)
        {
            var sb = new System.Text.StringBuilder();
            CleaningSystem cs = CleaningSystem.Instance;
            for (int z = 0; z < ZoneInfo.Count; z++)
            {
                var id = (ZoneId)z;
                if (!g.Grid.Zones.IsOpen(id)) continue;
                int seats = 0;
                for (int i = 0; i < g.Grid.Seats.Count; i++)
                    if (g.Grid.ZoneOf(g.Grid.Seats[i].Furniture) == id) seats++;
                sb.Append(id).Append("[ghế=").Append(seats).Append(" F=").Append(g.Grid.FurnitureScoreIn(id).ToString("0.00"));
                if (cs != null && cs.Model != null) sb.Append(" CI=").Append(cs.Model.CiOf(id).ToString("0"));
                sb.Append("] ");
            }
            sb.Append("nhu cầu x").Append(g.Tier.ZoneDemandMult.ToString("0.00"));
            return sb.ToString();
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
                if (autoPrep) yield return PrepStep(g);   // như bấm "Chuẩn bị quán" rồi "MỞ CỬA"
                g.StartNextDay();   // như bấm "Ngày mới" / "MỞ CỬA" (HUD tự ẩn bảng tổng kết khi ngày bắt đầu)
                Debug.Log($"[AutoPilot] MỞ CỬA: phase={g.Day.CurrentPhase} ngày={g.Day.Day} elapsed={g.Day.Elapsed:0.0}");
                yield break;
            }
            Application.Quit();
        }

        /// -autoprep: một lượt pha Chuẩn bị như người chơi thật. Kiểm tra: đồng hồ đứng, không có khách, save là "ngày kế" với Xu hiện tại.
        IEnumerator PrepStep(GameBootstrap g)
        {
            g.EnterPrep();
            yield return new WaitForSecondsRealtime(1f);
            int day = g.Day.Day;
            float elapsed = g.Day.Elapsed;
            if (shotDir != null) Shot("prep_day" + day);

            // Lần đầu: mua thử một món đồ rẻ ngay trong pha Chuẩn bị (qua đúng luồng build mode)
            if (!prepBought)
            {
                FurnitureDef def = g.GetDef(g.Tier.Current >= 2 ? "Plant_Monstera" : "Plant_Pot_Small");
                if (def != null && g.Wallet.Xu >= ShopRules.Price(g.Grid, def) + 250)
                {
                    int xu0 = g.Wallet.Xu;
                    bool ok = false;
                    for (int y = 0; y < g.Grid.Size.y && !ok; y++)
                        for (int x = 0; x < g.Grid.Size.x && !ok; x++)
                            ok = g.Builder.DebugPlace(def, new Vector2Int(x, y));
                    prepBought = ok;
                    Debug.Log($"[AutoPilot] PREP mua {def.id}: {(ok ? "đã đặt" : "không có chỗ")}, Xu {xu0} -> {g.Wallet.Xu}");
                }
            }

            g.Builder.Open();   // như bấm nút Cửa hàng
            yield return new WaitForSecondsRealtime(1f);
            if (shotDir != null) Shot("prep_shop_day" + day);
            g.Builder.Close();

            if (shotMissions && shotDir != null && !missionsShot && g.Hud != null && g.Missions != null)
            {
                missionsShot = true;
                // Thưởng dài nhất ("+Xu +RP +Hạt") chỉ có ở try_recipe (cấp 2+): ép bộ nhiệm vụ có nó rồi chụp bảng
                int tier = Mathf.Max(2, g.Tier.Current);
                var ctx = new MissionContext { RecipeKnown = id => g.Recipes != null && g.Recipes.IsKnown(id), HasHiddenKnown = true };
                for (int key = g.Missions.DayKey + 1; key < g.Missions.DayKey + 400; key++)
                {
                    var scratch = new MissionBook(x => { }, gm => { }, r => { });
                    scratch.EnsureToday(key, tier, ctx);
                    bool has = false;
                    for (int i = 0; i < scratch.Items.Count; i++) has |= scratch.Items[i].id == "try_recipe";
                    if (has) { g.Missions.EnsureToday(key, tier, ctx); break; }
                }
                g.Hud.DebugOpenMissions(true);
                yield return new WaitForSecondsRealtime(1f);
                Shot("prep_missions_day" + day);
                yield return new WaitForSecondsRealtime(0.5f);
                g.Hud.DebugOpenMissions(false);
            }

            yield return new WaitForSecondsRealtime(3f);   // đứng yên ~3 giây thực (nhiều phút in-game nếu -timescale cao)
            SaveData sd;
            bool saved = SaveSystem.TryLoad(out sd);
            bool clockStill = Mathf.Approximately(g.Day.Elapsed, elapsed);
            bool noGuests = g.Spawner.ActiveCount == 0;
            bool inPrep = g.Day.CurrentPhase == DayCycle.Phase.Prep;
            bool saveOk = saved && sd.day == day + 1 && sd.xu == g.Wallet.Xu;
            Debug.Log($"[AutoPilot] PREP ngày {day}: {(clockStill && noGuests && inPrep && saveOk ? "OK" : "LỖI")} " +
                      $"phase={g.Day.CurrentPhase} elapsed {elapsed:0.0}->{g.Day.Elapsed:0.0} khách={g.Spawner.ActiveCount} " +
                      $"save(ngày={(saved ? sd.day : -1)}, xu={(saved ? sd.xu : -1)}) ví={g.Wallet.Xu}");
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
        /// -autoupgrade3 (kèm -cheatt3 để có đủ Xu + RP): tiếp tục mua nhà hàng ở cấp 2.
        IEnumerator UpgradeLoop()
        {
            bool cheated = false;
            while (true)
            {
                yield return new WaitForSeconds(1f);
                var g = GameBootstrap.Instance;
                if (g == null || g.Tier == null || g.Day == null) continue;
                if (g.Tier.Current >= 3 || (g.Tier.Current >= 2 && !autoUpgrade3)) yield break;
                if (g.Creator != null && g.Creator.IsOpen) continue;
                if (g.Tier.Current == 2 && cheatT3 && !cheated)
                {
                    cheated = true;
                    // Chỉ đủ để mua + còn 20.000 Xu vốn như người chơi vừa mua xong
                    g.Wallet.Earn(Mathf.Max(0, g.Tier.NextTierXu + 20000 - g.Wallet.Xu));
                    if (g.Reputation.RP < g.Tier.NextTierRp) g.Reputation.Add(g.Tier.NextTierRp - g.Reputation.RP);
                    Debug.Log($"[AutoPilot] CHEAT cấp 3: Xu={g.Wallet.Xu}, RP={g.Reputation.RP:0}");
                }
                if (g.Day.CurrentPhase != DayCycle.Phase.Open || !g.Tier.CanUpgrade) continue;

                Debug.Log($"[AutoPilot] UPGRADE: lên cấp {g.Tier.NextTier} (Xu={g.Wallet.Xu}, RP={g.Reputation.RP:0}, ghế={g.Grid.Seats.Count}, cọc={g.Tier.Deposit})");
                if (!g.Tier.BeginUpgrade(true)) continue;
                float t0 = Time.realtimeSinceStartup;
                while (g.Tier.Transitioning && Time.realtimeSinceStartup - t0 < 30f) yield return null;
                Debug.Log($"[AutoPilot] TIER={g.Tier.Current} sau nâng cấp: ghế={g.Grid.Seats.Count}, đồ={g.Grid.Placed.Count}, " +
                          $"kho={g.Tier.InventoryTotal}, cọc={g.Tier.Deposit}, Xu={g.Wallet.Xu}, quầy={(g.Grid.BrewStationFurniture != null ? g.Grid.BrewStationFurniture.Def.id : "KHÔNG CÓ")}");
                if (g.Tier.Current >= 3 && t3ExtraXu > 0)
                {
                    g.Wallet.Earn(t3ExtraXu);
                    Debug.Log($"[AutoPilot] CHEAT +{t3ExtraXu} Xu sau khi mua nhà hàng -> {g.Wallet.Xu}");
                }
                if (autoStaff) HireStaff(g);
            }
        }

        /// -autostaff: thuê nhân viên theo thứ tự Dọn dẹp, Phục vụ, Pha chế (rồi người thứ hai...) tới giới hạn của cấp,
        /// chỉ khi còn dư tiền sau phí tuyển (đi qua đúng StaffRoster.Hire). Gọi lại mỗi chu kỳ mua để thuê dần.
        void HireStaff(GameBootstrap g)
        {
            // Vòng Dọn dẹp -> Phục vụ -> Pha chế lặp tới trần lớn nhất (cấp 3 đủ khu: 6 / 5 / 6); trần thật do CanHire quyết định
            var order = new System.Collections.Generic.List<StaffRole>();
            for (int round = 0; round < 6; round++)
            {
                order.Add(StaffRole.Cleaner);
                order.Add(StaffRole.Server);
                order.Add(StaffRole.Brewer);
            }
            var hired = new System.Text.StringBuilder();
            for (int i = 0; i < order.Count; i++)
            {
                StaffRole role = order[i];
                if (role == StaffRole.Brewer && noBrewer) continue; // -nobrewer: đối chứng "trước khi có Pha chế"
                if (role == StaffRole.Cleaner && g.Staff.CountOf(role) >= cleanerLimit) continue;
                int n = g.Staff.CountOf(role);
                // Người đầu tiên của mỗi vai trò thuê ngay; người thêm chỉ thuê khi còn quỹ dự phòng
                int reserve = fullStaff || (n == 0 && g.Tier.Current <= 2) ? 0 : 6000;
                if (!fullStaff && g.Tier.Current >= 3 && g.Grid.Seats.Count < 24) continue; // nhà hàng: lo đủ ghế trước, nhân viên thêm sau
                if (!g.Staff.CanHire(role) || g.Wallet.Xu < GameBalance.StaffHireFee(role) + reserve) continue;
                if (g.Staff.Hire(role, StaffRoles.DefaultPreset(role, n), null))
                    hired.Append(StaffRoles.ToId(role)).Append('#').Append(n + 1).Append(' ');
            }
            if (hired.Length > 0)
                Debug.Log($"[AutoPilot] STAFF thuê {hired}-> tổng={g.Staff.Count}, Xu={g.Wallet.Xu}");
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

        /// Trạng thái từng nhân viên (vai trò, việc đã làm, có đứng trên NavMesh không, đang đi đâu) để chẩn đoán khi nhân viên ít việc.
        static string StaffStates(GameBootstrap g)
        {
            var sb = new System.Text.StringBuilder();
            int unclaimed = 0;
            for (int i = 0; i < g.Orders.Items.Count; i++) if (!g.Orders.Items[i].IsClaimed) unclaimed++;
            sb.Append("đơnChưaNhận=").Append(unclaimed).Append(" | ");
            foreach (StaffWorker w in g.Staff.Workers)
            {
                if (w == null) continue;
                var a = w.Agent;
                sb.Append(StaffRoles.ToId(w.Role)).Append("[việc=").Append(w.JobsDoneToday)
                  .Append(a != null && a.isOnNavMesh ? " mesh" : " NGOÀI-MESH")
                  .Append(a != null && a.hasPath ? " đi" : " đứng")
                  .Append(" ").Append(w.transform.position.x.ToString("0.0")).Append(',').Append(w.transform.position.z.ToString("0.0"))
                  .Append(" {").Append(w.DebugState).Append("}] ");
            }
            return sb.ToString();
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

        /// -tutorial: bấm TIẾP / Đã hiểu khi hướng dẫn chờ; khi gợi ý mua ghế hiện thì đặt ghế đẩu qua build mode (DebugPlace).
        IEnumerator TutorialLoop()
        {
            bool stoolDone = false;
            while (true)
            {
                yield return new WaitForSecondsRealtime(0.6f);
                var g = GameBootstrap.Instance;
                if (g == null || g.Tutorial == null) continue;
                if (g.Tutorial.DebugAdvance()) Debug.Log("[AutoPilot] TUTORIAL bấm TIẾP");
                if (!stoolDone && g.Tutorial.ActiveTipId == TutorialTips.Stool)
                {
                    FurnitureDef def = g.GetDef("Stool_Plastic_Red");
                    bool ok = false;
                    if (def != null)
                        for (int y = 0; y < g.Grid.Size.y && !ok; y++)
                            for (int x = 0; x < g.Grid.Size.x && !ok; x++)
                                ok = g.Builder.DebugPlace(def, new Vector2Int(x, y));
                    stoolDone = true;
                    Debug.Log($"[AutoPilot] TUTORIAL mua ghế đẩu: {(ok ? "đã đặt" : "không có chỗ")}");
                }
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
                if (g.Staff != null && g.Staff.Workers.Count > 0) Debug.Log("[AutoPilot] NV: " + StaffStates(g));
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
            // Ô thử theo thứ tự; ô nào bị luật đặt đồ từ chối (chặn lối, trùng đồ) thì bỏ qua
            Vector2Int[] tableSpots =
            {
                new Vector2Int(1, 8), new Vector2Int(9, 8), new Vector2Int(1, 11), new Vector2Int(9, 11), new Vector2Int(5, 11),
                new Vector2Int(1, 1), new Vector2Int(9, 1), new Vector2Int(1, 14), new Vector2Int(9, 14)
            };
            int next = 0, decorNext = 0, tierSeen = 0, machineNext = 0;
            var triedT3 = new System.Collections.Generic.HashSet<Vector2Int>();
            while (true)
            {
                yield return new WaitForSeconds(10f);
                var g = GameBootstrap.Instance;
                if (g == null || g.Builder == null || g.Tier == null || g.Tier.Transitioning) continue;
                if (g.Day.CurrentPhase != DayCycle.Phase.Open) continue;
                if (g.Tier.Current < 2) { TryBuyStool(g); continue; } // vỉa hè: mua ghế đẩu khi nhu cầu vượt sức chứa
                if (g.Tier.Current != tierSeen) { tierSeen = g.Tier.Current; decorNext = 0; triedT3.Clear(); } // phòng mới: mua trang trí lại từ đầu
                if (autoStaff) HireStaff(g);                          // thuê dần khi có tiền

                // Nâng cấp nhân viên khi dư tiền: dọn dẹp trước (cổ chai đo được ở cấp 2), rồi pha chế, rồi phục vụ
                if (upgradeStaff && g.Wallet.Xu >= 4500 && (g.Tier.Current < 3 || g.Grid.Seats.Count >= 24))
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
                if (g.Tier.Current >= 3)
                {
                    // Đủ 24 chỗ rồi mới tính máy móc (không chặn việc mua bàn / trang trí khi chưa đủ tiền cho máy)
                    if (!noMachines && g.Grid.Seats.Count >= 24 && TryBuyMachine(g, ref machineNext)) continue;
                    if (autoZones && TryUnlockZone(g)) { triedT3.Clear(); continue; } // khu mới: có ô thử mới
                    if (!TryBuyTableSetTier3(g, triedT3)) TryBuyDecor(g, ref decorNext);
                    continue;
                }
                if (next >= tableSpots.Length)
                {
                    TryBuyDecor(g, ref decorNext); // đủ ghế rồi thì mua đồ trang trí cho F (điểm nội thất)
                    continue;
                }

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

        /// Ô thử cho bộ bàn ghế ở nhà hàng: các hàng của từng khu ĐÃ MỞ (sảnh chính trước, rồi sân vườn, lầu, VIP), mỗi hàng quét x từ
        /// trái sang phải (bộ bàn rộng 4 ô: ghế trái ở x-1, ghế phải ở x+2). Ô bị luật đặt đồ từ chối thì bỏ qua; hàng đã đầy tự trôi qua.
        static System.Collections.Generic.List<Vector2Int> TableSpotsTier3(GameBootstrap g)
        {
            var list = new System.Collections.Generic.List<Vector2Int>();
            ZoneMap zones = g.Grid.Zones;
            int[] hallRows = { 6, 10, 14, 18, 2 };
            int[] zoneRows = { 1, 5, 9, 13, 17, 21 };
            for (int z = 0; z < ZoneInfo.Count; z++)
            {
                if (zones != null && !zones.IsOpen((ZoneId)z)) continue;
                RectInt rc = zones != null ? zones.Rects[z] : new RectInt(0, 0, g.Grid.Size.x, g.Grid.Size.y);
                int[] rows = z == (int)ZoneId.Hall ? hallRows : zoneRows;
                for (int r = 0; r < rows.Length; r++)
                {
                    if (rows[r] + 2 > rc.height) continue;
                    for (int x = rc.xMin + 2; x <= rc.xMax - 3; x++) list.Add(new Vector2Int(x, rc.yMin + rows[r]));
                }
            }
            return list;
        }

        /// Kiểm tra NavMesh KHÔNG nối sảnh với khu qua tường (chỉ được nối qua lỗ mở): đo đường đi giữa hai điểm cách nhau ~1 m, một bên
        /// tường là sảnh, bên kia là khu, ở chỗ KHÔNG có lỗ mở. Đi qua lỗ mở hợp lệ dài ít nhất 2 x (khoảng cách tới mép lỗ mở) + khoảng cách thẳng;
        /// đường ngắn hơn 80% mức đó nghĩa là NavMesh rò xuyên tường.
        static void LogWallTest(GameBootstrap g, ZoneId z)
        {
            float x = z == ZoneId.Garden ? -1f : 1f;                              // hướng từ sảnh sang khu
            float zz = z == ZoneId.Terrace ? -5.5f : (z == ZoneId.Vip ? 5.5f : 4.5f);
            float edge = z == ZoneId.Garden ? 1.5f : (z == ZoneId.Terrace ? -4.5f : 4.5f); // mép gần nhất của lỗ mở theo trục z
            float toOpening = Mathf.Abs(zz - edge);
            Vector3 inHall = new Vector3(x * 4.55f, 0f, zz), inZone = new Vector3(x * 5.65f, 0f, zz);
            UnityEngine.AI.NavMeshHit a, b;
            bool okA = UnityEngine.AI.NavMesh.SamplePosition(inHall, out a, 0.3f, UnityEngine.AI.NavMesh.AllAreas);
            bool okB = UnityEngine.AI.NavMesh.SamplePosition(inZone, out b, 0.3f, UnityEngine.AI.NavMesh.AllAreas);
            if (!okA || !okB)
            {
                Debug.Log($"[AutoPilot] WALLTEST {z}: bỏ qua (điểm sảnh trên mesh={okA}, điểm khu trên mesh={okB}; có thể bị đồ chiếm)");
                return;
            }
            var path = new UnityEngine.AI.NavMeshPath();
            bool found = UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, UnityEngine.AI.NavMesh.AllAreas, path)
                         && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete;
            float len = 0f;
            if (found) for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            float direct = Vector3.Distance(a.position, b.position);
            float viaOpening = 2f * toOpening + direct;
            bool leak = found && len < 0.8f * viaOpening;
            Debug.Log($"[AutoPilot] WALLTEST {z}: đường đi sảnh -> khu ở chỗ KHÔNG có lỗ mở = {(found ? len.ToString("0.0") + " m" : "không có đường")} " +
                      $"(thẳng {direct:0.0} m, qua lỗ mở tối thiểu {viaOpening:0.0} m) -> {(leak ? "RÒ XUYÊN TƯỜNG!" : "ổn")}");
        }

        /// Mở khu kế tiếp (sân vườn -> lầu -> VIP) khi dư tiền sau quỹ dự phòng 25.000. true nếu vừa mở một khu.
        static bool TryUnlockZone(GameBootstrap g)
        {
            for (int i = 0; i < ZoneInfo.Buyable.Length; i++)
            {
                ZoneId z = ZoneInfo.Buyable[i];
                if (g.Tier.IsZoneOpen(z)) continue;
                if (!g.Tier.CanUnlockZone(z) || g.Wallet.Xu < ZoneInfo.Cost(z) + 25000) return false; // để dành cho khu này
                int xu0 = g.Wallet.Xu, seats0 = g.Grid.Seats.Count;
                bool ok = g.Tier.UnlockZone(z);
                Debug.Log($"[AutoPilot] MỞ KHU {z}: {(ok ? "OK" : "thất bại")}, Xu {xu0} -> {g.Wallet.Xu}, ghế {seats0} -> {g.Grid.Seats.Count}");
                if (ok) LogWallTest(g, z);
                return ok;
            }
            return false;
        }

        /// Cấp 3: mua thêm một bộ bàn + 2 ghế ở ô thử kế tiếp còn đặt được. false nếu hết ô hoặc chưa đủ tiền (để mua trang trí).
        static bool TryBuyTableSetTier3(GameBootstrap g, System.Collections.Generic.HashSet<Vector2Int> tried)
        {
            FurnitureDef table = g.GetDef(TierConfig.IdTable) ?? g.GetDef(TierConfig.IdTableFallback);
            FurnitureDef chair = g.GetDef(TierConfig.IdChair) ?? g.GetDef(TierConfig.IdChairFallback);
            if (table == null || chair == null) return false;
            int cost = ShopRules.Price(g.Grid, table) + 2 * ShopRules.Price(g.Grid, chair);
            if (g.Wallet.Xu < cost + 3000) return true; // đang để dành tiền cho bộ bàn kế tiếp
            var spots = TableSpotsTier3(g);
            int attempts = 0;
            for (int i = 0; i < spots.Count && attempts < 12; i++)
            {
                Vector2Int c = spots[i];
                if (!tried.Add(c)) continue;
                attempts++;
                int seats0 = g.Grid.Seats.Count, xu0 = g.Wallet.Xu;
                if (!g.Builder.DebugPlace(table, c)) continue;
                bool okL = g.Builder.DebugPlace(chair, new Vector2Int(c.x - 1, c.y));
                bool okR = g.Builder.DebugPlace(chair, new Vector2Int(c.x + 2, c.y + 1));
                Debug.Log($"[AutoPilot] MUA bàn T3 @ {c} ({g.Grid.ZoneAt(c)}): ghế trái={okL} phải={okR}, Xu {xu0} -> {g.Wallet.Xu}, ghế {seats0} -> {g.Grid.Seats.Count}");
                return true;
            }
            return attempts >= 12; // còn ô chưa thử thì thử tiếp lần sau; hết ô: chuyển sang mua trang trí
        }

        /// Vùng ô bot quét khi cần "bất cứ chỗ trống nào" (máy móc, trang trí): sảnh chính, để kết quả so sánh được giữa các lần chạy.
        static RectInt ScanRect(GameBootstrap g)
        {
            return g.Grid.Zones != null ? g.Grid.Zones.Hall : new RectInt(0, 0, g.Grid.Size.x, g.Grid.Size.y);
        }

        static readonly string[] MachineBuyOrder = { "Machine_Grinder", "Machine_ColdBrew", "Machine_Espresso" };

        /// Cấp 3: khi đã đủ chỗ ngồi (hết ô thử bàn), mua máy móc theo thứ tự xay -> cold brew -> espresso nếu còn dư tiền.
        /// true = vừa mua (hoặc bỏ qua máy không có chỗ đặt). false = hết máy để mua hoặc chưa đủ tiền cho máy kế tiếp.
        static bool TryBuyMachine(GameBootstrap g, ref int index)
        {
            if (index >= MachineBuyOrder.Length) return false;
            FurnitureDef def = g.GetDef(MachineBuyOrder[index]);
            if (def == null) { index++; return true; }
            int price = ShopRules.Price(g.Grid, def);
            if (g.Wallet.Xu < price + 5000) return false; // chưa đủ tiền: để bàn / trang trí mua tiếp
            int xu0 = g.Wallet.Xu;
            bool ok = false;
            RectInt scan = ScanRect(g);
            for (int y = scan.yMin; y < scan.yMax && !ok; y++)
                for (int x = scan.xMin; x + 2 <= scan.xMax && !ok; x++)
                    ok = g.Builder.DebugPlace(def, new Vector2Int(x, y));
            Debug.Log($"[AutoPilot] MUA máy {def.id}: {(ok ? "đã đặt" : "không có chỗ")}, Xu {xu0} -> {g.Wallet.Xu}, hệ số pha={g.BrewTimeFactor:0.00}");
            index++;
            return true;
        }

        /// Vỉa hè (cấp 1): mua ghế đẩu kế tiếp khi sức chứa (ghế × 4 lượt) thấp hơn nhu cầu + 6 (economy §16), còn quỹ dự phòng.
        static void TryBuyStool(GameBootstrap g)
        {
            int seats = g.Grid.Seats.Count;
            if (seats >= 12) return;
            int demand = GameBalance.DemandWithAttract(g.Reputation.Stars, g.Day.Day, ShopRules.AttractSum(g.Grid));
            if (GameBalance.Capacity(seats) >= demand + 6) return;
            FurnitureDef def = g.GetDef("Stool_Plastic_Red");
            if (def == null) return;
            int price = ShopRules.Price(g.Grid, def);
            if (g.Wallet.Xu < price + 250) return;
            int xu0 = g.Wallet.Xu;
            bool ok = false;
            for (int y = 0; y < g.Grid.Size.y && !ok; y++)
                for (int x = 0; x < g.Grid.Size.x && !ok; x++)
                    ok = g.Builder.DebugPlace(def, new Vector2Int(x, y));
            Debug.Log($"[AutoPilot] MUA ghế đẩu: {(ok ? "đã đặt" : "không có chỗ")}, Xu {xu0} -> {g.Wallet.Xu}, ghế {seats} -> {g.Grid.Seats.Count}, nhu cầu {demand}");
        }

        static readonly string[] DecorBuyOrder =
            { "Plant_Monstera", "Lamp_Floor", "Radio_Stand", "Lantern_Hoian", "Bookshelf_Low", "Lantern_Hoian", "Plant_Monstera" };

        /// Mua món trang trí kế tiếp trong DecorBuyOrder nếu còn dư tiền (giữ quỹ dự phòng) và tìm được ô đặt hợp lệ.
        static void TryBuyDecor(GameBootstrap g, ref int index)
        {
            if (index >= DecorBuyOrder.Length) return;
            FurnitureDef def = g.GetDef(DecorBuyOrder[index]);
            if (def == null) { index++; return; }
            int price = ShopRules.Price(g.Grid, def);
            if (g.Wallet.Xu < price + 3000) return;
            index++;
            int xu0 = g.Wallet.Xu;
            bool ok = false;
            RectInt scan = ScanRect(g);
            for (int y = scan.yMin; y < scan.yMax && !ok; y++)
                for (int x = scan.xMin; x < scan.xMax && !ok; x++)
                    ok = g.Builder.DebugPlace(def, new Vector2Int(x, y));
            Debug.Log($"[AutoPilot] MUA trang trí {def.id}: {(ok ? "đã đặt" : "không có chỗ")}, Xu {xu0} -> {g.Wallet.Xu}, " +
                      $"F={g.Grid.FurnitureScore:0.00} ({g.Grid.ComfortStyleTotal}/{g.Grid.FRef})");
        }

        /// -autodecor: sau khi lên cấp 2, mua và đặt thử bộ trang trí kiểu Việt qua đúng luồng build mode (Builder.DebugPlace),
        /// kiểm tra luật đặt đồ + NavMesh rebake, rồi chụp màn hình (cần -autoshot và chạy có đồ họa).
        IEnumerator DecorTest()
        {
            GameBootstrap g = null;
            int needTier = autoDecor3 ? 3 : 2;
            while (g == null || g.Tier == null || g.Tier.Current < needTier || g.Tier.Transitioning || g.Builder == null || g.Day.CurrentPhase != DayCycle.Phase.Open)
            {
                yield return new WaitForSeconds(2f);
                g = GameBootstrap.Instance;
            }
            g.Wallet.Earn(autoDecor3 ? 300000 : 20000);
            yield return new WaitForSeconds(1f);
            // (món, ô ưu tiên); không đặt được thì quét cả lưới
            (string id, Vector2Int cell)[] plan3 =
            {
                ("Piano_Upright", new Vector2Int(1, 23)), ("Aircon_Standing", new Vector2Int(16, 23)), ("Plant_Fig_Large", new Vector2Int(17, 20)),
                ("Easel_Art", new Vector2Int(0, 18)), ("Armchair_Velvet", new Vector2Int(16, 15)), ("Armchair_Velvet", new Vector2Int(16, 12)),
                ("Table_Marble_Round", new Vector2Int(16, 8)), ("Chair_Leather", new Vector2Int(15, 8)), ("Chair_Leather", new Vector2Int(18, 9)),
                ("Chandelier_Brass", new Vector2Int(9, 8)), ("Chandelier_Brass", new Vector2Int(4, 12)),
                ("Machine_Grinder", new Vector2Int(4, 23)), ("Machine_ColdBrew", new Vector2Int(14, 23)), ("Machine_Espresso", new Vector2Int(17, 22)),
            };
            (string id, Vector2Int cell)[] plan2 =
            {
                ("Table_Round_Tile", new Vector2Int(5, 11)), ("Chair_Rattan", new Vector2Int(4, 11)), ("Chair_Rattan", new Vector2Int(7, 12)),
                ("Plant_Monstera", new Vector2Int(0, 1)), ("Lamp_Floor", new Vector2Int(11, 1)), ("Bookshelf_Low", new Vector2Int(9, 14)),
                ("Radio_Stand", new Vector2Int(0, 6)), ("Lantern_Hoian", new Vector2Int(5, 9)), ("Lantern_Hoian", new Vector2Int(9, 6)),
                ("Jar_Tea_Big", new Vector2Int(11, 8)), ("Fan_Stand", new Vector2Int(0, 10)), ("Stool_Plastic_Yellow", new Vector2Int(2, 14)),
            };
            var plan = autoDecor3 ? plan3 : plan2;
            Vector2Int hallOrigin = g.Grid.Zones != null ? g.Grid.Zones.Hall.min : Vector2Int.zero; // toạ độ ô ở trên viết cho sảnh 20 x 24
            int placed = 0;
            foreach (var (id, cell0) in plan)
            {
                Vector2Int cell = cell0 + hallOrigin;
                FurnitureDef def = g.GetDef(id);
                if (def == null) { Debug.Log($"[AutoPilot] DECOR {id}: KHÔNG có FurnitureDef"); continue; }
                int xu0 = g.Wallet.Xu;
                bool ok = g.Builder.DebugPlace(def, cell);
                for (int y = 0; y < g.Grid.Size.y && !ok; y++)
                    for (int x = 0; x < g.Grid.Size.x && !ok; x++)
                        ok = g.Builder.DebugPlace(def, new Vector2Int(x, y));
                if (ok) placed++;
                Debug.Log($"[AutoPilot] DECOR {id}: {(ok ? "đã đặt" : "KHÔNG đặt được")}, Xu {xu0} -> {g.Wallet.Xu}, giá {def.price}, " +
                          $"ghế={g.Grid.Seats.Count}, F={g.Grid.FurnitureScore:0.00} (điểm {g.Grid.ComfortStyleTotal}/{g.Grid.FRef})");
                yield return null;
            }
            Debug.Log($"[AutoPilot] DECOR xong: {placed}/{plan.Length} món");
            yield return new WaitForSeconds(4f);
            if (shotDir != null) Shot("decor");
            if (shotDir != null && g.Hud != null)
            {
                g.Hud.DebugToggleStaffPanel();   // bảng nhân viên (3 / 8 hàng tùy cấp)
                yield return new WaitForSecondsRealtime(1.5f);
                Shot("staffpanel");
                g.Hud.DebugToggleStaffPanel();
            }
        }

        /// -bestmenu: mỗi 8 giây (từ cấp 2) đặt menu = các món đã biết giá cao nhất mà gọi được (món cần máy chỉ khi máy đã đặt),
        /// tối đa theo giới hạn menu của cấp. Đo kinh tế "người chơi biết chọn món": menu đại trà có món 30-100 Xu kéo giá trung bình xuống.
        IEnumerator BestMenuLoop()
        {
            string last = "";
            while (true)
            {
                yield return new WaitForSeconds(8f);
                var g = GameBootstrap.Instance;
                if (g == null || g.Recipes == null || g.Tier == null || g.Tier.Current < 2 || g.Tier.Transitioning) continue;
                var cands = new System.Collections.Generic.List<RecipeDef>();
                foreach (RecipeDef r in g.Recipes.Ordered)
                {
                    if (r == null || !g.Recipes.IsKnown(r) || r.unlockTier > g.Tier.Current) continue;
                    if (!string.IsNullOrEmpty(r.requiredMachine) && !g.Grid.HasMachine(r.requiredMachine)) continue;
                    cands.Add(r);
                }
                cands.Sort((a, b) => b.price.CompareTo(a.price));
                int limit = Mathf.Min(g.Recipes.MenuLimit, cands.Count);
                var want = new System.Collections.Generic.HashSet<string>();
                for (int i = 0; i < limit; i++) want.Add(cands[i].id);
                foreach (RecipeDef r in g.Recipes.Ordered)
                    if (r != null && g.Recipes.InMenu(r) && !want.Contains(r.id)) g.Recipes.SetOnMenu(r.id, false);
                foreach (string id in want) g.Recipes.SetOnMenu(id, true);
                int sum = 0, n = 0;
                var sb = new System.Text.StringBuilder();
                foreach (RecipeDef r in g.Recipes.Ordered)
                    if (r != null && g.Recipes.InMenu(r)) { sum += r.price; n++; sb.Append(r.id).Append('=').Append(r.price).Append(' '); }
                string now = sb.ToString();
                if (now != last)
                {
                    last = now;
                    Debug.Log($"[AutoPilot] MENU cấp {g.Tier.Current}: {n} món, giá TB {(n > 0 ? sum / n : 0)}: {now}");
                }
            }
        }

        /// -shotzones: ở nhà hàng, chụp bảng Mở rộng, rồi mở lần lượt 3 khu bằng đường mua thật (TierManager.UnlockZone) và chụp từng khu.
        IEnumerator ZoneShots()
        {
            GameBootstrap g = null;
            while (g == null || g.Tier == null || g.Tier.Current < 3 || g.Tier.Transitioning || g.Hud == null || g.Day.CurrentPhase != DayCycle.Phase.Open)
            {
                yield return new WaitForSecondsRealtime(1f);
                g = GameBootstrap.Instance;
            }
            yield return new WaitForSecondsRealtime(3f);
            if (shotDir != null) { Shot("zones_locked"); yield return null; } // chụp ở cuối khung: chờ 1 khung trước khi đổi camera
            g.CameraRig.FocusOn(new Vector3(8f, 0f, 0f), 15f, Vector3.back, 55f);
            yield return new WaitForSecondsRealtime(1.5f);
            if (shotDir != null) { Shot("zones_locked_right"); yield return null; }
            g.CameraRig.FocusOn(new Vector3(-8f, 0f, 0f), 15f, Vector3.back, 55f);
            yield return new WaitForSecondsRealtime(1.5f);
            if (shotDir != null) { Shot("zones_locked_left"); yield return null; }
            g.CameraRig.ClearFocus();

            g.Hud.DebugOpenZones(true);
            yield return new WaitForSecondsRealtime(1.2f);
            if (shotDir != null) { Shot("zones_panel"); yield return null; }
            g.Hud.DebugOpenZones(false);

            foreach (ZoneId z in ZoneInfo.Buyable)
            {
                if (g.Wallet.Xu < ZoneInfo.Cost(z)) g.Wallet.Earn(ZoneInfo.Cost(z) - g.Wallet.Xu + 1000); // đủ tiền để chụp (bản Development)
                int seats0 = g.Grid.Seats.Count, xu0 = g.Wallet.Xu;
                bool ok = g.Tier.UnlockZone(z);
                Debug.Log($"[AutoPilot] ZONESHOT mở {z}: {(ok ? "OK" : "thất bại")}, Xu {xu0} -> {g.Wallet.Xu}, ghế {seats0} -> {g.Grid.Seats.Count}");
                if (ok) LogWallTest(g, z);
                yield return new WaitForSecondsRealtime(2.5f);
                Rect w = RoomLayout.For(3).ZoneWorldRect(z);
                g.CameraRig.FocusOn(new Vector3(w.center.x, 0f, w.center.y), z == ZoneId.Garden ? 13f : 11f, Vector3.back, 52f);
                yield return new WaitForSecondsRealtime(1.5f);
                if (shotDir != null) { Shot("zone_" + z); yield return null; }
                g.CameraRig.ClearFocus();
                yield return new WaitForSecondsRealtime(1f);
            }
            g.Hud.DebugOpenZones(true);
            yield return new WaitForSecondsRealtime(1f);
            if (shotDir != null) { Shot("zones_panel_all"); yield return null; }
            g.Hud.DebugOpenZones(false);
            g.CameraRig.FocusOn(new Vector3(0f, 0f, 0f), 24f, Vector3.back, 58f);
            yield return new WaitForSecondsRealtime(1.5f);
            if (shotDir != null) { Shot("zones_all"); yield return null; }
            g.CameraRig.ClearFocus();
            // Đủ khu => trần nhân viên 6 / 5 / 6: thuê đầy rồi chụp bảng Nhân viên (đầu + cuối danh sách cuộn)
            g.Wallet.Earn(60000); // đủ phí thuê 17 người (bản Development)
            HireStaff(g);
            yield return new WaitForSecondsRealtime(1.5f);
            g.Hud.DebugToggleStaffPanel();
            yield return new WaitForSecondsRealtime(1f);
            if (shotDir != null) { Shot("staff_top"); yield return null; }
            g.Hud.DebugScrollStaff(0f);
            yield return new WaitForSecondsRealtime(0.6f);
            if (shotDir != null) { Shot("staff_bottom"); yield return null; }
            g.Hud.DebugToggleStaffPanel();
            Debug.Log($"[AutoPilot] STAFFCAP sau khi mở đủ khu: dọn dẹp {g.Staff.CountOf(StaffRole.Cleaner)}/{g.Staff.MaxFor(StaffRole.Cleaner)}, " +
                      $"phục vụ {g.Staff.CountOf(StaffRole.Server)}/{g.Staff.MaxFor(StaffRole.Server)}, pha chế {g.Staff.CountOf(StaffRole.Brewer)}/{g.Staff.MaxFor(StaffRole.Brewer)}");
            Debug.Log("[AutoPilot] ZONESHOT xong: " + ZoneSummary(g));
        }

        /// -gfxcycle: sau khi vào ngày, đổi mức đồ họa Vừa -> Thấp -> Cao ngay trong lúc chơi (mỗi mức 12 giây thực) và chụp từng mức.
        IEnumerator GfxCycle()
        {
            GameBootstrap g = null;
            while (g == null || g.Day == null || g.Day.CurrentPhase != DayCycle.Phase.Open)
            {
                yield return new WaitForSecondsRealtime(1f);
                g = GameBootstrap.Instance;
            }
            foreach (GraphicsLevel level in new[] { GraphicsLevel.Medium, GraphicsLevel.Low, GraphicsLevel.High })
            {
                yield return new WaitForSecondsRealtime(8f);
                GraphicsQuality.DebugApply(level);
                yield return new WaitForSecondsRealtime(4f);
                if (shotDir != null) Shot("gfx_" + level);
            }
        }

        void OnDestroy() { GameEvents.LevelUp -= OnLevelUpShot; }

        void OnLevelUpShot(int level) { StartCoroutine(LevelUpShot(level)); }

        IEnumerator LevelUpShot(int level)
        {
            yield return new WaitForSecondsRealtime(0.8f);   // để chữ nổi + pháo giấy bay xuống rồi mới chụp
            Shot("levelup_" + level);
        }

        /// -shotsettings: mở bảng Cài đặt đồ họa và chụp màn hình (cần -autoshot).
        IEnumerator SettingsShot()
        {
            GameBootstrap g = null;
            while (g == null || g.Hud == null || g.Day == null || g.Day.CurrentPhase == DayCycle.Phase.Idle)
            {
                yield return new WaitForSecondsRealtime(1f);
                g = GameBootstrap.Instance;
            }
            yield return new WaitForSecondsRealtime(3f);
            g.Hud.DebugOpenSettings(true);
            yield return new WaitForSecondsRealtime(1.5f);
            if (shotDir != null) Shot("settings");
            yield return new WaitForSecondsRealtime(1f);
            g.Hud.DebugOpenSettings(false);
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
#endif
