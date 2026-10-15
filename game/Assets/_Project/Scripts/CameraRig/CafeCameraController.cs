using UnityEngine;

namespace Cafe
{
    /// Rig camera orbit quanh tâm quán (docs/ui_style.md §6):
    ///   1 ngón kéo ngang = xoay 360° tự do (có quán tính), kéo dọc = nghiêng
    ///   2 ngón: kéo = pan, chụm = zoom, xoay = xoay
    ///   Chuột: trái kéo = xoay/nghiêng, phải hoặc giữa kéo = pan, lăn = zoom; phím Q/E xoay.
    /// Chạm nhanh không kéo vẫn thuộc về TapInput (cùng ngưỡng DragThresholdPx).
    public class CafeCameraController : MonoBehaviour
    {
        public const float MinPitch = 30f;
        public const float MaxPitch = 70f;
        public const float MinDistance = 5f;
        public const float MaxDistance = 15f;       // mặc định (phòng nhỏ); SetBounds nới ra cho phòng lớn hơn

        const float DefaultPitch = 48f;
        const float DefaultDistance = 11f;
        const float DefaultYaw = 0f;
        // Chống chóng mặt (đánh giá 2026-10-15): xoay chậm hơn, quán tính ngắn, lọc tốc độ ngón theo thời gian thực
        const float YawDegPerPixel = 0.14f;   // độ xoay mỗi pixel kéo (ở màn 1080p); trước là 0,28
        const float PitchDegPerPixel = 0.08f;
        const float InertiaDamping = 6f;      // càng lớn quán tính tắt càng nhanh
        const float MaxFling = 360f;          // độ/giây tối đa khi thả tay: văng thêm tối đa ~60 độ, dừng sau ~0,5 giây
        const float InertiaStop = 15f;        // dưới mức này dừng hẳn, không "trôi"
        const float TwistDeadzone = 10f;      // chụm 2 ngón chỉ xoay khi đã xoay hơn ngần này độ
        const float MaxDegPerFrame = 8f;      // trần xoay mỗi khung: 30 FPS không quá 240 độ/giây, khựng khung không làm nhảy góc
        const float SnapDeg = 45f;            // "Giảm chuyển động": thả tay thì dính góc
        const float PivotHeight = 0.35f;      // nhìn vào ngang mặt bàn, không phải mặt đất

        private Camera cam;
        private Transform camTransform;
        private Rect panRect;

        private float yaw = DefaultYaw, yawTarget = DefaultYaw, yawVelocity;
        private float pitch = DefaultPitch, pitchTarget = DefaultPitch;
        private float distance = DefaultDistance, distanceTarget = DefaultDistance;
        private float maxDistance = MaxDistance;
        private float inertiaYaw;             // độ/giây sau khi thả tay
        private float insetVelocity;          // SmoothDamp của lens shift
        private float twistSum;               // góc 2 ngón đã xoay từ lúc chạm (để qua vùng chết)
        private bool twisting;

        // Trạng thái 1 con trỏ (1 ngón hoặc chuột trái)
        private bool orbitTracking, orbiting;
        private Vector2 startPos, lastPos;
        private float lastYawSpeed;

        // Trạng thái 2 ngón
        private bool twoFinger;
        private Vector2 lastMid;
        private float lastSpan;
        private float lastAngle;

        // Chuột pan (phải/giữa)
        private bool mousePanning;
        private Vector2 lastPanMouse;
        private float lastTouchTime = -10f;

        // Phần dưới màn hình bị UI che (0..1 chiều cao): dịch khung hình lên để quán không bị che
        private float bottomInset, bottomInsetTarget;

        /// Build mode đặt true khi đang kéo đồ: camera bỏ qua 1 ngón/chuột trái (vẫn zoom/pan 2 ngón).
        public bool InputBlocked { get; set; }

        /// Tạo rig trên camera. gridBounds: vùng lưới (m), pivot bị giới hạn trong đó (+ lề).
        public static CafeCameraController Create(Camera camera, Bounds gridBounds)
        {
            var rig = new GameObject("CameraRig");
            var ctl = rig.AddComponent<CafeCameraController>();
            ctl.Setup(camera, gridBounds);
            return ctl;
        }

        private void Setup(Camera camera, Bounds gridBounds)
        {
            cam = camera;
            camTransform = camera.transform;
            const float margin = 1.5f;
            panRect = new Rect(gridBounds.min.x - margin, gridBounds.min.z - margin,
                gridBounds.size.x + margin * 2f, gridBounds.size.z + margin * 2f);

            transform.position = new Vector3(gridBounds.center.x, PivotHeight, gridBounds.center.z);
            camTransform.SetParent(transform, false);
            Apply();
        }

        /// Đổi vùng giới hạn pan + đưa tâm xoay về giữa vùng mới (chuyển cấp quán: phòng thuê thay lưới vỉa hè).
        /// viewDistance > 0: đặt luôn khoảng cách camera (phòng thuê rộng hơn vỉa hè); pivotZOffset: dời tâm xoay theo Z
        /// (kéo khung hình về phía mặt tiền để thấy cả vỉa hè). Có đặt lại góc xoay/nghiêng mặc định.
        public void SetBounds(Bounds gridBounds, float viewDistance = 0f, float pivotZOffset = 0f)
        {
            const float margin = 1.5f;
            panRect = new Rect(gridBounds.min.x - margin, gridBounds.min.z - margin,
                gridBounds.size.x + margin * 2f, gridBounds.size.z + margin * 2f);
            transform.position = new Vector3(gridBounds.center.x, PivotHeight, gridBounds.center.z + pivotZOffset);
            focusSaved = false;
            inertiaYaw = 0f;
            maxDistance = Mathf.Max(MaxDistance, viewDistance * 1.2f);
            if (viewDistance > 0f)
            {
                distance = distanceTarget = Mathf.Clamp(viewDistance, MinDistance, maxDistance);
                yaw = yawTarget = DefaultYaw;
                pitch = pitchTarget = DefaultPitch;
                yawVelocity = 0f;
            }
        }

        /// UI gọi mỗi frame: tỉ lệ chiều cao màn hình bị panel đáy che (0 = không che).
        public void SetBottomInset(float fraction) { bottomInsetTarget = Mathf.Clamp(fraction, 0f, 0.6f); }

        // ---- Tiêu điểm (màn tạo nhân vật) ----
        private bool focusSaved;
        private Vector3 savedPos;
        private float savedYaw, savedPitch, savedDistance;

        /// Đưa camera lại gần một điểm (vd. chủ quán ở xe đẩy). Nhớ trạng thái cũ để ClearFocus() khôi phục.
        /// viewFromDir: camera nằm phía hướng này (ngang) nhìn vào điểm; 0 = giữ yaw hiện tại.
        /// screenShift: dịch điểm nhìn sang phải màn hình (m, 0.9 ≈ nhân vật lệch phải ~1/4 màn hình ở distance 3.5).
        public void FocusOn(Vector3 worldPos, float distance, Vector3 viewFromDir = default(Vector3), float pitchDeg = 20f, float screenShift = 0f)
        {
            if (!focusSaved)
            {
                focusSaved = true;
                savedPos = transform.position;
                savedYaw = yawTarget;
                savedPitch = pitchTarget;
                savedDistance = distanceTarget;
            }
            viewFromDir.y = 0f;
            if (viewFromDir.sqrMagnitude > 0.0001f)
            {
                // camera ở phía viewFromDir: hướng nhìn = -viewFromDir
                float target = Mathf.Atan2(-viewFromDir.x, -viewFromDir.z) * Mathf.Rad2Deg;
                yawTarget = yaw + Mathf.DeltaAngle(yaw, target);
            }
            pitchTarget = Mathf.Clamp(pitchDeg, 5f, MaxPitch);
            distanceTarget = Mathf.Max(1.5f, distance);
            inertiaYaw = 0f;
            // nhân vật nằm bên phải tâm màn hình: dịch pivot sang trái (theo trục phải của camera)
            Vector3 right = Quaternion.Euler(0f, yawTarget, 0f) * Vector3.right;
            Vector3 p = worldPos - right * screenShift;
            transform.position = new Vector3(p.x, PivotHeight, p.z);
        }

        /// Khôi phục góc nhìn trước FocusOn.
        public void ClearFocus()
        {
            if (!focusSaved) return;
            focusSaved = false;
            transform.position = savedPos;
            yawTarget = savedYaw;
            pitchTarget = savedPitch;
            distanceTarget = savedDistance;
        }

        /// Giữ cho tương thích (bàn phím / nút cũ): xoay 90°.
        public void RotateLeft() { yawTarget -= 90f; inertiaYaw = 0f; }
        public void RotateRight() { yawTarget += 90f; inertiaYaw = 0f; }

        private void Update()
        {
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;

            int tc = Input.touchCount;
            if (tc >= 2) { lastTouchTime = Time.unscaledTime; HandleTwoFingers(); }
            else
            {
                twoFinger = false;
                if (tc == 1) { lastTouchTime = Time.unscaledTime; if (!InputBlocked) HandleOneFinger(Input.GetTouch(0)); else EndOrbit(); }
                else if (Time.unscaledTime - lastTouchTime > 0.3f) HandleMouse();
            }

            // Quán tính khi đã thả tay (dt bị chặn để một cú khựng khung không làm camera nhảy góc)
            if (!orbiting && !GraphicsQuality.ReduceMotion && Mathf.Abs(inertiaYaw) > InertiaStop)
            {
                float idt = Mathf.Min(dt, 1f / 30f);
                yawTarget += inertiaYaw * idt;
                inertiaYaw *= Mathf.Exp(-InertiaDamping * idt);
            }
            else if (!orbiting) inertiaYaw = 0f;
            ClampPivot();
        }

        private void LateUpdate()
        {
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;
            // Đang kéo / văng thì bám sát ngón (0,03 s); nhảy rời rạc (nút xoay, FocusOn) thì êm hơn (0,12 s)
            float smooth = (orbiting || inertiaYaw != 0f) ? 0.03f : 0.12f;
            float maxYawSpeed = Mathf.Min(GraphicsQuality.ReduceMotion ? 150f : 480f, MaxDegPerFrame / Mathf.Max(dt, 1f / 240f));
            yaw = Mathf.SmoothDampAngle(yaw, yawTarget, ref yawVelocity, smooth, maxYawSpeed, dt);
            float k = 1f - Mathf.Exp(-12f * dt);
            pitch = Mathf.Lerp(pitch, pitchTarget, k);
            distance = Mathf.Lerp(distance, distanceTarget, k);
            // Lens shift: panel đáy hiện / ẩn không được làm cả khung hình trượt gấp (UI đã tự ease, đây chỉ bắt kịp)
            bottomInset = Mathf.SmoothDamp(bottomInset, bottomInsetTarget, ref insetVelocity, 0.15f, Mathf.Infinity, dt);
            Apply();
            ApplyInset();
        }

        /// Dịch frustum theo trục Y màn hình (lens shift) mà không đổi góc nhìn.
        /// ndc_y' = ndc_y − m12, nên m12 = −inset đưa tâm vùng nhìn thấy lên giữa phần không bị che.
        private void ApplyInset()
        {
            cam.ResetProjectionMatrix();
            if (bottomInset < 0.001f) return;
            Matrix4x4 p = cam.projectionMatrix;
            p.m12 = -bottomInset;
            cam.projectionMatrix = p;
        }

        private void Apply()
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Quaternion tilt = Quaternion.Euler(pitch, 0f, 0f);
            camTransform.localRotation = tilt;
            camTransform.localPosition = tilt * new Vector3(0f, 0f, -distance);
        }

        // ---------------------------------------------------------------- 1 con trỏ: xoay / nghiêng

        private void HandleOneFinger(Touch t)
        {
            switch (t.phase)
            {
                case TouchPhase.Began:
                    BeginOrbit(t.position, TapInput.IsOverUI(t.position, t.fingerId));
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    DragOrbit(t.position);
                    break;
                default:
                    EndOrbit();
                    break;
            }
        }

        private void BeginOrbit(Vector2 pos, bool overUI)
        {
            orbitTracking = !overUI;
            orbiting = false;
            startPos = lastPos = pos;
            lastYawSpeed = 0f;
            inertiaYaw = 0f;
        }

        private void DragOrbit(Vector2 pos)
        {
            if (!orbitTracking) return;
            float thr = TapInput.DragThresholdPx;
            if (!orbiting)
            {
                if ((pos - startPos).sqrMagnitude <= thr * thr) return;
                orbiting = true;
                lastPos = startPos;   // không vứt phần đã kéo trong vùng chết (SmoothDamp 0,03 s hấp thụ cú nhảy đầu)
            }
            bool calm = GraphicsQuality.ReduceMotion;
            Vector2 d = (pos - lastPos) * PixelScale();
            lastPos = pos;
            if (calm) d *= 0.5f;
            float dYaw = d.x * YawDegPerPixel;
            yawTarget += dYaw;
            // Vuốt ngang không được làm nghiêng camera; "Giảm chuyển động" khóa hẳn độ nghiêng
            if (!calm && Mathf.Abs(d.x) <= Mathf.Abs(d.y) * 1.5f)
                pitchTarget = Mathf.Clamp(pitchTarget - d.y * PitchDegPerPixel, MinPitch, MaxPitch);

            // Tốc độ ngón lọc với hằng số 60 ms thật, không phụ thuộc FPS (cảm ứng lấy mẫu 60 Hz, màn hình có thể 120 Hz)
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 1f / 240f, 1f / 20f);
            lastYawSpeed = Mathf.Lerp(lastYawSpeed, dYaw / dt, 1f - Mathf.Exp(-dt / 0.06f));
        }

        private void EndOrbit()
        {
            if (orbiting)
            {
                if (GraphicsQuality.ReduceMotion)
                {
                    inertiaYaw = 0f;
                    yawTarget = Mathf.Round(yawTarget / SnapDeg) * SnapDeg;   // dính góc thay vì trôi
                }
                else inertiaYaw = Mathf.Clamp(lastYawSpeed, -MaxFling, MaxFling);
            }
            orbitTracking = false;
            orbiting = false;
        }

        // ---------------------------------------------------------------- 2 ngón: pan / zoom / twist

        private void HandleTwoFingers()
        {
            Touch a = Input.GetTouch(0);
            Touch b = Input.GetTouch(1);
            if (a.fingerId > b.fingerId) { Touch t = a; a = b; b = t; }   // thứ tự chỉ số có thể đổi giữa các khung: góc sẽ nhảy 180 độ
            Vector2 mid = (a.position + b.position) * 0.5f;
            Vector2 span = b.position - a.position;
            float len = span.magnitude;
            float angle = Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg;

            // Ngón thứ hai chạm vào giữa lúc đang xoay: chuyển hẳn sang chế độ 2 ngón
            if (!twoFinger)
            {
                twoFinger = true;
                twisting = false;
                twistSum = 0f;
                orbitTracking = orbiting = false;
                inertiaYaw = 0f;
                bool overUI = TapInput.IsOverUI(a.position, a.fingerId) || TapInput.IsOverUI(b.position, b.fingerId);
                if (overUI) { lastSpan = -1f; return; }
                lastMid = mid; lastSpan = len; lastAngle = angle;
                return;
            }
            if (lastSpan < 0f) return; // bắt đầu trên UI: bỏ qua cả cử chỉ

            Pan(mid - lastMid);
            if (len > 1f && lastSpan > 1f)
                distanceTarget = Mathf.Clamp(distanceTarget * lastSpan / len, MinDistance, maxDistance);
            // Chụm để zoom / kéo để pan không được tự xoay: chỉ xoay khi 2 ngón đã xoay quá vùng chết; "Giảm chuyển động" bỏ hẳn
            float dAngle = Mathf.DeltaAngle(lastAngle, angle);
            twistSum += dAngle;
            if (!GraphicsQuality.ReduceMotion && (twisting || Mathf.Abs(twistSum) > TwistDeadzone))
            {
                twisting = true;
                yawTarget -= dAngle;
            }

            lastMid = mid; lastSpan = len; lastAngle = angle;
        }

        // ---------------------------------------------------------------- chuột / bàn phím (Editor, PC)

        private void HandleMouse()
        {
            Vector2 mp = Input.mousePosition;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f && !TapInput.IsOverUI(mp, -1))
                distanceTarget = Mathf.Clamp(distanceTarget * (1f - scroll * 0.1f), MinDistance, maxDistance);

            if (InputBlocked) EndOrbit();
            else if (Input.GetMouseButtonDown(0)) BeginOrbit(mp, TapInput.IsOverUI(mp, -1));
            else if (Input.GetMouseButton(0)) DragOrbit(mp);
            if (Input.GetMouseButtonUp(0)) EndOrbit();

            bool panDown = Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
            if (panDown && !TapInput.IsOverUI(mp, -1)) { mousePanning = true; lastPanMouse = mp; }
            if (mousePanning && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
            {
                Pan(mp - lastPanMouse);
                lastPanMouse = mp;
            }
            if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2)) mousePanning = false;

            float key = (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);
            if (key != 0f) { yawTarget += key * 120f * Time.unscaledDeltaTime; inertiaYaw = 0f; }
        }

        // ---------------------------------------------------------------- helpers

        /// Chuẩn hóa pixel theo màn 1080p để tốc độ xoay giống nhau trên mọi máy.
        private static float PixelScale() => Screen.height > 0 ? 1080f / Screen.height : 1f;

        private void Pan(Vector2 deltaPixels)
        {
            if (Screen.height <= 0) return;
            float worldPerPixel = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = flat * Vector3.right;
            Vector3 fwd = flat * Vector3.forward;
            float sinPitch = Mathf.Max(0.3f, Mathf.Sin(pitch * Mathf.Deg2Rad));
            Vector3 move = right * (deltaPixels.x * worldPerPixel) + fwd * (deltaPixels.y * worldPerPixel / sinPitch);
            transform.position -= move;
        }

        private void ClampPivot()
        {
            Vector3 p = transform.position;
            p.x = Mathf.Clamp(p.x, panRect.xMin, panRect.xMax);
            p.z = Mathf.Clamp(p.z, panRect.yMin, panRect.yMax);
            p.y = PivotHeight;
            transform.position = p;
        }
    }
}
