# Kế hoạch nhịp khách đến (arrival pacing) — 2026-10-19

> Phản hồi: "khách đến dày / dồn cục, game bị đẩy sang nhịp gấp, khó kiểm soát". Tài liệu này CHỈ là phân tích + kế hoạch, chưa đổi code.
> Nhãn: **[ĐO]** = lấy từ số đo bot đã có trong `economy_monetization.md`; **[ƯỚC]** = tôi tự tính/mô phỏng bằng Python (`/tmp/pacing/sim.py`, tạm, không nằm trong repo); **[CHƯA ĐO]** = chưa có dữ liệu.
> Không chạy Unity/bot lần này (chạy bot 100 ngày mất hàng giờ và dễ ghi vào repo). Repo giữ nguyên: `git diff` và `git status` trước/sau trùng nhau.

## 1. Luồng thật, từ sinh khách đến rời quán

| Khâu | Cách hoạt động | Nguồn |
|---|---|---|
| Số khách/ngày | `min(DemandWithAttract × sự kiện ngày, CapacityFor)`; `Demand_T1 = 18 + 9(★-1) + 0,5·ngày`, nhân 1,5 (T2) / 2,6 (T3), × (1 + attract ≤ 40%) × (1 + 0,2/khu) | `CustomerSpawner.cs:39-50`, `GameBalance.cs:224-266` |
| Sức chứa | `ghế × 4` (T1), `ghế × 4,5 × (1+0,25·NV)` (T2), `ghế × 5 × (1+0,15·NV)` (T3). Chỉ tính GHẾ và số NV, KHÔNG tính tốc độ pha / giao / dọn. Comment ở `GameBalance.cs:143` tự nhận "sức chứa gấp ~5 lần thực phục vụ" | `GameBalance.cs:259-266` |
| Rải thời điểm | Cửa sổ sinh khách `[0,02; 0,80]` độ dài ngày (`GameBalance.cs:59-60`). Mỗi khách = vị trí phân tầng `(i+0,5+U(-0,4;0,4))/n` đưa qua nghịch đảo CDF của `ArrivalDensity` rồi sort. Đây là rải ĐỀU có nhiễu, KHÔNG phải Poisson | `CustomerSpawner.cs:53-70`, `ArrivalCurve.cs:58-66` |
| Đường cong | Mật độ (0→0,35, 0,15→1, 0,4→1,5, 0,6→0,8, 0,8→1,6, 1→0,5); trung bình 1,094 nên đỉnh = **1,46× trung bình** | `GameBalance.cs:65` |
| Chặn khi nghẽn | Chỉ T1-T2 (`Tier.Current < 3`), tắt khi hướng dẫn. Hoãn khách kế nếu ≥3 người tìm chỗ hoặc đơn chờ ≥ max(3, ghế/2). Tổng dời tối đa **45 s/ngày**, hai khách cách ≥1 s | `CustomerSpawner.cs:138-154`, `ArrivalCurve.cs:70-77`, `GameBalance.cs:66-68` |
| Giới hạn khách đồng thời | **Không có.** `active` là List không trần; sau khi sinh không có cổng nào nữa | `CustomerSpawner.cs:10,180-190` |
| Hết ghế | Khách VẪN sinh, vào `FindSeat`, thử 0,4 s/lần; quá `NoSeatSeconds`=20 s thì bỏ về (RP 0). Chờ món quá `PatienceSeconds`=60 s (× sự kiện: Mưa 1,3; Cao điểm 0,85) thì bỏ về, RP -1 | `Customer.cs:145-180, 274-287`, `GameBalance.cs:53-58` |
| Khách quen | `TryScheduleVisit` gắn khách quen vào lượt sinh ĐẦU TIÊN của ngày; không đổi mật độ | `CustomerSpawner.cs:91,185`, `RegularsManager.cs:81-96` |
| Sự kiện ngày | Nhân nhu cầu: Cao điểm 1,15 (kèm kiên nhẫn 0,85), Cuối tuần 1,10, Sinh viên 1,10, Mưa 0,80 | `DayEvents.cs:42-52` |
| Đổi giữa ngày | Mua ghế/đồ hút khách: `RefreshDemand` tính lại tổng rồi **dồn số khách tăng thêm vào phần ngày còn lại**; lên cấp: `RescaleRemaining` co giãn lịch | `CustomerSpawner.cs:108-128, 102-105` |
| Đồng hồ / tốc độ | Ngày 210 s thực (T1-2), 300 s (T3), `deltaTime` thuần. Người chơi KHÔNG có nút dừng/tua; `Time.timeScale` chỉ do `StoryDialog` (dừng) và bot (`-timescale`) | `DayCycle.cs:74-98`, `StoryDialog.cs:63`, `DebugAutoPilot.cs:165` |
| Pha Chuẩn bị | `Phase.Prep`: đồng hồ đứng, KHÔNG khách. Đã là quãng nghỉ giữa ngày nhưng không liên quan tới nhịp trong ngày | `DayCycle.cs:8,56-60` |
| Nhân viên | Chủ quán là người pha duy nhất ở T1-T2; Pha chế chỉ từ T3 (`StaffMaxCount`); Phục vụ/Dọn chỉ giải phóng ly và bàn | `GameBalance.cs:164-175` |

Nhánh `feat/prep-phase-pacing-missions`: các thay đổi chưa commit (art, ánh sáng, đồ họa, tutorial) KHÔNG đụng tới sinh khách. Phần "nhịp" đã có từ commit `e2ece88` (đường cong + chặn nghẽn, `NoSeatSeconds` 10→20) và đã được ghi ở economy §24.

## 2. Chẩn đoán có số

### 2.1 Tốc độ đến so với tốc độ phục vụ
Dựng lại bằng cách port `ArrivalCurve` + `Distribute` sang Python, 200 lần rải mỗi dòng. "Phút" là phút THỰC (1 s thực = 4,6 phút in-game ở ngày 210 s). Cấu hình mẫu là suy luận từ economy §7, §17, §18.

| Kịch bản (cấu hình mẫu) | Khách/ngày [ƯỚC] | Khoảng cách TB | Khoảng cách p5 / p50 / p95 | Khách/phút TB → đỉnh 30 s | Phục vụ thực tế/phút |
|---|---|---|---|---|---|
| T1 sớm (★1, ngày 3, 4 ghế) | 16 (trần ghế 16 < nhu cầu 20) | 10,2 s | 4,0 / 9,3 / 16,6 s | 5,9 → 8,9 | ~5,5 [ƯỚC] |
| T1 muộn (★2, ngày 11, 10 ghế, +20% attract) | 39 | 4,2 s | 1,7 / 3,9 / 7,2 s | 14,3 → 20 | ~5,5 [ƯỚC]; ngày 1-10 đo 24,7 khách/164 s ≈ 9/phút [ĐO §24.4] |
| T2 (★2, ngày 20, 14 ghế, 2 NV) | 67 (đo T2: 33-47 phục vụ [ĐO §17]) | 2,4 s | 1,0 / 2,3 / 4,3 s | 24,5 → 32 | ~14-17 [ĐO §17] |
| T3 (★3, ngày 60, 44 ghế, 8 NV, 1,2 khu) | 247 (đo: phục vụ 61-90, mất 100-180 [ĐO §19.3, §24.3]) | 0,9 s | 0,4 / 0,9 / 1,7 s | 63 → 85 (**1,4 khách/giây**) | ~18-20 [ĐO] |

Nhận xét:
1. **Gốc rễ là cung > cầu 2,5-4 lần**, không phải ngẫu nhiên. Công thức khách/ngày (`TargetCount`) dựa vào ghế và hệ số cố định, không biết tốc độ pha/giao/dọn thực. Từ T1 muộn trở đi tốc độ đến vượt hẳn khả năng phục vụ, nên khách chất đống, thanh kiên nhẫn đỏ hàng loạt và người chơi cảm thấy "bị dí".
2. **Không phải Poisson**: khoảng cách quanh trung bình khá đều (rải phân tầng). Dồn cục đến từ (a) đường cong đỉnh 1,46× trung bình, (b) tổng quá lớn so với dịch vụ, (c) `RefreshDemand` dồn thêm khách vào đoạn còn lại khi mua ghế giữa ngày.
3. **Vì sao "game tăng tốc" theo thời gian**: số khách/ngày tăng theo `+0,5·ngày` (không trần), `+9/★`, ×1,5 → ×2,6 khi lên cấp, +20%/khu, attract tới +40%, sự kiện Cao điểm/Cuối tuần, trong khi độ dài ngày không đổi (210 s) và tốc độ pha của chủ quán không đổi. Khoảng cách TB giảm từ 10 s (T1) xuống 0,9 s (T3).
4. **Chặn nghẽn hiện tại không đủ**: chỉ dời tối đa 45 s trên cả ngày (≈27% cửa sổ 164 s), chỉ T1-T2, và bot đo kém hơn ở T3 khi áp (economy §24.4: −5 khách, −520 Xu/ngày). Nó trì hoãn chứ không giới hạn số khách đang trong quán.
5. **Hết chỗ/kiên nhẫn chạy song song**: mỗi khách không có ghế đồng hồ 20 s chạy; mỗi khách đã ngồi đồng hồ 60 s chạy; nhiều thanh cùng đỏ một lúc là phần lớn cảm giác "gấp" [ƯỚC, cần playtest xác nhận].

### 2.2 Mô phỏng hàng đợi (ƯỚC, đối chiếu thô với số đo)
Mô hình: 1 hàng đợi FIFO, thời gian phục vụ gamma quanh `s_eff` (T1 11 s, T2 3,8 s, T3 2,7 s hiệu dụng), ghế giữ tới khi uống xong + dọn, kiên nhẫn 60 s, hết chỗ 20 s, 30 lần chạy. Hiệu chuẩn thô: mô phỏng phục vụ T2 = 51 (đo 33-47), T3 = 109 (đo 61-90) nên **mô hình hơi lạc quan về dịch vụ**, tức mức giảm khách thật còn lớn hơn bảng dưới. Chưa mô phỏng chặn nghẽn cũ, đường đi, nhân viên Dọn/Phục vụ.

| | Hiện tại: sinh / phục vụ / mất / đỉnh trong quán | Cổng theo tải (đề xuất): sinh / phục vụ / mất / đỉnh |
|---|---|---|
| T1 muộn | 39 / 21,0 / 18,0 / 15,5 | 22,6 / 22,2 / 0,5 / 5 |
| T2 | 67 / 51,3 / 15,7 / 21,2 | 58,2 / 58,2 / 0 / 9 |
| T3 | 247 / 108,6 / 138 / 67,8 | 112,3 / 112,3 / 0 / 12 |

Kết luận ƯỚC: phục vụ không giảm (vì dịch vụ vốn là nút thắt, cổng chỉ ngăn khách chất đống); khách đồng thời giảm 3-5 lần; mất khách gần 0. Đây là giả thuyết cần bot xác nhận (mục 5).

## 3. Các phương án

| PA | Ý chính | Ưu | Nhược |
|---|---|---|---|
| (a) Cổng theo tải (token/leaky bucket) | Chỉ cho khách mới vào khi `đang trong quán < L(μ̂)` | Bám đúng nguyên nhân gốc; tự co giãn theo nâng cấp NV/máy; tự chặn tăng tốc theo ★/ngày; ít tham số | Cần đo μ̂; phải chống vòng xoắn "ít khách → μ̂ thấp → ít khách" |
| (b) Khoảng cách tối thiểu + nhiễu | Không bao giờ đến dưới `g_min` | Dễ; hết "chùm" | Không giải quyết tổng quá tải (T3 TB 0,9 s) |
| (c) Đợt cao điểm báo trước | Banner trước đỉnh, ramp, quãng nghỉ | Tăng khả năng kiểm soát tinh thần | Chỉ cảm giác; không giảm tải thật |
| (d) DDA theo thành tích | Giảm tốc khi mất khách/chờ lâu | Công bằng, cá nhân hóa | Dễ bị khai thác (cố ý mất khách), khó debug nếu dùng trong ngày |
| (e) Nút người chơi | Dừng, biển "hết bàn", tua chậm | Cho quyền kiểm soát rõ ràng | Chỉ giải quyết nếu người chơi chủ động; biển "hết bàn" cần UI + luật kinh tế |

**Chọn ĐÚNG MỘT làm lõi: (a) Cổng theo tải**, kèm hai thành phần nhỏ của (b) và (c) (khoảng cách tối thiểu có nhiễu, banner đỉnh). (d) chỉ ở mức theo ngày, tuỳ chọn, mặc định tắt. (e) chỉ nút Dừng (rẻ), biển "hết bàn" để sau.

## 4. Thuật toán đề xuất: `ArrivalPacer`

Lớp thuần `static class ArrivalPacer` (đặt trong `Scripts/Customers/ArrivalPacer.cs`, cùng kiểu `SpawnThrottle`/`BrewerRules`) + một lớp trạng thái nhỏ `ServiceRateEstimator`.

**Trạng thái** (ở `CustomerSpawner`):
- `inFlight` = số khách ở `FindSeat | WalkToSeat | Sitting | WaitOrder` (chưa được phục vụ).
- `muHat` (khách/giây): ước lượng tốc độ phục vụ. Cập nhật khi nghe `GameEvents.CustomerServed`.
- `pending` = số khách đến hạn lịch nhưng cổng đóng (đợi ngoài cửa, chưa tạo GameObject).
- `lastSpawn`.

**Công thức**
1. Tải cho phép: `L = clamp(ceil(muHat · WTarget) + WalkBuffer, Lmin, ceil(0,6 · ghế))`.
2. Khoảng cách tối thiểu: `gMin = clamp(0,6 / muHat, 1,5 s, 8 s)`, nhân nhiễu `U(0,75; 1,25)` mỗi lần.
3. Mỗi frame: khi `lịch[next] <= t` (đã đến hạn) → `pending++`. Nếu `pending > 0 && inFlight < L && t - lastSpawn >= gMin` thì `Spawn()`, `pending--`, `lastSpawn = t`.
4. Hạn ngoài cửa: khách `pending` quá `HoldMax` 30 s thì hủy im lặng (không tính "mất", không phạt RP, không lên `DaySummary.lost`); `pending` tối đa 3, vượt thì hủy khách cũ nhất. Không bao giờ có cú xả dồn khi cổng mở.
5. Kéo sớm chống nhàn rỗi: nếu `inFlight <= 1` và khách kế còn ≤ 8 s thì sinh ngay (giữ pha chế luôn bận, giữ doanh thu).
6. Ước lượng `muHat`: EWMA theo thời gian bận (chỉ cộng dồn khi `đơn đang chờ > 0` hoặc `đang pha`), τ = 45 s thực. Kẹp trong `[0,5; 3] × muPrior(tier)`. Reset về `muPrior` khi đổi cấp. Chống vòng xoắn: không cập nhật khi hệ thống nhàn rỗi và có sàn `0,5 × muPrior`.

**Hằng số khởi điểm** (đặt trong `GameBalance`, tunable):

| Tên | Giá trị | Lý do |
|---|---|---|
| `PacerWTarget` | 25 s | = 42% kiên nhẫn 60 s; hàng đợi dự kiến chờ ≲ 25 s |
| `PacerWalkBuffer` | 2 | khách đang đi bộ chưa vào hàng |
| `PacerLmin` | 3 | tránh quán trống |
| `muPrior` T1 / T2 / T3 | 0,09 / 0,25 / 0,33 /s | ≈ 5,5 / 15 / 20 khách/phút [ĐO §17-18, §24.4] |
| `PacerGapMin / Max` | 1,5 / 8 s | |
| `PacerHoldMax`, `PacerPendingMax` | 30 s, 3 | |
| `PacerEwmaTau` | 45 s | |

Với bảng trên: L ≈ 5 (T1), 9 (T2), 12 (T3). Trần `ceil(0,6·ghế)` giữ quán không chật kín.

**Lớp phụ**
- **Báo đỉnh (c)**: đỉnh ở x=0,4 và x=0,8 của cửa sổ sinh (tính từ `ArrivalDensity`). 15 s thực trước mỗi đỉnh: banner "Giờ cao điểm sắp tới"; sau đỉnh: "Quán vắng dần". Đường cong giữ nguyên (đỉnh có thể hạ 1,6 → 1,35 sau đo; cổng đã tự chặn tải).
- **Chỉ báo nhịp** trong HUD cạnh đồng hồ: `inFlight / L` ba màu (<0,6 xanh, 0,6-0,9 vàng, ≥0,9 đỏ) + chữ "Quán đang đông" và số khách xếp ngoài cửa (`pending`). Người chơi nhìn thấy nhịp thay vì chỉ thấy thanh kiên nhẫn.
- **Nút Dừng (e)**: `Time.timeScale = 0` trong `Phase.Open`, tái dùng cơ chế của `StoryDialog` (audio đã duck theo timeScale ≤ 0, `AudioHost.cs:190`). Phải tôn trọng `previousTimeScale`.
- **DDA theo ngày (d)**, tuỳ chọn, mặc định tắt: nếu 2 ngày liền tỷ lệ mất ngoài hạn bỏ >15% thì `WTarget` −5 (sàn 15); <5% thì +5 (trần 32). Chỉ đổi giữa các ngày, không đổi giá/thưởng nên khó khai thác.

**Tương tác**
- Tổng khách/ngày (`TargetCount`) thành TRẦN, không đổi: kinh tế ở nơi dịch vụ ≥ cầu (T1 sớm) không đổi. `PlannedCount`/`SpawnedCount` khác nhau: kiểm tra `TutorialDirector.cs:231-232` (đang dùng cả hai).
- Hướng dẫn (`TutorialGate.Active`): tắt cổng (giữ hành vi cũ).
- Khách quen: vẫn gắn vào lượt sinh đầu; khi cổng đóng, `scheduledRegular` giữ nguyên tới lúc sinh được (không mất lượt). Tùy chọn sau: đưa khách quen vào giữa ngày.
- `RefreshDemand`/`RescaleRemaining`: không cần đổi; cổng chặn cú dồn.
- Sự kiện ngày: `demand` vẫn tác động trần; nên coi `patience` của Cao điểm (0,85) như tham số cho `WTarget` (× 0,85).
- Offline: **[CHƯA ĐO]** không thấy khách chạy khi tắt app (thu nhập offline theo công thức §8), nên không ảnh hưởng; cần xác nhận khi có hệ offline.
- Nhiệm vụ: `serve_30` đếm số PHỤC VỤ (`MissionCatalog.cs:68`) nên không đổi; nhiệm vụ dựa vào `lost - lostNoSeat` (`GameBootstrap.cs:698`) sẽ dễ hơn vì mất khách ít đi (chấp nhận được, kiểm tra lại giá trị mục tiêu).
- Hiệu năng: đỉnh khách đồng thời giảm (ƯỚC T3 68 → 12), giảm số `NavMeshAgent`/`CustomerVisual`. Kế hoạch tối ưu hiệu năng riêng, không chỉnh ở đây.

## 5. Chỉ số và tiêu chí nghiệm thu

Thêm log mỗi ngày vào `DebugAutoPilot` (số khách đỉnh trong quán, p95 chờ món, khách/10 s, số hủy ngoài cửa, CV khách/10 s). Chạy A/B cùng máy: `-nopacer` (cờ mới) và mặc định, cờ như economy §24.2.

| Chỉ số | Cách đo | Đạt khi |
|---|---|---|
| Đỉnh khách đồng thời | log theo ngày | ≤ `L` (cứng) và ≤ 0,7 × ghế |
| p95 chờ món | `Customer.waited` | ≤ 40 s (kiên nhẫn 60 s) |
| Tỷ lệ mất khách | `lost / sinh` | 5-15% ở T2/T3; ≤5% ở T1; `lostNoSeat` ≈ 0 |
| Burstiness | CV của số khách mỗi 10 s trong cửa sổ sinh | ≤ 0,6 (ƯỚC hiện 0,3-0,7 nhưng ở mức tuyệt đối 3-4× cao hơn dịch vụ) |
| Phục vụ/ngày (bot, ngày 81-100) | A/B | ≥ 0,97 × `-nopacer` và ≥ 61,0 (mốc §24.3, mục tiêu cũ ≥ 69) |
| Lãi/ngày (ngày 81-100) | A/B | ≥ 0,95 × `-nopacer`, tham chiếu 2.867 (có chặn) / 3.387 (`-nothrottle`) / 4.545 (§23.7) |
| RP/ngày | A/B | ≥ baseline (kỳ vọng tăng vì ít phạt −1) |
| Tiến độ | ngày lên Mặt bằng / Nhà hàng | 7 / 46 (±2, §24.4); nếu RP tăng khiến T3 sớm >3 ngày thì chỉnh `WalkAwayRp` hoặc `Tier3StarRP`, không chỉnh cổng |
| Doanh thu T1 | bot ngày 1-10 | giữ 24,7 khách / 2.145 Xu (§24.4) ±10% |

Lưu ý: bot chạy `-timescale 8` nhưng mọi logic dùng `deltaTime`, nên cổng và EWMA phải dùng thời gian game (không dùng `unscaledTime`).

**Playtest người thật (checklist)**: (1) 10 phút liền ở T1 ngày 8-12, T2 ngày 20, T3 ngày 60: có lúc nào hơn 3 thanh kiên nhẫn đỏ cùng lúc? (2) Có đủ thời gian thở sau đỉnh (≥ 10 s thực)? (3) Banner đỉnh có hiểu được? (4) Chỉ báo nhịp có gây lo lắng hay thấy nhàm? (5) Có lúc quán vắng quá 15 s thực? (6) Mua ghế/NV giữa ngày có thấy nhịp nhanh hơn tương ứng? (7) Ngày Cao điểm/Mưa có khác biệt nhận ra được? (8) Dừng/tiếp tục có làm đứt âm thanh hay đồng hồ ngày?

## 6. Kế hoạch triển khai

| # | Việc | Cỡ | File chính | Test |
|---|---|---|---|---|
| 1 | Đo trước: log nhịp mỗi ngày + cờ `-nopacer`; chạy bot 100 ngày ×2 làm mốc `-nopacer` | S | `DebugAutoPilot.cs`, `DaySummary.cs` | — |
| 2 | `ArrivalPacer` thuần: `LoadLimit`, `MinGap`, `CanAdmit`, `UpdateMu`, `PullForward`, `RushWarn` | S | `Customers/ArrivalPacer.cs`, `GameBalance.cs` | `ArrivalPacerTests.cs` (mới, EditMode) |
| 3 | Tích hợp vào `CustomerSpawner.Update` thay khối `IsCongested`/`MinSpawnGap`; cờ `PacerEnabled` kiểu `ThrottleEnabled`; reset `muHat` khi đổi cấp | M | `CustomerSpawner.cs`, `GameBootstrap.cs` | thêm vào `ArrivalCurveTests.cs` |
| 4 | Bot A/B 100 ngày ×2 máy, chỉnh hằng số (đặc biệt `WTarget`, `muPrior`, `gMin`), ghi kết quả vào economy §25 | M | `economy_monetization.md` | — |
| 5 | HUD: chỉ báo nhịp + banner đỉnh + chuỗi tiếng Việt | S | `HudController.cs`, `HudBuilder.cs`, `Strings.cs` | — (kiểm tay) |
| 6 | Nút Dừng trong `Phase.Open` | S | `HudBuilder.cs`, `StoryDialog.cs` (dùng chung `timeScale`) | `PauseRulesTests` nhỏ |
| 7 | Tuỳ chọn: hạ đỉnh `ArrivalDensity`, DDA theo ngày, biển "hết bàn" | M-L | `GameBalance.cs`, `ShopSign.cs` | thêm test DDA |

**Test cần thêm** (`ArrivalPacerTests`): (1) `LoadLimit` đơn điệu theo `muHat` và bị kẹp [Lmin, 0,6·ghế]; (2) `CanAdmit` đóng khi `inFlight >= L`, mở lại không xả quá 1 khách mỗi `gMin`; (3) `UpdateMu` không rơi dưới `0,5×prior` khi rảnh, kẹp ≤ `3×prior`; (4) `PullForward` chỉ khi `inFlight <= 1`; (5) mô phỏng hạt (seed cố định, hàng đợi FIFO đơn giản trong test): đỉnh khách ≤ L và phục vụ ≥ 0,95 × không cổng; (6) `RushWarn` đúng vị trí đỉnh từ `ArrivalDensity`.

**Rủi ro và hoàn tác**
- RP tăng (ít phạt) làm lên cấp sớm hơn → xem tiêu chí tiến độ.
- Vòng xoắn `muHat` → sàn `0,5×prior` + chỉ cập nhật khi bận + kéo sớm.
- Bot vs người: bot hiện không đại diện người chơi giỏi (economy §23.7); số bot chỉ chặn hồi quy.
- `PlannedCount` ≠ `SpawnedCount` cuối ngày có thể ảnh hưởng tutorial; kiểm tra `TutorialDirector.cs:231`.
- Hoàn tác: `GameBalance.PacerEnabled = false` (cờ tĩnh, cùng cách với `ThrottleEnabled`) đưa về hành vi hiện tại, kể cả chặn nghẽn cũ T1-T2; không đổi save format.

## 7. Điều chưa đo được (trung thực)
- Không chạy Unity/bot 100 ngày; mọi số "ƯỚC" là mô phỏng Python đơn giản (1 hàng đợi, không có đường đi, Dọn/Phục vụ, chặn nghẽn cũ). Lệch so với số đo: phục vụ ước T2 51 vs đo 33-47, T3 109 vs 61-90.
- `muPrior` suy từ số khách phục vụ/ngày của bot (không phải người chơi giỏi). Người chơi giỏi sẽ có μ cao hơn; EWMA sẽ bù, nhưng chưa kiểm.
- Chưa có dữ liệu người thật về ngưỡng "gấp": các ngưỡng L, 25 s, 15% là giả thuyết chờ playtest.
