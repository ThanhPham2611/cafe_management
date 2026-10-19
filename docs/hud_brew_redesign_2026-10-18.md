# Thiết kế lại HUD + thao tác pha – 2026-10-18

> Chỉ là spec, chưa sửa code. Nguồn: ảnh `shots/t2/12_t025.png`, `16_menu_open.png`, `onboarding/04–06`; `HudController.cs`, `BrewPanel.cs`, `HoldButtonBehaviour.cs`, `StepAction.cs`, `BrewStation.cs`, `onboarding_spec.md`.
> **Đơn vị:** mọi số px trong tài liệu tính theo khung **1280x720**. Canvas HUD thật là 1600x900 (`HudBuilder.HudRefHeight = 900`), nên khi code thì **nhân 1,25**.

## 0. Chẩn đoán (đo từ code và ảnh)
- **Bàn pha** khi mở rộng có kích thước 960x372 trên canvas 900, tức **768x298 px = 41% chiều cao, 25% diện tích**. Khi thu gọn vẫn còn một dải 768x102 ghi "Chạm một đơn để pha". Dải này chặn chạm (`Panel(..., true)`) mà không làm được gì. Ở ảnh 04, hàng nút còn bị cắt ở mép dưới màn hình.
- **Tổng HUD che màn hình** (ước lượng từ ảnh 12): khoảng **30% khi rảnh** (1 thẻ đơn) và khoảng **45% khi đang pha**. Thẻ mục tiêu "Mua nhà hàng" (350x125), dòng tiền thuê, tên quán, 2 chip ly/độ sạch, nút "Cửa hàng" 220x75 và câu toast to giữa màn hình chiếm đúng vùng có khách và quầy.
- **Thao tác giữ:** thời gian giữ thật ra đã ngắn (0,4–0,5 s). Cái khó nằm ở chỗ khác:
  1. Vòng tiến độ nằm ngay dưới ngón tay nên không nhìn thấy.
  2. Không có tín hiệu "đủ rồi".
  3. Nhả sớm bị tính **sai bước** (`FailStep → RegisterWrong`), sai 2 lần là hỏng ly.
  
  Vì vậy người chơi giữ lâu "cho chắc", do dự, và trượt mốc 3★ theo `PerfectSeconds`. Thêm nữa, với bước kiểu Chạm thì gợi ý cố ý "không lộ đáp án", nên người chơi phải tự dò nút trên khay.

---

## A. Dọn HUD

### A1. Nguyên tắc
1. **Vùng chơi trung tâm** là hình chữ nhật **x 220–1060, y 76–556** (gốc toạ độ ở góc trên trái, 840x480). Trong lúc chơi, vùng này **không có phần tử UI nào nhận chạm**. Chữ nổi, toast và bong bóng được phép hiện ở đây nhưng luôn đặt `raycastTarget = false`.
2. **Chỉ hiện thứ cần ngay lúc đó.** Thứ để xem thì thu nhỏ thành viên thuốc. Thứ để làm thì chỉ hiện khi có việc.
3. Mọi phần tử nằm trong `SafeArea` (đã có), cách mép **16 px**. Nút chạm được phải **≥ 64x64 px** (khoảng 9 mm trên máy 6").

### A2. Bố cục khi đang mở cửa (1280x720)
| Phần tử | Trạng thái | Anchor / vị trí | Kích thước | Ghi chú |
|---|---|---|---|---|
| Chip Xu | Luôn hiện | trên-trái (16, 8) | 200x56 | icon 56, chữ 30 |
| Huy hiệu "★2 Lv6" + thanh RP mảnh | Luôn hiện | trên-trái, x = 228 | 230x56 | bỏ dòng "400/420", chạm vào thì hiện số |
| Chip ngày/giờ | Luôn hiện | trên-phải (-16, 8) | 180x56 | "N1 · 07:29" trên một dòng |
| MENU | Luôn hiện | trên-phải, bên trái chip giờ | **64x56 nút icon ☰** + chấm đỏ | popup giữ lưới 2 cột như hiện nay; thêm "Sổ công thức" và "Cửa hàng" vào đây |
| Mục tiêu "Mua nhà hàng" / "Thuê mặt bằng" | **Thu gọn** | trên-phải, dưới chip giờ (-16, 70) | viên thuốc 220x36: "Nhà hàng 11% ▸" + thanh mảnh | chạm thì mở thẻ đầy đủ 300x140, tự đóng sau 4 s. Tự mở 3 s khi qua mốc 25/50/75% |
| Dòng "Thuê nhà: còn N ngày" | Gộp vào viên thuốc | — | — | chỉ đổi đỏ và hiện chữ khi còn ≤ 2 ngày |
| Tên quán | **Ẩn khi mở cửa** | — | — | chỉ hiện ở pha Chuẩn bị và ở bảng tổng kết |
| Cột "Đơn chờ" | Luôn hiện (khi có đơn) | trên-trái (16, 76) | thẻ **200x64** (icon 44, tên 24, giá 18, vòng kiên nhẫn 44), cách nhau 8 | tối đa **3 thẻ** + dòng "+N"; bỏ tiêu đề "Đơn chờ" khi đã qua hướng dẫn. Container không chặn chạm, chỉ thẻ nhận chạm |
| Cửa hàng | Thu nhỏ | dưới-trái (16, -16) | **nút tròn 72x72** (icon xe đẩy) | ở pha Chuẩn bị vẫn là nút chữ to như cũ |
| Chip ly sạch | Luôn hiện | dưới-phải (-16, -16) | 120x52 | |
| Chip độ sạch | **Ẩn khi ≥ 90%** | phía trên chip ly | 120x52 | |
| "Thu hết ly" | Chỉ khi có ly chờ thu | phía trên chip ly | 200x56 | |
| Toast "Xong rồi! Chạm khách..." | Chữ 26, không chặn chạm | **ngay trên mép bàn pha** (hoặc trên dải "đang cầm ly") | ≤ 600x40 | thêm mũi tên nhảy nhẹ trên đầu khách cần phục vụ, thay cho câu chữ to giữa màn hình |
| Thẻ thưởng level | Chạm để nhận | dưới cột đơn, bên trái (16, ≥ 300) | 200x56 | đưa ra khỏi giữa-trên màn hình |
| Bàn pha | **Chỉ hiện khi có đơn được chọn / đang pha** | xem A3 | — | — |

**Ước tính diện tích che:** khi rảnh khoảng **11%** (hiện nay khoảng 30%), khi đang pha khoảng **25%** (hiện nay khoảng 45%).

### A3. Bàn pha mới, dạng dải dưới
- **Idle, chưa chọn đơn:** `root.SetActive(false)`, bỏ hẳn dải "Chạm một đơn để pha". Thay vào đó, thẻ đơn cũ nhất nhấp nháy viền vàng 1 lần mỗi 3 s.
- **Đã chọn đơn / đang pha:** dải đáy-giữa, anchor (0.5, 0), cách đáy 8, kích thước **840x156 (21,7% chiều cao)**. Nằm giữa nút Cửa hàng (bên trái) và chip ly (bên phải), không đè lên hai nút này.
  - **Hàng trên, 36 px:** icon món 32, tên + giá (chữ 24), 2–6 chấm bước (28 px), thanh "pha nhanh" mảnh 160x10, nút **✕ Hủy 56x56** ở góc phải.
  - **Hàng dưới, 112 px:** cột **ly-thước** 72x104 bên trái (xem B2), sau đó là các nút nguyên liệu **112x104**, cách nhau 12. Năm nút Tier 1 chiếm 608 px, vừa khít. Từ Tier 2 thì cuộn ngang như hiện nay, vẫn tự cuộn tới bước kế.
  - Nút "Sổ công thức" nằm trong MENU khi đang mở cửa. Chỉ hiện dạng icon 48 trong hàng trên khi đang thử pha tự do.
- **Ready (đang cầm ly):** thu thành viên thuốc **380x60** đáy-giữa: "☕ Cà phê đen ★★★ · chạm khách" + nút "Đổ ly" 96x48. Chỉ nút Đổ ly nhận chạm.
- **Chặn chạm:** chỉ hình chữ nhật của bàn pha nhận chạm; bóng đổ và hiệu ứng thì `raycastTarget=false`. Gọi `CameraRig.SetBottomInset` theo chiều cao mới: 0 khi ẩn, 0,22 khi mở.
- Hiệu ứng trượt 220 ms vẫn giữ, nhưng lấy vị trí ẩn là ngoài màn hình rồi `SetActive(false)` khi trượt xong.

### A4. Hướng dẫn (TutorialOverlay)
- **Giữ nguyên tên GameObject** (`CoinChip`, `Card0`, `BrewPanel/Step_Phin`, `MenuButton`, `MissionButton`…) để các mục tiêu đang chỉ vẫn dùng được.
- Thêm `HudController.RevealForTutorial(string targetName)`: tự mở MENU / bung viên thuốc mục tiêu / hiện bàn pha trước khi đặt "lỗ" lên mục tiêu, và đóng lại khi xong bước.
- Bước "chạm khách" và "thu ly" đều nằm trong vùng chơi trung tâm, giờ không còn bị HUD đè.

---

## B. Thao tác pha mới

### B1. Phương án chọn: **"Chạm để rót – tự đầy – chạm ở vạch vàng"** (thay cho Giữ)
- **Chạm (ngay lúc nhấn xuống)** vào nút rót (Phin, Sữa đặc, Sữa tươi, Trà, Cốt dừa): ly bắt đầu **tự rót**, không cần giữ tay.
- **Nếu không chạm thêm**, ly tự dừng khi đầy = **"Đủ"**, đạt bình thường. Người chơi đang quen giữ nút cũng tự ra kết quả "Đủ", vì việc nhả tay không còn tác dụng gì.
- **Chạm lại** lên nút đang rót, lên ly-thước, hoặc **lên nút nguyên liệu kế** để dừng rót. Kết quả tính theo thời điểm chạm. Nếu chạm vào nút kế và nút đó đúng bước thì làm luôn bước đó, nghĩa là **một lần chạm vừa dừng rót vừa thêm nguyên liệu**. Ví dụ: Phin đang rót, tới vạch vàng thì chạm Đá.

| Bước | Thời gian rót T | Vùng "Chuẩn!" (vạch vàng) |
|---|---|---|
| Phin | 1,0 s | 60–85% T = 0,60–0,85 s (cửa sổ 250 ms) |
| Cốt dừa | 0,9 s | 0,54–0,77 s |
| Sữa đặc / Sữa tươi / Trà | 0,8 s | 0,48–0,68 s (200 ms) |

Cộng thêm `PerfectGraceSeconds = 0.05` sau mép phải để bù độ trễ cảm ứng.

| Chạm dừng lúc | Kết quả | Hệ quả |
|---|---|---|
| < 15% T | bỏ qua, coi là chạm nhầm, vẫn rót tiếp | không phạt; nếu chạm vào nút khác thì không thêm bước đó |
| 15–60% T | **"Hơi ít"** | bước vẫn tính xong, **không tính sai**, ly tối đa 2★ ("hơi nhạt") |
| Vùng vàng | **"Chuẩn!"** | ting + chữ nổi nhỏ; tính vào thưởng "Chuẩn vị" |
| 85–100% T, hoặc không chạm | **"Đủ"** | bình thường, vẫn được 3★ nếu nhanh và không sai |

- **Không bao giờ hỏng ly vì rót.** Chỉ chọn sai nguyên liệu mới tính `Wrong` (giữ nguyên luật 1 sai = 1★, 2 sai = hỏng).
- **Thưởng chuẩn vị:** nếu ly có ≥ 1 bước rót, **mọi bước rót đều "Chuẩn!"**, và ly đạt 3★, thì được tip thêm **+10% giá món** (ít nhất 2 Xu), kèm chữ nổi "Chuẩn vị!". Ở tỉ lệ chuẩn 50% thì doanh thu tăng khoảng +3–5%, cần bot kiểm lại.
- **Vì sao chọn cách này:** người chơi không cần đoán "giữ bao lâu", vì không chạm vẫn đúng. Kỹ năng là tuỳ chọn, chỉ cộng thêm thưởng. Hình phạt nhẹ và không bao giờ phá ly. Số lần chạm cũng giảm nhờ "dừng + bước kế" gộp làm một.
- **Vuốt (Kem muối, Cacao) và Xoay (Lắc, Đánh) giữ nguyên**, vì tự thân đã rõ khi nào xong. Chỉ đo lại Xoay ở mục C "để sau".

### B2. Phản hồi
- **Ly-thước** 72x104 ở trái khay: ly thuỷ tinh đầy dần theo màu nguyên liệu, **vạch vàng** vẽ cố định trên thành ly. Ly-thước nằm ngoài chỗ ngón tay, nên luôn nhìn thấy được.
- Trên nút đang rót có viền vòng chạy, thêm một cung vàng đánh dấu vùng chuẩn.
- **Âm thanh:** tiếng róc rách lặp khi rót, "tíc" nhẹ khi vào vùng vàng, "ting" khi đạt "Chuẩn!".
- **Rung:** rung nhẹ khi "Chuẩn!" (để sau, cần plugin; không dùng `Handheld.Vibrate` vì quá mạnh).
- **Tô sáng bước kế: có.** Nút đúng bước kế có viền vàng 6 px và nhịp phóng 1,00–1,06 với tần số 1,2 Hz. Không làm mờ các nút khác. Hàng chấm bước vốn đã lộ thứ tự, nên tô sáng không làm mất gì của lối chơi; kỹ năng bây giờ là nhanh và đúng nhịp, không phải nhớ.
  - Tắt tô sáng khi đang thử pha tự do.
  - Có tuỳ chọn Cài đặt "Gợi ý nguyên liệu kế" (mặc định BẬT).
- **Đặt trước một bước:** trong lúc đang rót, chạm nút kế (≥ 15% T) thì dừng rót và làm bước đó, như đã nêu ở B1. Ngoài lúc rót không xếp hàng.

### B3. Code (gợi ý)
- `StepActions`: thêm `PourSeconds(step)`, `PerfectBandStart = 0.60f`, `PerfectBandEnd = 0.85f`, `PerfectGraceSeconds = 0.05f`, `UnderIgnoreFraction = 0.15f`. `HoldSeconds` giữ lại cho chế độ cũ (cờ `UseLegacyHold`).
- Tạo `PourButtonBehaviour` (thay `HoldButtonBehaviour`): `OnPointerDown` thì bắt đầu rót; `Update` chạy fill; dừng qua `StopPour()`, được gọi từ chính nút, từ ly-thước, hoặc từ `BrewPanel.OnStep` của nút khác.
- `enum PourGrade { Under, Good, Perfect }`. Thêm `BrewStation.TapStep(BrewStep step, PourGrade grade = PourGrade.Good)`, đếm `UnderCount` và `PerfectPours / PourSteps`.
  - `Finish()`: ly 3★ cần `Wrong == 0 && UnderCount == 0 && nhanh`.
  - Lưu cờ `HeldPerfect` để `Customer.Serve` cộng tip.
- `PerfectSeconds` phải ≥ **1,3 × Σ ngân sách bước**, với ngân sách: Chạm 0,6 s, Rót T, Vuốt 0,8 s, Xoay 1,2 s, cộng 1,0 s phản xạ. Món nào đang dưới mức này thì nâng `brewSeconds`.
- **DebugAutoPilot:** vẫn gọi thẳng `TapStep(step)` với grade mặc định Good, nên số đo kinh tế không đổi. Thêm cờ `-perfectRate 0.5` để mô phỏng thưởng chuẩn vị.
- **Nhân viên Pha chế (Brewer):** không đi qua khay nên không bị ảnh hưởng. Để sau: cho tay nghề cao có tỉ lệ "Chuẩn vị".
- **Hướng dẫn:**
  - Đổi câu bước 3 thành "Chạm Phin để rót." Ly tự đầy, người mới không thể làm sai.
  - Ly thứ 2 (Cà phê sữa) dạy: "Chạm khi cà phê tới vạch vàng!"
  - Khi `TutorialGate.NoPenalty` thì "Hơi ít" không giới hạn 2★.

---

## C. Ưu tiên và tiêu chí nghiệm thu

**Làm ngay (nhỏ, an toàn, tác động lớn)**
1. Ẩn bàn pha khi chưa chọn đơn; dạng viên thuốc khi đang cầm ly (A3).
2. Bàn pha thành dải 840x156; nút 112x104; chỉ rect của bàn pha chặn chạm; `SetBottomInset` theo chiều cao mới.
3. Thu gọn hàng trên: MENU thành icon, mục tiêu thành viên thuốc, ẩn tên quán, gộp dòng tiền thuê, chip độ sạch ẩn khi ≥ 90%.
4. Thẻ đơn 200x64 (tối đa 3); Cửa hàng thành nút tròn khi mở cửa; toast dời xuống trên khay và không chặn chạm.
5. `PourButtonBehaviour` với các mức Hơi ít / Đủ / Chuẩn; bỏ luật "nhả sớm = sai"; thêm ly-thước.
6. Tô sáng nguyên liệu kế; chạm nút kế để dừng rót.
7. `RevealForTutorial` + đổi câu hướng dẫn.

**Để sau**
- Tip "Chuẩn vị" +10% (cần bot đo kinh tế trước khi bật) và nhiệm vụ "Pha 5 ly chuẩn vị".
- Âm thanh rót/ting, rung nhẹ.
- Tự chọn đơn kế khi quầy rảnh (bớt 1 lần chạm mỗi ly; có cờ tắt/bật).
- Tuỳ chọn "Vạch vàng rộng" (+50%) và "Gợi ý nguyên liệu kế".
- Đo lại Xoay 330° (cân nhắc 270°).
- Analytics `brew_step` (grade, ms), `brew_done` (giây, sao).

**Tiêu chí nghiệm thu** (kiểm ở 1280x720, 2400x1080 và 2048x1536)
- HUD che **≤ 12%** diện tích khi rảnh và **≤ 27%** khi đang pha. Tính bằng tổng rect các Graphic đang hiện, trừ chữ nổi.
- Bàn pha cao **≤ 22%** màn hình. Không còn dải bàn pha nào khi chưa chọn đơn.
- Lưới 21x13 điểm trong vùng trung tâm (A1): `EventSystem.RaycastAll` trả về **0 kết quả UI** khi không mở bảng nào (thêm vào autoplay).
- Mọi nút chạm được ≥ 64x64 px; ở cả 3 tỉ lệ màn hình không có phần tử nào đè nhau hoặc bị cắt.
- Thời gian từ bước đầu tới lúc ly sẵn sàng, tính trung vị (5 người mới, sau ly thứ 3):
  - Cà phê đen ≤ **2,0 s**
  - Cà phê sữa ≤ **3,0 s**
  - Món 4 bước ≤ **4,5 s**
- Tỉ lệ 3★ ngày 1–3 ≥ **70%**. Số bước sai trên mỗi ly ≤ 0,1. **0 ly hỏng do rót.**
- Tỉ lệ "Chuẩn!" ở phiên đầu nằm trong khoảng **30–60%**. Trên 85% thì thu hẹp vùng vàng, dưới 20% thì nới rộng.
- Bot 100 ngày (grade Good) lệch Xu/RP ≤ ±3% so với lần đo bot 100 ngày gần nhất (economy §23.7). Bot `-tutorial` qua hết hướng dẫn, không kẹt.
