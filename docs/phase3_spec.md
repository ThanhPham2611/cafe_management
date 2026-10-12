# Giai đoạn 3 – Đặc tả "Cấp 3: Mua nhà hàng" (bản nháp 1, 2026-10-11)

> Nguồn: `GDD.md` §2, §8; `economy_monetization.md` §3.1, §3.5, §5–6, §11; `art_bible.md`. Bản này chỉ gom và sắp thứ tự, các con số lấy nguyên từ tài liệu kinh tế.
> Trạng thái: **Giai đoạn 2 đã đủ tính năng** (cấp 2, dọn bàn, công thức, nhân viên dọn / phục vụ / **pha chế**, khách quen, **nhiệm vụ ngày + rương**). Chưa kiểm chứng trên máy thật.

## 0. Việc đã làm ngay trước giai đoạn này (đóng Giai đoạn 2)

| Việc | Nội dung | Ghi chú |
|---|---|---|
| Nhân viên **Pha chế** | Thuê 2.500 Xu, lương 450/ngày. Tự nhận đơn khách chưa ai làm, pha tại quầy (mất nguyên liệu + 1 ly sạch như chủ quán), mang ra khách, về quầy. Thời gian pha = chuẩn × 1,8 / 1,5 / 1,25 / 1,1 / 1,0 theo cấp 1..5; cấp ≥ 3 pha được 3★ (món ẩn cần dụng cụ ≥ cấp 2) | Mở khóa nút thắt "chủ quán là người pha duy nhất" của cấp 2 |
| Sửa lỗi **Phục vụ** | Nhân viên Phục vụ trước đây không bao giờ giao được ly (đọc `Station.Selected` đã bị xóa khi pha xong). Nay chọn khách theo món đang cầm | |
| Đơn "giữ chỗ" | `Order.claimedBy`: chủ quán không chọn / giao nhầm đơn Pha chế đang làm; thẻ order đổi màu bạc hà | |
| **Nhiệm vụ ngày + rương** | 3 nhiệm vụ / ngày thật (đổi 04:00 giờ VN), thưởng Xu = cơ sở × (1/4/12/30 theo cấp). Nhận đủ 3 → rương 10 Hạt + 20 RP. Tiến độ cộng dồn qua các ngày in-game. Nút "NHIỆM VỤ" + chấm đỏ, dòng "Nhiệm vụ" trong tổng kết ngày | Pool: phục vụ N khách, bán N ly một món, 3★, món ẩn, ngày không khách bỏ về, độ sạch ≥ 80, tip |
| FPS | `Application.targetFrameRate = 60` lúc chạy trên mobile; sửa lò xo `ClayButton` mất ổn định khi < 40 FPS | |

### 0.1 Lỗi tìm ra khi kiểm chứng bằng Unity thật (đã sửa)

Chạy `QuanCaPhe.exe -batchmode -nographics -autoplay ... -days 9` trên bản sao project (xem `docs/` ghi chú cuối file):

| Lỗi | Triệu chứng đo được | Sửa |
|---|---|---|
| Nhân viên Phục vụ không bao giờ giao được ly | `serve=0` mọi ngày | chọn khách theo món đang cầm (`OrderPicker.NextForServer`) |
| Chủ quán tự bỏ ly **đang pha dở** để đi rửa ly | quầy kẹt `Brewing` cả ngày, 0–5 khách/ngày | `OwnerController`: đang pha dở thì ở lại quầy |
| Chủ quán bỏ khách để đi rửa ly dù còn ly sạch | đơn chất đống, chủ quán luôn "đang đi" | pha trước, rửa sau khi còn ly sạch + còn đơn chưa ai nhận |
| Cả quán chỉ có **6 ly** cố định | `cleanCups = 0` trong 43% thời gian; mọi cấu hình kẹt ~27–30 ly/ngày dù thêm ghế | `GameBalance.CupsFor`: cấp 1 ≥ 1 ly/ghế, cấp 2 1,5 ly/ghế; thêm ly khi mua ghế |
| Nhân viên Dọn dẹp không lau sàn (spec yêu cầu) | sàn bẩn kéo chủ quán đi lau liên tục | `StaffWorker`: nhận việc `MopFloor` khi rảnh (ưu tiên thấp nhất) |

### 0.2 Số liệu cân bằng cấp 2 (bot autoplay, 10 ngày, đã gồm các sửa trên)

Bot là người chơi "khá": thêm cờ `-autobuy` (mua bàn khi dư tiền), `-autohidden` (mở 2 món ẩn, tắt món rẻ), `-upgradestaff` (nâng cấp nhân viên), `-nobrewer` (đối chứng). Số liệu từng lượt nằm ở `D:\cafe_store_verify\run*.log` (không đưa vào repo).

- Cấp 2 chỉ 6 ghế + 6 ly (trước khi sửa): ~25–30 khách/ngày, lãi ~1.100–1.200/ngày sau lương; **tiền thuê 7.000/tuần ≈ 1.000/ngày ăn gần hết lãi**.
- Sau khi sửa ly, dọn dẹp, lau sàn và thứ tự ưu tiên của chủ quán, có mở rộng ghế + menu món ẩn + nâng cấp nhân viên (lượt cuối, 10 ngày, có Pha chế): **trung bình ~32 khách/ngày, các ngày tốt 36–40 khách, doanh thu 4.700–5.400/ngày, lãi tuần ≈ +9.200 sau tiền thuê**. So với tài liệu (52–63 khách, lãi 4.200+/ngày) vẫn thấp khoảng một nửa; mua nhà hàng 250.000 Xu sẽ mất ~190 ngày in-game thay vì ~58 → **cần cân bằng thêm** (xem 0.3).
- **Pha chế có ích hơn dự đoán ở lượt cuối**: có Pha chế trung bình ~32 khách/ngày (ngày 2–10) so với ~22 khi không có (lượt đối chứng, nhiều ngày sập còn 3–16 khách); lãi tuần +9.200 so với ~+7.000. Chỉ 1 lượt mỗi phương án và dao động lớn, nên coi là xu hướng chứ chưa phải kết luận chắc.
- Vẫn còn **ngày tụt bất thường** (12–22 khách) khoảng 1 ngày trong 4–5 ngày. Phần lớn do chủ quán vừa pha vừa giao ly nên hay "đang đi" khi có đơn chờ; cần chẩn đoán tiếp trước khi khóa số cân bằng.

### 0.3 Việc nên làm tiếp ngay cho cấp 2 (đề xuất)

1. **Ly pha xong giải phóng quầy ngay khi Phục vụ cầm đi** (hiện quầy giữ trạng thái `Ready` tới khi ly tới tay khách, chủ quán không pha được ly kế).
2. Cho thuê **tối đa 2 nhân viên Dọn dẹp** (hiện 1 người/vai trò), hoặc đẩy tốc độ nâng cấp rẻ hơn ở cấp 1→3.
3. Kiểm chứng lại tiền thuê 7.000/tuần với lãi đo được; cân nhắc 4.500–5.500.
4. Tutorial / gợi ý "mua thêm bàn ghế" khi khách bỏ về vì hết chỗ (mỗi bộ bàn + 2 ghế tự hoàn vốn trong ~1–2 ngày).

## 1. Phạm vi Giai đoạn 3 (Alpha)

| Mã | Việc | Mô tả (số liệu theo economy) | Phụ thuộc |
|---|---|---|---|
| **3.1** | Chuyển cấp 3 | Điều kiện ★3 (4.000 RP) + **250.000 Xu**; cọc 18.000 được hoàn (thực trả 232.000). Hết tiền thuê; nhà thuộc về người chơi. Cảnh chuyển như cấp 2 | TierManager |
| **3.2** | Nhà hàng + 3 khu | Phòng lớn dựng bằng khối palette; chỗ ngồi 30 → 80. Mở khu: sân vườn 40.000 · lầu 90.000 · VIP 180.000. Mỗi khu có độ sạch (CI) và F_ref riêng (150) | RoomLayout / EnvironmentBuilder, CleanlinessModel theo khu |
| **3.3** | Nhân viên cấp 3 | Tối đa nhiều người hơn mỗi vai trò (4–10 tổng), **Thu ngân** (3.000 / 400 ngày), **Trưởng ca** (6.000 / 800 ngày); kỹ năng cấp 5 chỉ mở ở cấp 3 (đã có) | StaffRoster |
| **3.4** | Ca làm | Sáng / chiều / tối, thiếu người trong ca → sức chứa ca đó −30%; lương ca tối ×1,2 | StaffRoster, DayCycle |
| **3.5** | Máy móc | Máy xay 2 họng 25.000, máy cold brew 60.000, máy espresso 150.000: pha nhanh hơn, mở món T3 | RecipeBook (dụng cụ) |
| **3.6** | Món cấp 3 | Cold brew cam sả 200, bạc xỉu kem muối 230, phin Robusta Cầu Đất 300 (đặc sản, khách VIP) | RecipeBook, Tier2RecipeData → thêm Tier3RecipeData |
| **3.7** | Kinh tế cấp 3 | Demand ×2,6, chi phí cố định 700/ngày, lương, thu ngân giảm hàng chờ thanh toán | GameBalance |
| **3.8** | Nội thất cấp 3 | Sofa, ghế bành, đèn, máy lạnh… (3.000–40.000 Xu) cần model Blender mới (`tools/gen_furniture.py`) | Tech Artist |
| **3.9** | Nhiệm vụ mở rộng | Nhiệm vụ tuần (7 nhiệm vụ: 1 món trang trí độc quyền + 40 Hạt), quà đăng nhập 7 ngày, reroll nhiệm vụ | MissionBook |
| **3.10** | Thu nhập offline | Công thức §8 (T2 hệ số 0,25; T3 0,30; cần đủ nhân viên); chống chỉnh giờ | Server time (Firebase) |

## 2. Việc CHƯA làm được nếu thiếu thông tin từ bạn (không đoán)

- **Firebase** (Auth ẩn danh, Cloud Save, Remote Config, Analytics, Crashlytics): cần tài khoản Firebase + `google-services.json` của bạn.
- **Quảng cáo thưởng / IAP**: cần tài khoản AdMob hoặc LevelPlay, Google Play Console (App ID, SKU). Gói `com.unity.purchasing` đã nằm trong manifest nhưng chưa có mã.
- **Model 3D cấp 3**: cần quyết định kiểu (script Blender tự sinh hay asset pack) trước khi làm 3.8.

## 3. Thứ tự đề xuất

1. Kiểm chứng Giai đoạn 2 mới (autoplay 8 ngày cấp 2 gồm ngày trả thuê nhà; chạy thử trên máy thật).
2. 3.1 → 3.2 (khung cấp 3 chơi được) → 3.7 (kinh tế) → chạy autoplay.
3. 3.3 / 3.4 / 3.5 / 3.6.
4. 3.9 / 3.10, rồi mới tới Firebase + quảng cáo + IAP.
5. 3.8 song song khi có quyết định về nguồn model.

## 4. Tiêu chí nghiệm thu Giai đoạn 3

- Bot `-autoplay -cheatxu` lên cấp 3, chơi trọn 3 ngày không lỗi runtime; log `[AutoPilot]` in số việc từng vai trò nhân viên làm, CI từng khu.
- Mở được khu mới; thiếu ca thì sức chứa giảm đúng 30%.
- Mua máy → món cấp 3 xuất hiện trong menu.
- Lưu / đọc đúng khi giữa các ngày, kể cả file cũ.
- Cân bằng: T3 lãi ròng / ngày trong dải 20.000–35.000 Xu (economy §3.1) với người chơi trung bình.
