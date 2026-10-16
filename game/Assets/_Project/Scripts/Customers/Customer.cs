using UnityEngine;
using UnityEngine.AI;

namespace Cafe
{
    public enum CustomerState { FindSeat, WalkToSeat, Sitting, WaitOrder, Drink, Leaving, Done }

    /// Khách: FSM Đến -> Tìm chỗ -> Đi tới ghế -> Ngồi/Gọi món -> Chờ món -> Uống -> Trả tiền -> Rời đi.
    public class Customer : MonoBehaviour, ITappable
    {
        private GameBootstrap game;
        private NavMeshAgent agent;
        private CustomerVisual visual;
        private OrderBubble bubble;
        private Seat seat;
        private Order order;
        private RecipeDef orderedRecipe;
        private LeftoverCup cup;

        private Vector3 approachPoint;
        private Vector3 sitFrom;
        private Quaternion sitFromRot;
        private Quaternion sitFace = Quaternion.identity;

        private float stateTime;
        private float noSeatTimer;
        private float retryTimer;
        private float waited;        // giây thực từ lúc ngồi đến lúc nhận ly
        private float patienceUsed;  // đã tiêu hao (chậm hơn nếu ghế êm)
        private float drinkDuration;
        private int quality;
        private RegularDefinition regular;

        public CustomerState State { get; private set; }
        public Order CurrentOrder { get { return order; } }
        public Seat Seat { get { return seat; } }
        /// Khách còn ngồi chờ món (chủ quán mang ly tới được).
        public bool IsWaitingForDrink { get { return State == CustomerState.WaitOrder && order != null; } }
        public bool WasServed { get; private set; }
        /// Món đã gọi và chất lượng đã nhận (cho nhiệm vụ ngày).
        public RecipeDef ServedRecipe { get { return orderedRecipe; } }
        public int ServedQuality { get { return quality; } }
        public int LastPay { get; private set; }
        public int LastTip { get; private set; }
        public string RegularId { get { return regular != null ? regular.id : null; } }
        public string RegularName { get { return regular != null ? regular.displayName : null; } }

        /// Thời gian kiên nhẫn chờ món (giây): mặc định 60, nhân theo sự kiện của ngày (trời mưa ngồi lâu, giờ cao điểm sốt ruột).
        private float PatienceLimit
        {
            get
            {
                float limit = GameBalance.PatienceSeconds * (game != null ? game.TodayEvent.patience : 1f);
                return specialMult > 1f ? limit * SpecialOrders.PatienceMult : limit;   // đơn đặc biệt: chờ lâu hơn vì chỉ chủ quán pha
            }
        }

        private float specialMult = 1f;   // thưởng x2 của đơn đặc biệt; giữ lại vì order = null sau Serve mà Pay chạy sau đó

        /// 1 = vừa gọi món, 0 = hết kiên nhẫn.
        public float PatienceNormalized
        {
            get { return Mathf.Clamp01(1f - patienceUsed / PatienceLimit); }
        }

        private static readonly int[] ShirtSwatches = { 26, 28, 30, 58, 59, 60, 61 };

        public void Init(GameBootstrap g)
        {
            Init(g, null);
        }

        public void Init(GameBootstrap g, RegularDefinition regularDefinition)
        {
            game = g;
            regular = regularDefinition;

            Vector3 start = g.SpawnPosition;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(start, out hit, 3f, NavMesh.AllAreas)) start = hit.position;
            transform.position = start;

            visual = gameObject.AddComponent<CustomerVisual>();
            GameObject prefab = null;
            if (g.customerPrefabs != null && g.customerPrefabs.Length > 0)
            {
                string wanted = regular != null && regular.look != null ? regular.look.customerPrefab : null;
                if (!string.IsNullOrEmpty(wanted))
                    for (int i = 0; i < g.customerPrefabs.Length; i++)
                        if (g.customerPrefabs[i] != null && g.customerPrefabs[i].name.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        { prefab = g.customerPrefabs[i]; break; }
                if (prefab == null) prefab = g.customerPrefabs[Random.Range(0, g.customerPrefabs.Length)];
            }
            visual.Build(prefab, g.toonMaterial);

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.6f, 0f); // nhân vật chibi cao ~1.15 m
            col.radius = 0.3f;
            col.height = 1.2f;

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = NavMeshBaker.AgentRadius;
            agent.height = 1.2f;
            agent.speed = Random.Range(1.4f, 1.9f);
            agent.acceleration = 10f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.05f;
            agent.autoBraking = true;
            agent.avoidancePriority = Random.Range(30, 70);
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            if (g.Hud != null && g.Hud.BubbleLayer != null)
                bubble = OrderBubble.Create(g.Hud.BubbleLayer, transform);

            State = CustomerState.FindSeat;
            retryTimer = 0f;
            if (regular != null && g.Hud != null) g.Hud.ShowToast(regular.displayName + " ghé quán!");
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            switch (State)
            {
                case CustomerState.FindSeat: UpdateFindSeat(dt); break;
                case CustomerState.WalkToSeat: UpdateWalk(); break;
                case CustomerState.Sitting: UpdateSitting(); break;
                case CustomerState.WaitOrder: UpdateWait(dt); break;
                case CustomerState.Drink: UpdateDrink(dt); break;
                case CustomerState.Leaving: UpdateLeaving(); break;
            }

            bool sit = State == CustomerState.Sitting || State == CustomerState.WaitOrder || State == CustomerState.Drink;
            float speed = (agent != null && agent.enabled && agent.isOnNavMesh) ? agent.velocity.magnitude : 0f;
            visual.Tick(speed, sit, dt);
        }

        // ---- Tìm chỗ / đi tới ghế ----

        private void UpdateFindSeat(float dt)
        {
            noSeatTimer += dt;
            if (game.Day.CurrentPhase == DayCycle.Phase.Closing)
            {
                Abandon(false);
                return;
            }

            retryTimer -= dt;
            if (retryTimer <= 0f)
            {
                retryTimer = 0.4f;
                Seat s;
                bool reserved;
                using (PerfNote.Measure("chọn ghế")) reserved = game.Grid.TryReserveSeat(this, game.Day.InGameSeconds, out s);
                if (reserved)
                {
                    Vector3 p;
                    if (s.TryGetApproachPoint(transform.position, out p))
                    {
                        seat = s;
                        approachPoint = p;
                        agent.isStopped = false;
                        agent.SetDestination(p);
                        State = CustomerState.WalkToSeat;
                        stateTime = 0f;
                        return;
                    }
                    s.Occupant = null;
                    s.MarkUnreachable();
                }
            }

            if (noSeatTimer > GameBalance.NoSeatSeconds) Abandon(true, LostReason.NoSeat);
        }

        private void UpdateWalk()
        {
            if (agent.pathPending) return;
            if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                // Không tới được ghế này: nhả ghế, tìm ghế khác
                seat.MarkUnreachable();
                ReleaseSeat();
                seat = null;
                agent.ResetPath();
                State = CustomerState.FindSeat;
                stateTime = 0f;
                retryTimer = 0f;
                return;
            }
            if (agent.remainingDistance <= agent.stoppingDistance + 0.1f || stateTime > 30f)
                BeginSit();
        }

        private void BeginSit()
        {
            // T2+: quán bẩn (CI < 60) thì có khách quay đầu bỏ đi ngay khi tới ghế (+15% mỗi 10 CI dưới 60)
            if (CleaningRules.Enabled)
            {
                float p = CleaningRules.WalkAwayChanceAt(seat); // CI của khu mình ngồi (cấp 3 có nhiều khu)
                if (p > 0f && Random.value < p)
                {
                    UiFx.FloatText(transform.position + new Vector3(0f, 1.9f, 0f), Strings.DirtyWalkAway, HudBuilder.Red);
                    Abandon(true, LostReason.Dirty);
                    return;
                }
            }

            agent.ResetPath();
            agent.enabled = false;
            sitFrom = transform.position;
            sitFromRot = transform.rotation;

            // Ngồi quay mặt vào bàn gần nhất, nếu không thì theo hướng ghế
            Vector3 look = seat.transform.forward;
            PlacedFurniture table;
            if (game.Grid.FindNearestTable(seat.transform.position, 1.5f, out table, out _, (int)game.Grid.ZoneOf(seat.Furniture)))
                look = table.WorldBounds.center - seat.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude < 0.0001f) look = Vector3.forward;
            sitFace = Quaternion.LookRotation(look.normalized, Vector3.up);

            State = CustomerState.Sitting;
            stateTime = 0f;
        }

        private void UpdateSitting()
        {
            float t = Mathf.Clamp01(stateTime / 0.4f);
            float e = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(sitFrom, seat.SitPoint, e);
            transform.rotation = Quaternion.Slerp(sitFromRot, sitFace, e);
            if (t >= 1f) PlaceOrder();
        }

        // ---- Gọi món / chờ ----

        private void PlaceOrder()
        {
            RecipeDef recipe = null;
            if (regular != null && game.Recipes != null)
            {
                // Khách quen luôn gọi món ruột nếu quán biết và pha được, kể cả khi người chơi đã tắt món đó khỏi menu
                // (trước đây tắt món rẻ khỏi menu từ Mặt bằng làm Bà Sáu không bao giờ lên thân thiết)
                RecipeDef favorite = game.Recipes.Get(regular.favoriteRecipe);
                if (favorite != null && game.Recipes.IsKnown(favorite) && game.Recipes.MachineReady(favorite)) recipe = favorite;
            }
            if (recipe == null && regular == null) recipe = TutorialGate.TakeForcedRecipe(game); // hướng dẫn: 2 đơn đầu ép món
            if (recipe == null) recipe = game.PickRecipe(game.Day.Day);
            if (recipe == null)
            {
                Abandon(false);
                return;
            }
            orderedRecipe = recipe;
            int tier = game.Tier != null ? game.Tier.Current : 1;
            bool special = SpecialOrders.ShouldBeSpecial(tier, SpecialOrders.Count(game.Orders.Items), Random.value);
            specialMult = special ? SpecialOrders.RewardMult : 1f;
            order = game.Orders.Add(this, recipe, special);
            if (bubble != null) bubble.Show(regular != null, special);
            Bark("order", 0.12f);
            State = CustomerState.WaitOrder;
            stateTime = 0f;
            waited = 0f;
            patienceUsed = 0f;
        }

        private void UpdateWait(float dt)
        {
            if (TutorialGate.PatiencePaused) // hướng dẫn: đang ở bước có kịch bản, khách chờ không mất kiên nhẫn
            {
                if (bubble != null) bubble.SetPatience(PatienceNormalized);
                return;
            }
            waited += dt;
            // Ghế êm (comfort cao) làm thanh kiên nhẫn giảm chậm hơn
            patienceUsed += dt / (1f + 0.05f * seat.Def.comfort);
            if (bubble != null) bubble.SetPatience(PatienceNormalized);
            if (!barkedWaiting && PatienceNormalized < 0.4f) { barkedWaiting = true; Bark("waiting", 0.5f); }
            if (patienceUsed >= PatienceLimit) Abandon(true, LostReason.Patience);
        }

        private bool barkedWaiting;

        /// Câu thoại nền trên đầu khách (hiếm, có thời gian nghỉ chung; không chen vào thẻ hướng dẫn / lúc game dừng).
        private void Bark(string group, float chance)
        {
            if (game == null || game.Barks == null || Time.timeScale <= 0f) return;
            if (game.Tutorial != null && game.Tutorial.IsShowingCard) return;
            if (!game.Barks.Allow(chance, Time.unscaledTime)) return;
            string line = game.Barks.Pick(group, RegularName);
            if (line != null) UiFx.Bark(transform.position + Vector3.up * 1.5f, line, HudBuilder.Cream);
        }

        /// Được phục vụ ly với chất lượng 1..3 sao.
        public bool Serve(int servedQuality)
        {
            if (State != CustomerState.WaitOrder || order == null) return false;
            quality = Mathf.Clamp(servedQuality, 1, 3);
            game.Orders.Remove(order);
            order = null;
            if (bubble != null) bubble.Hide();
            cup = LeftoverCup.Spawn(game, seat);
            drinkDuration = Random.Range(GameBalance.DrinkMinSeconds, GameBalance.DrinkMaxSeconds);
            State = CustomerState.Drink;
            stateTime = 0f;
            visual.SetPose(CustomerPose.Happy);
            visual.SetPose(CustomerPose.Drink);
            return true;
        }

        private void UpdateDrink(float dt)
        {
            if (stateTime < drinkDuration) return;
            Pay();
            TableDirt dirt = (CleaningRules.Enabled && seat != null) ? seat.Table : null;
            if (dirt != null)
            {
                // T2+: ly ở lại trên bàn thành đồ bẩn của bàn (bàn chặn ghế tới khi được lau), không còn LeftoverCup
                Vector3 cupPos = cup != null ? cup.transform.position : game.Grid.GetCupPoint(seat);
                if (cup != null) cup.PickUp();
                cup = null;
                dirt.AddDish(cupPos);
            }
            else if (cup != null)
            {
                cup.SetCollectable(game.Day.InGameSeconds); // T1 / ghế không có bàn: ly để lại, chạm để thu
            }
            BeginLeaving();
        }

        private void Pay()
        {
            RecipeDef r = orderedRecipe;
            float qMult = GameBalance.QualityMult(quality);
            // Cấp 3: điểm nội thất F, CI và hệ số giá đều theo KHU mình ngồi (VIP trả 1,5x)
            float f = game.Grid.FurnitureScoreAt(seat);
            float zoneMult = seat != null ? ZoneInfo.PriceMult(game.Grid.ZoneOf(seat.Furniture)) : 1f;

            zoneMult *= specialMult;
            int pay = Mathf.RoundToInt(r.price * qMult * zoneMult);
            int tip = 0;
            float chance = GameBalance.TipChance(quality) + 0.2f * f;
            if (Random.value < chance)
                tip = Mathf.RoundToInt(r.price * qMult * zoneMult * GameBalance.TipSize(quality) * (1f + 0.5f * f));
            if (tip > 0) tip = Mathf.RoundToInt(tip * game.TodayEvent.tip); // sự kiện của ngày (cuối tuần, ngày sinh viên...)
            if (tip > 0 && CleaningRules.Enabled)
                tip = Mathf.RoundToInt(tip * CleaningRules.TipMultiplierAt(seat)); // khu bẩn (CI < 60): tip ×0,5

            // T1: S = 0.40·Q + 0.35·W + 0.10·C_ly + 0.15·F
            // T2+ (economy §4.1): S = 0.35·Q + 0.30·W + 0.20·C + 0.15·F với C = CI/100
            float q = (quality - 1) / 2f;
            float w = Mathf.Clamp01(1f - (waited - 20f) / 40f);
            float s;
            if (CleaningRules.Enabled)
            {
                float c = CleaningRules.CiAt(seat) / 100f;
                s = 0.35f * q + 0.30f * w + 0.20f * c + 0.15f * f;
            }
            else
            {
                float cly = game.LeftoverCleanliness();
                s = 0.40f * q + 0.35f * w + 0.10f * cly + 0.15f * f;
            }

            game.Wallet.Earn(pay + tip);
            float rp = game.Reputation.Add(GameBalance.RpPerCustomer(s));
            if (tip > 0) Bark("tip", 0.4f);
            else if (s >= 0.72f) Bark("served_happy", 0.3f);
            else if (s < 0.5f) Bark("served_meh", 0.5f);

            DaySummary sum = game.Day.Summary;
            if (sum != null)
            {
                sum.revenue += pay;
                sum.tips += tip;
                sum.served++;
                sum.rpGained += rp;
                sum.satisfactionSum += s;
            }

            LastPay = pay;
            LastTip = tip;
            WasServed = true;
            if (regular != null && game.Regulars != null)
                game.Regulars.RecordService(regular.id, r.id, quality, waited,
                    CleaningRules.Enabled ? CleaningRules.CiAt(seat) : game.LeftoverCleanliness() * 100f);
            GameEvents.RaiseCustomerServed(this);
        }

        // ---- Rời đi ----

        /// Bỏ đi (hết kiên nhẫn / không có ghế / quán bẩn). penalty = true: khách bực, trừ RP (WalkAwayRp; hết chỗ thì WalkAwayRpNoSeat).
        private enum LostReason { Other, NoSeat, Patience, Dirty }

        private void Abandon(bool penalty, LostReason reason = LostReason.Other)
        {
            if (State == CustomerState.Leaving || State == CustomerState.Done) return;
            if (order != null)
            {
                game.Orders.Remove(order);
                order = null;
            }
            if (bubble != null) bubble.Hide();

            DaySummary sum = game.Day.Summary;
            if (sum != null)
            {
                sum.lost++;
                if (reason == LostReason.NoSeat) sum.lostNoSeat++;
                else if (reason == LostReason.Patience) sum.lostPatience++;
                else if (reason == LostReason.Dirty) sum.lostDirty++;
            }
            if (reason == LostReason.Patience) Bark("leaving_angry", 0.7f);
            else if (reason == LostReason.Dirty) Bark("dirty_table", 0.7f);
            else if (reason == LostReason.NoSeat) Bark("leaving_angry", 0.3f);
            if (penalty)
            {
                visual.SetPose(CustomerPose.Angry);
                float delta = reason == LostReason.NoSeat ? GameBalance.WalkAwayRpNoSeat : GameBalance.WalkAwayRp;
                if (delta != 0f)
                {
                    float rp = game.Reputation.Add(delta);
                    if (sum != null) sum.rpGained += rp;
                }
            }
            BeginLeaving();
        }

        /// DayCycle gọi khi đã quá giờ đóng cửa mà khách còn chờ.
        public void ForceLeave()
        {
            if (State == CustomerState.FindSeat || State == CustomerState.WalkToSeat || State == CustomerState.WaitOrder)
                Abandon(false);
        }

        private void BeginLeaving()
        {
            if (!agent.enabled)
            {
                // Đứng dậy: về điểm đứng cạnh ghế trên NavMesh
                transform.position = approachPoint;
                agent.enabled = true;
                agent.Warp(approachPoint);
            }
            ReleaseSeat();
            visual.SetPose(CustomerPose.Walk);

            State = CustomerState.Leaving;
            stateTime = 0f;
            agent.isStopped = false;
            if (!agent.SetDestination(game.ExitPosition)) Finish();
        }

        private void UpdateLeaving()
        {
            if (stateTime > 45f) { Finish(); return; }
            if (agent.pathPending) return;
            if (agent.pathStatus == NavMeshPathStatus.PathInvalid || agent.remainingDistance <= 0.4f) Finish();
        }

        private void Finish()
        {
            if (State == CustomerState.Done) return;
            State = CustomerState.Done;
            GameEvents.RaiseCustomerLeft(this);
            Destroy(gameObject);
        }

        private void ReleaseSeat()
        {
            if (seat != null && seat.Occupant == this) seat.Occupant = null;
        }

        private void OnDestroy()
        {
            ReleaseSeat();
            if (order != null && game != null) game.Orders.Remove(order);
            if (bubble != null) Destroy(bubble.gameObject);
        }

        // ---- Chạm ----

        public void OnTap()
        {
            if (State != CustomerState.WaitOrder || order == null) return;
            if (order.IsClaimed)
            {
                GameEvents.RaiseToast(Strings.OrderTakenByBrewer);
                return;
            }

            BrewStation st = game.Station;
            if (st.State == BrewStation.Phase.Ready)
            {
                // Chủ quán cầm ly đi bộ ra phục vụ (OwnerController gọi st.TryServe khi tới nơi)
                if (game.Owner != null) game.Owner.QueueDeliver(this);
                else if (!st.TryServe(this)) GameEvents.RaiseToast(Strings.WrongCustomer);
            }
            else if (st.State == BrewStation.Phase.Idle)
            {
                st.Select(order); // chạm khách = chọn đơn của khách
            }
            else
            {
                GameEvents.RaiseToast(Strings.BrewingInProgress);
            }
        }
    }
}
