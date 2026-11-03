# Combo pha chuẩn + Giờ đông có nhịp — kế hoạch (2026-10-20)

Mục tiêu: trong một ngày, người chơi có **nhịp căng – thả** rõ ràng và có lý do để pha **khéo**, không chỉ pha **đủ**. Đây là phần đang yếu nhất của vòng chơi ngắn (review 10-16, mục C).

Nguyên tắc: **không đổi tổng số khách mỗi ngày**, thưởng thêm chỉ qua **tip**, và giới hạn tổng tip tăng thêm ≤ 5% doanh thu ngày để không lệch nhịp kinh tế (§21, §23.7).

## 0. Hiện trạng (đọc từ code)

- **Ly "chuẩn vị" đã được tính nhưng bỏ phí**: `BrewStation.Finish` tính `HeldPerfect` (mọi lần rót đều "Chuẩn!" và ly 3 sao, xem `PourRules.IsPerfectPourDrink`), nhưng không nơi nào dùng giá trị này.
- **Đã có sẵn 2 đỉnh khách**: `GameBalance.ArrivalDensity` = `{0,.35, .15,1, .4,1.5, .6,.8, .8,1.6, 1,.5}` trải trên cửa sổ sinh khách 2%–80% ngày. Đỉnh rơi vào khoảng **11:20** và **16:20**, nhưng người chơi **không hề thấy**: không báo trước, không biểu ngữ, không thưởng.
- **Throttle làm phẳng đỉnh**: ở cấp 1–2, `CustomerSpawner` dời lịch khách khi quán nghẽn (`SpawnThrottle.ShouldHold`, tối đa 45 giây mỗi ngày), nên đúng lúc đỉnh lại bị cắt bớt.
- **Sự kiện "Giờ cao điểm"** (`DayEvents.Rush`) chỉ là hệ số cho cả ngày (khách x1,15, kiên nhẫn x0,85), không có giờ cụ thể.
- **Pha chế bắt buộc phải có đơn** (`EnsureBrewing` cần `Selected`), nên không thể pha "khống" để nuôi combo.
- **Có 2 đường giao ly**: chủ quán tự giao (`TryServe` → `Customer.Serve(quality)`), hoặc nhân viên Phục vụ lấy ly ở quầy (`TakeReadyDrink`).

## 1. Combo pha chuẩn

### 1.1 Luật

| | Quy tắc |
|---|---|
| Tăng combo +1 | Chủ quán pha xong một ly **3 sao** (`HeldQuality == 3`). Ly chuẩn vị (`HeldPerfect`) được **+2**. |
| Đứt combo (về 0) | Bước sai (`RegisterWrong`), hỏng ly (`Ruin`), ly dưới 3 sao, hủy hoặc đổ ly đang cầm, hoặc **một khách bỏ về vì hết kiên nhẫn**. |
| Không ảnh hưởng | Thời gian rảnh không có đơn (không có đồng hồ đếm lùi), ly do NV Pha chế làm, chế độ thử công thức, ngày 1 trong hướng dẫn. |
| Hết ngày | Combo về 0, không lưu sang ngày sau. |

Lý do chọn "khách bỏ về thì đứt combo": buộc người chơi cân giữa pha thật kỹ và pha cho kịp, nên combo không chỉ là bài kiểm tra phản xạ.

### 1.2 Thưởng

Thưởng là **tip combo cộng ngay khi pha xong ly**, kèm hiệu ứng xu bay (`CoinFly`), để phản hồi tức thì ngay dưới tay người chơi.

| Combo | Bậc | Tip combo = giá ly × |
|---|---|---|
| 0–2 | — | 0 |
| 3–5 | "Khéo tay" | 10% |
| 6–9 | "Pha như gió" | 20% |
| ≥ 10 | "Barista!" | 30% (trần) |

- Trong **giờ đông** (mục 2), hệ số được ×1,5 (trần 45%).
- **Trần cả ngày**: tổng tip combo ≤ `ComboRules.DailyCapFloor` (30 Xu) + `DailyCapFraction` (4%) × doanh thu đồ uống tới lúc đó. Chạm trần thì vẫn hiện số combo nhưng không cộng thêm xu (hiện "Đã chạm trần thưởng combo" một lần mỗi ngày); trần tăng dần theo doanh thu nên các ly sau vẫn có thể được thưởng tiếp.
  - Đo bot (2026-11-03, cấp 1, 5 ngày, bot pha 3 sao gần như mọi ly, combo max 16–30): với 60 + 5% thì tip combo = 6,5–7,9% doanh thu, vượt mục tiêu; đã hạ còn 30 + 4%.
- **Đơn đặc biệt** (cấp 3) được +1 combo như ly thường. Bản thân đơn này đã x2 tiền, nên không thưởng chồng thêm.
- Ước lượng (cần bot xác nhận): ở cấp 1, khoảng 40% ly đạt 3 sao trong chuỗi ≥ 3. Tip combo ≈ 40% × 15% × doanh thu ≈ **3–6% doanh thu**, sát trần.

### 1.3 Phản hồi (phần quyết định cảm giác "đã")

- **HUD**: một huy hiệu `x7` cạnh bàn pha (`BrewPanel`). Mỗi lần tăng thì nảy lên, lên bậc mới thì đổi màu (kem → cam → đỏ).
- **Chữ nổi** (`FloatLayer`): "Chuẩn vị! +2", "Pha như gió! x6", "+36 xu". Đứt combo hiện "Đứt combo (x7)" màu xám, không dùng màu đỏ phạt.
- **Âm thanh** (`Sfx` + `tools/gen_audio.py`): thêm `combo_up` (cao độ tăng dần theo bậc), `combo_tier`, `combo_break` (nhẹ, không gắt). Mỗi lần lên bậc rung nhẹ (`Haptics`).
- Tôn trọng "Giảm chuyển động": tắt nảy và pháo giấy, chỉ đổi số.
- **Tổng kết ngày**: thêm dòng "Combo cao nhất x12 · tip combo +420".

### 1.4 Nhiệm vụ

Thêm 2 mẫu nhiệm vụ vào `MissionCatalog`:
- `combo_n`: đạt combo N trong ngày, N = 3/5/8/10 theo cấp.
- `perfect_n`: pha N ly chuẩn vị.

Cả hai không xuất hiện ở ngày 1–2.

## 2. Giờ đông có nhịp

### 2.1 Lịch trong ngày

Đặt hai khung giờ đông cố định, khớp với đỉnh sẵn có của `ArrivalDensity`, để **tổng khách không đổi**, chỉ thấy được:

| Khung | Giờ in-game | Thời gian thực (cấp 1–2 / cấp 3) |
|---|---|---|
| "Nghỉ trưa" | 10:45 – 12:15 | ~20 s / ~28 s |
| "Tan tầm" | 15:45 – 17:15 | ~20 s / ~28 s |

- 20 giây là ngắn. Nếu playtest thấy chưa kịp cảm nhận, nới thành 2 giờ in-game (~26 s / ~38 s) và làm đỉnh nhọn hơn (1,5 → 2,0).
- `ArrivalDensity` sẽ **được sinh từ** bảng khung giờ (một nguồn sự thật duy nhất), để sau này đổi giờ thì mật độ khách tự đổi theo.
- Ngày có sự kiện "Giờ cao điểm": thêm khung "Sáng đi làm" 7:00–8:15; các khung đông hơn, kiên nhẫn x0,85 chỉ áp **trong khung** thay cho cả ngày. Khi thực hiện cần kiểm tra trung bình nhu cầu của sự kiện này vẫn ≈ x1,15.
- Ngày 1–2 và lúc đang chạy hướng dẫn: không có giờ đông.

### 2.2 Throttle trong giờ đông

Throttle ở cấp 1–2 hiện cắt đúng đỉnh. Trong khung giờ đông, nới `ThrottleSeatWait` 3 → 4 và không tính thời gian dời lịch vào hạn mức 45 giây. **Phải đo bằng bot**: số khách bỏ về vì hết chỗ (`lostNoSeat`) không được tăng quá 2 khách/ngày ở cấp 1.

### 2.3 Báo trước – trong – sau

1. **Pha Chuẩn bị**: thẻ "Hôm nay" hiện thêm dòng "Đông lúc 11h và 16h", kèm vạch trên dải giờ.
2. **Còn khoảng 10 giây thực**: thông báo "Sắp tới giờ trưa! Chuẩn bị ly" kèm âm `rush_warn`.
3. **Trong giờ đông**:
   - Đồng hồ HUD có viền cam và nhãn "GIỜ ĐÔNG".
   - Nhạc nền chuyển sang bản nhịp nhanh (AudioHost đang có 3 bản nhạc; thêm một bản hoặc tăng tempo).
   - Hệ số combo ×1,5.
4. **Hết giờ đông**: thẻ nhỏ 2 giây "Giờ trưa: phục vụ 12/14 · combo x8 · +180 tip".
   - **Phục vụ ≥ 80%** khách đến trong khung: thưởng **Hạt 2–5** theo cấp.
   - **100%**: thêm một lượt "★ Giờ đông hoàn hảo", tính cho nhiệm vụ.

### 2.4 Cấp 3 (nhà hàng)

Ở cấp 3 nhân viên làm phần lớn việc, nên giờ đông là **bài kiểm tra cách xếp nhân viên**: tỉ lệ phục vụ tính cho cả quán, combo chỉ tính ly chủ quán tự pha. Sau này có thể thêm nút "Gọi thêm người" trong giờ đông (tốn Xu, NV thời vụ 1 khung). **Ngoài phạm vi** đợt này.

### 2.5 Nhiệm vụ

- `rush_clear`: phục vụ ≥ 80% trong N khung giờ đông.
- `rush_perfect`: có một khung giờ đông hoàn hảo.

## 3. Kỹ thuật

### 3.1 File mới (logic thuần, có test EditMode)

- `Scripts/Brewing/ComboRules.cs`: bậc combo, hệ số tip, xử lý trần ngày, quyết định cộng/đứt combo theo kết quả ly.
- `Scripts/Brewing/ComboTracker.cs`: trạng thái trong ngày (đếm combo, combo cao nhất, tổng tip combo). Phát `GameEvents.ComboChanged(count, tier)` và `ComboBroken(prev)`.
- `Scripts/Economy/RushSchedule.cs`: bảng khung giờ (theo cấp và sự kiện), hàm `PhaseAt(clockHours)` trả về Ngoài / Sắp tới / Trong / Vừa hết, và hàm sinh `ArrivalDensity`.
- `Scripts/Economy/RushTracker.cs`: đếm khách đến và phục vụ theo khung, phát `RushStarted`, `RushEnded(served, arrived)`.
- Test: `ComboRulesTests`, `RushScheduleTests` (gồm: tích phân mật độ mới = cũ ±2%; đỉnh nằm trong khung; ngày 1–2 không có khung), `RushTrackerTests`.

### 3.2 File sửa

| File | Thay đổi |
|---|---|
| `Brewing/BrewStation.cs` | `Finish` báo cho ComboTracker (quality, perfect); `RegisterWrong`, `Ruin`, `CancelOrDiscard` báo đứt combo; bỏ qua khi `Experiment`. |
| `Customers/Customer.cs` | Khách bỏ về vì kiên nhẫn → đứt combo; ghi nhận khách đến và được phục vụ cho RushTracker. Kiên nhẫn x0,85 chỉ áp trong khung giờ đông của ngày "Giờ cao điểm". |
| `Customers/CustomerSpawner.cs` | Nới throttle trong khung giờ đông (2.2). |
| `Core/GameBalance.cs` | Bảng hằng số combo/rush; `ArrivalDensity` sinh từ `RushSchedule`. |
| `Core/GameEvents.cs` | Thêm `ComboChanged`, `ComboBroken`, `RushStarted`, `RushEnded`. |
| `Economy/DaySummary.cs`, `UI/SummaryPanel.cs` | `maxCombo`, `comboTips`, kết quả từng khung giờ đông. |
| `Economy/DayEvents.cs` | Sự kiện "Giờ cao điểm" dùng khung giờ thay cho hệ số cả ngày. |
| `UI/BrewPanel.cs`, `UI/HudController.cs`, `UI/FloatLayer.cs` | Huy hiệu combo, viền đồng hồ, thông báo, thẻ hết khung. |
| `Audio/Sfx.cs`, `Audio/AudioHost.cs`, `tools/gen_audio.py` | 5 SFX mới (`combo_up`, `combo_tier`, `combo_break`, `rush_warn`, `rush_end`) và bản nhạc giờ đông; ghi vào `docs/audio_licenses.md`. |
| `Missions/MissionCatalog.cs` | 4 mẫu nhiệm vụ mới. |
| `Core/DebugAutoPilot.cs` | Log `[AutoPilot] COMBO max=… tips=…` và `RUSH n served/arrived`; cờ `-norush` để so sánh A/B. |
| `Tutorial/*` | Lần đầu đạt combo 3 hiện gợi ý một lần. Ngày 1–2 không có giờ đông. |

Save: **không cần đổi**, vì mọi trạng thái chỉ sống trong một ngày. Tiến độ nhiệm vụ đi qua `MissionBook` như các nhiệm vụ hiện có.

### 3.3 Hiệu năng

- Combo và rush đều chạy theo sự kiện, không duyệt mỗi frame.
- Huy hiệu combo dùng lại Text/Image đã có, không tạo UI mới mỗi lần.
- Chữ nổi đi qua pool sẵn có của `FloatLayer`.

## 4. Lộ trình (ngày công)

| Bước | Nội dung | Công |
|---|---|---|
| 1 | `ComboRules` + `ComboTracker` + test, nối vào `BrewStation` và `Customer` | 1 |
| 2 | UI combo (huy hiệu, chữ nổi, tổng kết) + SFX + haptics | 1,5 |
| 3 | `RushSchedule` + sinh `ArrivalDensity` + test bảo toàn tổng khách | 1 |
| 4 | Báo trước / trong / sau giờ đông (HUD, nhạc, thẻ) + `RushTracker` + thưởng | 1,5 |
| 5 | Throttle trong khung giờ đông, sự kiện "Giờ cao điểm" | 0,5 |
| 6 | Nhiệm vụ, gợi ý hướng dẫn, log bot | 0,5 |
| 7 | Bot 100 ngày (có và không có `-norush`), chỉnh hằng số, ghi kết quả vào economy §25 | 1 |
| 8 | Playtest 3–5 người, 20 phút: có nhận ra giờ đông không, combo có "đã" không, có thấy bị ép không | 0,5 |
| | **Tổng** | **~7,5 ngày công** (khoảng 3–4 tuần ở nhịp 2 ngày/tuần) |

Bước 1–2 có thể phát hành riêng để thử sớm, vì combo không phụ thuộc giờ đông.

## 5. Tiêu chí chấp nhận

- **Kinh tế (bot 100 ngày)**: ngày lên Mặt bằng và Nhà hàng lệch ≤ 2 ngày so với §23.7; tip combo ≤ 5% doanh thu; `lostNoSeat` cấp 1 tăng ≤ 2 khách/ngày; tổng khách/ngày lệch ≤ 2%.
- **Test**: toàn bộ EditMode xanh; có test bảo toàn tích phân mật độ khách.
- **Playtest**:
  - ≥ 4/5 người nói được "giờ đông là lúc nào" sau 2 ngày game.
  - ≥ 3/5 người tự tìm cách giữ combo.
  - Không ai phàn nàn bị phạt khi đứt combo.
- Không có exception trong log bot, và qua được kiểm tra pha Chuẩn bị.

## 6. Rủi ro

- **Combo biến game thành game phản xạ, mất chất "cozy"**: đứt combo chỉ làm mất thưởng thêm, không phạt; không có đồng hồ đếm lùi; màu và âm thanh khi đứt phải nhẹ.
- **Người chơi chậm thấy mình kém**: bậc thấp nhất chỉ cần 3 ly 3 sao; chữ nổi luôn tích cực.
- **Giờ đông quá ngắn (~20 s)**: giữ kế hoạch nới khung ở mục 2.1, quyết định sau playtest.
- **Throttle và đỉnh khách đụng nhau**: có cờ `-norush` cho bot để so sánh A/B.
- **Cấp 3 chủ quán ít tự pha nên combo ít ý nghĩa**: chấp nhận; ở cấp 3, phần thưởng giờ đông theo tỉ lệ phục vụ của cả quán mới là chính.
