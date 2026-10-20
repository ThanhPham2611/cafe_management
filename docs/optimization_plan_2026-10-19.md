# Kế hoạch tối ưu hiệu năng (2026-10-19)

Nguồn: `docs/performance_report_2026-10-19.md` (đo trên Apple M5, bản Development macOS, KHÔNG có số điện thoại). Tài liệu này đã đối chiếu từng nhận định với mã, rồi chia việc thành Phase A/B/C. Chỉ đọc mã khi lập kế hoạch, chưa sửa gì. Nhãn: **[ĐO]** số trong báo cáo (M5, nhiễu ±15%), **[ƯỚC]** suy ra/quy tắc kinh nghiệm, **[CHƯA]** chưa kiểm chứng (cần Profiler/máy thật), **[MÃ]** đọc từ mã.

## 0. Kiểm chứng báo cáo: đúng, sai, thiếu
Đúng (đã đối chiếu): `Recommend` chỉ xét RAM + nhân (GraphicsPresets.cs:121-127); vòng đèn phụ per-pixel (Toon_Palette.shader:129-139, keyword dòng 59); sàn là MỘT mesh toàn phòng (EnvironmentBuilder.cs:83-91); URP asset Cao/Vừa/Thấp: đèn phụ 8/4/tắt, MSAA 2/1/1, renderScale 1/0,85/0,7, bóng 1024/512/tắt (URP_Mobile*.asset dòng 28-29, 46-48, 57); `m_UseSRPBatcher: 1`; Forward thường (`URP_Mobile_Renderer.asset:49 m_RenderingMode: 0`); không có `ShaderVariantCollection` (`GraphicsSettings.asset:43`); `enableFrameTimingStats: 0` (ProjectSettings.asset:160); `accelerometerFrequency: 60` (:14); `androidBlitType: 0` (:78); không có `GraphicsGovernor`; FloatLayer đã pool (FloatLayer.cs:6-9); `Camera.main`/Find không nằm trong Update.

Cần sửa/bổ sung:
1. **Sàn/tường đã KHÔNG đổ bóng** (EnvironmentBuilder.cs:91,165,178,308-309,390; ZoneEnvironment.cs:204). ~95% renderer đổ bóng là **đồ nội thất** (ArtImporter.cs:172 ép `On` cho mọi prefab) và **nhân vật** (CharacterImporter.cs:130,178,211). Vậy "chính sách đổ bóng cho sàn/tường" là vô nghĩa; đòn bẩy thật là khách (60-88 người x ~6-10 renderer **[ƯỚC]**, 6 khớp Hips/Head/Arm_L/Arm_R/Leg_L/Leg_R theo CustomerVisual.cs:109-114) chiếm đa số trong 845-978 caster. Tỉ lệ chính xác theo loại: **[CHƯA]**, bước A6 đo.
2. Tường đã tách theo từng cạnh (Lower/Upper, EnvironmentBuilder.cs:306-307); chỉ **sàn** là mesh khổng lồ. Việc "chia sàn/tường" thu về "chia sàn".
3. Shader early-out `if (lamp.distanceAttenuation < ...) continue;` chỉ tiết kiệm phần `dot` + 2 `smoothstep` + `lerp`; việc nạp dữ liệu đèn và tính suy giảm nằm ngay trong `GetAdditionalLight` nên vẫn chạy. Lợi thật nhỏ hơn mô tả "giảm phần lớn" **[ƯỚC ~30-40% vòng đèn ở pixel ngoài tầm]**, lại thêm rẽ nhánh phân kỳ. Cách tốt hơn: B3 (hồ đèn riêng, kiểm tầm bằng 1 phép so sánh trước khi làm gì).
4. Phần CPU của đèn đêm (+0,5-1,2 ms Cao) được báo cáo gán cho "lọc đèn theo ~900 renderer" bằng **loại trừ**, chưa từng được Profiler xác nhận **[CHƯA]**. Phải xác nhận ở A0 trước khi đầu tư B3.
5. Sinh khách: `GetComponentsInChildren<Transform>` chạy **7 lần/khách**, không phải 2 (1 lần tìm HandSocket ở CustomerVisual.cs:121 + `Find` x6 ở :109-114, hàm :163-171), kèm `StartsWith` x6 mỗi lần. Ước lượng GC cấp 3 (230-660 B/khung) có thể phần lớn đến từ đây khi `-timescale 4` **[ƯỚC]**; thực tế 1x thấp hơn.
6. HUD: setter UGUI (`Image.color`, `fillAmount`) đã so sánh giá trị cũ nên "ghi `color` mỗi khung" (HudBuilder.cs:262-263) chỉ tốn phép so sánh; thứ làm dựng lại HudCanvas là **`fillAmount` đổi liên tục** (HudController.cs:738,777; tối đa 4 thẻ). Bong bóng nằm trên `BubbleCanvas` riêng (HudController.cs:142), nên `anchoredPosition` của chúng (OrderBubble.cs:132, CupCollectBubble.cs:75, OwnerTaskIcons.cs:143) chỉ làm bẩn BubbleCanvas, không phải HudCanvas.
7. ASTC: `UiSpriteImporter.OnPreprocessTexture` (UiSpriteImporter.cs:61) ép `Uncompressed` ở mọi PNG dưới `Art/UI` (cả Logo) mỗi lần nhập, và `BrandingSetup.cs:86` cũng vậy. Sửa tay `.meta` không bền; phải sửa importer. **Bỏ sót:** `Art/UI/Logo/logo_wide.png` là 2048x1024 (~8 MB RGBA32 khi nạp) được `Boot.unity` tham chiếu; có thể là ảnh lớn nhất, và khả năng chưa tính trong "42 icon + 33 UI". Số bộ nhớ thực: **[CHƯA]**, đo bằng Memory Profiler/BuildReport.
8. `maximumDeltaTime` nằm ở `ProjectSettings/TimeManager.asset:12`, không phải ProjectSettings.asset. Không thấy `Input.acceleration` nào (activeInputHandler 0) nên tắt accelerometer là an toàn **[MÃ]**. Khách/nhân viên có `Rigidbody` kinematic + collider (Customer.cs:96-103, StaffWorker.cs:75) và ma trận va chạm vật lý mở hoàn toàn (DynamicsManager.asset:20): chi phí PhysX **[CHƯA]**, xem mục 3.
9. `ShowToast` bị nuốt khi thẻ hướng dẫn đang hiện (HudController.cs:708-709): toast tự hạ mức cần dự phòng (mục B1).
10. Số file có Update/LateUpdate/FixedUpdate là 33 (gồm Debug), không phải ~31; không đổi kết luận.

## 1. Phase A (tuần này; rẻ, an toàn, đo được)
Thứ tự đề nghị: A0 -> A1 -> A2 -> A3+A4 -> A5 -> A6.

### A0. Đưa bản vá đo máy thật vào repo (S, rủi ro ~0, tiên quyết cho mọi mục khác)
- Mục tiêu: có CPU/GPU ms, draw call, SetPass, tam giác, shadow caster theo loại trên máy thật bằng đúng một cách đo.
- Sửa: `Scripts/Core/DebugPerfProbe.cs` (chỉ `#if UNITY_EDITOR || DEVELOPMENT_BUILD`): port đoạn `FrameTimingManager.CaptureFrameTimings/GetLatestTimings` và `ProfilerRecorder` (Draw Calls Count, SetPass Calls Count, Triangles Count) đã có ở `/tmp/perf_build/proj/Assets/_Project/Scripts/Core/DebugPerfProbe.cs` (có thể đã mất; lấy lại từ `perf_report` nếu cần). `ProjectSettings.asset:160 enableFrameTimingStats: 1` (chỉ ảnh hưởng chi phí nhỏ; nếu ngại, bật bằng `PlayerSettings.enableFrameTimingStats = true` trong `BuildTools.cs` cho nhánh `BuildOptions.Development`).
- Thêm vào probe: ghi p50/p95/p99 khung, `Screen.currentResolution`, `Application.targetFrameRate`, `GraphicsQuality.Current`, `SystemInfo.graphicsDeviceName/graphicsDeviceType`, thermal (Android: đọc ngoài bằng `adb shell dumpsys thermalservice`; iOS: Xcode).
- Đo: chạy bot như báo cáo (`-perf -autoplay -autoupgrade3 -cheatt3 -autostaff -fullstaff -autobuy -bestmenu -autohidden -upgradestaff -autozones -t3xu 200000 -gfx <mức> -dayhour 12|21 -timescale 4`). Trên Android đẩy tham số bằng `adb shell am start -n <pkg>/com.unity3d.player.UnityPlayerGameActivity -e unity "<args>"`; iOS qua Xcode scheme Arguments **[CHƯA kiểm chứng trong dự án này]**.
- Chấp nhận: một lần chạy 200 s in được avg/p95/p99 CPU, GPU, draw call trên cả Mac và Android; mỗi cấu hình đo >= 3 lượt, máy Mac nghỉ (load < 2) để tránh nhiễu ±15%.
- Đồng thời: chụp Profiler (Timeline) 1 lần đêm cấp 3 Cao để **xác nhận nguồn +0,5-1,2 ms CPU** (marker `UniversalRenderPipeline.SetupLights`/`Culling`/`RenderLoop`). Kết quả này quyết định có làm B3 hay không.

### A1. Chặn Cao tự động theo GPU (S, rủi ro thấp)
- Mục tiêu: máy 6-8 GB RAM nhưng GPU yếu không bị xếp Cao. Gợi ý Cao chỉ khi nhận diện **chắc chắn** GPU mạnh.
- Mã mới: `Scripts/Core/GpuTiers.cs` (thuần logic, không đụng Unity): `static GraphicsLevel MaxLevel(string gpuName, bool mobile)`. Sửa `GraphicsQuality.Init` (GraphicsQuality.cs:55): `Recommended = GraphicsPresets.Min(Recommend(...), GpuTiers.MaxLevel(SystemInfo.graphicsDeviceName, IsMobile))`. `Recommend` giữ nguyên chữ ký nên 8 TestCase cũ (GraphicsPresetsTests.cs:72-83) không đổi.
- Bảng (hằng số, chỉnh được không cần đổi logic; so khớp không phân biệt hoa thường, lấy dãy số đầu tiên sau tên họ GPU):
  - Desktop: không giới hạn. Chuỗi rỗng/null trên điện thoại: **Vừa** (mặc định an toàn).
  - `Adreno`: n < 610 -> Thấp; 610 <= n < 650 hoặc n in {662, 663, 702, 710, 720, 722} -> Vừa; n in {650, 660} hoặc n >= 730 -> Cao.
  - `Mali-G`: n >= 710 (G710/G715/G720/G925) -> Cao; còn lại và `Mali-T`/`Mali-4` -> Vừa (T/4xx -> Thấp). `Immortalis`, `Xclipse` -> Cao. `PowerVR` -> Thấp.
  - `Apple M...` -> Cao; `Apple A...` -> không thêm giới hạn (quy tắc nhân đã chặn iPhone khỏi Cao, 6 nhân).
  - Nguồn các ngưỡng là kiến thức chung về dòng chip, **[CHƯA]** xác nhận bằng máy; bảng là dữ liệu để chỉnh sau đợt thử máy thật.
- KHÔNG đổi ngưỡng RAM 5500 -> 7000 (mục 4 báo cáo): GPU cap + governor đã phủ trường hợp, và `systemMemorySize` báo lệch giữa các máy cùng model.
- Không dùng `SystemInfo.graphicsMemorySize` làm tín hiệu (trên di động thường là phần RAM dùng chung **[CHƯA]**).
- Test EditMode mới `GpuTiersTests.cs`: ~15 tên thật ("Adreno (TM) 610", "Adreno (TM) 740", "Mali-G52 MC2", "Mali-G715", "Immortalis-G715", "Apple A15 GPU", "Apple M2 GPU", "", null), desktop luôn Cao, `Min`; cộng 2 TestCase `Recommend` đầu-cuối.
- Đo trước/sau: log `[Gfx]` đã in mức; thêm tên GPU vào log đó. Chấp nhận: bảng máy x mức (báo cáo mục 3.2) không còn "Cao (quá nặng)" ở Android tầm trung; không đổi gì trên Mac/iPad M.

### A2. ASTC cho icon/sprite UI và logo (S, rủi ro thấp)
- Mục tiêu: -9 đến -11 MB bộ nhớ ảnh **[ƯỚC]**, thêm ~-7,5 MB nếu `logo_wide` thực sự nằm trong bản dựng, ~-8 MB bản cài **[ƯỚC]**.
- Sửa: `Editor/UiSpriteImporter.cs:61` và `Editor/BrandingSetup.cs:86`. Mặc định `textureCompression = Compressed` cho tab mặc định và đặt tường minh bằng `ti.SetPlatformTextureSettings(...)` cho `Android` và `iOS`: `TextureImporterFormat.ASTC_6x6` cho `Art/UI/Shop/ICON_*.png` (42 ảnh 256x256), `ASTC_4x4` cho 33 sprite UI 9-slice (viền sắc) và `ui_hand_pointer`, `ASTC_6x6` cho `logo_wide` (2048x1024; thử 8x8 nếu viền chữ vẫn ổn). Giữ: không mip (ảnh UI), `crunchedCompression` tắt, `maxTextureSize` 256/128 tương ứng, `T_Palette_256.png` GIỮ không nén (ArtImporter.cs:302, lem màu giữa các ô). Chạy menu `Cafe/UI/Reimport UI Sprites`; sẽ đổi ~81 `.meta` (commit riêng).
- Rủi ro: viền 9-slice bị bleed nếu khối 6x6 cắt ngang viền; ảnh có chữ nhỏ. Kiểm bằng ảnh chụp trước/sau tại Cửa hàng, thanh HUD, màn tiêu đề. Editor/Mac dùng DXT/ASTC giải mã mềm nên chỉ so sánh bằng bản Android/iOS.
- Đo: Memory Profiler (Texture2D tổng) và `BuildReport` (Textures); kích thước AAB/IPA. Chấp nhận: tổng texture <= trước - 9 MB (không tính logo), không khác biệt thị giác ở ảnh chụp 3 màn.

### A3. HUD: giảm làm bẩn canvas (S, rủi ro thấp)
- Sửa `HudBuilder.SetRing` (HudBuilder.cs:260): lượng tử `fillAmount` theo bước 1/50 (hoặc đổi chỉ khi |chênh| >= 0,02); `color` chỉ đặt khi đổi bậc (PatienceColor). Vòng kiên nhẫn đi hết trong ~30-60 s nên giảm từ ~60 thay đổi/s xuống ~1-2 thay đổi/s mỗi thẻ. Cùng ý cho vòng tiến độ chủ quán (CleaningSystem.cs:363-370).
- Bong bóng: làm tròn `anchoredPosition` đến 0,5 px và chỉ ghi khi lệch (OrderBubble.cs:132, CupCollectBubble.cs:75, OwnerTaskIcons.cs:143, CleaningSystem.cs:339). Khách ngồi yên nên phần lớn khung không ghi; khi xoay camera vẫn ghi (đúng).
- Đo (A0 + Profiler): `Canvas.BuildBatch`, `Canvas.SendWillRenderCanvases`, `Canvas.RenderOverlays`, với 4 thẻ chờ + 40 khách ngồi, camera đứng yên. Chấp nhận: giảm >= 50% thời gian rebuild của HudCanvas. Nếu tổng < 0,05 ms trên M5 sau khi đo thì giữ thay đổi (rẻ) nhưng không cần làm thêm (chia canvas = Phase C). Lợi ước tính: 0,1-0,3 ms M5, x4-8 trên máy yếu **[ƯỚC, CHƯA đo]**.
- Test EditMode: hàm lượng tử thuần (`HudRules.QuantizeRing(float)`): biên 0, 1, giá trị âm, giá trị gần bước.

### A4. Cài đặt dự án (S, rủi ro rất thấp)
- `ProjectSettings/TimeManager.asset:12 Maximum Allowed Timestep: 0.1` (hiện 0,333): không còn bước dồn sau khựng. Hệ quả: sau khựng > 100 ms, thời gian trò chơi chậm lại thay vì nhảy bước; kiểm cùng `-timescale 4` (khung ở timescale 4 vẫn nhỏ, không ảnh hưởng).
- `ProjectSettings.asset:14 accelerometerFrequency: 0` (không có mã dùng cảm biến).
- Không đổi `androidBlitType` hay Vulkan/GLES khi chưa có AGI (Phase C).
- Chấp nhận: bot 100 ngày cho cùng kinh tế (23,7) +- sai số; không đổi số liệu sinh khách.

### A5. Warm-up biến thể shader (S, rủi ro thấp)
- Tạo `ShaderVariantCollection` (hoặc `GraphicsStateCollection` của Unity 6) gồm `Toon_Palette` ForwardLit với `_ADDITIONAL_LIGHTS` x {bóng On/Off, soft} cho Vừa/Cao; thêm vào `GraphicsSettings > Preloaded shaders` và gọi `WarmUp()` trong lúc Boot (tìm nơi trong `Boot/BootLoader.cs`). Nếu làm B3 (hồ đèn riêng) thì làm A5 SAU B3, vì biến thể thay đổi.
- Đo: khung dài nhất khi chuyển sang tối lần đầu trên máy thật (`dayhour 17 -> 19`), trước/sau. Chấp nhận: không khung > 3x trung vị trong 10 s đầu của đêm đầu **[CHƯA có số nền]**.

### A6. Đo caster theo loại (S, chỉ Development)
- `DebugPerfProbe.cs:94-98`: đếm `shadowCastingMode != Off` tách theo loại (tên cha `Customer`, `StaffWorker`, `Owner`, `PlacedFurniture` + kích thước bounds, còn lại) và số renderer/khách (một lần log). Dữ liệu quyết định B2. Chấp nhận: bảng "caster theo loại" ở Cao/Vừa, cấp 3 đông khách.

## 2. Phase B (vài tuần; chỉ làm khi A0/A6 ủng hộ)

### B1. Bộ tự hạ mức theo thời gian khung `GraphicsGovernor` (M, rủi ro vừa)
- Mục tiêu: máy lạ vẫn chơi được mà không phải chờ người chơi mò Cài đặt.
- Mã mới: `Scripts/Core/GraphicsGovernorRules.cs` (thuần, test được) + `Scripts/Core/GraphicsGovernor.cs` (MonoBehaviour mỏng, tạo ở `GameBootstrap` ngay sau `GraphicsQuality.Init`, GameBootstrap.cs:113; chỉ điện thoại). Sửa `GraphicsQuality`: thêm `AutoDowngrade(GraphicsLevel)` gọi `Apply` + ghi `cafe.gfxcap` (PlayerPrefs) nhưng KHÔNG ghi `cafe.graphics`; `Init` (dòng 60-63): nếu chưa có khóa `cafe.graphics` thì `level = Min(Recommended, cap)`.
- Luật (hằng số, mọi số điều chỉnh được sau khi có dữ liệu máy thật):
  - Mẫu `Time.unscaledDeltaTime` vào mảng cố định (không cấp phát); cửa sổ 3 s; lấy **trung vị** (chống cú khựng đơn lẻ như bake NavMesh/lưu).
  - Ngân sách khung = `1000 / min(TargetFps, DisplayHz)`; cửa sổ "xấu" khi trung vị > 1,35 x ngân sách (Vừa/Cao: ~22,5 ms; Thấp 30 FPS: ~45 ms).
  - Bỏ qua 15 s đầu sau lúc vào ngày/sau `Apply`, khi `Time.timeScale == 0` hay tiêu điểm vừa trở lại (`OnApplicationFocus`), và khi thẻ hướng dẫn/hộp thoại đang mở.
  - Hạ một bậc sau **3 cửa sổ xấu liên tiếp** (9 s); nghỉ 60 s sau mỗi lần hạ; tối đa 2 lần hạ mỗi phiên; **không bao giờ tự nâng** trong phiên; Thấp là sàn.
  - Người chơi đã chọn mức trong Cài đặt (`cafe.graphics` tồn tại) -> governor tắt hẳn (tôn trọng lựa chọn). Chọn lại "Gợi ý cho máy bạn" xoá `cafe.gfxcap`.
  - Cờ tắt: hằng `GraphicsGovernor.Enabled` + dev `-nogovernor`; dev `-govtest <ms>` bơm tải giả (busy-wait) để thử.
- Thông báo: `GameEvents.RaiseToast(Strings.GfxAutoDowngradeFmt)`, thêm vào Core/Strings.cs, ví dụ "Máy hơi nặng, đã chuyển đồ họa sang {0} cho mượt. Đổi lại ở nút ĐỒ HỌA." Vì `ShowToast` bị nuốt khi thẻ hướng dẫn hiện (HudController.cs:708), giữ cờ `pendingToast` và phát lại khi thẻ đóng; thêm nhãn "Tự hạ do máy nặng" ở badge SettingsPanel.cs:211.
- Cảnh báo kỹ thuật: `Apply` gọi `QualitySettings.SetQualityLevel(index, true)` + `FindObjectsByType<Outline>` (GraphicsQuality.cs:109-112, 152) nên sẽ có một khung khựng khi hạ **[CHƯA đo trên máy thật; ước 50-300 ms]**; đo và nếu lớn thì chọn thời điểm hạ (giữa hai ngày, ở màn Tổng kết). iOS Low Power Mode/Android tiết kiệm pin hạ nhịp màn hình sẽ khiến governor hạ mức; chấp nhận được nhưng phải thử.
- Test EditMode `GraphicsGovernorRulesTests`: không hạ trong grace; hạ sau đúng 3 cửa sổ xấu liên tiếp; 1 cửa sổ tốt xen vào thì reset bộ đếm; nghỉ 60 s; max 2 lần; không nâng; sàn Thấp; trung vị bền với 1 khung 300 ms; ngân sách theo `DisplayHz`; tắt khi `userChose`.
- Chấp nhận: máy dev ép tải giả 30 ms: hạ Cao -> Vừa -> Thấp tối đa 2 lần, không dao động; chạy 15 phút bình thường trên M5 không hạ lần nào.

### B2. Chính sách đổ bóng theo loại renderer (M, rủi ro vừa; làm sau A6)
- Mục tiêu: -20-35% draw call Vừa/Cao theo báo cáo, **[ƯỚC]**; thực tế phụ thuộc tỉ lệ khách trong caster (A6).
- Mã mới: `ShadowPolicy.Casts(GraphicsLevel, ShadowCategory)` thuần + test; `ShadowCategory { Environment, FurnitureLarge, FurnitureSmall, Character, Prop }`. Áp dụng khi đặt/dời đồ (`PlacedFurniture`), khi khách/nhân viên được dựng (`CustomerVisual.Build`) và khi `GraphicsQuality.Changed`. Chỉ đụng `Renderer.shadowCastingMode`, không đụng prefab/`ArtImporter` (giữ `On` làm mặc định cao nhất).
- Đề xuất ban đầu (duyệt art): Thấp: tắt tất cả (đã vậy qua URP). Vừa: đồ lớn (cao >= 0,5 m hoặc thể tích lớn) + chủ quán + nhân viên cast; đồ nhỏ (ly, cây nhỏ, phụ kiện) tắt; **khách thay bằng bóng giả** (một quad tối dưới chân, một vật liệu Unlit dùng chung, `ShadowCastingMode.Off`): 1 draw call thay cho 6-10 lượt caster. Cao: giữ nguyên (đồ nhỏ tắt nếu A6 cho thấy đáng).
- Đo: draw call Vừa cấp 3 đêm (báo cáo: 1370-1410), GPU ms, ảnh chụp trước/sau cho duyệt art. Chấp nhận: draw call Vừa -20% so với baseline cùng save, GPU không tệ hơn, art duyệt.

### B3. Đèn đêm: hồ đèn riêng thay cho đèn phụ URP (M-L, rủi ro vừa; chỉ làm nếu A0 xác nhận chi phí là lọc đèn theo renderer)
- Ý tưởng: bỏ `Light` + per-object light culling của URP; `LampLights` gửi tối đa 8 đèn qua **global shader array** (`_CafeLampPos[8]` xyz + 1/r², `_CafeLampCol[8]`, `_CafeLampCount`); shader lặp tối đa `_CafeLampCount` lần với kiểm tầm `dsq > r2 -> continue` rẻ (3-4 ALU) trước khi tính bậc/`smoothstep`. Bỏ `_ADDITIONAL_LIGHTS` (đặt `m_AdditionalLightsRenderingMode: 0` ở cả 3 URP asset, UrpSetup.cs đồng bộ), thay bằng keyword toàn cục `_CAFE_LAMPS` (Vừa/Cao bật; Thấp tắt). Kèm lợi phụ: biến thể shader ít hơn (128 -> 10 đã strip), warm-up (A5) đơn giản hơn.
- Sửa: `Toon_Palette.shader` (vòng lặp dòng 129-139 + `multi_compile`), `LampLights.cs` (thay `pool` bằng mảng `Vector4[]`; `Assign` vẫn chọn đèn gần tâm, dòng 77-100; cập nhật mảng chỉ khi `dirty`, cường độ theo `night` là 1 float global), `URP_Mobile*.asset`, `GraphicsPresets` (tiện bỏ ràng buộc "<= PerObjectLimit"), `UrpSetup.cs`. Giữ đường cũ sau hằng `LampLights.UsePool` đến khi xác nhận hình ảnh.
- Kỳ vọng **[ƯỚC]**: ngày/đêm CPU Cao từ +0,5-1,2 ms còn <= +0,3 ms; GPU từ +0,26-0,30 ms còn <= +0,15 ms (kiểm tầm sớm cho pixel sàn xa đèn). Rủi ro: công thức suy giảm khác URP, đèn nhìn khác (cần duyệt art, so ảnh chụp đêm 3 cấp, 3 mức); rủi ro dùng chung SRP Batcher thấp vì mảng là global, không nằm `UnityPerMaterial` (đúng như `_CafeNight`, shader:29-41).
- Phương án rẻ thay thế (S-M): chia **sàn** thành ô ~4 m (cho `AddUpQuad` trong `BuildInteriorFloor`, EnvironmentBuilder.cs:83-91) để mỗi ô chỉ nhận đèn trong tầm bounds của nó; đổi mesh sàn nên phải kiểm họa tiết gạch/parquet, `NavMeshBaker` (nguồn bake) và `Floor` được tham chiếu bởi tên "Floor". Lợi ước tính 20-40% ở sàn, vì đèn chọn "gần tâm nhất" tụ ở giữa quán nên ô giữa vẫn thấy nhiều đèn. Chọn B3 nếu CPU là vấn đề, chia sàn nếu chỉ GPU.

### B4. Dựng khách rẻ hơn (S-M, rủi ro thấp)
- Sửa `CustomerVisual.Build` (CustomerVisual.cs:82-133): tìm `Hips/Head/Arm_*/Leg_*/HandSocket_R` một lần/prefab (cache chỉ số con theo `GameObject prefab`, hoặc duyệt đệ quy một lượt không cấp phát) rồi tái dùng; bỏ 6 `StartsWith` lặp. Pre-warm Instantiate bằng pool nhỏ nếu B4a cho thấy `Instantiate` mới là phần lớn (đã `Destroy` ở Customer.cs:474).
- Đo: `[Perf] việc nặng` mục "sinh khách" (báo cáo: TB 0,6-0,9 ms, max 16,6) và B/khung cấp 3. Chấp nhận: sinh khách TB <= 0,3 ms; B/khung cấp 3 giảm >= 30% (**[ƯỚC]**; nguồn GC chưa gán chắc chắn: dùng Profiler call stack ở Development để xác nhận).

### B5. Giảm nhịp cập nhật khách ngồi (S-M, chỉ nếu Profiler cho thấy `Customer.Update` đáng kể)
- Bằng chứng: `Customer.Update` gọi `visual.Tick` mỗi khung cho mọi khách (Customer.cs:124-143), ghi ~7 Transform/khách (CustomerVisual.cs:216-260) kể cả khi ngồi chỉ để "thở" (`breathe`). Khoảng 60-88 khách x ~10 ghi = ~600-900 ghi transform/khung ở cấp 3 **[CHƯA đo]**.
- Đề xuất: khách `Sitting/WaitOrder/Drink` tick 30 Hz so le theo id (`dt` tích luỹ); đi bộ vẫn mỗi khung. Chấp nhận: `Customer.Update` self ms -40%, không thấy giật hình chuyển động.

## 3. Phase C (chỉ khi dữ liệu máy thật cho thấy cần)
| Việc | Điều kiện kích hoạt | Ghi chú |
|---|---|---|
| Hạ `renderScale` Vừa 0,85 -> 0,75 / Cao 1,0 -> 0,9 | GPU p95 > 12 ms trên máy tầm trung | S, mờ hơn; thử trước khi hạ thêm mức |
| `_ADDITIONAL_LIGHTS_VERTEX` / đèn per-vertex cho Vừa | Đêm Vừa GPU cao sau B3 | sàn ít đỉnh nên ánh đèn thô; hợp sau khi chia sàn |
| Bake NavMesh async, lưu game ở luồng nền | Khựng > 50 ms khi đặt đồ/lưu trên máy thật (NavMeshBaker.cs:103, SaveStore.cs:91-126) | S-M, cẩn thận race |
| Layer vật lý cho khách/nhân viên, tắt va chạm giữa chúng (Rigidbody kinematic x ~90) | Profiler thấy `Physics.Simulate`/`SyncTransforms` > 0,3 ms | S; raycast chạm dùng mask nên cần layer riêng `Tap` |
| Tách canvas HUD (thẻ order, chip) | `SendWillRenderCanvases` > 1 ms sau A3 | M |
| Chọn Vulkan/GLES3 theo thiết bị (`m_BuildTargetGraphicsAPIs: []`, mặc định Unity) | AGI/crash thấy driver Vulkan cũ lỗi hoặc blit thừa | S, đo trước; kèm `androidBlitType` |
| SpriteAtlas cho UI/icon | Batch UI cao khi mở Cửa hàng | S-M |
| `DayNightController.Apply` chỉ ghi khi đổi (DayNightController.cs:56-86 ghi `RenderSettings` ambient mỗi 4/10/20 Hz kể cả lúc trời không đổi) | Profiler thấy đáng kể | S, lợi nhỏ **[CHƯA]** |
| GPU Resident Drawer/instancing/static batching | Draw call > 1000 mà CPU render thread là cổ chai sau B2/B3 | M-L, rủi ro > lợi; đồ do người chơi đặt nên static batching không hợp |

## 4. Phân PR/commit và thứ tự
1. PR1 "perf: probe FrameTiming + caster theo loại" (A0, A6; test: không). Chạy đo nền Mac + Android + iPhone cũ.
2. PR2 "gfx: GpuTiers + Recommend cap" (A1 + `GpuTiersTests`).
3. PR3 "art: ASTC cho icon/UI/logo" (A2: 1 commit importer + 1 commit `.meta`/re-import để dễ revert).
4. PR4 "ui: lượng tử vòng kiên nhẫn/bubble" (A3 + `HudRulesTests`) và PR4b "settings: timestep, accelerometer" (A4).
5. PR5 "perf: warm-up shader" (A5) -- sau PR7 nếu làm B3.
6. PR6 "gfx: GraphicsGovernor" (B1 + `GraphicsGovernorRulesTests` + chuỗi `Strings`).
7. PR7 "gfx: ShadowPolicy" (B2 + `ShadowPolicyTests`), PR8 "gfx: hồ đèn global" (B3, giữ đường cũ sau cờ), PR9 "customer: dựng/tick rẻ hơn" (B4, B5).
Mỗi PR kèm bảng trước/sau theo mục 6; chạy EditMode toàn bộ (`Cafe.Tests`) trước khi gộp.

Thứ tự lý do: A0 trước vì mọi quyết định dựa vào số máy thật; A1 là phòng tuyến đầu rẻ nhất; governor (B1) là lưới an toàn nên làm sau khi biết ngưỡng từ A0.

## 5. Cờ tắt / hoàn tác
- A1: sửa bảng `GpuTiers` hoặc `MaxLevel` trả `High` (hoàn tác 1 dòng). A2: revert commit importer + `.meta`. A3/A4: revert commit. B1: `GraphicsGovernor.Enabled = false`; khoá `cafe.gfxcap` bị bỏ qua khi governor tắt (không kẹt mức thấp). B2: `ShadowPolicy.Enabled` hoặc bảng trả `true` cho mọi loại. B3: `LampLights.UsePool = false` quay lại đèn URP (giữ asset ở chế độ per-pixel tới khi hết cờ).
- Mọi thay đổi người chơi nhìn thấy (bóng, đèn) phải đi kèm ảnh chụp trước/sau (`docs/screenshots`) và duyệt art.

## 6. Ma trận máy thật và quy trình đo
Máy: D1 = Android RAM 3-4 GB, Mali-G52/Adreno 610 (720p-1080p); D2 = iPhone cũ (8/XR/11); tuỳ chọn D3 = Android tầm trung 6-8 GB (SD 7xx); iPad cũ nếu có.
Kịch bản (mỗi ô 200 s, >= 3 lượt, thiết bị nguội): {cấp 1, cấp 3 đông khách (60-88 khách, `-t3xu 200000 -cheatt3 ...`)} x {`-dayhour 12`, `-dayhour 21`} x {Thấp, Vừa, Cao (ép bằng `-gfx`)}; thêm 1 lượt "mặc định" (không `-gfx`) để kiểm A1 + B1.
Số cần ghi: avg/p95/p99 khung, % khung > 1,5x ngân sách, CPU chính/render, GPU ms (FrameTimingManager), Draw Calls, SetPass, Triangles, B/khung, mono heap, texture memory, nhiệt, pin %/10 phút. Mác tất cả là **[ĐO máy X]**.
Công cụ: Unity Profiler (Development + Autoconnect, `adb forward`); Android GPU Inspector (System Profiler + Frame Profiler, xem fragment cost của `Toon_Palette`, băng thông, blit thừa); `adb shell dumpsys thermalservice`, `dumpsys gfxinfo`, `dumpsys batterystats`; Xcode Instruments (Game Performance, Metal System Trace, Thermal State), GPU Frame Capture/Metal HUD; Memory Profiler cho texture.
Thermal soak 15 phút: từ máy nguội, cấp 3 đông khách ban đêm Cao rồi mặc định, ghi mỗi 30 s: FPS, nhiệt, pin; chấp nhận: p95 khung không tăng > 20% giữa phút 1-3 và 12-15; governor không hạ quá 1 lần khi thiết bị mát. Thêm: bật Low Power Mode/Battery Saver, xoay màn, nền -> tiền cảnh, 120 Hz.
Ngưỡng hạ mức báo cáo (60 FPS): p95 > 18 ms, p99 > 33 ms, > 5% khung > 20 ms, GPU > 12 ms, CPU chính > 10 ms.
Quy tắc đo Mac: tránh nhiễu (load < 2), A/B cùng save và cùng hạt giống, chênh < 15% không đáng tin; so cả `trung vị` lẫn `p95`.

## 7. Điểm nóng khác có bằng chứng (không có trong báo cáo hoặc cần nhấn mạnh)
- Sinh khách 7 lần duyệt phân cấp (B4) [MÃ]; vật lý khách/nhân viên (Rigidbody kinematic + ma trận mở) [MÃ, CHƯA đo]; tick transform khách ngồi (B5) [MÃ, CHƯA đo]; `logo_wide` 8 MB (A2) [MÃ, CHƯA đo]; `Apply` đồ họa khựng (B1) [MÃ]; `DayNightController.Apply` ghi vô điều kiện (Phase C) [MÃ].
- Đã kiểm, KHÔNG phải vấn đề: LINQ chỉ ở khởi tạo/quarantine (SaveStore.cs:184, ngoài vòng nóng); `FindObjectsByType` chỉ ở khởi tạo/đổi mức; chữ nổi đã pool; `string.Format` HUD đã chặn bằng `xuShownInt`/`lastMinute`/`cupWaitShown`; không có Animator, không có ParticleSystem; khách không dùng MPB nên SRP Batcher giữ nguyên.

## 8. Không nên làm (chưa có dữ liệu biện minh)
- Không nâng ngưỡng RAM Cao lên 7000 khi đã có GPU cap + governor; không thêm `Adaptive Performance` package; không GPU instancing/Resident Drawer/static batching/occlusion/baked light trước khi A0 cho thấy CPU render thread là cổ chai; không chia canvas HUD, không SpriteAtlas khi A3 đã đủ; không tối ưu `DayNightController`, `LampLights.Update`, SRP Batcher/MPB (đã sạch); không viết lại mesh khách hay bake NavMesh async/bất đồng bộ lưu khi chưa thấy khựng > 50 ms trên máy thật; không hứa 120 FPS ở Cao; không đặt cấu hình tối thiểu khi chưa đo D1/D2.
