# Kết quả Phase A tối ưu hiệu năng (2026-10-19)

Theo `docs/optimization_plan_2026-10-19.md` mục 1 (A0-A6). Đo trên Apple M5 (Mac), bản Development (Mono, Metal), cửa sổ 2556x1179, `-timescale 4`. **Không có số điện thoại.** Chưa commit.

## 1. Đã làm / bỏ qua
| Mục | Trạng thái | Chỗ sửa |
|---|---|---|
| A0 probe FrameTiming | Xong | `Scripts/Core/DebugPerfProbe.cs` (`ProfilerRecorder` draw/SetPass/tam giác/caster/texture + `FrameTimingManager` CPU chính/render/GPU, dòng `[PerfX]`, dòng `[Perf] máy` in GPU/API/độ phân giải); `Editor/BuildTools.cs` bật `enableFrameTimingStats` quanh bản Android và macOS Development rồi trả lại giá trị cũ (không sửa ProjectSettings) |
| A1 GpuTiers | Xong | `Scripts/Core/GpuTiers.cs` (mới), `GraphicsPresets.Recommend(..., gpuName)` (overload mới, 3 tham số cũ giữ nguyên), `GraphicsQuality.cs:55` truyền `SystemInfo.graphicsDeviceName`, log `[Gfx]` thêm tên GPU. Không đổi ngưỡng RAM 5500 |
| A2 ASTC | Xong | `Editor/UiSpriteImporter.cs` (`ApplyCompression`): icon cửa hàng + `logo_wide` ASTC 6x6, sprite UI + `ui_hand_pointer` ASTC 4x4 (Android + iOS, tab mặc định Compressed); 3 ảnh nguồn icon app giữ không nén. `Editor/BrandingSetup.cs:86` bỏ dòng ép Uncompressed. Đã reimport: 77 `.meta` đổi |
| A3 HUD | Xong | `Scripts/UI/HudRules.cs` (mới: `QuantizeRing` 1/50, `SnapPosition` 0,5 px, `Place`); dùng ở `HudBuilder.SetRing`, `OrderBubble`, `CupCollectBubble`, `OwnerTaskIcons`, `CleaningSystem` (2 vị trí + vòng chủ quán) |
| A4 | Xong | `ProjectSettings/TimeManager.asset` max timestep 0,333 → 0,1; `ProjectSettings.asset` `accelerometerFrequency` 60 → 0 (không có mã dùng cảm biến) |
| A5 warm-up | Xong một phần | `Editor/ShaderWarmupSetup.cs` (menu Cafe/Graphics) tạo `Settings/SVC_ToonPalette.shadervariants` (6 biến thể ForwardLit: {không bóng, bóng, bóng mềm} x {có/không đèn phụ}) và thêm vào Preloaded shaders (`GraphicsSettings.asset`). **Không** gọi `WarmUp()` thêm trong `BootLoader`: Unity tự warm-up bộ sưu tập preloaded lúc khởi động, gọi lại là thừa |
| A6 caster theo loại | Xong | `DebugPerfProbe.LogSceneStats`: đổ bóng/renderer theo Customer, StaffWorker, Owner, đồ <0,5 m, đồ >=0,5 m, môi trường; in cả renderer/khách |

Bỏ qua: Phase B/C. Không đụng `CustomerSpawner`, `QualitySettings.asset`, `PerformanceTestRun*.json` (vẫn ở trạng thái xóa).

## 2. Kiểm thử
EditMode toàn bộ (batchmode): **450/450 đạt** (trước 408; thêm `GpuTiersTests` + `HudRulesTests` = 42 ca).

## 3. Cách đo
Cùng công thức cho trước/sau: bản dựng từ bản sao dự án (`/tmp/perf_base`, `/tmp/perf_after`, đổi tên sản phẩm để không đụng save thật). Cấp 1 `-freshsave`; cấp 2 và cấp 3 nạp save cố định (cấp 3 đông khách: 65-77 khách). {Thấp, Vừa, Cao} x {`-dayhour 10`, `21`}, mỗi ô 2 lượt (cấp 1-2 100 s, cấp 3 160 s; bỏ cửa sổ 30 s đầu, lấy trung vị). Số liệu thô: `/tmp/perf_results/baseline_*`, `after_*`. **Nhiễu ±15%** (máy dùng chung).

Hạn chế của lần đo "sau": cuối đợt máy bị quá tải bởi tiến trình ngoài (load average 30-95, máy tạm ngủ giữa chừng). Hai lượt bị loại vì hỏng: cấp 3 Vừa ngày lượt 2 (nhiễu) và cấp 3 Vừa đêm lượt 2 (treo 4 giờ); hai lượt cấp 3 Cao lượt 2 không chạy được. Các ô đó chỉ có n=1 (ghi ở cột).

## 4. Trước → sau (trung bình hai lượt; CPU = luồng chính, ms)
| Ô | CPU avg | CPU p95 | GPU avg | Draw call | GC B/khung | Texture MB |
|---|---|---|---|---|---|---|
| T1 Cao ngày | 1.23→1.10 | 2.05→1.76 | 0.82→0.93 | 126→128 | 50→49 | 19.1→9.9 |
| T1 Cao đêm | 1.27→1.15 | 2.13→1.88 | 0.84→0.92 | 128→129 | 54→49 | 19.1→9.9 |
| T1 Vừa ngày | 0.99→0.86 | 1.60→1.30 | 0.68→0.73 | 128→128 | 45→40 | 25.9→16.8 |
| T1 Vừa đêm | 0.92→0.86 | 1.43→1.30 | 0.64→0.72 | 129→128 | 46→44 | 25.9→16.7 |
| T1 Thấp ngày | 0.74→0.59 | 1.12→0.83 | 0.59→0.58 | 85→86 | 35→26 | 22.8→13.6 |
| T1 Thấp đêm | 0.69→0.61 | 1.02→0.87 | 0.52→0.58 | 85→85 | 28→26 | 22.8→13.6 |
| T2 Cao ngày | 1.78→1.61 | 2.80→2.43 | 1.07→1.17 | 340→352 | 101→112 | 19.5→10.0 |
| T2 Cao đêm | 2.62→1.94 | 4.38→2.89 | 1.41→1.57 | 339→354 | 168→143 | 19.1→10.0 |
| T2 Vừa ngày | 1.32→1.21 | 2.02→1.78 | 0.84→0.97 | 343→342 | 83→83 | 26.1→16.8 |
| T2 Vừa đêm | 1.60→1.35 | 2.53→1.98 | 0.97→1.02 | 348→351 | 104→106 | 25.9→16.9 |
| T2 Thấp ngày | 1.00→0.86 | 1.46→1.21 | 0.70→0.76 | 212→196 | 92→74 | 22.8→13.7 |
| T2 Thấp đêm | 1.51→0.87 | 3.03→1.21 | 0.82→0.78 | 203→198 | 118→72 | 22.9→13.6 |
| T3 Cao ngày (n sau=1) | 3.22→2.96 | 4.99→4.15 | 1.74→2.11 | 1502→1510 | 491→458 | 19.2→9.9 |
| T3 Cao đêm (n sau=1) | 4.18→3.73 | 6.52→5.40 | 2.19→2.43 | 1529→1566 | 660→624 | 19.2→9.9 |
| T3 Vừa ngày (n sau=1) | 2.78→2.78 | 4.09→4.36 | 1.56→1.84 | 1526→1494 | 468→522 | 26.0→16.8 |
| T3 Vừa đêm (n sau=1) | 2.92→3.22 | 4.13→4.73 | 1.66→2.08 | 1485→1456 | 481→482 | 26.0→16.8 |
| T3 Thấp ngày | 1.82→1.69 | 2.73→2.34 | 1.12→1.41 | 758→771 | 348→314 | 22.8→13.6 |
| T3 Thấp đêm | 1.91→2.14 | 2.87→3.33 | 1.14→1.44 | 776→772 | 349→431 | 22.8→13.6 |

Khởi động (T1/T3, Cao): trước: engine 0,22 s, mức đồ họa 2,44-2,51 s, vào ngày 2,5-2,6 s. Sau: engine 0,18-0,24 s, mức đồ họa 2,43-2,50 s (lượt vào ngày bị nhiễu 2,7-4,0 s vì tải máy). Không khác biệt đo được.
Bản dựng macOS (BuildReport): ảnh 18,2 MB → 9,0 MB; tổng 183,2 → 174,0 MB.

## 5. Đọc kết quả cho trung thực
- **Texture memory**: giảm 9,2 MB ở mọi ô (đo thật). Lưu ý: trên Mac Unity dùng DXT/BC7 (tab mặc định Compressed), **không phải ASTC**; Mac không dựng được ASTC nên phần ASTC 6x6/4x4 và mức giảm thật trên điện thoại (ước -9 đến -11 MB + logo 8 MB) **chưa kiểm**.
- **CPU**: thấp hơn 5-15% ở đa số ô, nằm trong nhiễu ±15%; tôi không quy cho A3. Ngoại lệ T2 đêm (2,62→1,94; 1,51→0,87) có lượt trước bị nhiễu lớn (p95 baseline cao bất thường). Không khẳng định có lợi CPU từ A3 trên M5 (plan đã ước chỉ 0,1-0,3 ms).
- **GPU**: số "sau" thường CAO hơn 5-25%. Chưa giải thích được (bản "sau" đo lúc tải máy khác); không thấy cơ chế nào trong A1-A6 làm GPU tệ đi (shader, draw call, tam giác không đổi). Cần đo lại trên máy nghỉ trước khi kết luận, không coi là cải thiện hay hồi quy.
- **Draw call/tam giác/SetPass**: không đổi (đúng kỳ vọng, Phase A không đụng).
- **GC B/khung**: dao động theo số khách; không có kết luận.
- **A1, A4, A5**: không tác dụng trên Mac (A1 chỉ chạy nhánh điện thoại; A4 chỉ khi khung >100 ms; A5 cần đo lúc đêm xuống lần đầu, `-dayhour` cố định giờ nên không tái hiện được). Kinh tế bot 100 ngày chưa chạy lại cho A4.
- **Ảnh chụp** (`/tmp/perf_results/shots_before*`, `shots_after_prep`): HUD, nút, thẻ, vòng, bong bóng giống nhau trước/sau trên Mac (BC7). Bảng Cửa hàng với icon KHÔNG chụp được (bot không mở bảng); cần xem tay.
- Cảnh Boot không nằm trong bản dựng macOS test (chỉ có Prototype_Tier1), nên `logo_wide` ASTC chưa thấy trên màn tải.

## 6. Việc người dùng phải kiểm trên máy thật
- [ ] Android + iPhone: mở Cửa hàng, thanh HUD, màn tải/tiêu đề: icon, viền 9-slice, chữ nhỏ, logo không lem/viền sọc (nếu `logo_wide` xấu thử ASTC 4x4/8x8 ở `UiSpriteImporter.ApplyCompression`).
- [ ] Log `[Perf] máy` và `[Gfx]`: tên GPU thật và mức tự chọn (Adreno 610 / Mali-G52 → Vừa; Adreno 7xx → Cao; tên lạ → Vừa); chỉnh bảng `GpuTiers` nếu sai.
- [ ] Dựng Development, chạy bot như plan mục 6: avg/p95 CPU, GPU ms, draw call, texture memory (Memory Profiler), kích thước AAB/IPA.
- [ ] Khung dài nhất khi đêm xuống lần đầu, trước/sau A5 (warm-up có hiệu lực thật hay không).
- [ ] Chạy bot 100 ngày xác nhận kinh tế (23,7) không đổi sau max timestep 0,1; kiểm đồng hồ không chậm bất thường khi khựng.

## 7. Dọn dẹp
- URP assets: `m_PrefilteringModeAdditionalLight` các mức vẫn 0 như ban đầu (đã so với bản chụp trước).
- `PerformanceTestRun*.json(.meta)` vẫn xóa; `QualitySettings.asset` giữ nguyên thay đổi của người dùng.
- Đổi so với `/tmp/phaseA_before.txt`: file mã ở mục 1, `GraphicsSettings.asset`, `ProjectSettings.asset` (accelerometer), `TimeManager.asset`, `SVC_ToonPalette.shadervariants(.meta)`, 77 `.meta` ảnh UI (Shop 42, UI 33, Logo 2).
