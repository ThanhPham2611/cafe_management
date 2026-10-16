using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Cafe
{
    /// Chủ quán = người chơi (GDD §2.1). Đứng ở xe đẩy; pha chế chỉ được khi đang ở xe đẩy.
    /// Hàng đợi việc: ReturnToCart / Deliver (mang ly ra cho khách) / CollectCup (thu ly để lại).
    /// T2+ (CleaningRules.Enabled): CleanTable (lau bàn bẩn) / WashCups (rửa ly ở bồn) / MopFloor (lau sàn),
    /// lấy/đăng việc qua WorkQueue (nhân viên sau này dùng chung).
    public class OwnerController : MonoBehaviour
    {
        public const int MaxQueue = 3;
        public const int MaxCarriedCups = 4;

        private const float Speed = 2.4f;
        private const float AtCartRadius = 0.7f;   // trong bán kính này (và hàng đợi trống) = "đang ở quầy"
        private const float ArriveRadius = 0.22f;
        private const float StuckSeconds = 25f;
        private const float ServeDelay = 0.3f;     // quay mặt về khách rồi mới đưa ly
        private const float CupStackStep = 0.09f;

        private GameBootstrap game;
        private NavMeshAgent agent;
        private CustomerVisual visual;
        private OwnerTaskIcons icons;
        private OwnerLook look;
        private int appliedPreset = -1;
        private Transform fallbackAnchor;

        private readonly List<OwnerTask> queue = new List<OwnerTask>(6);
        private readonly List<GameObject> cupVisuals = new List<GameObject>(5);
        private int carriedScrapCups;   // ly thu từ bàn: về quầy được +1 Xu ve chai mỗi ly
        private int carriedPlainCups;   // ly đã đổ (khách bỏ đi): về quầy chỉ trả ly vào kho
        private int carriedDirtyCups;   // ly bẩn lau từ bàn (T2+): phải mang tới bồn rửa, rửa xong mới về kho
        private bool lastAtCart;
        private float workProgress = -1f;   // 0..1 khi đang lau/rửa/lau sàn (vòng tiến độ), <0 = không
        private Vector3 workAnchor;
        private OwnerTaskType workType;

        private Vector3 cartPoint;
        private Vector3 cartFaceDir = Vector3.back;
        private bool cartValid;

        /// Hàng đợi hoặc vị trí đổi (BrewPanel, icon nghe sự kiện này).
        public event Action Changed;

        public IReadOnlyList<OwnerTask> Tasks { get { return queue; } }
        public int QueueCount { get { return queue.Count; } }
        public OwnerLook Look { get { return look; } }
        public int CarriedCups { get { return carriedScrapCups + carriedPlainCups + carriedDirtyCups; } }
        public int DirtyCupsCarried { get { return carriedDirtyCups; } }

        /// Tiến độ việc đang làm tay (lau bàn / rửa ly / lau sàn): 0..1; âm nếu không có. Vòng tiến độ đọc.
        public float WorkProgress { get { return workProgress; } }
        /// Điểm thế giới để vẽ vòng tiến độ.
        public Vector3 WorkAnchor { get { return workAnchor; } }
        public OwnerTaskType WorkType { get { return workType; } }

        /// Chủ quán đang đứng ở xe đẩy và rảnh tay: được phép pha chế.
        public bool IsAtCart
        {
            get { return queue.Count == 0 && PlanarDistance(transform.position, CartPoint) <= AtCartRadius; }
        }

        public bool IsDelivering { get { return FindTask(OwnerTaskType.Deliver) != null; } }

        /// Điểm đứng phục vụ trước xe đẩy (trên NavMesh).
        public Vector3 CartPoint
        {
            get
            {
                if (!cartValid) ComputeCart();
                return cartPoint;
            }
        }

        /// Hướng nhìn của chủ quán khi đứng ở quầy (quay về xe đẩy).
        public Vector3 CartFaceDirection
        {
            get
            {
                if (!cartValid) ComputeCart();
                return cartFaceDir;
            }
        }

        /// Điểm nhìn cho camera (ngực chủ quán).
        public Vector3 FocusPoint { get { return transform.position + Vector3.up * 0.55f; } }

        // ------------------------------------------------------------------ tạo

        public static OwnerController Create(GameBootstrap g, OwnerLook look)
        {
            var go = new GameObject("Owner");
            var oc = go.AddComponent<OwnerController>();
            oc.Init(g, look);
            return oc;
        }

        private void Init(GameBootstrap g, OwnerLook lk)
        {
            game = g;
            look = lk;
            ComputeCart();
            transform.position = cartPoint;
            transform.rotation = Quaternion.LookRotation(cartFaceDir, Vector3.up);

            visual = gameObject.AddComponent<CustomerVisual>();
            RebuildVisual();

            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = NavMeshBaker.AgentRadius;
            agent.height = 1.2f;
            agent.speed = Speed;
            agent.acceleration = 14f;
            agent.angularSpeed = 540f;
            agent.stoppingDistance = 0.05f;
            agent.autoBraking = true;
            // Số lớn = nhường đường: khách (30..69) luôn được ưu tiên, chủ quán không chặn đường
            agent.avoidancePriority = 80;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            if (agent.isOnNavMesh) agent.Warp(cartPoint);

            game.Station.Changed += OnStationChanged;
            if (game.Grid != null) game.Grid.LayoutChanged += OnLayoutChanged;
            lastAtCart = true;

            CleaningSystem.Ensure(g); // bộ máy dọn dẹp (CI, bồn rửa, hiệu ứng); chỉ hoạt động khi CleaningRules.Enabled
        }

        private void OnDestroy()
        {
            if (game == null) return;
            if (game.Station != null) game.Station.Changed -= OnStationChanged;
            if (game.Grid != null) game.Grid.LayoutChanged -= OnLayoutChanged;
        }

        private void OnLayoutChanged()
        {
            cartValid = false;
        }

        // ------------------------------------------------------------------ diện mạo

        /// Đổi ngoại hình (màn tạo nhân vật). Đổi dáng thì dựng lại mô hình.
        public void SetLook(OwnerLook lk)
        {
            look = lk;
            if (lk.preset != appliedPreset) RebuildVisual();
            else visual.ApplyOwnerLook(look);
        }

        /// Quay mặt về hướng dir (màn tạo nhân vật quay chủ quán ra phía camera).
        public void FaceDirection(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private void RebuildVisual()
        {
            for (int i = 0; i < cupVisuals.Count; i++)
                if (cupVisuals[i] != null) Destroy(cupVisuals[i]);
            cupVisuals.Clear();

            visual.Clear();
            GameObject prefab = null;
            GameObject[] op = game.ownerPrefabs;
            if (op != null && look.preset >= 0 && look.preset < op.Length) prefab = op[look.preset];
            if (prefab == null && game.customerPrefabs != null && game.customerPrefabs.Length > 0)
                prefab = game.customerPrefabs[Mathf.Abs(look.preset) % game.customerPrefabs.Length];
            visual.Build(prefab, game.toonMaterial);
            visual.ApplyOwnerLook(look);
            appliedPreset = look.preset;
            RefreshCarry();
        }

        // ------------------------------------------------------------------ xe đẩy

        private void ComputeCart()
        {
            Vector3 p;
            Vector3 fwd = Vector3.forward;
            PlacedFurniture pf = game.Grid != null ? game.Grid.BrewStationFurniture : null;
            if (pf != null)
            {
                fwd = pf.transform.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude < 0.0001f ? Vector3.forward : fwd.normalized;
                Bounds b = pf.WorldBounds;
                float reach = Mathf.Abs(fwd.x) * b.extents.x + Mathf.Abs(fwd.z) * b.extents.z;
                p = b.center + fwd * (reach + 0.6f);
                p.y = pf.transform.position.y;
            }
            else
            {
                p = game.Grid != null ? game.Grid.WorldBounds.center : Vector3.zero;
                p.y = 0f;
            }
            NavMeshHit hit;
            if (NavMesh.SamplePosition(p, out hit, 2f, NavMesh.AllAreas)) p = hit.position;
            cartPoint = p;
            cartFaceDir = -fwd;
            cartValid = true;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ------------------------------------------------------------------ giao việc

        private OwnerTask FindTask(OwnerTaskType type)
        {
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].type == type) return queue[i];
            return null;
        }

        private int PlayerTaskCount()
        {
            int n = 0;
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].type != OwnerTaskType.ReturnToCart && queue[i].type != OwnerTaskType.WashCups) n++;
            return n;
        }

        private void RaiseChanged()
        {
            if (Changed != null) Changed();
        }

        /// Về quầy trước (BrewStation gọi khi chọn đơn / chạm bước lúc chủ quán đang đi xa).
        public void RequestReturn()
        {
            if (IsAtCart) return;
            if (queue.Count > 0 && queue[0].type == OwnerTaskType.ReturnToCart) return;
            RemoveReturnTasks();
            if (queue.Count > 0) queue[0].started = false; // việc đang làm sẽ đặt lại đích khi tới lượt
            queue.Insert(0, new OwnerTask { type = OwnerTaskType.ReturnToCart });
            if (agent != null && agent.isOnNavMesh) agent.ResetPath();
            RaiseChanged();
        }

        /// Chạm đúng khách khi ly đã sẵn sàng. Trả true nếu đã giao việc.
        public bool QueueDeliver(Customer c)
        {
            BrewStation st = game.Station;
            if (st.State != BrewStation.Phase.Ready) return false;
            if (c == null || !c.IsWaitingForDrink || c.CurrentOrder.recipe != st.Recipe)
            {
                GameEvents.RaiseToast(Strings.WrongCustomer);
                return false;
            }
            if (c.CurrentOrder.IsClaimed)
            {
                GameEvents.RaiseToast(Strings.OrderTakenByBrewer);
                return false;
            }

            OwnerTask existing = FindTask(OwnerTaskType.Deliver);
            if (existing != null)
            {
                // Đổi người nhận
                if (existing.customer != c)
                {
                    existing.customer = c;
                    existing.order = c.CurrentOrder;
                    existing.started = false;
                    existing.arrived = false;
                    RaiseChanged();
                }
                return true;
            }
            if (PlayerTaskCount() >= MaxQueue)
            {
                GameEvents.RaiseToast(Strings.OwnerBusy);
                return false;
            }
            AddPlayerTask(new OwnerTask { type = OwnerTaskType.Deliver, customer = c, order = c.CurrentOrder, recipe = st.Recipe });
            return true;
        }

        /// Còn chỗ nhận thêm một việc thu ly (không báo toast).
        public bool CanQueueCollect()
        {
            int queuedCollects = 0;
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].type == OwnerTaskType.CollectCup) queuedCollects++;
            return PlayerTaskCount() < MaxQueue && CarriedCups + queuedCollects < MaxCarriedCups;
        }

        /// Chạm ly để lại. Trả true nếu đã giao việc (hoặc đã có trong hàng).
        public bool QueueCollect(LeftoverCup cup)
        {
            if (cup == null || !cup.IsCollectable) return false;
            int queuedCollects = 0;
            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].type != OwnerTaskType.CollectCup) continue;
                if (queue[i].cup == cup) return true;
                queuedCollects++;
            }
            if (PlayerTaskCount() >= MaxQueue)
            {
                GameEvents.RaiseToast(Strings.OwnerBusy);
                return false;
            }
            if (CarriedCups + queuedCollects >= MaxCarriedCups)
            {
                GameEvents.RaiseToast(Strings.OwnerFullHands);
                return false;
            }
            AddPlayerTask(new OwnerTask { type = OwnerTaskType.CollectCup, cup = cup });
            return true;
        }

        private void AddPlayerTask(OwnerTask t)
        {
            RemoveReturnTasks(); // sẽ tự về quầy sau khi làm xong
            if (CleaningRules.Enabled) InsertByPriority(t);
            else queue.Add(t);
            RaiseChanged();
        }

        /// Thứ tự hợp lý (T2+): giao ly cho khách đang chờ trước, rồi thu ly, lau bàn, lau sàn; rửa ly sau cùng.
        private static int Priority(OwnerTaskType type)
        {
            switch (type)
            {
                case OwnerTaskType.Deliver: return 0;
                case OwnerTaskType.CollectCup: return 1;
                case OwnerTaskType.CleanTable: return 2;
                case OwnerTaskType.MopFloor: return 3;
                case OwnerTaskType.WashCups: return 4;
                default: return 5;
            }
        }

        private void InsertByPriority(OwnerTask t)
        {
            int p = Priority(t.type);
            int min = 0;
            if (queue.Count > 0 && queue[0].started && queue[0].arrived) min = 1; // đang làm dở tại chỗ: không giật việc
            int idx = queue.Count;
            for (int i = min; i < queue.Count; i++)
            {
                if (Priority(queue[i].type) > p)
                {
                    idx = i;
                    break;
                }
            }
            queue.Insert(idx, t);
            if (idx == 0 && queue.Count > 1)
            {
                queue[1].started = false; // việc đang đi dở sẽ lập lại đường khi tới lượt
                if (agent != null && agent.isOnNavMesh) agent.ResetPath();
            }
        }

        /// Nhả claim job của việc bị bỏ / hủy (job vẫn nằm trong WorkQueue cho người khác nhận).
        private void ReleaseTask(OwnerTask t)
        {
            if (t == null || t.job == null) return;
            WorkQueue.Cancel(t.job, this);
            t.job = null;
        }

        private void RemoveReturnTasks()
        {
            for (int i = queue.Count - 1; i >= 0; i--)
                if (queue[i].type == OwnerTaskType.ReturnToCart) RemoveTaskAt(i);
        }

        private void RemoveTaskAt(int i)
        {
            if (i < 0 || i >= queue.Count) return;
            ReleaseTask(queue[i]);
            queue.RemoveAt(i);
            if (i == 0)
            {
                if (agent != null && agent.isOnNavMesh) agent.ResetPath();
                if (queue.Count > 0) queue[0].started = false;
            }
            RaiseChanged();
        }

        private void FinishTask(OwnerTask t)
        {
            RemoveTaskAt(queue.IndexOf(t));
        }

        // ------------------------------------------------------------------ sự kiện từ quầy

        private void OnStationChanged()
        {
            if (game.Station.State != BrewStation.Phase.Ready)
            {
                // Ly đã được giao / đổ / reset: bỏ việc giao ly còn treo
                for (int i = queue.Count - 1; i >= 0; i--)
                    if (queue[i].type == OwnerTaskType.Deliver) RemoveTaskAt(i);
            }
            RefreshCarry();
        }

        /// Sang ngày mới: bỏ hết việc, trả ly đang cầm về kho, về quầy.
        public void ResetForNewDay()
        {
            for (int i = 0; i < queue.Count; i++) ReleaseTask(queue[i]);
            queue.Clear();
            workProgress = -1f;
            if (agent != null && agent.isOnNavMesh) agent.ResetPath();
            int n = CarriedCups;
            for (int i = 0; i < n; i++) game.Stock.Return();
            carriedScrapCups = 0;
            carriedPlainCups = 0;
            carriedDirtyCups = 0; // ly bẩn đang cầm coi như được rửa qua đêm
            cartValid = false;
            Vector3 p = CartPoint;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Warp(p); else transform.position = p;
            transform.rotation = Quaternion.LookRotation(cartFaceDir, Vector3.up);
            RefreshCarry();
            RaiseChanged();
        }

        // ------------------------------------------------------------------ cập nhật

        private void Update()
        {
            float dt = Time.deltaTime;
            if (agent == null) return;

            if (!agent.isOnNavMesh)
            {
                NavMeshHit h;
                if (NavMesh.SamplePosition(transform.position, out h, 3f, NavMesh.AllAreas)) agent.Warp(h.position);
            }

            ValidateTasks();
            workProgress = -1f; // CompleteArrival đặt lại mỗi frame khi đang lau/rửa/lau sàn

            if (queue.Count == 0)
            {
                // Đang pha dở thì ở lại quầy: bỏ ly đi rửa giữa chừng làm hỏng nhịp pha (TapStep báo "chủ quán đang đi") và kẹt ly
                bool brewingNow = game.Station != null && game.Station.State == BrewStation.Phase.Brewing;
                // Có khách đang chờ (chưa ai nhận) và còn ly sạch: pha trước, rửa sau. Hết ly sạch thì phải đi rửa mới pha tiếp được.
                bool customersWaiting = game.Stock != null && game.Stock.Count > 0 && HasUnclaimedOrder();
                if (!brewingNow && !customersWaiting && WantsAutoWash())
                {
                    // đang cầm ly bẩn (hoặc còn ly bẩn ở bồn mà không ai rửa): đi rửa
                    queue.Add(new OwnerTask { type = OwnerTaskType.WashCups });
                    RaiseChanged();
                }
                else if (PlanarDistance(transform.position, CartPoint) > AtCartRadius)
                {
                    queue.Add(new OwnerTask { type = OwnerTaskType.ReturnToCart });
                    RaiseChanged();
                }
                else if (CarriedCups > 0)
                {
                    Unload();
                }
            }
            if (queue.Count > 0) RunCurrent(dt);

            bool moving = agent.isOnNavMesh && agent.velocity.sqrMagnitude > 0.01f;
            if (!moving) FaceIdle(dt);

            float speed = agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            visual.Tick(speed, false, dt);

            bool at = IsAtCart;
            if (at != lastAtCart)
            {
                lastAtCart = at;
                RaiseChanged();
            }
        }

        private void LateUpdate()
        {
            if (icons == null && game.Hud != null && game.Hud.BubbleLayer != null)
                icons = OwnerTaskIcons.Create(game.Hud.BubbleLayer, this);
        }

        private void FaceIdle(float dt)
        {
            Vector3 dir = Vector3.zero;
            OwnerTask t = queue.Count > 0 ? queue[0] : null;
            if (t != null && t.arrived && t.type == OwnerTaskType.Deliver && t.customer != null)
                dir = t.customer.transform.position - transform.position;
            else if (t != null && t.arrived && t.type == OwnerTaskType.CollectCup && t.cup != null)
                dir = t.cup.transform.position - transform.position;
            else if (t != null && t.arrived && t.type == OwnerTaskType.CleanTable && t.table != null)
                dir = t.table.TopPoint - transform.position;
            else if (t != null && t.arrived && t.type == OwnerTaskType.WashCups && CleaningSystem.Instance != null && CleaningSystem.Instance.Sink != null)
                dir = CleaningSystem.Instance.Sink.WorldBounds.center - transform.position;
            else if (queue.Count == 0 && PlanarDistance(transform.position, CartPoint) <= AtCartRadius && !holdFacing)
                dir = CartFaceDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir.normalized, Vector3.up), 420f * dt);
        }

        private bool holdFacing;

        /// Màn tạo nhân vật giữ nguyên hướng mặt (không quay lại phía xe đẩy).
        public void SetHoldFacing(bool hold)
        {
            holdFacing = hold;
        }

        /// Hủy các việc đã vô hiệu (khách bỏ đi, ly đã biến mất).
        private void ValidateTasks()
        {
            BrewStation st = game.Station;
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                if (i >= queue.Count) continue;
                OwnerTask t = queue[i];
                switch (t.type)
                {
                    case OwnerTaskType.Deliver:
                        if (st.State != BrewStation.Phase.Ready)
                        {
                            RemoveTaskAt(i);
                        }
                        else if (t.customer == null || !t.customer.IsWaitingForDrink || t.customer.CurrentOrder != t.order)
                        {
                            DropDeliveredDrink(Strings.OwnerCustomerGone);
                            return; // hàng đợi đã đổi
                        }
                        break;
                    case OwnerTaskType.CollectCup:
                        if (t.cup == null || !t.cup.IsCollectable) RemoveTaskAt(i);
                        break;
                    case OwnerTaskType.CleanTable:
                        if (!CleaningRules.Enabled || t.table == null || !t.table.IsDirty) RemoveTaskAt(i);
                        break;
                    case OwnerTaskType.WashCups:
                        if (!WashStillNeeded(t)) RemoveTaskAt(i);
                        break;
                    case OwnerTaskType.MopFloor:
                        if (!CleaningRules.Enabled) RemoveTaskAt(i);
                        break;
                }
            }
        }

        /// Việc rửa ly còn cần: đang cầm ly bẩn, đang rửa dở, còn ly ở bồn, hoặc sắp lau thêm bàn.
        private bool WashStillNeeded(OwnerTask t)
        {
            if (!CleaningRules.Enabled) return false;
            if (carriedDirtyCups > 0 || t.job != null) return true;
            if (WorkQueue.Count(WorkJobType.WashCup, true) > 0) return true;
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].type == OwnerTaskType.CleanTable) return true;
            return false;
        }

        /// Rảnh việc mà đang cầm ly bẩn / còn ly bẩn chưa ai rửa ở bồn -> tự đi rửa.
        private bool HasUnclaimedOrder()
        {
            if (game.Orders == null) return false;
            for (int i = 0; i < game.Orders.Items.Count; i++)
                if (!game.Orders.Items[i].IsClaimed) return true;
            return false;
        }

        private bool WantsAutoWash()
        {
            if (!CleaningRules.Enabled) return false;
            return carriedDirtyCups > 0 || WorkQueue.Count(WorkJobType.WashCup, true) > 0;
        }

        /// Hủy giao ly: đổ ly trong tay; ly sạch về kho khi chủ quán về quầy.
        private void DropDeliveredDrink(string toast)
        {
            for (int i = queue.Count - 1; i >= 0; i--)
                if (queue[i].type == OwnerTaskType.Deliver) RemoveTaskAt(i);
            if (game.Station.DiscardKeepCup()) carriedPlainCups++;
            if (!string.IsNullOrEmpty(toast)) GameEvents.RaiseToast(toast);
            RefreshCarry();
            RaiseChanged();
        }

        private void RunCurrent(float dt)
        {
            OwnerTask t = queue[0];
            t.timer += dt;
            if (!t.started)
            {
                BeginTask(t);
                return;
            }
            if (t.timer < 0.12f) return; // chờ NavMesh tính đường

            if (t.arrived)
            {
                t.arriveTimer += dt;
                CompleteArrival(t);
                return;
            }

            float planar = PlanarDistance(transform.position, t.dest);
            bool pathBad = !agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid;
            bool stopped = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f;
            if (planar <= ArriveRadius || (stopped && planar <= 0.9f))
            {
                t.arrived = true;
                t.arriveTimer = 0f;
                agent.ResetPath();
                return;
            }
            if (pathBad || t.timer > StuckSeconds || (stopped && planar > 0.9f))
                Unreachable(t);
        }

        private void BeginTask(OwnerTask t)
        {
            t.started = true;
            t.arrived = false;
            t.timer = 0f;
            t.arriveTimer = 0f;
            t.work = 0f;

            Vector3 d = transform.position;
            switch (t.type)
            {
                case OwnerTaskType.ReturnToCart:
                    d = CartPoint;
                    break;
                case OwnerTaskType.CleanTable:
                    if (t.table != null)
                    {
                        Vector3 ap;
                        if (t.table.TryGetApproachPoint(transform.position, out ap)) d = ap;
                    }
                    break;
                case OwnerTaskType.WashCups:
                    {
                        Vector3 ap;
                        CleaningSystem cs = CleaningSystem.Instance;
                        if (cs != null && cs.TryGetSinkApproach(transform.position, out ap)) d = ap;
                        else d = CartPoint;
                    }
                    break;
                case OwnerTaskType.MopFloor:
                    d = transform.position; // lau ngay tại chỗ
                    break;
                case OwnerTaskType.Deliver:
                    d = ApproachOf(t.customer != null ? t.customer.Seat : null, t.customer != null ? t.customer.transform.position : d);
                    break;
                case OwnerTaskType.CollectCup:
                    d = ApproachOf(t.cup != null ? t.cup.Seat : null, t.cup != null ? t.cup.transform.position : d);
                    break;
            }
            t.dest = d;
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(d);
            }
        }

        private Vector3 ApproachOf(Seat seat, Vector3 fallback)
        {
            Vector3 p;
            if (seat != null && seat.TryGetApproachPoint(transform.position, out p)) return p;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(fallback, out hit, 1.5f, NavMesh.AllAreas)) return hit.position;
            return fallback;
        }

        private void CompleteArrival(OwnerTask t)
        {
            switch (t.type)
            {
                case OwnerTaskType.ReturnToCart:
                    Unload();
                    FinishTask(t);
                    break;

                case OwnerTaskType.CollectCup:
                    if (t.arriveTimer < 0.25f) return; // cúi nhặt ly
                    if (t.cup != null && t.cup.IsCollectable)
                    {
                        t.cup.PickUp();
                        carriedScrapCups++;
                        RefreshCarry();
                    }
                    FinishTask(t);
                    break;

                case OwnerTaskType.Deliver:
                    if (t.arriveTimer < ServeDelay) return; // quay mặt về khách trước
                    Customer c = t.customer;
                    bool ok = c != null && game.Station.TryServe(c); // gọi Customer.Serve(quality)
                    if (!ok) GameEvents.RaiseToast(Strings.WrongCustomer);
                    FinishTask(t);
                    RefreshCarry();
                    break;

                case OwnerTaskType.CleanTable:
                    CompleteCleanTable(t);
                    break;

                case OwnerTaskType.WashCups:
                    CompleteWash(t);
                    break;

                case OwnerTaskType.MopFloor:
                    CompleteMop(t);
                    break;
            }
        }

        // ------------------------------------------------------------------ dọn dẹp (T2+)

        /// Tới cạnh bàn: tự lau CleanSecondsOwner giây (vòng tiến độ hiện trên bàn), rồi nhận các ly bẩn.
        private void CompleteCleanTable(OwnerTask t)
        {
            TableDirt td = t.table;
            if (td == null || !td.IsDirty || !CleaningRules.Enabled)
            {
                FinishTask(t);
                return;
            }
            if (t.work <= 0f)
            {
                t.work = 1f; // đã kiểm tra khách ngồi
                if (td.HasSeatedCustomer())
                {
                    GameEvents.RaiseToast(Strings.TableHasGuests);
                    FinishTask(t);
                    return;
                }
            }

            td.MarkCleaning();
            float need = GameBalance.CleanSecondsOwner;
            workType = OwnerTaskType.CleanTable;
            workProgress = Mathf.Clamp01(t.arriveTimer / need);
            workAnchor = td.TopPoint + Vector3.up * 0.45f;
            if (t.arriveTimer < need) return;

            int free = MaxCarriedCups - CarriedCups;
            if (free <= 0)
            {
                GameEvents.RaiseToast(Strings.OwnerFullHands);
                FinishTask(t);
                return;
            }
            int taken = td.FinishClean(free); // hết ly thì job CleanTable tự hoàn thành
            carriedDirtyCups += taken;
            RefreshCarry();
            if (taken > 0)
                UiFx.FloatText(td.TopPoint + Vector3.up * 0.6f, string.Format(Strings.CupsToWashFmt, taken), HudBuilder.Mint);
            EnsureWashTask();
            FinishTask(t); // còn dư ly (tay đầy) thì nhả claim, bàn vẫn bẩn
        }

        /// Tới bồn: đặt ly bẩn xuống (mỗi ly = một job WashCup), rồi rửa lần lượt WashSecondsPerCup giây/ly.
        private void CompleteWash(OwnerTask t)
        {
            CleaningSystem cs = CleaningSystem.Instance;
            if (cs == null || !CleaningRules.Enabled)
            {
                FinishTask(t);
                return;
            }
            if (!t.dropped)
            {
                t.dropped = true;
                if (carriedDirtyCups > 0)
                {
                    cs.DropAtSink(carriedDirtyCups);
                    carriedDirtyCups = 0;
                    RefreshCarry();
                }
            }
            if (t.job == null)
            {
                WorkJob j = WorkQueue.Peek(WorkJobType.WashCup, this);
                if (j == null || !WorkQueue.Claim(j, this))
                {
                    FinishTask(t); // hết ly để rửa
                    return;
                }
                t.job = j;
                t.work = 0f;
            }

            t.work += Time.deltaTime;
            workType = OwnerTaskType.WashCups;
            workProgress = Mathf.Clamp01(t.work / GameBalance.WashSecondsPerCup);
            PlacedFurniture sink = cs.Sink;
            workAnchor = sink != null
                ? new Vector3(sink.WorldBounds.center.x, sink.WorldBounds.max.y + 0.45f, sink.WorldBounds.center.z)
                : transform.position + Vector3.up * 1.0f;
            if (t.work >= GameBalance.WashSecondsPerCup)
            {
                cs.WashOne(t.job); // ly sạch về kho
                t.job = null;
                t.work = 0f;
            }
        }

        /// Lau sàn tại chỗ MopSecondsOwner giây, FloorDirt -= MopFloorDirtReduce.
        private void CompleteMop(OwnerTask t)
        {
            if (!CleaningRules.Enabled)
            {
                FinishTask(t);
                return;
            }
            float need = GameBalance.MopSecondsOwner;
            workType = OwnerTaskType.MopFloor;
            workProgress = Mathf.Clamp01(t.arriveTimer / need);
            workAnchor = transform.position + Vector3.up * 1.0f;
            if (t.arriveTimer < need) return;

            CleaningSystem cs = CleaningSystem.Instance;
            if (cs != null && cs.Model != null) cs.Model.Mop();
            if (t.job != null) WorkQueue.Complete(t.job);
            t.job = null;
            UiFx.FloatText(transform.position + Vector3.up * 1.7f, Strings.FloorSparkling, HudBuilder.Mint);
            FinishTask(t);
        }

        /// Luôn có đúng một việc WashCups ở cuối hàng khi có ly bẩn phải rửa.
        private void EnsureWashTask()
        {
            if (!CleaningRules.Enabled || FindTask(OwnerTaskType.WashCups) != null) return;
            queue.Add(new OwnerTask { type = OwnerTaskType.WashCups });
            RaiseChanged();
        }

        // ------------------------------------------------------------------ giao việc dọn dẹp (T2+)

        /// Chạm bàn bẩn (hoặc bot): xếp việc lau bàn. Trả true nếu đã giao việc (hoặc đã có trong hàng).
        public bool QueueCleanTable(TableDirt t)
        {
            if (!CleaningRules.Enabled || t == null || !t.IsDirty) return false;
            int queuedCups = 0;
            for (int i = 0; i < queue.Count; i++)
            {
                OwnerTask q = queue[i];
                if (q.type != OwnerTaskType.CleanTable) continue;
                if (q.table == t) return true;
                if (q.table != null) queuedCups += q.table.DirtyCups;
            }
            if (t.HasSeatedCustomer())
            {
                GameEvents.RaiseToast(Strings.TableHasGuests);
                return false;
            }
            if (PlayerTaskCount() >= MaxQueue)
            {
                GameEvents.RaiseToast(Strings.OwnerBusy);
                return false;
            }
            if (CarriedCups + queuedCups + Mathf.Min(t.DirtyCups, MaxCarriedCups) > MaxCarriedCups)
            {
                GameEvents.RaiseToast(Strings.OwnerFullHands);
                return false;
            }
            WorkJob job = WorkQueue.PostCleanTable(t);
            WorkQueue.Claim(job, this, true); // tay chủ quán luôn ưu tiên hơn nhân viên
            AddPlayerTask(new OwnerTask { type = OwnerTaskType.CleanTable, table = t, job = job });
            EnsureWashTask();
            return true;
        }

        /// Lau sàn (nút HUD "Lau sàn" hoặc bot).
        public bool QueueMopFloor()
        {
            if (!CleaningRules.Enabled) return false;
            if (FindTask(OwnerTaskType.MopFloor) != null) return true;
            if (PlayerTaskCount() >= MaxQueue)
            {
                GameEvents.RaiseToast(Strings.OwnerBusy);
                return false;
            }
            WorkJob job = WorkQueue.PostMopFloor();
            WorkQueue.Claim(job, this, true);
            AddPlayerTask(new OwnerTask { type = OwnerTaskType.MopFloor, job = job });
            return true;
        }

        /// Đi rửa ly (đang cầm ly bẩn hoặc còn ly ở bồn). Việc này cũng tự được thêm khi lau bàn / rảnh tay.
        public bool QueueWashCups()
        {
            if (!CleaningRules.Enabled) return false;
            if (FindTask(OwnerTaskType.WashCups) != null) return true;
            if (carriedDirtyCups <= 0 && WorkQueue.Count(WorkJobType.WashCup, true) <= 0) return false;
            RemoveReturnTasks();
            queue.Add(new OwnerTask { type = OwnerTaskType.WashCups });
            RaiseChanged();
            return true;
        }

        private void Unreachable(OwnerTask t)
        {
            switch (t.type)
            {
                case OwnerTaskType.ReturnToCart:
                    // Kẹt: dịch thẳng về quầy
                    if (agent.isOnNavMesh) agent.Warp(CartPoint);
                    t.arrived = true;
                    return;
                case OwnerTaskType.Deliver:
                    if (t.customer != null && PlanarDistance(transform.position, t.customer.transform.position) < 1.6f)
                    {
                        t.arrived = true; // đủ gần để đưa ly
                        t.arriveTimer = 0f;
                        return;
                    }
                    GameEvents.RaiseToast(Strings.OwnerCantReach);
                    FinishTask(t); // giữ ly ở tay, người chơi chọn lại khách hoặc bấm Đổ ly
                    return;
                case OwnerTaskType.WashCups:
                    // Không tới được bồn: rửa luôn tại chỗ cho khỏi kẹt vòng lặp
                    t.arrived = true;
                    t.arriveTimer = 0f;
                    return;
                default:
                    GameEvents.RaiseToast(Strings.OwnerCantReach);
                    FinishTask(t);
                    return;
            }
        }

        /// Về tới quầy: ly sạch về kho, ly thu từ bàn +1 Xu ve chai mỗi ly.
        private void Unload()
        {
            for (int i = 0; i < carriedScrapCups; i++)
            {
                game.Stock.Return();
                game.Wallet.Earn(GameBalance.ScrapXu);
                if (game.Day.Summary != null) game.Day.Summary.scrap += GameBalance.ScrapXu;
            }
            for (int i = 0; i < carriedPlainCups; i++) game.Stock.Return();
            carriedScrapCups = 0;
            carriedPlainCups = 0;
            if (!CleaningRules.Enabled)
            {
                // luật dọn đã tắt mà còn ly bẩn trên tay: coi như đã rửa
                for (int i = 0; i < carriedDirtyCups; i++) game.Stock.Return();
                carriedDirtyCups = 0;
            }
            RefreshCarry();
        }

        // ------------------------------------------------------------------ ly trên tay

        private Transform Anchor
        {
            get
            {
                if (visual.CarryAnchor != null) return visual.CarryAnchor;
                if (fallbackAnchor == null)
                {
                    fallbackAnchor = new GameObject("CarryPoint").transform;
                    fallbackAnchor.SetParent(transform, false);
                    fallbackAnchor.localPosition = new Vector3(0.2f, 0.55f, 0.24f);
                }
                return fallbackAnchor;
            }
        }

        private void RefreshCarry()
        {
            if (visual == null) return;
            int want = CarriedCups + (game.Station.State == BrewStation.Phase.Ready ? 1 : 0);
            want = Mathf.Min(want, MaxCarriedCups);

            while (cupVisuals.Count > want)
            {
                int last = cupVisuals.Count - 1;
                if (cupVisuals[last] != null) Destroy(cupVisuals[last]);
                cupVisuals.RemoveAt(last);
            }
            while (cupVisuals.Count < want) cupVisuals.Add(MakeCupVisual(cupVisuals.Count));
            visual.SetCarry(want > 0);
        }

        private GameObject MakeCupVisual(int index)
        {
            GameObject go;
            if (game.CupPrefab != null)
            {
                go = Instantiate(game.CupPrefab);
                Collider[] cols = go.GetComponentsInChildren<Collider>();
                for (int i = 0; i < cols.Length; i++) Destroy(cols[i]);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(go.GetComponent<Collider>());
                go.transform.localScale = new Vector3(0.07f, 0.05f, 0.07f);
            }
            go.name = "CarriedCup";
            Transform t = go.transform;
            t.SetParent(Anchor, false);
            t.localPosition = new Vector3(0f, index * CupStackStep, 0f);
            t.localRotation = Quaternion.identity;
            return go;
        }
    }
}
