# GDD – Quán Cà Phê Việt (tên tạm)

> Tài liệu thiết kế game, bản 0.1. Tài liệu liên quan: [art_bible.md](art_bible.md) · [economy_monetization.md](economy_monetization.md)
> Các mục ghi *(đề xuất)* là ý tưởng thêm của Claude, bạn có thể giữ hoặc bỏ.

## 1. Tổng quan
| | |
|---|---|
| Thể loại | Cozy tycoon / quản lý quán 3D, yếu tố idle ở giai đoạn sau |
| Nền tảng | Mobile: Android trước, iOS sau. **Màn hình ngang** |
| Engine | Unity 6 LTS, URP, C# |
| Người chơi mục tiêu | 16–35 tuổi, thích game nhẹ nhàng, chơi lúc rảnh 3–10 phút |
| Kiếm tiền | F2P: rewarded ads + IAP mỹ phẩm. Không pay-to-win |
| Điểm khác biệt | Văn hóa cà phê Việt (phin, ghế nhựa vỉa hè, gạch bông), hành trình "từ vỉa hè lên chuỗi" |

### Câu chuyện một dòng
*Bạn bắt đầu với một xe cà phê và vài ghế nhựa trên vỉa hè. Từng ly phin, từng vị khách quen sẽ đưa quán nhỏ của bạn thành chuỗi cà phê khắp ba miền.*

### 4 trụ cột thiết kế
1. **Đã tay:** pha chế và dọn dẹp có phản hồi cảm ứng, âm thanh, rung rõ ràng.
2. **Thấy mình lớn lên:** quán thay đổi rõ rệt qua từng cấp, từ vỉa hè lên chuỗi.
3. **Quán của tôi:** tự bày trí, tự sáng tạo công thức riêng.
4. **Ấm áp:** khách quen có chuyện đời, không áp lực, không phạt nặng.

---

## 2. Lộ trình quán (xương sống)

| Cấp | Tên | Điều kiện | Mở khóa |
|---|---|---|---|
| 1 | **Vỉa hè** | Bắt đầu | 3–4 món cơ bản, 4 ghế nhựa, thu ly đơn giản |
| 2 | **Thuê mặt bằng** | Tiền cọc + ★2 | Tiền thuê hằng tuần, **dọn bàn**, **pha chế theo công thức**, build mode, nhân viên đầu tiên |
| 3 | **Mua nhà hàng** | Giá mua + ★3 | Hết tiền thuê, nhiều khu (trong nhà/sân/ban công), xếp ca nhân viên, nâng cấp máy |
| 4 | **Chuỗi cửa hàng** | ★4 + vốn | Chi nhánh tự chạy (idle), quản lý chi nhánh, phong cách theo vùng |

Các con số cụ thể (giá, tiền thuê, thời gian để lên cấp) nằm trong `economy_monetization.md`.

*(đề xuất)* Mỗi lần lên cấp có cảnh "khai trương": dọn đồ, pháo giấy, khách quen tới chúc mừng.

### 2.1 Bổ sung cấp 1 (chốt 2026-10-11 theo ý chủ dự án)
- **Chủ quán là người chơi.** Lần đầu mở game: chọn 1 trong 4 dáng (2 nam, 2 nữ), màu da, màu tóc, màu áo (tạp dề nâu cà phê cố định), **đặt tên chủ quán** (≤ 16 ký tự) và **tên quán** (≤ 20 ký tự, mặc định "Cà Phê Vỉa Hè"). Tên quán hiện trên biển xe đẩy và HUD.
- **Chủ quán làm thật:** chỉ pha được khi đang đứng ở xe đẩy (nếu đang đi thì tự về trước). Pha xong → chạm khách → **chủ quán cầm ly đi bộ mang ra**. Chạm ly để lại → chủ quán đi thu (cầm tối đa 4 ly), về xe đẩy thì ly sạch trở lại kho. Hàng đợi tối đa 3 việc, hiện icon trên đầu.
- **Cửa hàng ngay từ vỉa hè** (nút "Cửa hàng"): ghế nhựa (giá `200 + 75·n`), bàn thấp, dù, **chậu cây** (+5% khách mỗi chậu), **bảng menu đứng** (+12% khách, tối đa 1). Tổng tăng khách tối đa +40%, vẫn bị giới hạn bởi số ghế → mua ghế và mua đồ hút khách bổ trợ nhau. Mua xong kéo thả lên lưới (xanh/đỏ), xoay 90°, xác nhận; đồ đã đặt có thể di chuyển hoặc bán lại 50%. Không di chuyển được ghế đang có khách ngồi.
- Build mode đầy đủ (tường, sàn, phong cách) vẫn mở từ cấp 2.

---

## 3. Vòng lõi

### 3.1 Một ngày trong game (≈ 3–4 phút)
```
[Chuẩn bị] chọn menu hôm nay, nhập nguyên liệu, bày lại quán (từ cấp 2)
   ↓
[Mở cửa] khách đến → tìm chỗ trống & SẠCH → gọi món (bong bóng order trên đầu)
   ↓
[Pha chế] cơ bản: 1–2 chạm | công thức: mini-game nhiều bước
   ↓
[Phục vụ] chạm món → chạm khách (hoặc nhân viên tự mang)
   ↓
[Khách rời] trả tiền + tip, để lại review; bàn chuyển sang BẨN (cấp 2+)
   ↓
[Dọn bàn] chạm bàn bẩn → chủ quán/nhân viên dọn → ly bẩn về bồn rửa
   ↓
[Đóng cửa] Tổng kết: doanh thu, chi phí (nguyên liệu, lương, tiền thuê), ★, review
   ↓
[Buổi tối] nâng cấp, nghiên cứu công thức, mua nội thất → Ngày mới
```

### 3.2 Trạng thái của khách (state machine)
`Đến → Tìm chỗ → (không có chỗ thì Chờ, chờ quá lâu thì Bỏ đi) → Ngồi → Gọi món → Chờ món (thanh kiên nhẫn) → Uống → Trả tiền → Rời đi`

- Thanh kiên nhẫn giảm dần theo thời gian chờ. Chỉ số Thoải mái của ghế làm thanh giảm chậm hơn.
- Bỏ đi vì chờ lâu thì mất danh tiếng nhẹ, nhưng không bị phạt tiền.

### 3.3 Trạng thái của bàn
`Trống-Sạch → Có khách → Bẩn → (đang dọn) → Trống-Sạch`
- Bàn bẩn thì không xếp khách vào.
- Bàn bẩn càng nhiều và để càng lâu thì **chỉ số Sạch sẽ** của quán càng giảm.
- Cấp 1 (vỉa hè): không có trạng thái Bẩn, chỉ có "ly để lại", chạm 1 lần để thu.

---

## 4. Hệ thống pha chế

### 4.1 Món cơ bản (cấp 1)
| Món | Thao tác |
|---|---|
| Cà phê đen đá | Chạm phin → chạm đá |
| Cà phê sữa đá | Chạm phin → chạm sữa đặc → chạm đá |
| Bạc xỉu | Sữa đặc → sữa tươi → phin (ít) → đá |
| Trà đá | Chạm bình trà → đá |

### 4.2 Pha chế theo công thức (từ cấp 2)
Một công thức gồm: **Nguyên liệu** + **Dụng cụ** + **Thứ tự các bước**.

| Nhóm | Danh sách ban đầu |
|---|---|
| Nguyên liệu | Robusta, Arabica, sữa đặc, sữa tươi, đá, đường, muối, trứng, cốt dừa, kem béo, cacao, matcha |
| Dụng cụ | Phin (đơn/đôi), máy đánh trứng, máy xay sinh tố, máy espresso (nâng cấp), bình lắc |
| Thao tác | Giữ (rót: giữ càng lâu càng nhiều), vuốt (thêm/đổ), chạm (thả), xoay vòng (khuấy/đánh) |

**Món ẩn để khám phá** (ví dụ): cà phê muối, cà phê trứng, cà phê cốt dừa, bạc xỉu cacao, cà phê sữa chua...

- **Sổ công thức:** món đã biết hiện đầy đủ các bước. Món ẩn chỉ hiện gợi ý (ví dụ: "Một vị mặn bất ngờ từ Huế...").
- **Thử nghiệm:** ghép tự do; nếu khớp một công thức ẩn thì mở khóa món mới kèm hiệu ứng ăn mừng. Không khớp thì ra "món lỗi" (vẫn bán được với giá thấp, khách chê nhẹ).
- **Chất lượng 1–3★:** tính theo độ chính xác lượng rót và đúng thứ tự. Chất lượng ảnh hưởng giá bán, tip và danh tiếng.
- **Sáng tạo món riêng** *(đề xuất, làm sau)*: người chơi tự đặt tên cho kết hợp của mình và đưa vào menu.

### 4.3 Tự động hóa
Thuê barista thì những món đã đạt 3★ ít nhất 5 lần sẽ được pha tự động (ở chất lượng của barista). Người chơi giữ quyền tự pha các món mới hoặc món đặc biệt.

---

## 5. Dọn dẹp
- Chạm bàn bẩn thì chủ quán đi tới (NavMesh) và lau, mất khoảng 2 giây. Có thể *(đề xuất)* vuốt lau nhanh để giảm thời gian.
- Ly bẩn mang về **bồn rửa**. Bồn đầy thì phải rửa, nếu không sẽ thiếu ly sạch để pha món.
- Đường nâng cấp: tự làm → nhân viên dọn → xe đẩy ly (mang được nhiều ly một lần) → máy rửa chén.
- Sự cố ngẫu nhiên *(đề xuất)*: khách làm đổ ly, mèo hoang nhảy lên bàn, mưa làm ướt bàn ngoài sân.

---

## 6. Bày trí nội thất (từ cấp 2)
- Lưới 0.5m. Kéo thả, xoay 90°, ô xanh/đỏ báo đặt được hay không.
- Ghế tự "dính" vào bàn gần nhất để thành **chỗ ngồi**. Bàn không có ghế thì không phục vụ được.
- Kiểm tra lối đi: cửa → quầy → mọi chỗ ngồi → bồn rửa phải đi tới được (dùng NavMesh path).
- Chỉ số nội thất:
  - **Thoải mái**: kiên nhẫn của khách, khả năng khách gọi thêm món.
  - **Phong cách**: Vintage / Hiện đại / Sân vườn / Đông Dương. Thu hút nhóm khách khác nhau, combo cùng phong cách được bonus.
  - **Sức chứa**: số chỗ ngồi.
- Tường, sàn, đèn, cây cảnh, tranh, quầy và thiết bị.
- Cấp 1: chỉ xếp được ghế nhựa và ô dù trong một khu nhỏ.

---

## 7. Khách hàng

### 7.1 Khách vãng lai
Sinh ngẫu nhiên theo **nhóm khách**: sinh viên, dân văn phòng, du khách, người lớn tuổi, cặp đôi. Mỗi nhóm có món thích, phong cách quán thích, độ kiên nhẫn và mức tip riêng.

### 7.2 Khách quen (móc giữ chân chính)
10–15 nhân vật, mỗi người có thanh thân thiết (0–10) và 6–8 đoạn truyện ngắn (3–6 câu thoại mỗi đoạn).

| Nhân vật (ví dụ) | Món ruột | Mạch truyện |
|---|---|---|
| Minh – sinh viên ôn thi | Bạc xỉu | Từ trượt nguyện vọng 1 tới tốt nghiệp |
| Chú Tư – xe ôm | Cà phê đen đá | Kể chuyện Sài Gòn xưa, con gái đi du học |
| Chị Lan – nhân viên văn phòng | Cà phê muối | Bỏ việc để mở tiệm bánh riêng |
| Bà Sáu – hàng xóm | Trà đá | Người đầu tiên tin vào quán vỉa hè |
| Ken – du khách Nhật | Cà phê trứng | Đi tìm quán mà ông nội từng uống năm xưa |

- Phục vụ đúng món ruột, bằng ly sạch, nhanh thì tăng thân thiết.
- Đạt mốc thân thiết thì mở đoạn truyện tiếp và nhận quà (nội thất độc quyền, công thức bí truyền).
- Một số khách quen chỉ xuất hiện từ một cấp quán nhất định.

---

## 8. Nhân viên (từ cấp 2)
| Vai trò | Việc làm | Chỉ số |
|---|---|---|
| Barista | Pha món đã thành thạo | Tốc độ, chất lượng |
| Phục vụ | Mang món, nhận order | Tốc độ di chuyển |
| Dọn dẹp | Dọn bàn, rửa ly | Tốc độ dọn |
| Thu ngân *(cấp 3)* | Tăng tip, giảm thời gian chờ thanh toán | Duyên dáng |
| Quản lý *(cấp 4)* | Chạy chi nhánh khi bạn không có mặt | Hiệu suất idle |

Nhân viên trả lương theo ngày, nâng cấp được bằng huấn luyện. *(đề xuất)* Mỗi nhân viên có một nét tính cách nhỏ (hay hát, sợ mèo...).

---

## 9. Danh tiếng & review
- ★ tổng của quán (1.0–5.0) là trung bình có trọng số của các review gần nhất.
- Mỗi khách để lại review, với điểm tính theo: chất lượng món, thời gian chờ, độ sạch, độ thoải mái/phong cách.
- Review hiện dạng câu ngắn kiểu Google Maps, ví dụ: *"Bạc xỉu ngon nhưng bàn hơi dơ 😅 – 3★"*. Câu review cho người chơi biết phải cải thiện chỗ nào.

---

## 10. Giữ chân người chơi
| Tầng | Cơ chế |
|---|---|
| Phiên chơi | Combo phục vụ, Perfect, tip, khám phá món, đặt đồ mới |
| Hằng ngày | 3 nhiệm vụ ngày, quà đăng nhập 7 ngày, thu nhập offline (cấp 4 hoặc khi có nhân viên) |
| Dài hạn | Lên cấp quán, truyện khách quen, sổ công thức, bộ sưu tập nội thất, sự kiện mùa |

Thông báo đẩy: tối đa 1–2 lần/ngày, có thể tắt.

---

## 11. FTUE – 5 phút đầu
1. **0:00** Cảnh mở: sáng sớm, xe cà phê, một ghế nhựa. Bà Sáu ngồi xuống.
2. **0:15** Hướng dẫn bằng bàn tay chỉ: chạm phin → chạm đá → chạm bà Sáu. *Món đầu tiên trước 30 giây.*
3. **0:45** Thêm 3 khách, người chơi tự làm, học về thanh kiên nhẫn.
4. **2:00** Mua ghế nhựa thứ 5 (mua nội thất lần đầu).
5. **3:00** Mở món thứ 3 (bạc xỉu).
6. **4:00** Tổng kết ngày 1. Hé lộ mục tiêu: *"Gom đủ tiền cọc để thuê mặt bằng!"* kèm thanh tiến độ.

---

## 12. Điều khiển (màn hình ngang)
| Thao tác | Hành động |
|---|---|
| Chạm | Chọn khách/bàn/món |
| Vuốt 1 ngón | Di chuyển camera |
| Chụm 2 ngón | Zoom |
| Nút xoay | Xoay camera 90° |
| Giữ + kéo (Build Mode) | Di chuyển nội thất; chạm nút ↻ để xoay |

---

## 13. Phạm vi MVP (Vertical Slice)
**Có:** cấp 1 + cấp 2, 8 món (4 cơ bản + 4 công thức, trong đó 2 món ẩn), dọn bàn, build mode khoảng 20 món nội thất, 3 khách quen (mỗi người 3 đoạn truyện), 1 loại nhân viên (dọn dẹp), save cục bộ, tiếng Việt + tiếng Anh.

**Chưa có:** cấp 3–4, ads/IAP, Firebase, sự kiện, iOS.

---

## 14. Câu hỏi còn mở (bạn quyết định)
- [ ] Tên chính thức của game?
- [ ] Bối cảnh khởi đầu ở thành phố nào: Sài Gòn hay Hà Nội?
- [x] Chủ quán có tùy biến nhân vật → **Có** (xem §2.1)
- [ ] Ở vỉa hè có sự kiện "công an phường dẹp vỉa hè" để tạo động lực chuyển lên cấp 2 không? *(đề xuất, mang tính hài hước)*
- [ ] Có chế độ ngày/đêm (quán bán buổi tối, đèn dây) không?
- [ ] F2P hay bán đứt? (Mặc định hiện tại: F2P)
