# Báo cáo hiệu năng: game có nặng không, máy nào chơi được (2026-10-19)

Phạm vi: nhánh `feat/prep-phase-pacing-missions` kèm phần ánh sáng đêm CHƯA commit (`LampLights.cs`, `Toon_Palette.shader` đèn phụ per-pixel, 3 mức Thấp/Vừa/Cao, `URP_Mobile_Low/Medium/.asset`). Chỉ đọc và đo; không sửa mã nguồn, scene, asset, ProjectSettings. Nhãn: **[ĐO]** = chạy thật trên máy này, **[ƯỚC]** = tính từ số đo hoặc quy tắc kinh nghiệm, **[CHƯA]** = chưa kiểm chứng. **Không có số liệu nào đo trên điện thoại thật.**

## 0. Kết luận
- Nhẹ ở cấp 1-2 (CPU chính 0,4-1,1 ms, 120-185 draw call trên M5), **nặng nhất ở cấp 3 đông khách**: 650-1900 draw call, 300-590k tam giác/khung (gồm bóng), CPU chính 2,1-4,5 ms và GPU 1,2-2,2 ms trên Apple M5. Mật độ này không phải "nhẹ" cho máy yếu. Ban đêm trên Vừa/Cao tốn thêm khoảng +15-35% CPU chính và +15-30% GPU (đo lặp 2 lần).
- Chơi ổn **[ƯỚC]**: iPhone XS/11 trở lên và Android tầm trung khá (Snapdragon 7xx, Dimensity 7000+) ở Thấp/Vừa 30-60 FPS; máy mạnh (Snapdragon 8 Gen 1+, iPhone 13+, iPad M) ở Cao 60 FPS. Máy yếu (RAM <= 3 GB, Mali-G52/Adreno 610) chạy Thấp 30 FPS ở cấp 1-2, cấp 3 đông khách là ranh giới.
- Hàm gợi ý mức (`GraphicsPresets.Recommend`, GraphicsPresets.cs:121) chỉ nhìn RAM và số nhân: máy 6-8 GB RAM nhưng GPU yếu (Adreno 610/Mali-G57) bị xếp Cao. Đây là rủi ro lớn nhất về "ai chơi được".
- Ba việc nên làm: (1) chặn Cao tự động theo tên GPU và thêm bộ tự hạ mức theo thời gian khung; (2) giảm số renderer đổ bóng (khách ~6-10 renderer/người, ~95% renderer đều đổ bóng) và chia mảnh sàn/tường để vòng đèn phụ không chạy trên toàn phòng; (3) nén ASTC cho 42 icon cửa hàng (-9 MB bộ nhớ ảnh, ~-8 MB bản cài).
- Bắt buộc đo trên 2 máy thật (1 Android RAM 3-4 GB, 1 iPhone cũ) trước khi hứa cấu hình tối thiểu; xem mục 5.

## 1. Game có nặng không?

### 1.1 Số đo cũ (commit `03a9ae9`, `docs/phase3_spec.md` §0.5, `unity_review_2026-10-15.md` §F)
| Chỉ số | Giá trị đã ghi | Điều kiện |
|---|---|---|
| CPU script/khung | TB 0,3-0,6 ms, p99 ~1 ms | PC, `-nographics`, cấp 2 |
| Cấp phát GC | 12-46 B/khung, GC gen0 2-8 lần/phút, mono heap 10-14 MB | cấp 2 |
| Đỉnh khung | 12-25 ms mỗi ~30 s (sinh khách TB 0,8 ms, lưu 0,6-4 ms, bake NavMesh 2-9 ms) | cấp 2 |
| Cảnh | ~43k tam giác, 1 vật liệu (review 10-15 ghi ~62k) | cấp 2 đầy đồ |
| Chưa có | GPU, draw call, SetPass, nhiệt, bộ nhớ ảnh, thời gian khởi động, dung lượng bản cài | |
Các con số renderer "400-700", GPU "3-5 ms (A15+)" trong review 10-15 là suy đoán; mục 1.2 thay bằng số đo.

### 1.2 Đo lại 2026-10-19 **[ĐO]**
Cách đo: bản **Development** macOS (Mono, Metal, Apple M5, RAM 32 GB) dựng ở `/tmp/perf_build`, từ **bản sao** dự án (gồm cả phần đèn đêm chưa commit); `DebugPerfProbe` trong bản sao được thêm `FrameTimingManager` + `ProfilerRecorder` (Draw Calls, SetPass, Triangles, Shadow Casters) và `enableFrameTimingStats=1`; repo gốc không đổi. Cửa sổ 2556x1180 (= iPhone 15), FPS không giới hạn nên thời gian khung = chi phí thật. Bot: `-autoplay -autoupgrade3 -cheatt3 -autostaff -fullstaff -autobuy -bestmenu -autohidden -upgradestaff -autozones -t3xu 200000`, cấp 3 đủ 3 khu (save dùng lại cho mọi mức), `-gfx low|medium|high`, `-dayhour 12|21`, `-timescale 4`. Mỗi cấu hình 200 s, lấy trung vị cửa sổ 30 s thứ 2-6; Vừa/Cao đêm đo 2 lần, kết quả cùng chiều. **Cảnh báo nhiễu:** máy đo có load average 7-10 (Chrome, v.v.), nên sai số ±15% là bình thường; đo trên MacBook không quạt.

Cấp 3 đông khách (khách đồng thời 60-88, renderer 860-995, đổ bóng 845-978, canvas 6, uGUI graphic ~245):
| Mức | Giờ | Draw call | Tam giác/khung | Bóng (renderer) | CPU chính ms avg (p95) | CPU render ms | GPU ms avg (p95) | SetPass |
|---|---|---|---|---|---|---|---|---|
| Thấp (0,7, không bóng) | ngày | 730 | 204k | 0 | 2,15 (2,8) | 0,64 | 1,26 (2,2) | 20 |
| Thấp | đêm | 690 | 189k | 0 | 2,11 (3,9) | 0,74 | 1,17 (2,2) | 21 |
| Vừa (0,85, bóng 512, 4 đèn) | ngày | 1310-1370 | 376-396k | 630 | 2,4-2,5 (3,7-3,8) | 0,97 | 1,36-1,46 (2,5-2,6) | 25 |
| Vừa | đêm | 1370-1410 | 396-412k | 670 | 2,8-3,5 (4,3-5,0) | 1,17-1,28 | 1,57-1,97 (2,7-3,1) | 25 |
| Cao (1,0, MSAA 2x, bóng mềm 1024, 8 đèn) | ngày | 1480-1580 | 438-468k | 749 | 3,3-3,6 (4,6-5,7) | 1,19-1,33 | 1,88 (2,9-3,0) | 24 |
| Cao | đêm | 1380-1460 | 400-425k | 700 | 4,1-4,5 (6,2-6,6) | 1,57-1,59 | 2,14-2,18 (3,0-3,4) | 24 |

Cấp 1 (Cao, đêm, bot, 0-5 khách): CPU chính 0,3-1,1 ms, GPU 0,7-1,1 ms, 100-185 draw call, 23-55k tam giác, SetPass 13-15 (khớp số cũ 0,3-0,6 ms).

Khác:
| Chỉ số | Giá trị | Ghi chú |
|---|---|---|
| Cấp phát/khung | cấp 1: 27-93 B; cấp 3: trung vị 230-660 B, tối đa ~1,4 KB (7-12 lần cấp phát/khung) | cấp 3 cao gấp 5-10 lần số cũ |
| GC gen0 | cấp 1: 2-6/phút; cấp 3: 6-8/phút ở timescale 4 | **[ƯỚC]** ~2/phút ở 1x; heap mono 15-20 MB |
| Việc nặng đơn lẻ (M5) | bake NavMesh TB 4-6 ms (tối đa 9); lưu game TB 2-4 ms (tối đa 9, ~2 lần/phút game); sinh khách TB 0,6-0,9 ms (tối đa 2-3, một lần 16,6) | NavMeshBaker.cs:103 đồng bộ |
| Khung tệ nhất | p99 5-11 ms; max 50-300 ms mỗi cửa sổ | chưa tách nguyên nhân (nghi nhiễu máy đo + sự kiện); phải đo lại trên máy thật |
| Bộ nhớ | RSS 425-437 MB (Mac); `gfxDriver` 78-90 MB; Unity tổng cấp phát ~112-115 MB; bộ nhớ ảnh 19-28 MB (gồm render target) | RSS Mac không phải số điện thoại |
| Khởi động | engine 0,2 s, mức đồ họa 2,5 s, vào ngày 2,7-2,9 s (M5, SSD) | điện thoại **[ƯỚC]** 4-10x chậm hơn |
| Dung lượng | bản Mac Development 183 MB; tài nguyên người dùng 21,3 MB (ảnh 18,2 = 85,5%, mesh 1,3, âm thanh 0,3, shader 0,29) | iOS sim Debug 231 MB (Frameworks 202 MB, Data 33,6 MB) không đại diện |
| Shader | `Toon_Palette` ForwardLit fp: 128 biến thể -> 10 sau strip (build2.log) | |
Đèn đêm (chênh Ngày -> Đêm, 2 lượt): Cao CPU chính +0,5 đến +1,2 ms, GPU +0,26-0,30 ms; Vừa CPU chính +0,3 đến +1,1 ms, GPU +0,2-0,5 ms; Thấp không đổi. Phần CPU là lọc đèn theo từng renderer (~900 renderer), không phải `LampLights.Update` (chỉ so một số float mỗi khung, LampLights.cs:57-68).

### 1.3 Từ máy tính sang điện thoại **[ƯỚC]** (hệ số bảo thủ, không có IL2CPP bù trừ)
| Loại máy | CPU 1 luồng so với M5 | GPU so với M5 | Ghi chú |
|---|---|---|---|
| Android yếu (Helio G35-G85, SD 4xx/6xx, Mali-G52, Adreno 610) | chậm 8-12x | chậm 20-40x | |
| Android tầm trung (SD 7xx, Dimensity 700-7050, Adreno 619, Mali-G57/G68) | 4-6x | 10-20x | |
| Android flagship (SD 8 Gen 1-3, Adreno 730-750) | 2-3x | 1,5-3x | |
| iPhone 8/XR (A11-A12) / iPhone 11-13 (A13-A15) / iPhone 15+ | 4-5x / 2-3x / 1,5x | 8-10x / 3-5x / 2x | |
Thang số: CPU chính T3 2-4,5 ms -> mỗi hệ số 10x cho 20-45 ms (vượt 33 ms của 30 FPS), 5x cho 10-22 ms, 2,5x cho 5-11 ms. Cấp 1-2 nhẹ hơn cấp 3 khoảng 3-5 lần. Điểm cần nhớ: người chơi thường ở cấp 1-2 hàng chục giờ trước khi tới cấp 3 (economy_monetization.md §17: mua cấp 3 mất hàng chục đến ~130 ngày game).

## 2. Phân tích tĩnh: điểm nóng
| # | Khu vực | Bằng chứng | Nhận định |
|---|---|---|---|
| S1 | Số renderer đổ bóng | Perf: 845-978 / 860-995 renderer đổ bóng; `ShadowCaster` Pass `Toon_Palette.shader:163`; chỉ môi trường tắt cast (EnvironmentBuilder.cs:91,165,178,308,390) | Draw call ~ renderer x (1 chính + 1 bóng); Thấp tắt bóng nên ít hơn 2x (730 so 1400). Khách ~6-10 renderer/người **[ƯỚC]** (CustomerVisual.cs:121-159). **Lớn nhất** |
| S2 | Vòng đèn phụ per-pixel | `Toon_Palette.shader:129-139` (`GetAdditionalLightsCount` + `smoothstep` x2 + `lerp` mỗi đèn); `AdditionalLightsPerObjectLimit` 8/4 (URP_Mobile.asset:48, Medium:48) | Sàn là **một mesh toàn phòng** (EnvironmentBuilder.cs:83-92) nên nhận đủ 8 (Cao) / 4 (Vừa) đèn trên **mọi** pixel sàn, kể cả pixel ngoài tầm đèn (ALU vẫn chạy). Đây là phần lớn chi phí đêm. Ước ~35-50 ALU/đèn, Cao 8 đèn x ~50% pixel sàn **[ƯỚC]** |
| S3 | Biến thể `_ADDITIONAL_LIGHTS` | `Toon_Palette.shader:59`; không có ShaderVariantCollection/warmup (GraphicsSettings.asset: `m_AdditionalWarmupCollections: []`) | Lần đầu trời tối có thể khựng khi tạo pipeline state (Metal/Vulkan). **[CHƯA]** cần quay trên máy thật |
| S4 | Hồ đèn `LampLights` | LampLights.cs:57-68 (so `Night`, chỉ ghi khi đổi); `Assign` sắp xếp bằng lambda (dòng 85) chỉ khi LayoutChanged/đổi mức | Chi phí script ~0, bỏ qua. Tối đa 8 `Light` điểm không bóng |
| S5 | SRP Batcher | `CBUFFER UnityPerMaterial` bao hết thuộc tính, dùng chung 3 pass qua `HLSLINCLUDE` (shader:29-41); `_CafeNight`, `_CafeLampSoft` ngoài CBUFFER (đúng); `m_UseSRPBatcher: 1` | Tương thích (đọc mã; **[CHƯA]** xem Frame Debugger). SetPass chỉ 17-26. Chỉ **chủ quán, vết bẩn bàn, ruồi, ghost build** dùng `MaterialPropertyBlock` (CustomerVisual.cs:159,197 là chủ quán và capsule dự phòng; TableDirt.cs:176; CleaningSystem.cs:268; BuildModeController.cs:836,843). Khách/nhân viên KHÔNG dùng MPB, nên mục B3 của review 10-15 phần "MPB của khách" là không đúng |
| S6 | Ánh sáng/bóng URP | Cao: MSAA 2x, bóng 1024 mềm, `shadowDistance` 30, 1 cascade (URP_Mobile.asset:28,46,57,58,66); Vừa: bóng 512 cứng, 26, 0,85; Thấp: tắt bóng, 0,7, 30 FPS. Không depth/opaque texture, không hậu kỳ, HDR tắt | Cấu hình gọn. Khoảng cách bóng đã sửa so với review 10-15. `QualitySettings.asset` có `shadowDistance` 14/18 là dữ liệu cũ, URP bỏ qua |
| S7 | CPU `Update` | ~31 file có Update/LateUpdate. Đã giãn nhịp: `OverheadVisibility` 10 Hz (dòng 57-62), `DayNightController` 4/10/20 Hz (GraphicsPresets.cs:108), nhân viên `retryAt`. Không thấy `FindObjectsByType`/LINQ trong vòng Update (chỉ khởi tạo: DayNightController.cs:36, GraphicsQuality.cs:152 khi đổi mức) | Sạch. `Camera.main` trong LateUpdate (OrderBubble.cs:116 v.v.) đã cache ở Unity 6, rẻ |
| S8 | Canvas/UI | HudBuilder.cs:260 `SetRing` ghi `fillAmount` + `color` mỗi khung (HudController.cs:777, tối đa 4 thẻ); `anchoredPosition` ghi mỗi khung cho bong bóng (OrderBubble.cs:132, CupCollectBubble.cs:75, CleaningSystem.cs:339,363, OwnerTaskIcons.cs:143); `resizeTextForBestFit` (HudBuilder.cs:303); 6 canvas, ~245 graphic ở cấp 3 | **Chưa sửa** từ review 10-15 (B4): HudCanvas bị dựng lại mỗi khung lúc có khách chờ, bong bóng khi xoay camera. Tác động thật **[CHƯA]**; chưa tách trong số đo vì UI nằm trong CPU chính 2-4 ms. `xuText` đã có `xuShownInt` chặn đúng (HudController.cs:471-476). Viền chữ Outline x5 đỉnh tắt ở Thấp |
| S9 | NavMesh/AI | Khách ~65-88 agent `LowQualityObstacleAvoidance` (Customer.cs:114); chọn ghế 0,4 s/khách, TB 0,01-0,04 ms (đo); bake đồng bộ `BuildNavMeshData` (NavMeshBaker.cs:103) | AI quy mô theo số khách tuyến tính, hiện rẻ. Bake 4-6 ms x 5-10 = **[ƯỚC]** 20-60 ms khựng khi đặt đồ/mở khu trên máy yếu |
| S10 | Lưu/nạp | SaveStore.cs:91-126: JSON -> ghi tmp -> đọc lại kiểm tra -> sao lưu .bak -> đổi tên; 9,5 KB; chạy trên luồng chính, 22 chỗ gọi | TB 2-4 ms (đo) ~2 lần/phút; **[ƯỚC]** 10-40 ms trên flash điện thoại |
| S11 | Sinh khách | Instantiate + `GetComponentsInChildren<Transform>` x2 + `StartsWith` (CustomerVisual.cs:121,165), không pool (92-170 khách/cửa sổ 30 s ở timescale 4-8) | 0,6-0,9 ms TB (đo); tối đa 16,6 ms một lần; cấp phát chính của cấp 3 |
| S12 | Particle | Không dùng `ParticleSystem` nào (grep trống) | Hiệu ứng bằng uGUI/FloatLayer |
| S13 | Ảnh | Cả 81 file PNG trong `Art/` có `textureCompression: 0` ở nền tảng mặc định và không override theo nền tảng (`overridden: 0`): **42 icon cửa hàng 256x256 + 33 sprite UI 128x128 là RGBA32 không nén**, không mip, không có SpriteAtlas. 42 x 256 KB = 10,5 MB; UI 2,1 MB; tổng ảnh 18,2 MB trong bản dựng. Palette 256 không nén có chủ ý (ArtImporter.cs:302) | Lãng phí bộ nhớ ảnh ~9-11 MB; ảnh UI rời nhau có thể cắt batch UI khi mở Cửa hàng **[CHƯA]** |
| S14 | Mesh | `isReadable: 0`, nén mesh Medium, bỏ animation/camera/ánh sáng (ArtImporter.cs:272-290); 1 vật liệu dùng chung | Tốt. Furniture/nhân vật là mesh nhiều phần Transform (không skin) |
| S15 | Âm thanh | SFX Vorbis 0,4 mono nạp sẵn `CompressedInMemory`, nhạc nền `Streaming` (AudioPostprocessor.cs:20-29); 308 KB trong bản dựng | Không đáng kể |
| S16 | Cấu hình dự án | IL2CPP Android (ProjectSettings.asset:726), `AndroidTargetArchitectures: 2` (ARM64), `stripEngineCode: 1`, `gcIncremental: 1`, `managedStrippingLevel` mặc định; `androidBlitType: 0` (dòng 78) và `accelerometerFrequency: 60` (dòng 14) còn nguyên; `enableFrameTimingStats: 0` (dòng 160); `TimeManager` max timestep 0,333 (dòng 12); gói `adaptiveperformance` chỉ là module, `m_UseAdaptivePerformance: 1` không có tác dụng **[CHƯA]**; iOS tối thiểu 15.0 | Cần bật FrameTiming khi đo máy thật |

## 3. Máy nào chơi được

### 3.1 Hàm gợi ý mức (GraphicsPresets.cs:121-127)
Điện thoại: RAM <= 3200 MB hoặc <= 4 nhân -> Thấp; RAM >= 5500 MB và >= 8 nhân -> Cao; còn lại Vừa. Máy tính luôn Cao. Test: GraphicsPresetsTests.cs:72-83.
| Nhận xét | Hệ quả |
|---|---|
| Chỉ RAM + nhân, không xét GPU | Máy 6-8 GB nhưng GPU yếu (SD 685/Adreno 610, Helio G99/Mali-G57) tự chọn Cao (MSAA, bóng mềm, 8 đèn, 60 FPS): quá nặng. Sửa: cap bằng `SystemInfo.graphicsDeviceName` hoặc thêm bộ tự hạ |
| `systemMemorySize` thường báo ít hơn danh nghĩa (**[CHƯA]**, từ kiến thức: máy "6 GB" báo ~5,4-5,8 GB, "4 GB" ~3,6-3,9 GB, "3 GB" ~2,7-3,0 GB) | Ngưỡng 5500 cắt ngay giữa nhóm 6 GB nên kết quả không ổn định giữa các máy cùng model; ngưỡng 3200 xếp "3 GB" vào Thấp (đúng ý) |
| Mọi iPhone có 6 nhân (**[CHƯA]**) | iPhone không bao giờ tự lên Cao; iPhone 4 GB (11/12) -> Vừa là hợp lý; iPhone 3 GB (XR/SE2) -> Thấp 30 FPS hơi bảo thủ (A12/A13 chạy Vừa tốt). iPad M-series (8+ nhân, 8+ GB) -> Cao, hợp lý |
| Tablet Android giá rẻ 4 GB, màn 2000x1200 (Mali-G52) | -> Vừa, nhiều khả năng quá nặng (GPU yếu, nhiều điểm ảnh) |
| Không có bộ tự hạ | Review 10-15 §D đề xuất `GraphicsGovernor` nhưng chưa làm (grep không thấy) |

### 3.2 Bảng máy x mức **[ƯỚC, chưa thử máy thật]** (cấp 3 đông khách là trường hợp xấu; cấp 1-2 thường dễ hơn một mức)
Mục tiêu mặc định là FPS của mức (Thấp 30, Vừa/Cao 60). Độ tin cậy: Cao/Vừa/Thấp của chính ước tính.
| Loại máy | Thấp (0,7, 30 FPS) | Vừa (0,85, 60 FPS) | Cao (1,0, 60 FPS) | Mức tự chọn | Tin cậy |
|---|---|---|---|---|---|
| Android yếu 2-3 GB (Mali-G52/Adreno 610, 720p-1080p) | cấp 1-2 30 FPS ổn; cấp 3 ~25-30 FPS (CPU chính ~21 ms) | 30 FPS tối đa, hay tụt | Không | Thấp (đúng) | Thấp |
| Android yếu-vừa 4 GB (SD 680/695, Helio G85/G99) | 30 FPS ổn | cấp 1-2 45-60; cấp 3 30-40 | Không | **Vừa (hơi lạc quan)** | Thấp |
| Android tầm trung 6-8 GB (SD 7 Gen 1, Dimensity 7050, Adreno 619-644) | 30-60 | cấp 3 40-60; đêm có thể 30-45 | Chỉ 30 FPS | **Cao (quá nặng)** | Vừa |
| Android flagship (SD 8 Gen 1-3, Dimensity 9xxx) | 60 | 60 | 60 (cấp 3 đêm nóng sau 15-30 phút) | Cao | Vừa |
| iPhone 8/SE2/XR (A11-A13, 2-3 GB) | 30 (A11 cấp 3 sát) | 30-60 (XR) | Không | Thấp | Vừa |
| iPhone 11-13 (A13-A15, 4 GB) | 60 | 60 | 30-60 | Vừa | Vừa |
| iPhone 14 Pro/15/16 (A16+, 6-8 GB) | 60 | 60 | 60 | Vừa (không bao giờ Cao) | Vừa |
| iPad cũ (A12-A13, 3 GB, ~3,5 Mpix) | 30 | 30-45 | Không | Thấp | Thấp |
| iPad Air/Pro (M-series) | 60 | 60 | 60 (120 không hứa) | Cao | Vừa |
Cơ sở: khung ~ max(CPU chính cấp 3 2,1-4,5 ms x hệ số CPU, GPU 1,2-2,2 ms x hệ số GPU x tỉ lệ điểm ảnh so với 3 Mpix); hệ số ở bảng 1.3. 120 FPS ở Cao không khả thi (8,3 ms) và không được hứa.

Nhiệt/pin: Cao + 60 FPS (MSAA, bóng mềm, độ nét gốc) là tổ hợp nóng nhất; Thấp 30 FPS (`TargetFps`, GraphicsPresets.cs:82) mát và tiết kiệm pin đáng kể so với 60 **[ƯỚC]**; mọi mức đã `vSyncCount = 0` + `targetFrameRate` (GraphicsQuality.cs:123-128). **[CHƯA]** đo nhiệt, pin/10 phút.

Cấu hình tối thiểu đề nghị (cần xác nhận bằng máy thật): Android 8.0+ ARM64, **RAM 3 GB, GPU Mali-G52/Adreno 610 trở lên, màn 720p+**, chơi Thấp 30 FPS; iOS 15+, iPhone 8 trở lên (2 GB) chơi Thấp. Khuyến nghị: Android 6 GB + Snapdragon 7xx/Dimensity 7000+ hoặc iPhone XS trở lên để chơi Vừa 60 FPS.

## 4. Tối ưu thêm: có đáng làm không?
Kết luận: **có, nhưng chỉ ít việc rẻ là đáng làm ngay**. Phần còn lại chờ số đo máy thật (nguyên tắc: không tối ưu mù khi không đo trên thiết bị).

### 4.1 Làm ngay (rẻ, an toàn)
| # | Việc | Lợi ước tính | Công | Rủi ro | Đo lại bằng |
|---|---|---|---|---|---|
| 1 | Nén ASTC 6x6 cho 42 icon cửa hàng (override Android/iOS), ASTC 4x4 cho sprite UI; giữ palette không nén | -9 đến -11 MB bộ nhớ ảnh, ~-8 MB bản cài, nạp nhanh hơn | S | Thấp (kiểm viền sprite 9-slice) | Memory Profiler, kích thước IPA/AAB |
| 2 | Thêm `ShaderVariantCollection`/GraphicsStateCollection có `_ADDITIONAL_LIGHTS` cho Toon_Palette, warm-up lúc boot | Tránh khựng lần đầu trời tối (S3) | S | Thấp | Khung dài nhất khi sang đêm lần đầu |
| 3 | Bật `enableFrameTimingStats`, đưa bản vá FrameTimingManager + ProfilerRecorder (batches/SetPass/tris) vào `DebugPerfProbe` | Có CPU/GPU ms trên máy thật | S | Không (chỉ Development) | Chính nó |
| 4 | Giới hạn Cao tự động theo GPU (`SystemInfo.graphicsDeviceName` chứa Adreno 6xx thấp/Mali-G5x/G7x -> tối đa Vừa); hạ ngưỡng RAM Cao 5500 -> 7000 | Tránh Cao trên máy 6 GB GPU yếu | S | Thấp | Bảng máy x mức của mục 5 |
| 5 | HUD: `SetRing` chỉ ghi khi đổi >= 0,01; `anchoredPosition` chỉ ghi khi lệch > 0,5 px | Bớt rebuild HudCanvas/bubble mỗi khung | S | Thấp | Profiler `Canvas.BuildBatch` / `UpdateBatches` |
| 6 | `TimeManager` max timestep 0,333 -> 0,1; `accelerometerFrequency` 60 -> 0 (ProjectSettings.asset:14) | Tránh bước vật lý dồn sau khựng; bớt đọc cảm biến | S | Rất thấp | |
| 7 | Chia sàn/tường thành mảnh theo ô/khu (hoặc trong shader `if (lamp.distanceAttenuation < 0.002h) continue;`) để pixel ngoài tầm đèn không chạy đủ ALU | Giảm phần lớn +0,3-0,5 ms GPU/đêm trên M5 (phone: vài ms trên GPU yếu) | S-M | Thấp-vừa (đổi mesh sàn; kiểm tile gạch) | Xcode GPU Capture / AGI, ms GPU ban đêm |
| 8 | Chủ động giảm số renderer đổ bóng: tắt `castShadows` cho vật nhỏ (ly, cây nhỏ, phụ kiện) ở Vừa; chỉ khách/nhân viên/đồ lớn đổ bóng | -20-35% draw call Vừa/Cao **[ƯỚC]** | M | Vừa (mất vài bóng nhỏ, cần duyệt art) | Draw call, GPU ms, ảnh chụp |
| 9 | Pre-warm/pool khách (Instantiate + dựng khớp 1 lần/prefab, bỏ `GetComponentsInChildren` mỗi lần sinh) | -0,3-0,5 ms/khách, bớt GC cấp 3 | M | Thấp | `sinh khách` trong `[Perf] việc nặng`, B/khung |

### 4.2 Chỉ làm nếu thử máy thật cho thấy vấn đề
| Việc | Khi nào | Lợi / công / rủi ro |
|---|---|---|
| `GraphicsGovernor` tự hạ FPS/độ nét/MSAA/bóng/mức theo trung bình trượt 5 s (review 10-15 §D) | p95 > 18 ms (60 FPS) hoặc nóng | Lợi lớn cho máy lạ; M; rủi ro dao động mức, cần toast và công tắc |
| Hạ `renderScale` Vừa/Cao (0,85 -> 0,75; Cao 1,0 -> 0,9), trần cạnh dài 1920 px ở Cao | GPU > 12 ms trên mid-range | Giảm GPU gần tỉ lệ số điểm ảnh; S; mờ hơn |
| Đèn vertex-lit (`_ADDITIONAL_LIGHTS_VERTEX`) cho Vừa | GPU đêm quá cao ở Vừa | Chi phí theo đỉnh, không theo pixel; S-M; ánh đèn thô trên mesh ít đỉnh (sàn/tường) |
| Giảm số đèn Vừa 4 -> 3 hoặc tầm 3,2 -> 2,8 m | Đêm tụt FPS | S; mất không khí |
| Giảm Hz `DayNightController` (20/10/4) hoặc ghi RenderSettings chỉ khi đổi đáng kể | Nếu Profiler thấy ambient/ shader global tốn | S; rất thấp lợi |
| GPU instancing / GPU Resident Drawer (`m_GPUResidentDrawerMode: 0`) | Nếu draw call vẫn > 1000 và CPU render thread là cổ chai | Cần Forward+, kiểm tương thích MPB; M-L; rủi ro cao hơn lợi |
| Bake NavMesh bất đồng bộ (`UpdateNavMeshDataAsync`), ghi save ở luồng nền | Khựng > 50 ms khi đặt đồ/lưu | S-M; thấp (cẩn thận race) |
| Chia canvas HUD (thẻ order, chip, bubble) hoặc dùng nhiều `Canvas` con | Nếu `Canvas.SendWillRenderCanvases` chiếm > 1 ms | M; thấp |
| `managedStrippingLevel` Medium + `link.xml`, IL2CPP LTO | Nếu bản cài > 100 MB | Giảm vài MB; S; rủi ro mất reflection |
| SpriteAtlas cho UI/icon | Nếu số batch UI cao khi mở Cửa hàng | Giảm batch UI; S-M |
| Occlusion culling, static batching, baked light | Không áp dụng (đồ do người chơi đặt, camera trên cao) | Bỏ qua |
| Android `androidBlitType` Always -> Auto/Never | Nếu AGI thấy blit thừa | S; đo trước |
| Adaptive Performance (cần package + provider) | Muốn phản ứng nhiệt Android | M; cần thêm package, mới có tác dụng |

## 5. Danh sách kiểm trên máy thật
- [ ] 3 máy tối thiểu: Android 3-4 GB (Mali-G52/Adreno 610), Android 6-8 GB tầm trung (SD 7xx/Adreno 619+), iPhone cũ (8/XR) + 1 iPhone 13+ nếu có; thêm 1 iPad.
- [ ] Dựng Development, bật `enableFrameTimingStats`, bản vá probe (mục 4.1 #3), `Autoconnect Profiler`; cờ `-perf -gfx <mức> -dayhour 21 -autoplay ...` như mục 1.2.
- [ ] Mỗi máy x mỗi mức x {cấp 1, cấp 3 đông khách} x {ngày, đêm}: avg/p95/p99 khung, % khung > 1,5x mục tiêu, CPU chính/render/GPU ms, draw call, SetPass, tam giác, B/khung, mono heap, bộ nhớ ảnh.
- [ ] Soak 15-30 phút từ máy nguội: nhiệt (`thermalState` iOS / `dumpsys thermalservice`), % pin mất mỗi 10 phút, FPS đo thực (Android 90/120 Hz: kiểm `Screen.currentResolution`).
- [ ] Khựng: khung dài nhất khi đêm xuống lần đầu, bake NavMesh, lưu, sinh khách, mở Cửa hàng.
- [ ] Công cụ: Xcode Instruments (Game Performance, Metal System Trace) + Metal HUD; Android GPU Inspector, Perfetto; Snapdragon Profiler / Arm Mobile Studio.
- [ ] Bản phát hành (không Development): kiểm `DebugAutoPilot`/`DebugPerfProbe` đã bị strip; đo kích thước AAB/IPA thật, thời gian khởi động lạnh, shader variant bị strip nhầm.
- [ ] Ngưỡng hạ mức (60 FPS): p95 > 18 ms, p99 > 33 ms, > 5% khung > 20 ms, GPU > 12 ms, CPU chính > 10 ms, hoặc nhiệt từ Serious / pin > 12% mỗi 10 phút.

## 6. Giới hạn, không chạy / không đo được
- Không có số liệu điện thoại, iPad, Android, GPU di động. Mọi bảng 1.3, 3.2 và các lợi ích trong mục 4 là **[ƯỚC]** từ số đo M5.
- M5 là GPU kiến trúc tile-based nhưng nhanh hơn GPU điện thoại 1,5-40x; hệ số ở 1.3 là đoán, thực tế còn phụ thuộc băng thông, nhiệt, trình điều khiển. Mono (dev, có profiler) khác IL2CPP (bản phát hành thường nhanh hơn).
- Máy đo nhiễu (load 7-10); mỗi cấu hình 1 lượt dài 200 s (Vừa/Cao đêm 2 lượt). Hệ quả: chênh nhỏ (< 15%) không đáng tin, chênh ngày/đêm đo lặp cùng chiều.
- Số khách 60-88 ở cấp 3 là do bot + `-timescale 4`; ở 1x số khách đồng thời thấp hơn, cấp phát/khung mỗi khung có thể khác. Số GC/phút ở 1x là ước tính.
- Batches Count luôn báo 0 trong Profiler Recorder dưới SRP Batcher; dùng Draw Calls Count (đã ghi) và SetPass.
- Bản Android/iOS release (IL2CPP) không dựng được trong phiên này, nên dung lượng IPA/AAB và thời gian khởi động điện thoại là **[CHƯA]**. Dự báo thô: AAB/IPA 50-90 MB (UnityFramework ~40-60 MB + 21 MB tài nguyên).
- Không chạy EditMode tests trong phiên này (chỉ xem mã). `QualitySettings.antiAliasing: 2` (thay đổi chưa commit của người dùng) không ảnh hưởng kết quả đã đo vì URP dùng MSAA của URP asset (**[CHƯA]** xác nhận trên iOS).
- Tạm: bản dựng và log ở `/tmp/perf_build` (bản sao dự án `proj`, `out`, `m2_*.log`, `m3_*.log`); không ghi vào repo. Dữ liệu lưu thử nằm ở thư mục riêng `PerfTmp`, không đụng save thật của người chơi.
