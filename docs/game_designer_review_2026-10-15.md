# Đánh giá game designer – 2026-10-15 (vòng kế tiếp)

> Chỉ đọc, không sửa code. Số đo lấy từ `game/Logs/bot100.log` (bot lý tưởng hóa): đọc kỹ ngày 100, lướt ngày 62 và 73; các số còn lại là tóm tắt của người giao việc. Cuối file có mục "Đo được / Suy luận".

## Tóm tắt
1. **Mất 2/3 khách ở cấp 3 là lỗi cân bằng + thiếu luật thiết kế, không phải lỗi bot.** Ngày 100 mất 199 khách thì **180 là "hết chỗ"** (90%), chỉ 12 hết kiên nhẫn, 0 vì bẩn. Nhu cầu cấp 3 (293 khách đến) gấp ~3 lần số quán phục vụ được (94), và mỗi khách "hết chỗ" bị trừ −1 RP.
2. **RP ≈ 0 là hệ quả toán học**: 94 × 1,75 = +165, trừ 199 × 1 = −199. Log: RP 3.297 (đầu ngày 62) → 3.000 (ngày 73, chạm sàn ★3) → 3.015 (ngày 100). ★4 = 40.000 RP không thể đạt dù bỏ phạt (cần ~250 ngày).
3. **Lên cấp 3 ngày 57 không đáng lo. Đáng lo là cấp 3 chưa trả công**: lãi TB ~5.500/ngày so với 3.000–5.700 (cấp 2, còn gánh thuê ~714/ngày) trong khi tốn 122.000 Xu + 2.950 lương/ngày. Hoàn vốn ~60 ngày đầu cấp 3.
4. **Chóng mặt**: nghi phạm hàng đầu là quán tính (thả tay còn quay ~160° trong ~1,6 s), 0,28°/px (vuốt 400 px ≈ 112°), xoay tự do 360°, chụm 2 ngón xoay không ngưỡng, 30 FPS. Sửa gói camera: việc nhỏ, rủi ro thấp, làm trước.
5. Nhiệm vụ ngày cấp 3 đặt cao hơn số đo thật (serve 120 > tối đa ~94–100; tip 2.400 > 1.571) và `no_walkaway` bất khả thi khi "hết chỗ" còn tính là bỏ đi.

---

## A. Cân bằng cấp 3

### A1. Chẩn đoán (ngày 100: 43 ghế, 8 NV, ★3)
| Chỉ số | Giá trị | Ý nghĩa |
|---|---|---|
| Khách đến / phục vụ / mất | 293 / 94 / 199 | Hết chỗ 180, kiên nhẫn 12, bẩn 0, khác 7 |
| Lượt/ghế/ngày | 94 / 43 = **2,2** | Thiết kế: `CapacityFor(3,43,8)` = 43×5×2,2 = **473** (gấp 5 lần thực). §18 đo 3,2 |
| Chiếm ghế TB (suy ngược) | 43 × 164 s ÷ 94 ≈ **75 s** | 164 s = cửa sổ sinh khách (0,02–0,80 × 210 s). Ghế trống mỗi ~1,75 s, khách đến mỗi ~0,55 s |
| Doanh thu / khách | 163 + tip 17 = 180 Xu | Nguyên liệu 56/khách (35%) |
| Lãi | 7.899 (đã trừ lương 2.950 + phí 700) | Chưa gồm nhiệm vụ (~1.900/ngày ở cấp 3: 50–60 × 12 × 3) |

**Vì sao đây là lỗi thiết kế, không chỉ cân bằng:** GDD/economy §3.2 ghi `Khách thực = min(Demand, Capacity)`, tức khách vượt sức chứa **không tồn tại**. Code sinh theo Demand rồi để khách vào cửa, chờ 10 s (`NoSeatSeconds`), quay ra và trừ RP. Thêm nữa, `SeatTurnsTier3 = 5` không thể đạt: uống 8–15 s, pha, mang, chờ lau bàn; ngày chỉ 210 s.

### A2. RP âm có hợp lý không?
Không. "Quán đầy" là tín hiệu tốt, không phải sự không hài lòng; GDD §3.2 chỉ nói "bỏ đi vì chờ lâu thì mất danh tiếng nhẹ". Phạt CI < 40 (`CiRpPenaltyBelow`) chỉ vài RP/ngày, không phải thủ phạm. Giả định S = 0,75 và 94 phục vụ, bỏ phạt "hết chỗ" cho **+145 RP/ngày** (S = 0,85: +192).

### A3. Chỉnh gì (số cụ thể)
| Hằng số / file | Hiện tại | Đề xuất | Lý do |
|---|---|---|---|
| `WalkAwayRp` (GameBalance.cs:35) | −1 cho mọi lý do | Tách: `WalkAwayRpNoSeat = 0`, `WalkAwayRpPatience = −1` (bẩn/kiên nhẫn giữ −1) | Bỏ phạt nhầm. Nơi gọi chưa đọc, cần tìm |
| Luật sinh khách (spawner, chưa đọc) | Sinh theo Demand | Không sinh thêm khi số khách đang chờ chỗ ≥ `max(4, 0,15 × ghế)` (43 ghế → 6). `NoSeatSeconds` 10 → 20 | Khớp economy §3.2; hết cảnh ~180 lượt vào-ra cổng |
| Tổng kết ngày (UI) | Không có | Dòng "Khách chưa vào được: N" + gợi ý "mua thêm ghế/bàn" khi N > 20% | Biến nhu cầu dư thành mồi nâng cấp |
| `StarThresholds[3]` (GameBalance.cs:170) | 40.000 | **12.000** (~62 ngày sau ★3 ở +145 RP/ngày, ★4 quanh ngày 120 trên bot); ★5 120.000 → 40.000 tạm | Sửa luôn doc §4.2 |
| `SeatTurnsTier3`, `StaffCapacityBonusTier3` (:97, :99) | 5 / 0,15 | 2,5 / 0,05 (theo số đo) | Để `CapacityFor` không gợi ý ghế "ảo". Nếu chỉ bot dùng thì sửa doc |
| Nếu **không** làm luật sinh khách: `Tier3DemandMult` (:95) | 2,6 | 1,3 (≈146 khách/ngày, mất ~35%) | Phương án dự phòng, kém hơn |
| `Tier2RentPerWeek` + `RentIntervalDays` (:77–78) | 5.000 / 7 ngày | 714 / 1 ngày, tổng kết hiện dòng "Tiền thuê" | Hết ngày đỏ (ngày 45: −1.897) cho game "không phạt nặng". Kiểm tra chỗ nào khác giả định 7 |
| `MissionCatalog.All` serve_30 / tips_300 (cấp 3) | 120 / 2.400 | 65 / 1.000 (cấp 2: 45 / 800, chốt lại theo log T2) | Mục tiêu ≈ 65% số bot (giả định người thật chậm hơn) |

**Chưa nên chỉnh vội: lợi nhuận mỗi ly và nhu cầu.** Chưa biết nút cổ chai là ghế hay sức lao động (xem A5). Số khách phục vụ gần như phẳng (90–100) dù ghế 36 → 43 và NV 4 → 8, nghiêng về sức lao động: 8 NV + chủ quán ≈ 9 người × 164 s ≈ 1.476 giây-công ÷ 94 ≈ 16 giây-công mỗi khách (pha + mang + lau). Giả thuyết, chưa đo.

### A4. Nhịp lên cấp 3 sớm hơn dự đoán (ngày 57 so với 70–75)
- Không đáng lo: chênh 13–18 ngày ≈ 45–60 phút chơi. Nguyên nhân: lãi cấp 2 đo 3.000–5.700, cao hơn giả định 2.100 của §19. Người thật chậm hơn bot nên khả năng cao rơi vào dải 65–90.
- Cái đáng lo là **sau** cấp 3: lãi chỉ gấp ~1,2–2× cấp 2 (doc hứa 3–4×), và T4 (CN1 2.000.000) mất 225–360 ngày nếu để dành 100% lãi. Cấp 4 chưa làm nên chưa sửa; ghi quy tắc: `branch_cost_base` ≈ 60 ngày × tiết kiệm cấp 3 sau khi cấp 3 đạt mục tiêu A6.
- Ngày 62 (2 NV, 36 ghế): khách có mặt 8 → 26 → 44 → 59 trong ~45 s đầu, đơn chưa nhận 25 sau 30 s. Ngày 73 (4 NV, chưa có Pha chế): đơn chưa nhận 20–36 suốt ngày. Khoảnh khắc "khai trương" đang là lúc người chơi bị ngợp nhất (xem D6).

### A5. Hai thí nghiệm bot trước khi chỉnh giá/nhu cầu (mỗi cái ~20 ngày cấp 3, dùng `-cheatt3`)
1. Khóa 30 ghế so với 43 ghế, cùng nhân viên: nếu số phục vụ không đổi thì ghế không phải cổ chai.
2. Tất cả NV Lv1 so với Lv3 (nâng 8 NV lên Lv3 ≈ 28.000 Xu; Lv5 ≈ 203.000): nếu phục vụ tăng thì cổ chai là sức lao động và nâng cấp NV là đòn bẩy làm cấp 3 đáng tiền.
Thêm vào log ngày: thời gian chiếm ghế TB, thời gian chờ ghế TB, đơn chưa nhận TB, Lv NV.

### A6. Mục tiêu số đo cho lần bot kế tiếp (100 ngày, cùng cờ lệnh)
| Chỉ số | Hiện tại | Mục tiêu |
|---|---|---|
| Lên cấp 3 | ngày 57 | ngày 50–65 (giữ) |
| Mất khách / khách đến (cấp 3) | 68% | ≤ 30% (≤ 10% nếu có luật sinh khách) |
| RP/ngày cấp 3 (S = 0,75) | ≈ 0, kẹt sàn 3.000 | ≥ +120; ★4 trước ngày 130 |
| Phục vụ/ngày, lượt/ghế | 90–100, 2,2 | ≥ 130 ngày 80; ≥ 160 ngày 100; ≥ 3,2 lượt/ghế |
| Lãi ròng cấp 3 (không gồm nhiệm vụ) | 5.500 TB; 7.500–8.900 cuối | ≥ 9.000 tuần đầu; ≥ 11.000 từ ngày 75 (≥ 2× đỉnh cấp 2) |
| Đơn chưa nhận | 20–36 (ngày 62–73) | ≤ 10 trong 90% thời gian |
| Ngày lỗ cấp 2 | có (−1.897) | 0 |
| Hài lòng TB | 0,75 | ≥ 0,78; giữ ★3 |
| Pha Chuẩn bị | 99/99 | 100% |

---

## B. Chóng mặt (CafeCameraController.cs)

### B1. Nguyên nhân, từ khả năng cao xuống thấp
1. **Quán tính 720°/s, damping 4,5**: thả tay quay thêm 720/4,5 = 160°, còn >0,5°/s trong ~1,6 s. Chuyển động người chơi không điều khiển là tác nhân vection mạnh nhất.
2. **0,28°/px** (ở 1080p): vuốt 400 px = 112°, cả bề ngang 1920 px = 537°. Cộng độ trễ `SmoothDampAngle` 0,08 s nên hình không bám ngón.
3. **Xoay 360° tự do**: mất phương hướng, không có "hướng gốc" để quay về. Quán tycoon ít khi cần nhìn từ phía sau.
4. **Chụm 2 ngón** (dòng 286–289): zoom + pan + xoay yaw cùng lúc, `yawTarget -= DeltaAngle(...)` không ngưỡng nên ngón lệch vài độ là xoay. Cầm ngang bằng hai ngón cái dễ kích hoạt cử chỉ ngoài ý muốn.
5. **30 FPS ở mức Thấp**: cùng tốc độ xoay thì mỗi khung nhảy gấp đôi (300°/s = 10°/khung), khuếch đại mọi mục trên.
6. **Vuốt dọc làm nghiêng** (0,12°/px, 30–70°, không vùng chết): chéo tay là đổi phối cảnh sàn.
7. Thấp hơn: khung hình tự trượt khi mở/đóng bàn pha (`bottomInset`, k = 12, tới 60% chiều cao, ~0,25 s, xảy ra ở mỗi đơn); sàn gạch bông tương phản cao + ~70 NPC chuyển động (khuếch đại, chưa kiểm).

### B2. Giá trị đề xuất
| Tham số | Hiện tại | Đề xuất |
|---|---|---|
| `YawDegPerPixel` (dòng 20) | 0,28 | **0,16** (vuốt 400 px = 64°) |
| Trần tốc độ xoay khi kéo (thêm vào `DragOrbit`) | không | 150°/s |
| Trần quán tính (`EndOrbit`, dòng 257) | ±720°/s | **±120°/s** |
| `InertiaDamping` (dòng 22) | 4,5 | **8** (thêm ≤ 15°, tắt sau ~0,7 s) |
| Phạm vi yaw | 360° | **±60°** quanh `DefaultYaw`, đàn hồi (nhân 0,25 khi vượt, bật lại 0,25 s); nút "Góc nhìn gốc" trong MENU |
| `MinPitch`/`MaxPitch` | 30°/70° | 38°/62° |
| `PitchDegPerPixel` | 0,12 | 0,07 + khóa trục: sau ngưỡng 10 px, trục trội (≥ 1,6 lần) thắng; vuốt dọc < 12 px bị bỏ |
| Xoay khi chụm | mọi góc | **Bỏ hẳn** (một ngón đã đủ xoay). Tối thiểu: ngưỡng tích lũy 15°, hệ số 0,5, chỉ khi \|Δspan\| < 6% |
| Chọn chế độ 2 ngón | cùng lúc | Chốt zoom hoặc pan sau 12 px; bỏ qua nếu hai ngón cách > 55% bề ngang màn hình (hai ngón cái) |
| Khung hình | Thấp: 30 | Nâng `Application.targetFrameRate` = 60 khi đang chạm hoặc camera còn chuyển động (+0,75 s), rồi về 30 |
| `bottomInset` | ~0,25 s | smoothstep 0,4 s, tối đa 0,35 chiều cao |

### B3. Có nên thêm "Giảm chuyển động" trong Cài đặt?
**Có.** Là một bool đọc ở ~6 hằng số, chỗ đặt `SettingsPanel.cs` (đang sửa trong working tree, tránh xung đột). Khi bật: yaw ±25°, không quán tính, `YawDegPerPixel` ×0,6, khung hình không tự trượt, tắt nảy/bay của UI (toast, spring nút), ép 60 FPS khi chạm. Mặc định tắt; gợi ý 1 lần (toast) khi tổng góc xoay > 540° trong 20 s. Bộ giá trị mới ở B2 áp dụng cho **mọi người** (đây là sửa mặc định, không phải tùy chọn).

### B4. Cách xác minh
5 người (ít nhất 2 người từng thấy chóng mặt), 10 phút/bản: A = hiện tại, B = mặc định mới, C = Giảm chuyển động. Hỏi điểm 0–3 (chóng mặt, buồn nôn, mỏi mắt) trước và sau. Log: tổng \|Δyaw\| mỗi phút, số lần quán tính, số lần chụm kèm xoay > 15°. Đạt: ≥ 4/5 người ≤ 1 điểm ở bản B.

---

## C. Cảm giác chơi mượt hơn
- **Đầu ngày dồn dập.** Khách có mặt 8 → 26 → 44 → 59 trong 45 s đầu; `SpawnStartFraction = 0,02` và tốc độ đến phẳng. Đề xuất ramp 12–15 s đầu (0 → 100%), hai đỉnh quanh 30% và 65% cửa sổ, báo "Giờ cao điểm" 10 s trước. Nhịp "dồn rồi thả" làm ngày có hơi thở; ads "Giờ cao điểm" (economy §9) gắn vào được.
- **Cảnh cổng quán.** Ngày 100 có ~180 lượt khách vào rồi quay ra sau 10 s: nhộn nhạo, tốn NavMesh/FPS, tạo cảm giác thất bại. Luật sinh khách ở A3 xử lý.
- **Chờ ở cửa.** Khi khách chờ chỗ phải thấy vòng kiên nhẫn trên đầu; khách còn chờ thì tổng kết không được gọi là "mất".
- **Phản hồi khi chạm** (chưa đọc `TapInput`, kiểm trên máy): ngưỡng kéo `DragThresholdPx` nên ≥ 8dp (~21–24 px ở 1080p). Một ngón kéo = xoay, nên chạm lệch tay biến thành nhúc nhích camera thay vì chọn; chọn mục tiêu gần nhất trong ~60 px thay vì raycast điểm vì khách đang đi; phản hồi tick/rung nhẹ ≤ 100 ms khi chọn đơn/ly.
- **NV đứng không khi còn đơn.** Ngày 100 có mẫu "đơn chưa nhận = 8" lúc cả 8 NV Idle (3 Pha chế "Idle", chặn rỗng). Nếu do luật "NV chỉ pha món 3★ ≥ 5 lần" (GDD §4.3), người chơi thấy NV đứng chơi khi khách chờ: hiện icon "chờ chủ quán pha" trên đơn đó, hoặc nới luật ở cấp 3.
- **Ngày đỏ khi trả thuê** (A3). **Prep:** thêm chấm đỏ trên "Chuẩn bị quán" khi đủ tiền mua món mới; nút "MỞ CỬA" cố định một vị trí, giữ ≤ 2 chạm giữa hai ngày.
- **Nhiệm vụ lệch cấp.** Log ngày 100 vẫn là nhiệm vụ cấp 1 (`tips_300` 300/300, `sell_bac_xiu` 8/8, key=2479): bộ nhiệm vụ sinh theo ngày thật, không sinh lại khi đổi cấp. Chưa rõ thưởng nhân `TierScale` lúc sinh hay lúc nhận; nếu lúc nhận thì có kẽ hở (sinh ở cấp 2, nhận ở cấp 3 ×3). Cần kiểm `MissionBook`.

---

## D. Việc đáng làm nhất ở vòng kế tiếp
| # | Việc | Lý do | Công sức | Rủi ro |
|---|---|---|---|---|
| 1 | Gói camera dễ chịu + "Giảm chuyển động" + 60 FPS khi chạm (B2, B3) | Phản hồi người chơi thật; chóng mặt đuổi người chơi ngay phiên đầu | Nhỏ (1 file camera + Cài đặt) | Thấp. Ai thích xoay tự do thì mất: nút "Góc nhìn gốc" bù |
| 2 | Tách RP "hết chỗ", luật sinh khách theo hàng chờ, dòng "Khách chưa vào được" (A3) | Gỡ RP ≈ 0 và cảnh cổng nhộn; mở đường ★4 | Nhỏ–vừa | Trung bình: mất "áp lực" cấp 3, bù bằng gợi ý mua ghế/bàn |
| 3 | Hai thí nghiệm bot A5 + thêm chỉ số vào log | Biết cổ chai là ghế hay sức lao động trước khi chỉnh giá | Nhỏ (chạy bot) | Không có; tránh chỉnh nhầm núm |
| 4 | Nhiệm vụ bám số đo (serve/tips cấp 3, `no_walkaway`) + kiểm `MissionBook` về cấp lúc sinh/nhận | Rương Hạt không nhận được đúng lúc lên cấp 3 | Nhỏ | Thấp |
| 5 | Ngưỡng ★4/★5 (12.000 / 40.000) + cập nhật doc §4.2, §7, §19 | ★4 không thể đạt, T4 bị khóa vô hình | Nhỏ | Thấp, nhưng làm sau việc 2 để khỏi chỉnh hai lần |
| 6 | Đường cong khách (ramp + 2 đỉnh + banner) và "khai trương mềm" 7 ngày: Demand ×0,5 → ×1, 1 Pha chế dùng thử | Khoảnh khắc lên cấp 3 là lúc bị ngợp nhất (đơn chưa nhận 25–36) | Vừa | Trung bình: đụng spawner và placement ads |
| 7 | Làm cấp 3 đáng tiền: lãi ≥ 2× cấp 2 (đòn bẩy theo kết quả việc 3: Lv NV, gọi thêm món, zone) | Mua nhà hoàn vốn ~60 ngày; T4 mất 225–360 ngày | Vừa–lớn | Cao: lan sang T4 và mission; làm sau việc 3 |
| 8 | Tiền thuê tính theo ngày + 2 dòng ở Tổng kết (A3) | Ngày đỏ trái trụ cột "ấm áp, không phạt nặng" | Nhỏ | Thấp. Kiểm chỗ giả định 7 ngày |

---

## E. Đo được và Suy luận
**Đo được (log hoặc code):** ngày 100: phục vụ 94, mất 199 (hết chỗ 180 / kiên nhẫn 12 / bẩn 0 / khác 7), doanh thu 15.288, tip 1.571, nguyên liệu 5.310, phí 700, lương 2.950, lãi 7.899, hài lòng 0,75, ghế 43, NV 8, CI TB 76,9 (cuối ngày 37,6); RP 3.297 → 3.000 → 3.015; khách có mặt và đơn chưa nhận ngày 62 và 73; Prep 99/99; các hằng số camera (0,28; 720; 4,5; 0,08) và tính toán 160°, 1,6 s, 112°; mục tiêu nhiệm vụ cấp 3 (120 / 2.400).

**Suy luận, cần thử trên máy thật hoặc bot:**
- Thời gian chiếm ghế ~75 s (tính ngược, chưa đo); ghế hay sức lao động là cổ chai (A5 sẽ trả lời).
- +145 RP/ngày chỉ đúng nếu S = 0,75 và 94 phục vụ giữ nguyên; hoàn vốn ~60 ngày dựa trên lãi cấp 2 giữa dải 3.000–5.700.
- "Người thật ≈ 60–70% bot" là giả định, dùng để đặt mục tiêu nhiệm vụ.
- Thứ tự nguyên nhân chóng mặt là xếp hạng của tôi từ 1 phản hồi; FOV, FPS thực tế trên máy đích, ảnh hưởng của lens-shift và ngưỡng 55% bề ngang cho 2 ngón cái đều chưa kiểm.
- Chưa đọc: `TapInput` (ngưỡng kéo), spawner, nơi gọi `WalkAwayRp`, `MissionBook`, `DebugAutoPilot`, log cấp 2 chi tiết; mọi chỗ ghi "chưa đọc" cần xác minh trước khi sửa.
- Lý do "NV Idle khi còn đơn" (luật 3★ × 5) chỉ là một giả thuyết từ GDD §4.3.
