# Đánh giá Unity: chóng mặt, độ mượt, hiệu năng điện thoại (2026-10-15)

Chỉ đọc mã và cấu hình; không chạy Unity, không đo máy thật. Phần cuối nêu rõ cái gì là suy luận. Đường dẫn rút gọn: `CAM` = `game/Assets/_Project/Scripts/CameraRig/CafeCameraController.cs`.

## 0. Kết luận
- Chóng mặt nhiều khả năng do camera, không phải GPU: độ lợi xoay quá cao, hai lớp làm mượt cộng quán tính dài, chụm 2 ngón luôn xoay, mức Thấp 30 FPS xoay tới 24 độ/khung.
- Hiệu năng: CPU script nhẹ (PC 0,45 ms). Rủi ro nằm ở số draw call, Canvas HUD bị đánh dấu bẩn mỗi khung, GPU ở độ phân giải gốc, và cấu hình bóng đổ sai khoảng cách ở cấp 3.
- FPS 60/90/120: mốc 90 gần như chắc sai trên iPhone ProMotion và máy Android 120 Hz không có chế độ 90 Hz thật (giật nhịp, tệ hơn 60 cố định).

## A. Chóng mặt: lỗi trong `CAM`
| # | Dòng | Vấn đề (tính từ code) |
|---|---|---|
| A1 | 20 | 0,28 độ/px: iPhone 1179 px cao => 0,256 độ/px; vuốt hết bề ngang (2556 px) = 650 độ. Vật cách tâm 5–8 m chạy nhanh ~2–3 px cho mỗi 1 px ngón (tier 3: r=7,8 m, d=19,5 m, 0,0104 m/px). Đây là nguyên nhân số 1. |
| A2 | 179 | SmoothDampAngle 0,08 s là bộ lọc bậc 2: khi kéo đều, view trễ đúng 80 ms sau mục tiêu (300 độ/s => 24 độ), rồi còn trôi ~0,2 s sau khi nhả. Quán tính lại cộng vào `yawTarget` (169) nên chịu thêm lần trễ này. |
| A3 | 167–170, 257 | Quán tính tối đa 720 độ/s, k=4,5: một cú vuốt = tới 160 độ (v/k), mất 1,6 s mới tới ngưỡng 0,5 độ/s; riêng đoạn dưới 20 độ/s kéo dài 0,8 s, cảnh "trôi chậm" rất khó chịu. |
| A4 | 252 | `Lerp(…, 0.5)` mỗi khung: hằng số thời gian phụ thuộc FPS (12 ms ở 120 FPS, 33 ms ở 30 FPS) => cùng một cú vuốt văng khác nhau theo mục FPS vừa thêm. Khi chạm lấy mẫu 60 Hz mà render 90–120 FPS, khung `Stationary` có d=0 làm tốc độ dao động ±50%. |
| A5 | 266–289 | Chụm luôn trừ `yawTarget` theo góc (không vùng chết), đồng thời pan theo trung điểm (286): zoom kèm xoay và trượt không chủ ý. Thứ tự `GetTouch(0/1)` không đảm bảo ổn định; đổi chỗ => `angle` nhảy 180 độ => camera quay 180 độ trong một khung. |
| A6 | 180–183, 249 | Yaw dùng SmoothDamp (bậc 2) còn pitch, zoom, lens shift dùng Lerp bậc 1: ba đường cong khác nhau, kéo chéo bị lệch pha. Pitch 0,12 vs yaw 0,28 và không khóa trục: vuốt ngang vẫn làm nghiêng, cảnh "lắc". |
| A7 | 239–244 | Vùng chết = `TapInput.DragThresholdPx` (~32 px ở 460 dpi) và `lastPos = pos` vứt phần đã kéo: đầu mỗi cử chỉ bị dính rồi giật. |
| A8 | 175–197 | Lens shift: `SetBottomInset(0)` khi ẩn (BrewPanel.cs:321, HudController.cs:152) là bước nhảy 0,35 NDC; Lerp 12/s bắt đầu ở tốc độ cực đại (~2 chiều cao màn hình/giây). Đường trượt thì mượt nhưng chồng 2 bộ lọc (BrewPanel.cs:549,555: 14/s rồi camera 12/s) nên cảnh còn trượt sau khi panel dừng. |
| A9 | 134, 142 | FocusOn/ClearFocus dịch pivot tức thì, còn yaw/pitch/zoom trượt: cảnh "văng" quanh điểm mới (11 -> 3,5 m, 48 -> 14 độ, tới 180 độ yaw trong ~0,3 s), không có fade. Chuyển cấp (SetBounds, 82–98) cắt cứng nhưng nằm dưới màn đen 0,6 s (TierManager.cs:17) nên ổn. |
| A10 | RoomWall.cs:30 | Tường hạ/nâng theo yaw, ngưỡng 0,35 không có độ trễ trạng thái; bức tường lớn đổi chiều cao trong lúc xoay là thêm chuyển động; sát ngưỡng thì tường bật tắt liên tục. |
| A11 | 175 | Không có `DefaultExecutionOrder`: OrderBubble.cs:103, OwnerTaskIcons.cs:122, CleaningSystem.cs:204, RoomWall.cs:20 cũng ở LateUpdate. Nếu chạy trước camera, bong bóng trễ 1 khung so với đầu khách (30 FPS, 240 độ/s => lệch 8 độ). Cần kiểm chứng. |
| A12 | 155, 178 | `dt` không bị chặn: khựng 12–25 ms (sinh khách, lưu game, bake NavMesh) lúc đang quay 400 độ/s = nhảy 5–10 độ một khung. Mức Thấp (30 FPS) với 720 độ/s = 24 độ/khung, hiện tượng strobing, gây buồn nôn mạnh nhất. |

Hằng số khuyến nghị:
| Hằng số | Nay | Đề xuất | Ghi chú |
|---|---|---|---|
| YawDegPerPixel (20) | 0,28 | 0,12 | gần 1:1 với vật cách tâm 5–8 m; vuốt 1 chiều cao màn = 130 độ |
| PitchDegPerPixel (21) | 0,12 | 0,08 | kèm khóa trục khi \|dx\| > 1,5\|dy\| |
| InertiaDamping (22) | 4,5 | 6 | |
| Trần vận tốc văng (257) | 720 | 360 độ/s | tối đa ~60 độ; dừng sau ~0,5 s |
| Ngưỡng dừng quán tính (167) | 0,5 | 15 độ/s | hết "trôi" |
| SmoothDamp yaw (179) | 0,08 | 0,03 khi kéo/văng; 0,12 khi nhảy rời rạc | trễ kéo còn 9 độ ở 300 độ/s |
| maxSpeed yaw (179) | vô hạn | min(480, 8 x FPS) độ/s | 30 FPS => 240; Giảm chuyển động => 150 |
| Lọc tốc độ ngón (252) | 0,5/khung | hằng số 60 ms theo dt | độc lập FPS |
| Vùng chết chụm xoay (289) | 0 | 10 độ cộng dồn | |
| Lens shift (183) | Lerp 12/s | SmoothDamp 0,15 s | hoặc bỏ lọc vì UI đã ease |
| Hysteresis tường (RoomWall.cs:9,30) | 0,35 | hạ khi > 0,42, nâng khi < 0,30 | |
| Fade FocusOn | không | trượt pivot 0,7 s (ease) hoặc fade 0,15 s | |

Bản vá đề xuất (chưa áp dụng, phác thảo):
```diff
-        const float YawDegPerPixel = 0.28f;   const float PitchDegPerPixel = 0.12f;   const float InertiaDamping = 4.5f;
+        const float YawDegPerPixel = 0.12f;   const float PitchDegPerPixel = 0.08f;   const float InertiaDamping = 6f;
+        const float MaxFling = 360f, InertiaStop = 15f, TwistDeadzone = 10f;
+        private float insetVel, twistSum; private bool twisting;
+        private static bool ReduceMotion { get { return GraphicsQuality.ReduceMotion; } }   // cờ mới, xem dưới
 // Update
-            if (!orbiting && Mathf.Abs(inertiaYaw) > 0.5f)
-            { yawTarget += inertiaYaw * dt; inertiaYaw *= Mathf.Exp(-InertiaDamping * dt); }
+            float idt = Mathf.Min(dt, 1f / 30f);                       // khựng khung không làm nhảy góc
+            if (!orbiting && !ReduceMotion && Mathf.Abs(inertiaYaw) > InertiaStop)
+            { yawTarget += inertiaYaw * idt; inertiaYaw *= Mathf.Exp(-InertiaDamping * idt); }
+            else if (!orbiting) inertiaYaw = 0f;
 // LateUpdate
-            yaw = Mathf.SmoothDampAngle(yaw, yawTarget, ref yawVelocity, 0.08f, Mathf.Infinity, dt);
+            int fps = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
+            float maxYaw = Mathf.Min(ReduceMotion ? 150f : 480f, 8f * fps);   // tối đa 8 độ/khung
+            yaw = Mathf.SmoothDampAngle(yaw, yawTarget, ref yawVelocity, (orbiting || inertiaYaw != 0f) ? 0.03f : 0.12f, maxYaw, dt);
-            bottomInset = Mathf.Lerp(bottomInset, bottomInsetTarget, k);
+            bottomInset = Mathf.SmoothDamp(bottomInset, bottomInsetTarget, ref insetVel, 0.15f, Mathf.Infinity, dt);
 // DragOrbit
-                lastPos = pos;                    // khi vượt ngưỡng
+                lastPos = startPos;               // không vứt phần đã kéo; SmoothDamp 0,03 hấp thụ ~4 độ
+            if (Mathf.Abs(d.x) > Mathf.Abs(d.y) * 1.5f) d.y = 0f;               // vuốt ngang không nghiêng
-            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
-            lastYawSpeed = Mathf.Lerp(lastYawSpeed, dYaw / dt, 0.5f);
+            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 1f / 240f, 1f / 20f);
+            lastYawSpeed = Mathf.Lerp(lastYawSpeed, dYaw / dt, 1f - Mathf.Exp(-dt / 0.06f));   // 60 ms, độc lập FPS
 // EndOrbit
-            if (orbiting) inertiaYaw = Mathf.Clamp(lastYawSpeed, -720f, 720f);
+            if (orbiting) inertiaYaw = Mathf.Clamp(lastYawSpeed, -MaxFling, MaxFling);
+            if (orbiting && ReduceMotion) yawTarget = Mathf.Round(yawTarget / 45f) * 45f;   // dính góc 45 độ
 // HandleTwoFingers
-            Touch a = Input.GetTouch(0); Touch b = Input.GetTouch(1);
+            Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
+            if (a.fingerId > b.fingerId) { Touch t = a; a = b; b = t; }          // thứ tự chỉ số có thể đổi
+            // trong khối !twoFinger: twistSum = 0f; twisting = false;
-            yawTarget -= Mathf.DeltaAngle(lastAngle, angle);
+            float dA = Mathf.DeltaAngle(lastAngle, angle); twistSum += dA;
+            if (twisting || Mathf.Abs(twistSum) > TwistDeadzone) { twisting = true; yawTarget -= dA; }
 // FocusOn/ClearFocus: thay `transform.position = X` bằng GlideTo(X); trong Update:
+            if (glideLeft > 0f) { glideLeft -= dt; transform.position = Vector3.SmoothDamp(transform.position, glideTarget, ref pivotVel, 0.22f, Mathf.Infinity, dt); }
+            // và k = 1 - exp(-(glideLeft > 0 ? 5 : 12) * dt); BeginOrbit/HandleTwoFingers đặt glideLeft = 0
+ [DefaultExecutionOrder(-100)] trên class CafeCameraController
```
"Giảm chuyển động" (Cài đặt, PlayerPrefs `cafe.reducemotion`, mặc định theo hệ điều hành nếu có): tắt quán tính; độ lợi x0,5; trần 150 độ/s; thả tay thì dính 45 độ; RoomWall đổi ngay (Speed 7 -> tức thì); FocusOn dùng fade; khóa pitch ở 48 độ; đề nghị thêm hàng nút bật/tắt cạnh hàng FPS trong SettingsPanel.cs.

## B. Hiệu năng ở cấp 3 (xếp theo khả năng gây hại)
Ngân sách: 60 FPS = 16,6 ms, 120 FPS = 8,3 ms, nhưng cần chừa 25–30% cho nhiệt (60: ~12 ms, 120: ~6 ms). Số bên dưới là ước lượng, không đo.
1. GPU và độ phân giải gốc (cao). `defaultIsNativeResolution: 1`, `resolutionScalingMode: 0` (ProjectSettings.asset:88,167); Cao = render scale 1,0 + MSAA 2x + bóng mềm 1024 (URP_Mobile.asset:28,29,46,66) ở 2,6–3,0 Mpix (iPhone 15: 2556x1179). Ước 3–5 ms trên A15+, 8–14 ms trên Mali-G57/Adreno 619 => Cao + 90/120 FPS không khả thi trên máy tầm trung. Cân nhắc trần 1920 px cạnh dài ở Cao, Vừa dùng 0,85 + MSAA 2x nếu đo ổn (răng cưa nhấp nháy khi xoay cũng góp vào chóng mặt: Thấp 0,7 và Vừa 0,85 đều không MSAA).
2. Bóng đổ sai khoảng cách ở cấp 3 (cao, lỗi cấu hình). Camera cách tâm 19,5 m (RoomLayout.cs:58), `m_ShadowDistance` 18 (Cao, URP_Mobile.asset:57) và 14 (Vừa): vùng sảnh nằm ngoài tầm bóng (cạnh gần ~15,5 m), nên Vừa gần như mất bóng ở cỡ zoom mặc định còn vẫn trả tiền pass bóng; Cao chỉ có bóng ở khoảng 1/3 gần camera, ranh giới bóng chạy khi pan/zoom/xoay. Đề xuất: Cao 30, Vừa 26 (hoặc gán `shadowDistance = camDistance + 8` lúc chạy) và đo lại; nếu bóng cấp 3 quá đắt thì tắt cast cho vật nhỏ (ly, cây nhỏ) và Vừa tắt bóng.
3. Số draw call (cao). Khách/nhân viên là mesh không skin nhiều phần (animationType 0, hoạt họa bằng Transform; CustomerVisual.cs:121–159): ước 6–10 renderer/người. Cấp 3 ước 400–700 renderer đang bật (đối chiếu dòng `[Perf] cảnh:` ở DebugPerfProbe.cs:113), bóng nhân đôi số lần vẽ; mục tiêu art bible < 150 batch (art_bible.md:217) khó đạt. 1 vật liệu, SRP Batcher bật (URP_Mobile.asset:72) nên SetPass thấp, nhưng renderer có MaterialPropertyBlock (CustomerVisual.cs:156,195; TableDirt.cs:176; CleaningSystem.cs:268) bị loại khỏi SRP Batcher. Đo bằng Profiler Rendering (Batches/SetPass). Hướng giảm: gộp mesh phần thân khi không cần tách khớp, tắt cast bóng chi tiết nhỏ, culling theo khu.
4. UI Canvas (vừa).
   - HudController.cs:507 gọi `SetRing` cho tối đa 4 thẻ mỗi khung, `fillAmount` đổi liên tục => HudCanvas (cả HUD, có Outline x5 đỉnh) dựng lại mỗi khung suốt lúc có khách chờ. OrderBubble.cs:91 đã có ngưỡng 0,004; thẻ HUD thì chưa. Sửa: ngưỡng 0,01 hoặc đặt các thẻ trong Canvas con riêng.
   - BubbleCanvas (HudController.cs:93) chứa mọi bong bóng, icon bàn bẩn, vòng chủ quán: `anchoredPosition` ghi mỗi khung (OrderBubble.cs:120, OwnerTaskIcons.cs:143, CleaningSystem.cs:339) nên xoay camera dựng lại cả canvas (~25 khách x 6 Graphic). Thêm bảo vệ `if ((new - old).sqrMagnitude > 0.25f)`.
   - `xuText` có `resizeTextForBestFit` (HudController.cs:201) và đổi chữ mỗi khung lúc đếm xu (513–515): sinh chuỗi + sinh lưới nhiều cỡ chữ. Chỉ cập nhật mỗi 3 khung hoặc bỏ BestFit cho chữ động.
   - Outline (HudBuilder.cs:291) nhân 5 số đỉnh chữ; mức Thấp đã tắt (GraphicsQuality.cs:119 chỉ chạy lúc đổi mức, chấp nhận được).
5. Khựng khung đã biết, đo PC rồi nhân 5–10 lần trên điện thoại (cao ở 120 FPS):
   - Bake NavMesh 2–9 ms PC => 10–45 ms phone, lưới 20x24 + lề 6 m (NavMeshBaker.cs:88 dùng `BuildNavMeshData` đồng bộ). Đổi sang `NavMeshBuilder.UpdateNavMeshDataAsync` (sửa nhỏ).
   - Lưu game đồng bộ `File.WriteAllText` (SaveSystem.cs:24), gọi từ 16 nơi: dựng JSON ở luồng chính, ghi file ở luồng nền.
   - Sinh khách: Instantiate + 6–7 lần `GetComponentsInChildren<Transform>` + `StartsWith` (CustomerVisual.cs:121,165) + AddComponent x5. Làm nóng trước từng prefab (pool khách) để mỗi khách < 0,3 ms.
6. NavMesh/vật lý (thấp): ~35 agent LowQualityObstacleAvoidance (Customer.cs:102) và Rigidbody kinematic + capsule cho mỗi người; nhân viên đã giãn nhịp tìm việc (`retryAt`). TimeManager `Maximum Allowed Timestep` 0,333 (TimeManager.asset:12) cho phép tới 16 bước vật lý sau một cú khựng: đặt 0,1.
7. Cấu hình chưa tối ưu: `androidBlitType: 0` = Always (ProjectSettings.asset:78) có thể thêm một blit toàn màn hình mỗi khung trên Android, thử Auto/Never và đo bằng AGI; `accelerometerFrequency: 60` (dòng 14) không dùng => 0; `m_UseAdaptivePerformance: 1` ở cả 3 URP asset nhưng manifest chỉ có module, không có package `com.unity.adaptiveperformance` và provider => cờ không có tác dụng; `QualitySettings.asset` mức Cao đổi `antiAliasing: 2` (dòng 131) thừa với URP (MSAA lấy từ URP asset), kiểm tra Xcode GPU Capture xem backbuffer có thêm MSAA không.

## C. Rà soát FPS 60/90/120 vừa thêm
| Mức | Chỗ | Vấn đề | Sửa |
|---|---|---|---|
| Cao | GraphicsPresets.cs:54–56 | Mốc 90 cho mọi màn >= 85 Hz. theo tài liệu Apple về ProMotion (nhớ lại, chưa kiểm) iPhone chạy theo ước của 240 (120, 80, 60, 48, 40, 30...), không có 90; Android 120 Hz không có chế độ 90 nhiều khả năng bị làm tròn về 60 hoặc 120 (Swappy chọn khoảng đổi nguyên) => nhịp 8,3/16,7 ms xen kẽ, đúng thứ gây chóng mặt. | `FpsChoices` chỉ gồm 60 và các tần số có thật trong `Screen.resolutions`, trên iOS chỉ {60, 120}; 90 chỉ khi có chế độ 90 Hz thật. Cập nhật test 90/119/144 (GraphicsPresetsTests.cs). |
| Cao | GraphicsQuality.cs:109–112 | Lấy max mọi chế độ: Samsung/OnePlus hay chỉ cho 120 Hz ở FHD+, còn QHD+ khóa 60 Hz; máy báo 120 nhưng không chạy được. Máy 144 Hz hiện 120 sẽ giật (144/120 = 1,2). | Lọc chế độ có cùng `width x height` với `Screen.currentResolution`; với 144 Hz dùng {72, 144} hoặc 60 mặc định. |
| Trung bình | GraphicsQuality.cs:99–104 | `targetFrameRate` không tự chuyển chế độ màn hình trên Android (do cài đặt hệ thống "Mượt/Thích ứng", Android 11+ cần `setFrameRate`). Chọn 120 có thể vẫn chạy 60 mà người chơi tưởng đã bật. Hơi thiếu: ProMotion bật 120 nhưng chế độ Pin thấp của iOS hạ về 60/30 và không có phản hồi. | Hiển thị FPS đo thực (EMA của `unscaledDeltaTime`) cạnh nút, và log `Screen.currentResolution.refreshRateRatio` trên máy thật. Đọc lại Hz khi `OnApplicationFocus`. |
| Trung bình | GraphicsQuality.cs:22, SettingsPanel | Cao + 120 FPS là tổ hợp nóng nhất (MSAA + bóng mềm 1024 ở độ phân giải gốc). | Chọn 120 thì tự dùng render scale <= 0,85 và tắt MSAA, hoặc ghi chú "tốn pin, máy nóng". |
| Thấp | GraphicsQuality.cs:101 | `vSyncCount = 0` đúng với Swappy và iOS; `androidUseSwappy: 1` đã bật (ProjectSettings.asset:74). `appleEnableProMotion: 1` đúng cách (dòng 261, ProjectSetup.cs:32). Cần kiểm iPad: không cần khóa này. | Không cần sửa. |
| Thấp | GraphicsQuality.cs:72 | `SetFps` khi đang ở mức Thấp lưu tuỳ chọn mà không có phản hồi. | Chữ ghi chú đã có; có thể làm mờ hàng nút. |

## D. Kế hoạch đo trên máy thật
- Bật: `enableFrameTimingStats: 1` (ProjectSettings.asset:160) và mở rộng DebugPerfProbe bằng `FrameTimingManager` (cpuMainThread, cpuRenderThread, gpuFrameTime ms; GPU time có thể trả 0 trên GLES hoặc máy không hỗ trợ); `ProfilerRecorder` cho `Gfx.WaitForPresentOnGfxThread` (đang chờ vsync hay chờ GPU), batches/SetPass/triangles; Development Build + Autoconnect Profiler (Deep Profile chỉ khi cần).
- iOS: Xcode Instruments (Game Performance, Metal System Trace, Thermal State), Metal HUD (`MTL_HUD_ENABLED=1`), GPU Frame Capture để xem backbuffer MSAA, băng thông, overdraw UI. Android: Android GPU Inspector (xung GPU, băng thông), Perfetto (khung giật SurfaceFlinger), `adb shell dumpsys gfxinfo`, `dumpsys thermalservice`; Adreno dùng Snapdragon Profiler, Mali dùng Arm Mobile Studio.
- Máy: 1 yếu (Android 4 GB, Mali-G52/Adreno 610), 1 tầm trung 120 Hz (Snapdragon 7 / Dimensity 7000), 1 iPhone ProMotion (13 Pro trở lên), 1 iPhone 60 Hz cũ.
- Kịch bản: cấp 3, 8 nhân viên, >= 20 khách ngồi, autoplay `-perf` cộng một đoạn script xoay camera đều 180 độ/s và mở/đóng panel pha chế; soak 15 phút từ máy nguội, mỗi mức đồ họa x mỗi mức FPS.
- Chỉ số: avg/p95/p99 thời gian khung, % khung > 1,5x mục tiêu, khựng dài nhất, CPU chính/render/GPU ms, batches, SetPass, tris, B/khung, nhiệt (thermalState iOS, `thermalstatus` Android) và % pin mất mỗi 10 phút.
- Ngưỡng hạ mức (60 FPS): p95 > 18 ms, p99 > 33 ms, > 5% khung > 20 ms, GPU > 12 ms, CPU chính > 10 ms, hoặc nhiệt từ Serious trở lên / pin > 12% mỗi 10 phút. 120 FPS: GPU > 6 ms, p95 > 9,5 ms.
- Tự hạ (đừng quá tay): một lớp ~60 dòng `GraphicsGovernor`. Mỗi giây tính trung bình trượt 5 s của `unscaledDeltaTime` (hoặc GPU ms từ FrameTimingManager); trễ 10 s sau mỗi lần đổi mức/chuyển cấp; nếu > 1,2x mục tiêu liên tục 5 s thì hạ một bậc theo thứ tự: FPS 120 -> 60, render scale -0,15 (tối thiểu 0,7, đặt qua `UniversalRenderPipelineAsset.renderScale`, không lưu PlayerPrefs), tắt MSAA, tắt bóng, rồi hạ mức. Không tự nâng lại (tránh dao động), chỉ nâng một lần sau 3 phút ổn định. Hiện toast "Máy hơi nóng, đã giảm đồ họa" và có công tắc Tự động trong Cài đặt. Nhiệt chính xác: Android dùng Adaptive Performance (cần thêm package + provider, hiện chưa có); iOS cần plugin native 15 dòng đọc `NSProcessInfo.thermalState`. Bắt đầu bằng khung thời gian, chưa cần nhiệt.

## E. 8 việc đáng làm nhất
| # | Việc | Công sức | Rủi ro |
|---|---|---|---|
| 1 | Chỉnh camera theo A1–A4, A12 (độ lợi 0,12, văng 360/6/15, SmoothDamp 0,03/0,12, lọc 60 ms theo dt, chặn dt, trần độ/khung) | Nhỏ | Thấp: đổi cảm giác, cần chơi thử 3 máy |
| 2 | Phân xử cử chỉ: vùng chết xoay 10 độ, sắp theo `fingerId`, khóa trục dọc/ngang, vùng chết 1 ngón; lens shift SmoothDamp; trượt FocusOn; `DefaultExecutionOrder` | Vừa | Thấp-vừa |
| 3 | Công tắc "Giảm chuyển động" (Cài đặt, PlayerPrefs) | Nhỏ-Vừa | Thấp |
| 4 | Sửa `FpsChoices`/`ReadDisplayHz` (bỏ 90 trên iOS, lọc theo độ phân giải, hiện FPS thực) + cập nhật test | Nhỏ | Thấp |
| 5 | Bật FrameTimingManager + mở rộng DebugPerfProbe, đo theo mục D trước khi chỉnh GPU | Vừa | Thấp |
| 6 | HUD: ngưỡng `SetRing` 0,01, bảo vệ `anchoredPosition`, giãn cập nhật `xuText` | Nhỏ | Thấp |
| 7 | Bóng cấp 3 (Cao 30, Vừa 26 hoặc theo khoảng cách camera), NavMesh bake bất đồng bộ, lưu game ghi ở luồng nền, đặt `Maximum Allowed Timestep` 0,1 | Nhỏ-Vừa | Vừa: bóng xa hơn tốn GPU hơn, phải đo |
| 8 | `GraphicsGovernor` tự hạ FPS/độ nét; Android Blit Type thử Auto | Vừa | Vừa: dao động mức, UX; làm sau mục 5 |

## F. Suy luận từ code, chưa đo
- Mọi số ms/GPU ở mục B và các ước số renderer (400–700), draw call, nhiệt đều là ước lượng từ cấu hình và quy tắc kinh nghiệm; chưa có số điện thoại thật. Số duy nhất đo là CPU script PC (phase3_spec §0.5, 0,44–0,49 ms) và ~62k tam giác.
- A1–A9 tính từ công thức trong code (độ lợi, trễ SmoothDamp = smoothTime ở tốc độ đều, quãng quán tính v/k); chưa kiểm cảm giác trên máy. Chưa chứng minh camera là nguyên nhân duy nhất; Thấp 30 FPS, răng cưa nhấp nháy và khựng khung cũng có thể góp.
- A5 (đổi thứ tự touch), A11 (thứ tự LateUpdate), bóng cấp 3 (tính khoảng cách 19,5 m vs 18/14 m) là suy luận; cần quay thử và Frame Debugger để xác nhận.
- Mục C: danh sách tần số của iPhone ProMotion, hành vi `targetFrameRate` trên iOS/Android Swappy, và việc `Screen.resolutions` trả gì trên iOS/Android đều từ kiến thức nền, chưa kiểm trên máy; phải log `Screen.currentResolution` và đo FPS thực trước khi tin.
- `QualitySettings.antiAliasing` có bị URP dùng hay không, `androidBlitType` có thêm blit hay không: chưa xác nhận, chỉ nghi.
