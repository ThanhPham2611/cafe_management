# Giai đoạn 3 – Đặc tả "Cấp 3: Mua nhà hàng" (bản nháp 2, cập nhật 2026-10-13)

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
- Sau khi sửa ly, dọn dẹp, lau sàn, thứ tự ưu tiên của chủ quán và để Phục vụ cầm ly đi là quầy rảnh ngay (lượt cuối, 10 ngày, bot mua bàn + menu món ẩn + nâng cấp nhân viên): **ổn định ~38 khách/ngày (31–48), doanh thu 4.400–6.900/ngày, lãi tuần ≈ +13.000 đến +17.000 sau tiền thuê**, không còn ngày sập. Mức này gần mức tài liệu suy ra (~14.700/tuần); mua nhà hàng 250.000 Xu mất khoảng 110–130 ngày in-game nếu bot không chi thêm.
- **Nhân viên Pha chế chưa đáng tiền ở cấp 2 (14 ghế)**: có Pha chế ~38 khách/ngày, lãi tuần +13.100; không có Pha chế ~38,6 khách/ngày, lãi tuần +16.900. Chủ quán + Phục vụ đã đủ nhanh, Pha chế chỉ tốn lương 450/ngày. Mỗi phương án 1 lượt nên chỉ là xu hướng. Gợi ý: giữ làm lựa chọn, giảm phí tuyển / lương, hoặc chỉ mở từ cấp 3 khi ghế nhiều hơn.
- Các ngày "sập" trước đó phần lớn do bot (chạm bước pha khi chủ quán tạm rời quầy thì ly kẹt mãi) và do chủ quán bị kéo đi dọn / giao ly. Đã sửa bot (thử lại bước, hủy ly kẹt) và luật ưu tiên của chủ quán; người chơi thật có thể vẫn gặp ly kẹt nếu không chạm lại bước, cần xem lại gợi ý UI.

### 0.3 Việc nên làm tiếp ngay cho cấp 2 (đề xuất)

1. ~~Ly pha xong giải phóng quầy ngay khi Phục vụ cầm đi~~ **Đã làm** (`BrewStation.TakeReadyDrink`): Phục vụ làm 24–34 lượt giao/ngày ở ngày tốt.
2. ~~Cho thuê tối đa 2 nhân viên Dọn dẹp ở cấp 2~~ **Đã đo (2026-10-13), không làm:** người thứ hai không tăng khách (xem §0.6); từ cấp 3 mới cho 3 người.
3. Tiền thuê 7.000/tuần: với lãi đo được (13–17k/tuần) vẫn chịu được; chưa cần giảm, xem lại khi có số liệu người chơi thật.
4. Tutorial / gợi ý "mua thêm bàn ghế" khi khách bỏ về vì hết chỗ (mỗi bộ bàn + 2 ghế tự hoàn vốn trong ~1–2 ngày), và gợi ý nâng cấp nhân viên Dọn dẹp trước các vai trò khác.

### 0.4 Bổ sung giai đoạn 2 (2026-10-12/13)

| Việc | Nội dung |
|---|---|
| Nội thất mới (Blender tự sinh, `tools/gen_furniture.py`) | Cấp 2 "kiểu Việt": ghế mây, bàn tròn gạch bông, cây trầu bà lá xẻ, đèn cây, kệ sách thấp, radio cổ, đèn lồng Hội An (`ASSETS_T2B`, `furniture_tier2b.blend`). Cấp 1: ghế nhựa vàng, bình trà đá to, quạt đứng. Tất cả < 800 tris, qua `blender_validate.py`, có icon cửa hàng và chạy được qua luồng build mode trong game. Cửa hàng cấp 2 giờ ~25 món (MVP yêu cầu ~20) |
| Điểm nội thất F | F tính trên **mọi** món (cả ghế, bàn): F_ref cấp 2 = 60 làm F = 1 ngay từ bố cục mặc định nên đồ trang trí vô nghĩa. Đổi F_ref T2 = 200, T3 = 400 (đo: mặc định ~61 điểm, +ghế/bàn ~116, +bộ trang trí ~160) |
| Gợi ý đầu ngày | `DayAdvisor`: tổng kết ngày hiện **một** gợi ý theo lý do mất khách (hết chỗ → mua bàn ghế; chờ lâu → nhân viên; bẩn → nâng cấp Dọn dẹp; chưa thuê Dọn dẹp ở cấp 2; còn nợ; nhiệm vụ chưa nhận). Không lặp hai ngày liền. 9 test |
| Sửa giao diện | Bảng tổng kết cấp 2 bị nút "Ngày mới" che dòng "Nợ" (thẻ cao 900 → 960; +80 khi có gợi ý) |
| Code | Tách `StaffWorker` thành thân nhân vật + `CleanerBehaviour` / `ServerBehaviour` / `BrewerBehaviour` (lớp cơ sở `StaffBehaviour`); `DebugAutoPilot`, `DebugPerfProbe` chỉ có trong Editor và bản Development; `PerfNote` rỗng ở bản release. Phân tích tĩnh (compiler + IDE0005/51/52/59) sạch: 0 cảnh báo |

### 0.5 Hiệu năng đo được (autoplay, PC, `-perf`)

Công cụ: `QuanCaPhe.exe … -perf` in `[Perf]` mỗi 30 giây (CPU/khung, p95/p99, cấp phát, GC) và thống kê cảnh. Chạy `-nographics` thì thời gian khung ≈ chi phí **script** (không gồm GPU).

| Chỉ số | Giá trị đo (PC) | Nhận xét |
|---|---|---|
| CPU script mỗi khung | TB 0,3–0,6 ms, p99 ~1 ms | Rất nhẹ; điện thoại tầm trung chậm hơn ~5–10× vẫn còn dư xa so với 16,6 ms |
| Cấp phát GC mỗi khung | 12–46 B (~0,4 lần/khung) | Thấp; không có cấp phát rác mỗi khung đáng kể |
| GC gen0 | 2–8 lần/phút, heap mono 10–14 MB | Nhỏ, mỗi lần rất ngắn |
| Đỉnh một khung | 12–25 ms mỗi ~30 giây | Chủ yếu sinh khách (TB 0,8 ms, tối đa 3,5), lưu game (0,6–4 ms, tối đa 8), bake NavMesh (2–9 ms, chỉ khi đặt đồ) |
| Lỗi đã sửa | `ClayButton` mất ổn định dưới 40 FPS (nút phóng to, nháy) | xem commit đầu |

Chưa đo được trên PC: GPU/draw call/nhiệt độ trên điện thoại thật.

### 0.6 Cấp 3 — khung chơi được (2026-10-13)

**Làm gì.** Mua nhà hàng (3.1), sảnh nhà hàng 10 × 12 m (3.2, chưa có 3 khu), nhân viên theo trần vai trò (3.3 một phần), máy móc + 3 món cấp 3 (3.5, 3.6), 11 model nội thất Blender (3.8). Chi tiết trạng thái ở §1.1.

| Việc | Nội dung |
|---|---|
| Chuyển cấp | Cùng luồng với cấp 2 (tối dần → dọn khách/đơn/ly → cất đồ cũ vào kho → dựng phòng → bake NavMesh → "Khai trương!"). Mua nhà = 250.000 − cọc 18.000 hoàn = **232.000 Xu** + ★3 (4.000 RP). Thẻ tiến độ cấp kế tiếp hiện ngay dưới tên quán (cấp 1 → "Thuê mặt bằng", cấp 2 → "Mua nhà hàng") + dòng "Thuê nhà: còn N ngày / Nợ" |
| Phòng | `RoomSpec` theo cấp (`RoomLayout.For`): cấp 3 lưới 20 × 24 ô, sàn gỗ ghép, cửa rộng 2,2 m, camera lùi 19,5. Bố cục mặc định: quầy, bồn, kệ ly sau tường; **6 bộ bàn ghế tặng** (hoặc nhiều hơn nếu kho có: đồ đã mua được bày ra trước, không nhận trùng) + quạt/đèn thả |
| Nhân viên | Trần theo vai trò: cấp 2 = 1 Dọn dẹp + 1 Phục vụ; cấp 3 = 3 + 2 + 3 Pha chế (8). Đo ở cấp 2 (9 ngày, 14–16 ghế, `-cleaners 1` so với `2`): **người Dọn dẹp thứ hai không tăng khách** (40,1 so với 39,8 khách/ngày) mà tốn thêm 300/ngày (lãi ~2.700 so với ~2.300/ngày gồm cả ngày trả thuê); Pha chế cũng không bù lương (§0.2). Vì thế cả hai chỉ mở từ cấp 3. Bảng "Đội ngũ quán" dạng hàng, tự dài theo số vai trò mở |
| Quầy giao | Nhà hàng rộng khiến Pha chế mất phần lớn thời gian đi bộ giao ly (đo: chỉ ~4 ly/ngày/người ở cấp 1 kỹ năng). Nay khi có Phục vụ, Pha chế đặt ly lên **quầy giao** (`ReadyShelf`, tối đa 6 ly) rồi pha ly kế; Phục vụ mang ra. Không có Phục vụ thì Pha chế tự giao như cũ. Đo sau khi sửa: 8–13 ly/ngày/người |
| Máy móc | Là nội thất đặt trong quán (không có màn hình riêng): **máy xay 2 họng** 25.000 (Pha chế pha nhanh −15%), **máy cold brew** 60.000 (mở món cold brew), **máy espresso** 150.000 (Pha chế −25%, mở món Robusta); mỗi loại tối đa 1; hệ số cộng dồn và nới cửa sổ "pha hoàn hảo" của chủ quán. Món cần máy chỉ có khách gọi khi máy đang đặt trong quán |
| Món cấp 3 | Bạc xỉu kem muối 230 (vốn 75, không cần máy), Cold brew cam sả 200 (60, cần cold brew), Phin Robusta Cầu Đất 300 (100, cần espresso). Dùng lại các bước khay có sẵn (khay nguyên liệu không đổi). Menu cấp 3 = 8 món nên người chơi phải chọn |
| Kinh tế | Phí vận hành 700/ngày, hết tiền thuê (nợ cũ vẫn trả dần), Demand ×2,6, quay vòng 5, F_ref 400 (đã có từ trước) |
| Tổng kết ngày | Cấp 3 ẩn dòng "Thuê nhà", giữ dòng "Nợ"; nhãn dòng dài tự co |

**Số đo (bot, `-autoplay -autoupgrade3 -cheatt3`, bản Development, PC).** Mỗi lượt 4–6 ngày; bot lên cấp 2 rồi cấp 3 bằng cheat Xu/RP (nên lãi những ngày đầu bị ảnh hưởng bởi việc bot thuê đủ 8 nhân viên). Ngày ổn định (ngày 4–6):

| Cấu hình | Ghế | Khách/ngày | Doanh thu | Lãi sau lương + phí 700 |
|---|---|---|---|---|
| 3 nhân viên (Dọn dẹp ×2, Phục vụ), không Pha chế | 25–27 | 53 (mất ~55 vì chỉ chủ quán pha) | 7.600 | 2.200–4.200 |
| 8 nhân viên (lương 2.950/ngày), 14–23 ghế | 14–23 | 64–69 | 5.400–8.600 | 700–3.000 |
| 8 nhân viên + cộng 120.000 Xu để mua bàn + máy | ~26–47 | 74–88 | 11.000–13.600 | 4.600–7.400 |

- **Quay vòng chỗ ngồi thực tế ~3,2 khách/ghế/ngày** (công thức dùng 5): một lượt = đi bộ ~10 s + chờ ly ~25 s + uống 8–15 s + chờ lau bàn ~10 s, trong khi cửa sổ sinh khách chỉ ~165 giây. Vì vậy lỗi "hết chỗ" vẫn là lý do mất khách chính ở mọi cấu hình, trước cả nhân viên.
- **Lãi cấp 3 ~5.000–7.000/ngày so với tài liệu 20.000–35.000** (§3.1): thiếu 3–4 lần. Nguyên nhân là hình học (sảnh 10 × 12 m, 1 ngày = 210 giây) khiến khách/ngày chỉ ~80–110 thay vì 100–260, chứ không phải lỗi mã. Núm chỉnh gợi ý: giá/biên món cấp 3, hệ số Demand cấp 3, chiều dài ngày cấp 3, lương nhân viên, giá mua nhà (232.000 ≈ 90 ngày lãi cấp 2).
- Hiệu năng cảnh cấp 3 (có 8 nhân viên + ~40 khách): CPU script TB 0,44–0,49 ms/khung, cấp phát 49–77 B/khung, GC0 6–10 lần/phút, ~62k tam giác lúc đặt ít đồ (ngân sách 150k), 1 vật liệu.

**Lỗi tìm ra khi kiểm chứng.** (a) Bot đặt đồ / thuê nhân viên **ngay trong lúc chuyển cấp** làm nhân viên Phục vụ / Pha chế hầu như đứng yên cả ngày (đã chặn bằng `Transitioning`; người chơi không chạm được vì màn đen). (b) Pha chế đi giao xuyên sảnh chỉ làm ~4 ly/ngày (đã sửa bằng quầy giao). (c) Bảng nhân viên cấp 3 cần 8 hàng: hàng cao 104 px, vừa màn 1080.

### 0.7 Việc nên làm tiếp cho cấp 3 (đề xuất)

1. Quyết định núm kinh tế ở trên (lãi cấp 3 đang thấp hơn tài liệu 3–4 lần).
2. 3.2 phần còn lại: 3 khu (sân vườn / lầu / VIP) = mở rộng lưới + môi trường + CI theo khu.
3. 3.3: Thu ngân, Trưởng ca; 3.4 ca làm.
4. Mua nhiều nhân viên Phục vụ hoặc cho Phục vụ mang 2 ly/chuyến (Phục vụ là nút thắt kế tiếp sau quầy giao).

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

### 1.1 Trạng thái (2026-10-13)

| Mã | Trạng thái | Ghi chú |
|---|---|---|
| 3.1 Chuyển cấp 3 | **Xong** | `TierManager` tổng quát 1→2→3; mua nhà = 250.000 − cọc 18.000 hoàn = 232.000 Xu + ★3 (4.000 RP); thẻ tiến độ + nút "Mua nhà hàng!" + hộp xác nhận; hết tiền thuê, phí vận hành 700/ngày; nợ cũ vẫn trả dần |
| 3.2 Nhà hàng + 3 khu | **Một phần** | Sảnh chính 10 × 12 m (lưới 20 × 24 ô, sàn gỗ ghép, cửa rộng 2,2 m, camera xa hơn). **Chưa có 3 khu** sân vườn / lầu / VIP và CI theo khu (cần mở rộng lưới + môi trường) |
| 3.3 Nhân viên cấp 3 | **Một phần** | Trần theo vai trò (`GameBalance.StaffMaxCount`): T2 = 1 Dọn dẹp + 1 Phục vụ (**Pha chế và người thứ hai chỉ từ T3**), T3 = 3 Dọn dẹp + 2 Phục vụ + 3 Pha chế (8, trong dải 4–10). **Chưa có** Thu ngân, Trưởng ca |
| 3.4 Ca làm | Chưa | |
| 3.5 Máy móc | **Xong (bản gọn)** | 3 máy là nội thất đặt trong quán (không có UI riêng): máy xay 25.000 (−15% thời gian pha NV), espresso 150.000 (−25%, cộng dồn), cold brew 60.000 (mở món). Hệ số cũng nới cửa sổ "pha hoàn hảo" của chủ quán |
| 3.6 Món cấp 3 | **Xong** | Bạc xỉu kem muối 230 (không cần máy), Cold brew cam sả 200 (cần máy cold brew), Phin Robusta Cầu Đất 300 (cần máy espresso); tự mở khi lên cấp 3, vào menu nếu còn chỗ (menu T3 = 8 món) |
| 3.7 Kinh tế cấp 3 | **Một phần** | Công thức, hệ số, giá đã vào code và đo bằng bot (xem §0.6); chưa chỉnh lại số vì cần quyết định thiết kế |
| 3.8 Nội thất cấp 3 | **Xong (bản đầu)** | 11 model Blender: ghế bành nhung, ghế da, bàn đá cẩm thạch, máy lạnh tủ đứng, đèn chùm đồng, cây bàng lá đàn, giá vẽ, piano, 3 máy móc |
| 3.9 Nhiệm vụ mở rộng | Chưa | |
| 3.10 Thu nhập offline | Chưa | cần Firebase |

## 2. Việc CHƯA làm được nếu thiếu thông tin từ bạn (không đoán)

- **Firebase** (Auth ẩn danh, Cloud Save, Remote Config, Analytics, Crashlytics): cần tài khoản Firebase + `google-services.json` của bạn.
- **Quảng cáo thưởng / IAP**: cần tài khoản AdMob hoặc LevelPlay, Google Play Console (App ID, SKU). Gói `com.unity.purchasing` đã nằm trong manifest nhưng chưa có mã.
- ~~Model 3D cấp 3~~: đã chốt dùng script Blender tự sinh (`tools/gen_furniture.py`, nhóm `ASSETS_T3`).

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
