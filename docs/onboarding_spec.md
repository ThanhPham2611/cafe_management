# Phương án: Hướng dẫn người chơi mới + Màn hình chờ + Logo (bản nháp 2026-10-14)

> Vấn đề: hiện tại vào game là màn tạo nhân vật, sau đó ngày 1 chạy luôn, khách đến sau ~2 s, không có ai nói phải làm gì.
> Người chơi không biết: chạm khách để nhận đơn, bấm nguyên liệu theo thứ tự, chạm khách lần nữa để mang ly, thu ly để lại, mục tiêu là gom tiền thuê mặt bằng.
> GDD §11 đã có ý tưởng FTUE 5 phút nhưng chưa làm. Tài liệu này biến nó thành phương án làm được, khớp với mã hiện tại.

---

## 1. Luồng vào game mới

```
[Android 12+: màn hệ thống = icon + màu nền]   (tự có, chỉ cần cấu hình)
        │
[Boot scene: logo động 1,5–2 s]  ──►  [Màn tải: thanh tiến độ "ly cà phê nhỏ giọt" + mẹo]
        │                               (tải scene chính bằng LoadSceneAsync, dựng lưới/NavMesh ở đây)
        ▼
[Màn tiêu đề: quán 3D buổi sáng phía sau, logo, "Chạm để bắt đầu", Cài đặt]
        │
        ├── Chưa có save ─► [Tạo chủ quán (đã có)] ─► [Ngày 1 có hướng dẫn]
        └── Có save ──────► vào thẳng ngày đang chơi
```

---

## 2. Hướng dẫn (Tutorial)

### 2.1 Nguyên tắc
- **Làm, không đọc**: mỗi bước một câu ngắn (≤ 12 chữ) + bàn tay chỉ vào đúng chỗ phải chạm. Không có trang chữ dài.
- **Người hướng dẫn**: một nhân vật nói chuyện bằng `StoryDialog` có sẵn (đề xuất: **bà Sáu** – khách quen đầu tiên, hoặc "chú Tư" chủ quán cũ nhượng lại xe). Cần anh/chị chọn.
- **Không bị phạt khi đang học**: trong các bước có kịch bản, khách không mất kiên nhẫn, bấm sai nguyên liệu chỉ nhắc lại, không trừ sao.
- **Chỉ dạy đúng lúc cần**: phần lõi dạy ở ngày 1; các hệ thống khác (mua đồ, lau bàn, nhân viên, khu…) dạy khi chúng xuất hiện lần đầu.
- **Bỏ qua được**: nút "Bỏ qua hướng dẫn" (hỏi xác nhận). Có thể xem lại trong Cài đặt → "Hướng dẫn".

### 2.2 Phần lõi – Ngày 1 (khoảng 2–3 phút)

| # | Lúc nào | Bàn tay chỉ vào | Câu nói | Xong khi |
|---|---|---|---|---|
| 1 | Sau khi tạo chủ quán | – (hộp thoại) | "Chào chủ quán mới! Bán cà phê, gom tiền thuê mặt bằng nhé." | Chạm |
| 2 | Khách đầu tiên ngồi xuống (chỉ 1 khách, giữ kiên nhẫn) | Khách / thẻ đơn bên trái | "Có khách! Chạm vào khách để nhận đơn." | `Station.Select` |
| 3 | Bàn pha mở ra | Nguyên liệu đúng bước (phin → đá) | "Làm theo thứ tự trên bảng." | Ly sẵn sàng |
| 4 | Ly sẵn sàng | Khách | "Chạm khách để mang ly ra." | `CustomerServed` |
| 5 | Vừa nhận tiền | Chip Xu | "Tiền về rồi! Phục vụ nhanh còn được tip." | Tự qua sau 2,5 s |
| 6 | Khách rời đi, còn ly | Ly trên bàn | "Thu ly về, không thì ghế bị chiếm." | Thu ly |
| 7 | Thả 2–3 khách cùng lúc (trả lại nhịp bình thường) | Vòng tròn quanh thẻ đơn | "Vòng này là kiên nhẫn. Hết là khách bỏ đi!" | Tự qua; người chơi tự làm |
| 8 | Cuối ngày – bảng tổng kết | Sao / thanh tiến độ cọc | "Sao tăng thì khách đông hơn. Gom đủ cọc để thuê mặt bằng!" | Bấm "Ngày mới" |

Đơn đầu tiên ép là **Cà phê đen** (2 bước: phin → đá – món ít bước nhất). Khách thứ hai ép **Cà phê sữa** (phin → sữa đặc → đá) để người chơi thấy món khác nhau thì thứ tự khác nhau; từ khách thứ ba chọn món ngẫu nhiên như bình thường.

### 2.3 Hướng dẫn theo tình huống (hiện một lần, khi gặp lần đầu)

| Khi | Nội dung |
|---|---|
| Ngày 2 bắt đầu | Mở Cửa hàng → mua ghế đẩu → kéo đặt / xoay / xác nhận (bàn tay dẫn cả chuỗi) |
| Khách đầu tiên bỏ về | "Khách chờ lâu quá nên đi mất. Thêm ghế hoặc làm nhanh hơn." |
| Hết ly sạch | Chỉ chip Ly: "Hết ly! Thu ly ở bàn về." |
| Ngày 3 – mở bạc xỉu | Chỉ nút Sổ công thức |
| Lần đầu có nhiệm vụ hoàn thành | Chỉ nút Nhiệm vụ / rương |
| Đủ điều kiện lên cấp 2 | Chỉ nút Nâng cấp |
| Vào cấp 2 | Bàn bẩn → chạm để lau; bồn rửa ly; lau sàn; thuê Dọn dẹp; thử pha tự do |
| Vào cấp 3 | Máy móc, Pha chế/Phục vụ, nút Mở rộng khu |

Có sẵn `DayAdvisor` (gợi ý ở bảng tổng kết) → giữ nguyên, hướng dẫn tình huống bổ sung chứ không thay.

### 2.4 Cách làm kỹ thuật

- **`TutorialDirector`** (Scripts/Tutorial/): máy trạng thái các bước. Mỗi bước = `{ id, điều kiện bắt đầu, mục tiêu chỉ, câu nói, điều kiện xong }`. Nghe `GameEvents` có sẵn (`OrderAdded`, `CustomerServed`, `CustomerLeft`, `DayStarted`, `DayEnded`…) + vài sự kiện mới (`CupCollected`, `ShopOpened`, `FurniturePlaced`).
- **`TutorialRules`** (logic thuần, có test EditMode): chọn bước kế, khi nào bỏ qua, bước nào đã xem.
- **`TutorialOverlay`** (UI): lớp tối có "lỗ" ở vùng mục tiêu (4 tấm `Image` quanh hình chữ nhật – không cần shader), vòng sáng nhấp nháy, sprite bàn tay động (nhấn / kéo). Mục tiêu UI lấy `RectTransform`; mục tiêu 3D (khách, ly, bàn) chiếu ra màn hình mỗi khung như `OrderBubble`. Lớp tối chặn chạm ngoài lỗ (TapInput đã bỏ qua chạm trên UI).
- **Móc vào gameplay** (ít, có cờ):
  - `CustomerSpawner`: ngày 1 khi đang hướng dẫn → chỉ 1 khách, sau bước 6 mới thả tiếp.
  - `Customer`: `PatiencePaused` khi đang ở bước có kịch bản.
  - `BrewStation`: sai bước khi hướng dẫn → chỉ nhắc, không tính lỗi.
  - Món đơn đầu ép bằng `RecipeBook.PickRecipe` override một lần.
- **Lưu**: `SaveData.tutorialStep` (int) + `tutorialSeen` (string[] id tình huống). Save cũ (đã chơi) → coi như xong phần lõi.
- **Bot**: autopilot thêm cờ `-tutorial` để bot đi qua hướng dẫn (kiểm tra không kẹt), mặc định bot bỏ qua hướng dẫn để không đổi số đo kinh tế.
- **Đo**: sự kiện `tutorial_step` đã có trong economy_monetization.md §FTUE → khi có Firebase thì log.
- **Chữ**: thêm vào `Strings.cs` (tiếng Việt, sẵn chỗ cho tiếng Anh).

---

## 3. Logo

### 3.1 Tên
GDD §14 còn để mở "tên chính thức". Hiện `productName = "Quan Ca Phe Viet"`. Cần chốt trước khi vẽ logo (gợi ý: **Quán Cà Phê Việt**, **Phin Ơi!**, **Cà Phê Vỉa Hè**).

### 3.2 Hướng thiết kế (khớp ui_style "Clay Café")
- Chữ **Baloo 2 ExtraBold** (đã có, đủ dấu), màu kem `#FFF4DC`, viền nâu cà phê `#4A2C1E` dày, bóng đổ dưới.
- Biểu tượng: **phin nhôm + ly sữa đá** (đã có mô hình `SM_Cup_Phin_SuaDa`), khói cà phê bay lên, điểm nhấn đỏ `#D9362B` + vàng `#F2B93B` (như art_bible: "cờ vàng-đỏ nhấn").
- Hai bản: **logo ngang** (màn tiêu đề, splash) và **icon vuông** (biểu tượng app, chỉ có phin + ly, không chữ).

### 3.3 Cách tạo (không cần thuê ngoài cho bản đầu)
1. Blender: render phin + ly ở góc 3/4, nền trong suốt, 1024 px – dùng lại `tools/render_icons.py` (đã render icon nội thất).
2. Script mới `tools/make_logo.py` (Python + Pillow 11.3, giống `gen_ui_sprites.py`): ghép chữ Baloo 2 + viền + bóng + hình ly → xuất:
   - `Art/UI/Logo/logo_wide.png` (2048×1024), `logo_icon_1024.png`
   - Icon Android: adaptive icon (lớp nền + lớp trước 432×432), bản cũ 192/144/96/72/48
3. Cấu hình `PlayerSettings` bằng editor script (icon, tên công ty, màu nền splash) để không phải bấm tay.
4. Khi game sắp phát hành có thể thuê họa sĩ vẽ lại logo từ bản này.

---

## 4. Màn hình chờ (Splash + Loading)

### 4.1 Unity splash
- Unity 6 bản Personal **cho phép tắt** "Made with Unity". Phương án: tắt splash Unity (hoặc chỉ hiện logo mình 1 s trên nền kem).
- Android 12+: hệ điều hành tự hiện icon app trên màu nền khi mở → đặt màu nền nâu cà phê/kem cho đồng bộ.

### 4.2 Boot scene mới (`Scenes/Boot.unity`, scene số 0)
- Nền kem, logo bật ra (scale 0.8 → 1.05 → 1, 400 ms), khói cà phê bay (2–3 sprite mờ trôi lên).
- Thanh tải = **ly cà phê nhỏ giọt đầy dần** (vẽ bằng sprite clay có sẵn `ui_bar_*`), dưới là **mẹo ngẫu nhiên** ("Thu ly sớm để ghế trống cho khách mới"…).
- Tải `Prototype_Tier1` bằng `SceneManager.LoadSceneAsync`; ẩn luôn thời gian dựng lưới + bake NavMesh trong `GameBootstrap.Build` (hiện đang làm đứng hình khung đầu) → màn tải chỉ biến mất khi `GameBootstrap` báo sẵn sàng.
- Tối thiểu 1,5 s để logo kịp hiện, tối đa theo thời gian tải thật.
- Scene này dựng bằng editor script (như `CafeMenu.SetupAll` đang dựng scene chính) và thêm vào Build Settings ở vị trí 0.

### 4.3 Màn tiêu đề (trong scene chính)
- Camera bay chậm quanh quán (quán đã dựng sẵn phía sau), logo ở trên, nút lớn **"Mở quán"** (hoặc "Chạm để bắt đầu"), nút nhỏ Cài đặt.
- Bấm → camera về vị trí chơi → nếu chưa tạo chủ quán thì mở `CreatorScreen`.

---

## 5. Thứ tự làm đề xuất

| Đợt | Việc | Ước lượng |
|---|---|---|
| A | Logo bản đầu (Blender render + `make_logo.py`) + icon app + cấu hình PlayerSettings | 0,5 ngày |
| B | Boot scene + màn tải + màn tiêu đề | 1 ngày |
| C | Khung hướng dẫn: `TutorialDirector/Rules/Overlay` + lưu + test | 1 ngày |
| D | Kịch bản ngày 1 (8 bước) + móc spawner/kiên nhẫn/bàn pha + bot `-tutorial` | 1 ngày |
| E | Hướng dẫn tình huống (ngày 2 mua ghế, hết ly, cấp 2, cấp 3) + mục "Hướng dẫn" trong Cài đặt | 1 ngày |

Kiểm chứng: test EditMode cho `TutorialRules`; chạy bản Windows có đồ họa + `-autoshot` để chụp từng bước; bot `-tutorial -freshsave` chạy hết ngày 1–3 không kẹt.

---

## 6. Cần anh/chị chốt
1. Tên chính thức của game (để làm logo).
2. Người hướng dẫn: bà Sáu, nhân vật mới (chú Tư / cô chủ cũ), hay không có nhân vật (chỉ bàn tay + chữ)?
3. Có cho bỏ qua hướng dẫn không (đề xuất: có).
4. Làm theo thứ tự A → E ở trên, hay làm hướng dẫn trước rồi logo sau?
