# HUD – nâng cấp hình ảnh "Clay Café v2" (2026-10-18)

Tiếp theo `hud_brew_redesign_2026-10-18.md` (bố cục gọn + chạm để rót đã xong). Mục tiêu đợt này: **trông xịn, đồng bộ, có "nhựa sống"** mà KHÔNG làm HUD to lại (giữ giới hạn ≤12% màn hình lúc rảnh, bàn pha ≤22% chiều cao, vùng giữa không chặn chạm).
Giữ phong cách Claymorphism trong `ui_style.md` (màu, font, bo góc, mép dưới dày). Số đo theo canvas thật 1600×900.

Vấn đề thấy trên ảnh chụp build (shots_hud 01–15):
1. Nút tròn "Cửa hàng" chữ bị xuống 2 dòng, trông như placeholder; nút ☰ là 3 vạch vẽ tay.
2. Chữ nổi trên cảnh ("Đơn chờ", "Chờ khách ghé quán…", "Thu ly!", "Chủ quán ơi, order nè!") là chữ trơn có viền mỏng, độ tương phản thấp (xám/mint trên sàn be) → rẻ tiền và khó đọc.
3. Pill mục tiêu "Mặt bằng 3%" chữ quá nhỏ, thanh tiến độ gần như không thấy, không có icon.
4. Huy hiệu sao: "Lv 1" chữ tí hon đè lên thanh mảnh.
5. Bàn pha: cái "cốc đong" bên trái chỉ là ô trống + 2 vạch, không ai hiểu; nhãn "Rót" dính mép trên nút; hàng nút sát mép dưới màn hình; dòng gợi ý "Chạm Phin để rót" xám nhạt lơ lửng.
6. Bảng tổng kết: hộp thoại bà Sáu đè lên các con số; "Dự án tuần16/110 khách" dính chữ.
7. Không có phản hồi khi tiền vào (số Xu chỉ đổi, không bay/nảy).

## 1. Bộ icon mới (tools/gen_ui_sprites.py, cùng kiểu khối mập, viền coffee, highlight trắng)
`icon_menu` (3 thanh bo tròn kem trên nền nút coffee — chỉ icon, nền là nút), `icon_shop` (mái hiên sọc đỏ-kem + quầy), `icon_book` (sổ công thức bìa đỏ), `icon_mission` (bảng kẹp giấy có dấu tích), `icon_staff` (tạp dề), `icon_home` (ngôi nhà nhỏ – cho pill mục tiêu), `icon_broom` (chổi – dọn dẹp), `icon_close` (X kem dày cho nút coffee).
Cốc đong: `ui_gauge_glass` (ly thủy tinh trong, viền coffee, highlight dọc) + `ui_gauge_mask` (lòng ly, để Image Filled Vertical đổ chất lỏng). Thêm `ui_badge` (viên tròn đỏ viền kem cho số đếm) và `ui_tag` (nhãn nhỏ bo tròn cho "Rót"/"Chạm").
Xuất 128×128 (icon), ly 96×160. Cập nhật contact sheet.

## 2. Thanh trên
- Mọi chip cùng một họ: `ui_chip` cao 64, **đĩa icon tròn 76 lồi ra trái** (đã có cho Xu – áp cho Sao, Đồng hồ, Mục tiêu).
- Sao: số sao **in trong ngôi sao** (Baloo 30, coffee), bên phải: dòng "Lv 2" Baloo 24 + thanh RP cao 14 (track + fill vàng có highlight), không đè nhau.
- Xu: khi tiền vào → số đếm lên trong 0,4 s, chip nảy scale 1,08 rồi về, đĩa coin xoay nhẹ. Xu bay: 3–5 đồng xu sprite bay từ vị trí khách theo cung tới đĩa coin (0,5 s, ease-in), đồng cuối chạm mới cộng số. Tắt khi bật "Giảm chuyển động".
- ☰: nút coffee 72×64 + `icon_menu`; chấm đỏ thông báo dùng `ui_badge` có số khi >1.
- Pill mục tiêu: cao 44, đĩa `icon_home` 52 bên trái, chữ "Mặt bằng" Nunito 22 + "3%" Baloo 24 bên phải, thanh 10px chạy dọc đáy pill. Khi mở rộng giữ như hiện tại.

## 3. Cột trái – đơn chờ
- Bỏ chữ nổi "Đơn chờ". Thay bằng **tab nhỏ** 120×36 (nền coffee, chữ kem "ĐƠN" + `ui_badge` số đơn) dính đầu cột. Tab chỉ hiện khi có ≥1 đơn hoặc đang hướng dẫn.
- Bỏ dòng "Chờ khách ghé quán…" (để trống là đủ; trong hướng dẫn thì bà Sáu đã nói).
- Thẻ đơn: ô icon món nền kem đậm 60×60 bo 16 bên trái, tên món Baloo 24, giá có icon coin 22 + số; vòng kiên nhẫn đổi màu **xanh → vàng (<50%) → đỏ nhấp nháy (<20%)**. Thẻ mới vào: trượt từ trái + nảy (0,25 s). Thẻ đang pha: viền vàng 4px + nghiêng nhẹ biểu tượng đang lắc.

## 4. Bàn pha (giữ kích thước khay hiện tại)
- Khay nhích lên **16px khỏi mép dưới safe area** (không chạm mép). Đầu khay: tab tên món bên trái (icon + "Cà phê đen" + giá), giữa là **chuỗi bước**: mỗi bước là đĩa tròn 40 (icon nguyên liệu), bước xong có dấu tích xanh, bước kế viền vàng nhấp nháy; dòng gợi ý chuyển thành chữ coffee_md đậm ngay dưới chuỗi bước (không xám nhạt).
- Nút nguyên liệu: mặt nút màu theo nguyên liệu (giữ), icon to 64 ở giữa-trên, tên dưới Baloo 22; nhãn `ui_tag` "Rót"/"Chạm" nằm **trong** góc trên-trái của mặt nút (không tràn mép). Nút đang là bước kế: viền vàng + nảy nhẹ (đã có) + mũi tên nhỏ ▼ sprite phía trên.
- Cốc đong: `ui_gauge_glass` cao ~150, chất lỏng màu theo nguyên liệu đang rót (phin = nâu cà phê, sữa đặc = kem, sữa tươi = trắng, trà = hổ phách, cốt dừa = trắng ngà), mặt chất lỏng có gợn (offset sóng sin nhẹ), **dải vàng** là dải trong ly có chữ nhỏ "CHUẨN" bên cạnh. Khi không rót: ly trống mờ 50%. Rót chuẩn: ly lóe sáng + 4 tia lấp lánh; "Hơi ít": ly rung nhẹ.
- Kết quả chấm: chữ nổi "Chuẩn!" vàng / "Đủ" xanh / "Hơi ít" cam xuất hiện ngay trên cốc đong, dùng kiểu chữ nổi mới (§5).
- Pill cầm ly: icon ly + tên + sao sprite (không dùng ký tự) + nút "Đổ ly" coffee nhỏ.

## 5. Chữ nổi & lời khách (FloatLayer)
- Một kiểu chung "nhãn kẹo": Baloo, **viền coffee 3px + bóng đổ 0,4 alpha lệch (0,−3)**, tô màu sáng: Xu = vàng `#FFD23F`, tốt = trắng kem, cảnh báo = cam `#FF8A3D`, "Thu ly!" = trắng kem (không dùng mint trên sàn be). Xuất hiện: pop scale 0,6→1,1→1 (0,2 s), bay lên, mờ dần.
- **Lời khách (bark)** ("Chủ quán ơi, order nè!") → hiện trong bong bóng `ui_bubble` + đuôi, chữ coffee Nunito 22, rộng tối đa 360, trên đầu khách, không chặn chạm. Tối đa 2 bong bóng cùng lúc.
- Banner lớn ("LÊN LEVEL 2! +180 Xu"): dải ruy-băng `ui_card` vàng có viền, chữ coffee, trượt xuống từ dưới thanh trên rồi tự ẩn sau 2 s, không đặt giữa màn hình.

## 6. Bảng tổng kết + hộp thoại hướng dẫn
- Khi bảng tổng kết/ panel modal đang mở, hộp thoại bà Sáu đặt **dưới cùng màn hình** (trên nút, không che số liệu) hoặc bên trái panel nếu không đủ chỗ; vùng highlight vẫn đúng.
- Sửa dòng "Dự án tuần" (nhãn và giá trị chia 2 cột có khoảng cách tối thiểu 16px, giá trị co chữ nếu dài).
- Số tiền đếm lên khi bảng mở (0,6 s), dòng "Lãi ròng" nảy khi đếm xong.

## 7. Nút tròn bên dưới
- "Cửa hàng" lúc mở quán: nút vàng tròn 96 + `icon_shop` 56 + nhãn nhỏ "Cửa hàng" Baloo 18 **một dòng** bên dưới icon (trong nút) — hoặc chỉ icon nếu không vừa. "Thu hết (N)": icon ly + `ui_badge` số thay cho chữ dài.
- Chip ly bên phải: số ly trống / tổng (vd "3/6"), đổi đỏ khi còn 0.

## 8. Tiêu chí nghiệm thu
- Không còn chữ nào xuống dòng ngoài ý muốn, không có chữ dính nhau (kiểm ảnh 1280×720 và 2400×1080).
- Mọi icon là sprite (không ký tự ☰★▸ làm icon).
- Chữ nổi đọc được trên cả sàn sáng (vỉa hè trưa) và tối (tối 20h): viền + bóng.
- Diện tích HUD không tăng so với đợt trước; vùng giữa vẫn 0 điểm chặn chạm.
- Hiệu năng: không thêm Canvas rebuild mỗi khung khi đứng yên (animation chỉ chạy khi có sự kiện); đồng xu bay dùng pool.
- "Giảm chuyển động" tắt: xu bay, gợn sóng, nảy chip.
