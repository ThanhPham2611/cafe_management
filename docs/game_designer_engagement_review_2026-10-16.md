# Đánh giá game designer – 2026-10-16: hệ level uy tín và độ hấp dẫn

> Chỉ đọc, không sửa code. Nguồn: `LevelRules.cs`, `Reputation.cs`, `MissionCatalog.cs`, GDD, economy (§3, §4.2, §7, §19–22), review 10-15, `bot100.log` (đọc ngày 1, 4, 15, 28 và mốc t = 3.916 / 7.852 / 14.588 s). Log là bản TRƯỚC khi bỏ phạt "hết chỗ", nên RP cấp 2 thấp hơn hiện tại. Cuối file có mục "Đo được / Suy luận".

## Tóm tắt
1. Level giải đúng nỗi đau đầu game: bot lên Lv2→Lv6 trong ~4 ngày (~14 phút). Tổng thưởng cả 40 lần lên chỉ ~46.700 Xu (≈ 6–7 ngày lãi cấp 3) nên **không lạm phát**.
2. Chia đều RP trong mỗi hạng sao trong khi RP/ngày gần phẳng (~100–127) làm nhịp chậm dần ~40 lần: 0,6 ngày/level (Lv1–5) → ~3 → ~5 → **22 ngày/level (Lv31–41, ~2 giờ mỗi level, cả dải ~19 giờ)**. Cuối thang là vùng chết, và cấp 4 chưa có để lấp.
3. Thưởng Xu **vô hình từ cấp 2**: Lv20 = 900 Xu ≈ 12% lãi một ngày cấp 3; level ★3 / ★4 (2.220 / 4.020 Xu) kém cả một ngày lãi. Mọi level thưởng cùng một loại (Xu, công thức tuyến tính) nên dễ đoán, ít "đã".
4. Thuật ngữ đang bị gánh quá tải: "cấp" = "level" trong tiếng Việt; ★ vừa là uy tín vừa là chất lượng ly; "Lv" vừa là uy tín vừa là bậc nhân viên.
5. Điểm dễ bỏ chơi nhất không nằm ở đầu game mà ở **bức tường tiền cấp 2 sau ★3 (ngày ~30–56)** và **giai đoạn sau cấp 3**, nơi chỉ còn "chờ đủ Xu".
6. Làm trước: đường cong level lồi + thưởng theo hạng sao (số ở mục D), thẻ "lên level" chạm-để-nhận, mở khóa đồ/danh hiệu theo level, mục tiêu nhiều ngày lấp bức tường tiền.

---

## A. Đánh giá hệ level (commit e292e9c)

### A1. Tần suất lên level
| Dải level | RP/level | RP/ngày (nguồn) | Ngày/level | Phút thực/level (3,5–5 phút/ngày) | Nhận xét |
|---|---|---|---|---|---|
| Lv1→6 (5) | 60 | ~100 (log: 425 RP sau 3 ngày + 20 RP rương) | 0,6 | ~2 | Tốt: ~1 level mỗi ngày |
| Lv6→16 (10) | 270 | 96 (ngày 4–15), 72 (15–28), +30 (ngày 28) | 2,8–9 | 10–35 | Tạm ổn trên giấy; log cũ chậm vì phạt hết chỗ |
| Lv16→31 (15) | 600 | +127 (bot C, cấp 3, §21) | 4,7 | ~24 (người thật ≥ 35 nếu bằng 65% bot) | Mép trên của chấp nhận được |
| Lv31→41 (10) | 2.800 | +127 | 22 | ~110 | Vùng chết; ★5 mất ~233 ngày ≈ 19 giờ |

Kết luận: đầu game rất tốt, giữa game chấp nhận được, **cuối thang hỏng**. Lỗi gốc là mọi hạng sao chia đều RP dù RP/ngày không tăng theo hạng. Dải Lv16–31 trùng đúng "khoảng trống sau cấp 3" (ngày 56–130): mỗi level ~5 ngày mà thưởng gần như không cảm nhận được (A2).

### A2. Mức thưởng
- Xu = 100 + 40·L (x3 ở level mốc sao). Tổng Lv2→16 ≈ 9.060 Xu; →Lv26 ≈ 18.700; cả thang ≈ 46.700 Xu + 750 Hạt. Hạt (50/100/200/400) khớp §4.2, ≈ 2–13 ngày thu Hạt F2P, không đáng ngại.
- Cảm nhận theo giai đoạn: cấp 1 Lv2 +180 Xu = 36% một ghế (có ý nghĩa); cấp 2 Lv10 +500 vs đồ 700–3.000 (yếu); cấp 3 Lv20 +900 vs khu 40.000 và lãi 6–8k/ngày (không thấy). Level ★3 (Lv16) 2.220 Xu = ~0,6 ngày lãi cấp 2, ★4 (Lv31) 4.020 = ~0,5 ngày lãi cấp 3: mốc lớn nhất lại thưởng nhỏ nhất so với lãi.
- Không lạm phát: dư địa còn nhiều. Nâng thưởng Xu ~3,5 lần (mục D) vẫn chỉ ~8% lãi ngày cấp 3 và lệch T3 ≤ 2 ngày.

### A3. Hiệu ứng có đủ "đã" chưa
Hiện có: chữ nổi, pháo giấy, biểu ngữ "SAO N!" (§22). Thiếu, theo mức quan trọng:
1. **Chạm để nhận** (hiện cộng tự động): hành động nhận là phần "đã", và là chỗ gắn tùy chọn "xem quảng cáo nhân đôi" (opt-in, hợp nguyên tắc §0).
2. **Âm thanh** (§22 ghi chưa làm): ting + nhạc ngắn, rẻ nhất mà hiệu quả nhất.
3. **Level ở màn tổng kết ngày** ("Hôm nay +2 level, Lv12→14"): level-up xảy ra lúc tay đang bận pha/dọn nên chữ nổi dễ bị bỏ lỡ; bản thân bot cũng nhận level đầu lúc nhận nhiệm vụ cuối ngày 1 (RP 50,9 → 70,9).
4. **Hàng đợi / gộp**: `Reputation.Add` phát `LevelUp` từng level trong một vòng lặp (chương truyện +100 RP, hoặc +RP lớn). Nếu UI không xếp hàng thì chữ nổi và pháo chồng nhau, và biểu ngữ ★ chồng lên level sau nó. Chưa đọc UI, cần kiểm.
5. **Xem trước level kế**: "Lv13: mở Đèn lồng Hội An" ngay trên thanh HUD.
6. Giới hạn: không rung camera, pháo giấy phải tắt theo "Giảm chuyển động" (phản hồi chóng mặt ở review 10-15).

### A4. Nên thưởng gì ngoài Xu/Hạt (theo ưu tiên)
1. **Đồ trang trí/biển hiệu mở khóa theo level** (đã có nhiều đồ Blender): cửa hàng hiện mờ "Mở ở Lv N". Chỉ khóa đồ thẩm mỹ, không khóa ghế/bàn. Biến level thành mục tiêu nhìn thấy được.
2. **Danh hiệu + biển hiệu quán** đổi theo sao ("Quán mới nổi" → "Quán quen của xóm" → …), mỗi 5 level một danh hiệu. Đây là "sở hữu quán" nhìn thấy trong thế giới.
3. **Phiếu tỉ lệ theo giai đoạn** (vd. "Nâng cấp nhân viên -50% một lần" ở Lv20/25/30): giá trị tự lớn theo cấp, không cần nâng số Xu.
4. **Mở nội dung**: chương khách quen, khách mới, gợi ý công thức ẩn gắn với level cụ thể.
5. **Rương bất ngờ** ngẫu nhiên (miễn phí, không loot box trả tiền) ở 1 trong 4 level để có yếu tố biến thiên.

### A5. Nhầm lẫn sao / level / cấp quán
Thực trạng: ★1–5 (uy tín), Lv1–41 (uy tín), cấp quán 1–4, cộng thêm ★1–3 (chất lượng ly; nhiệm vụ `three_star_5`) và Lv1–5 (bậc nhân viên). Người mới sẽ hỏi "Lv6 có phải là cấp 6 không?", "cần ★2 hay Lv6 để thuê mặt bằng?". Đề xuất:
- **Chỉ gọi cấp quán bằng tên** trong UI: Vỉa hè / Mặt bằng / Nhà hàng / Chuỗi; bỏ chữ "cấp N" cho quán.
- **Ghép sao vào level thành một huy hiệu**: `★★ Lv 12`, một thanh duy nhất; mốc sao là vạch đánh dấu trên thang (Lv6, 16, 31, 41). Chạm HUD mở "Hành trình uy tín" liệt kê mọi level, thưởng, và cái gì được mở.
- **Mỗi từ một nghĩa**: ★ và Lv chỉ dành cho uy tín; chất lượng ly dùng icon khác (hạt cà phê/ly), bậc nhân viên gọi "Tay nghề 1–5" thay vì "Lv".
- **Thông báo cổng nói gộp**: "Cần ★2 (Lv6) + 18.000 Xu để thuê mặt bằng"; thông báo lên level mốc sao ghi luôn "Mở khóa: Thuê mặt bằng".
- Nhãn gợi ý: "Uy tín Lv N" hoặc "Danh tiếng Lv N" (thử 5 người chọn tên dễ hiểu hơn).
- Kiểm tra hiểu: 5 người mới, sau 15 phút hỏi "muốn thuê mặt bằng cần gì?"; đạt khi ≥ 4/5 đúng.

---

## B. Bản đồ nguy cơ nhàm chán
| Giai đoạn (bot) | Lặp lại gì | Mục tiêu kế: rõ / gần? | Nguy cơ và lý do |
|---|---|---|---|
| 30 phút đầu (ngày 1–8) | Phin → đá → chạm khách; ~18–23 khách, 5–7 ghế; ngày 1: 18 khách, lãi 1.200 | Rõ (tutorial, ghế 5, Lv2–6 gần như mỗi ngày); cọc 18.000 còn xa (12–14 ngày) | THẤP–VỪA. Level đầu của bot đến sau khi nhận nhiệm vụ cuối ngày 1 (≥ 3,5 phút); người mới có thể 5–8 phút mới thấy level đầu |
| Ngày 4–10 (phút 14–35) | Như trên, mua ghế (tối đa 12) | ★2 xong ngày 3–4; còn 18.000 Xu (bot đạt ngày 9); Lv7 (570 RP) ~ngày 6 | VỪA. ~5 ngày (~20 phút) chỉ "gom tiền" |
| Cấp 2, ngày 9–29 | Pha công thức, dọn bàn, món ẩn; 40–63 khách; 16–18 ghế; tối đa 2 NV | ★3 (Lv16) và 140.000; level mỗi ~3 ngày rồi chậm (log cũ) | VỪA. Ngày 28: phục vụ 40, mất 38 (33 hết chỗ), lãi 3.644: trần ghế/NV làm tăng trưởng phẳng |
| **Cấp 2 sau ★3, ngày ~30–56** | Y nguyên vòng cũ; thứ duy nhất tiến là Xu: 15.308 (ngày ~30) → 93.359 (ngày ~52) ≈ +3.000/ngày | 140.000 (thực trả 122.000) rõ nhưng xa; level 1 lần/4–7 ngày (log cũ: RP 2.419 → 3.350 ≈ +39/ngày) | **CAO**. ~100–130 phút bot = ~4–7 ngày thực của người chơi 20–30 phút/ngày, đúng cửa sổ D3–D7 mà §14 đã cảnh báo |
| Cấp 3 đầu, ngày ~56–75 | Quản lý 8 NV; đơn chưa nhận 25–36 (review 10-15) | Gần: khu 40.000 ≈ 6 ngày lãi; level ~5 ngày | CAO ở phiên đầu (ngợp), sau đó dịu |
| **Sau cấp 3, ngày 56–130+** (~6 giờ bot, người thật ≥ 9 giờ) | ~85–95 khách/ngày do NV chạy, người chơi ít việc thủ công; lãi 6–8k (tới ~18k khi đủ 3 khu + 17 NV) | Khu 40k/90k/180k, NV 431k, level mỗi ~5 ngày: có nhưng toàn "chờ đủ Xu" | **CAO**. Sau khi mua khu 1–2 và nâng NV, ngày nào cũng giống nhau; thưởng level không cảm nhận được |
| Dài hạn (sau ★4, ngày 130+) | Như trên | Cấp 4 chưa có; Lv32–41 mỗi ~22 ngày | **CAO NHẤT**. Biểu ngữ "SAO 4!" + 200 Hạt rồi hết đích gần |

Thứ tự dễ bỏ chơi (suy luận, chưa có dữ liệu người thật): (1) bức tường tiền cấp 2, (2) phiên đầu cấp 3, (3) ngày ~70–130 cấp 3, (4) sau ★4.

---

## C. Làm game hấp dẫn hơn
**Hiện trạng các vòng.** Ngắn (trong ngày): phục vụ, tip, 3 nhiệm vụ, lên level; thiếu nhịp cao điểm và bất ngờ. Giữa các ngày: tổng kết + pha Chuẩn bị; chưa nói gì về level hay "ngày mai có gì". Vòng giữa (vài ngày–vài tuần): chỉ có level mỗi 3–5 ngày, 3 chương khách quen và tiền: **mỏng nhất**. Dài: Lv/★/cấp quán, cấp 4 chưa làm. Lý do quay lại hôm sau hiện thấy được: nhiệm vụ ngày (reset 04:00) + rương 10 Hạt + khách quen; login 7 ngày, offline, thông báo đẩy không nằm trong phần "đã làm" của tài liệu.

**Khách quen cạn truyện?** Log ngày 25–28: Minh bond 8, Chú Tư bond 10 (tối đa), cả hai "chương đã đọc = 3" (GDD §13 nói MVP 3 chương/người); Bà Sáu bond 1, 0 chương sau 28 ngày. Nếu đúng, câu chuyện hết trước khi lên cấp 3.

| # | Việc | Tác động | Công sức | Rủi ro | Đo bằng |
|---|---|---|---|---|---|
| 1 | **Level-up thành khoảnh khắc**: thẻ chạm-để-nhận, hàng đợi/gộp, âm thanh, xem trước level kế, dòng level ở tổng kết, tắt theo "Giảm chuyển động" | Cao | Nhỏ–vừa | Thấp | Thời gian tới level đầu (≤ 3 phút); % phiên có ≥ 1 level-up (≥ 70%); % chạm nhận (≥ 80%) |
| 2 | **Đường cong level lồi + thưởng theo hạng sao** (số ở mục D) | Cao | Nhỏ | Thấp | Phút giữa hai level-up theo dải (mục tiêu ≤ 4 / 12 / 25 / 40) |
| 3 | **Mở khóa theo level**: ~10 đồ/biển hiệu khóa "Mở ở Lv N" trong cửa hàng; danh hiệu mỗi 5 level | Cao | Vừa | Thấp (chỉ khóa đồ thẩm mỹ) | % người mở cửa hàng xem đồ khóa; % mua trong ≤ 1 ngày sau khi mở (≥ 40%) |
| 4 | **Mục tiêu nhiều ngày lấp bức tường tiền**: thanh "Quỹ mua nhà" 4 chặng 25/50/75/100% có quà nhỏ; 1 "dự án tuần" (vd. bán 60 cà phê muối trong 5 ngày → đồ độc quyền) | Cao | Vừa | Thấp | Số ngày liên tiếp không có thưởng/mục tiêu mới (≤ 2); tỉ lệ rơi D3–D7 ở cấp 2 |
| 5 | **Khách quen**: kiểm kho truyện; thêm chương gắn Lv/cấp (6–8 chương/người theo GDD §7.2); sửa Bà Sáu; 2–3 nhân vật mới cho cấp 3 | Cao | Vừa–lớn (nội dung) | Thấp | Chương đọc/ngày; % người đọc hết; ngày của chương mới cuối cùng |
| 6 | **Biến thể ngày**: 5–6 sự kiện nhỏ (mưa rào, đoàn du khách, khách VIP/đơn đặc biệt, phê bình gia), báo trước ở Chuẩn bị | Vừa–cao | Vừa | Vừa (cân bằng RP, độ sạch) | Phương sai doanh thu giữa các ngày; quay lại sau 1 ngày; % sự kiện có chuẩn bị |
| 7 | **Chủ quán lại có việc ở cấp 3**: đơn VIP/đặc biệt chỉ chủ pha 3★ (+RP, giá x1,5–2), cao điểm có ramp + banner (review 10-15) | Vừa | Vừa–lớn | Vừa | % thời gian thao tác thủ công ở cấp 3; RP/ngày; độ dài phiên |
| 8 | **Quà tri ân cho save cũ**: `levelRewarded = 0` nên người đang kẹt (chính người phản hồi) không nhận gì cho level đã đạt; tặng 1 hộp ≈ 50% thưởng các level đã có + hiệu ứng "Quán bạn đã Lv N" | Vừa | Nhỏ | Thấp | Tỉ lệ người cũ quay lại trong 7 ngày |

**Chỉ số chung cần thêm.** Sự kiện `level_up` (level, ngày game, phút chơi, RP/ngày) cho bot và analytics; "thời gian chết" = số phút liên tiếp không có level-up / mua đồ / mở khóa / chương mới (mục tiêu: P90 ≤ 25 phút trong 3 giờ đầu); D1 / D3 / D7 / D14; rơi theo ngày game (`day_end`, §13). Chạy bot 100 ngày mới (bản hiện tại) và log thêm dòng `LEVEL n ngày d t` cùng RP/ngày theo cấp, vì log cũ không có dòng level.

---

## D. Chỉnh ngay và đo thêm

### D1. Chỉnh ngay trong `LevelRules.cs` (rủi ro thấp, nên làm trước khi phát hành vì chưa ai có save theo level)
1. **Chia lồi trong từng hạng**: thay `th[s] + (th[s+1]-th[s]) * i / n` bằng `th[s] + (int)((th[s+1]-th[s]) * System.Math.Pow(i / (double)n, 1.35))`, làm tròn 5. i = 0 vẫn đúng mốc sao nên `IsStarLevel` không đổi; bước nhỏ nhất ≥ 35 RP, tăng ngặt. Tổng RP mỗi hạng giữ nguyên.
   - Lv2–6: 35, 85, 150, 220, 300
   - Lv7–16: 420, 610, 830, 1.085, 1.360, 1.655, 1.970, 2.300, 2.640, 3.000
   - Lv17–31: 3.230, 3.595, 4.025, 4.510, 5.040, 5.615, 6.215, 6.850, 7.515, 8.205, 8.920, 9.660, 10.420, 11.200, 12.000
   - Hiệu quả (ở 100 / 127 RP/ngày): Lv2 sớm hơn (bot ~130 s thay vì sau ngày 1); Lv7 chỉ 1,2 ngày sau ★2 (lúc đang gom 18.000 Xu); bước ở cấp 3 chạy từ 1,8 ngày đến 6,3 ngày thay vì phẳng 4,7. Tổng thời gian không đổi, nhưng hết cảm giác "level cuối hạng ngang level đầu hạng".
2. **`RewardXu` = (100 + 40·L) × sao của hạng** (Lv2–5 x1, Lv6–15 x2, Lv16–30 x3, Lv31–40 x4), level mốc sao nhân thêm x3. Mốc: Lv6 2.040, Lv16 6.660, Lv31 16.080, Lv41 26.100 Xu (Hạt giữ 50/100/200/400). Thưởng mỗi level ở cấp 3 ≈ 2.300–3.900 (≈ 6–10% lãi ngày). Tổng ≈ 79.000 Xu đến Lv31 (≈ 8% lãi ngày cấp 3 trung bình), ≈ 24.000 đến Lv18 (so với 18.700 đến Lv26 hiện nay: T3 sớm hơn ≤ 2 ngày).
3. **Không đụng**: số level 5/10/15 (hợp nhịp), Hạt, sàn mềm theo level, cơ chế `RewardedLevel`.

### D2. Đo thêm trước khi chỉnh
- **RP/ngày theo cấp sau khi bỏ phạt hết chỗ** (cấp 2 chưa đo lại; log cũ 39–96): chạy bot 100 ngày, log RP/ngày từng cấp và dòng `LEVEL`. Đây là mẫu số của mọi con số ở mục A1.
- **★5 = 40.000**: ở +127 RP/ngày mất ~233 ngày. Hai hướng, quyết sau bot: hạ ★5 xuống ~24.000 và đổi `StepsPerStar` thành `{5, 10, 15, 15}` (46 level, bước 310–1.070 RP, ~2,5–8,5 ngày); hoặc tăng RP/ngày cấp 3 (`rp_per_customer_scale` x1,5 từ cấp 3 → Lv16–31 còn ~3,3 ngày/level, ★4 quanh ngày ~110). Ảnh hưởng đến cầu khách (+9 khách/★) cần bot kiểm.
- **Thưởng nhân sao** (D1.2): bot 60 ngày xem lệch nhịp §21 (kỳ vọng ≤ 1 ngày).
- **Người thật (5 người, 30 phút)**: thời gian tới level đầu; có thấy chữ nổi khi đang bận tay không; hiểu sao / level / cấp quán (A5); chấm "đã" 1–5 cho 3 phương án thưởng (Xu thuần / đồ mở khóa / hộp bất ngờ).
- **Đọc thêm trước khi sửa**: `GameBootstrap.OnLevelUpReward`, UI hiệu ứng, `ShopCatalog`, `RegularCatalog` (truyện có cạn không), `DayAdvisor`, login/offline/push có chưa.

---

## E. Đo được và Suy luận
**Đo được (bot hoặc code, KHÔNG phải người thật):** ngày 1: 18 khách, lãi 1.200, RP 50,9 → 70,9 sau nhiệm vụ; RP 425 đầu ngày 4; RP 1.478 ngày 15; ngày 28: phục vụ 40, mất 38 (33 hết chỗ), lãi 3.644, RP +30,3, tổng RP 2.419; Xu 15.308 (t = 7.852 s) → 93.359 với RP 3.350 (t = 14.588 s); +127 RP/ngày cấp 3 (bot C, §21); "chương đã đọc = 3 / bond 10" ngày 25–28; toàn bộ bảng thưởng và ngưỡng RP tính từ code (`LevelRules`).

**Suy luận (chưa có người thật hoặc chưa đo lại):**
- Mọi "phút thực" quy từ 3,5 phút/ngày (cấp 1–2) và 5 phút/ngày (cấp 3); log cho thấy bot còn tốn thêm ~60–75 s mỗi ngày cho tổng kết + Chuẩn bị. Người thật = 65% bot là giả định cũ.
- RP/ngày ~100 ở cấp 2 sau khi bỏ phạt hết chỗ là ước lượng. "★3 sau ngày ~29" trong đề bài không khớp log cũ (RP 2.419 ở ngày 28, ★3 quanh ngày ~45–50); cần bot mới xác nhận.
- Thứ tự điểm dễ bỏ chơi, "vùng chết" đối với người thật, việc chữ nổi bị bỏ lỡ khi bận tay, truyện khách quen cạn, và nhầm lẫn thuật ngữ đều là suy luận từ thiết kế/log, chưa kiểm bằng người chơi.
- Mức "đã" của hiệu ứng và thưởng, các tỉ lệ 6–10% lãi ngày, số mũ 1,35 là đề xuất thiết kế cần tinh chỉnh bằng bot rồi playtest.
- Chưa đọc: `GameBootstrap.OnLevelUpReward`, UI hiệu ứng, `ShopCatalog`, `RegularCatalog`, `DayAdvisor`.
