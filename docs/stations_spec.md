# Quầy pha chế phụ + Quầy line (v1)

Yêu cầu: "thêm các quầy line, quầy pha chế để nhân viên pha chế cho nhanh". Chỉ áp dụng cho **cấp 3** (cấp 2 không có Pha chế, `StaffMaxCount(Brewer, 2) = 0`).

## 0. Hiện trạng (đã đọc code)
- Pha chế pha **tại chỗ đứng** (`StaffWorker.Home`, hàng ngang cạnh chủ quán), không đi tới quầy. Nghĩa là "bớt đi bộ khi pha" KHÔNG phải núm thật.
- Phục vụ lấy ly trên `ReadyShelf` **từ xa** (không đi tới kệ). Kệ chỉ có `Capacity = 6`.
- Khi kệ đầy, `TryHandOffToServer` trả false → Pha chế **tự đi giao xuyên sảnh** rồi về Home: đây là chỗ mất thông lượng lớn nhất lúc cao điểm (6 Pha chế đẩy ly nhanh hơn 5 Phục vụ mang đi).
- Thời gian pha = `recipe.brewSeconds × cấp (1,8..1,0) × máy (0,85 × 0,75)`, sàn 1 s.

## 1. Quầy pha chế phụ (`Bar_Station_4x2`)
| Mục | Giá trị |
|---|---|
| Mở | Cấp 3, đặt ở mọi khu đã mở |
| Giá | 12.000 Xu |
| Tối đa | 2 quầy / quán |
| Footprint | 4x2 ô (2 x 1 m), dùng lại mesh `SM_Counter_Bar_4x2` |
| Chỗ làm | 2 chỗ / quầy (điểm đứng phía sau quầy, `TryGetApproachPoint` kiểu ghế) |
| Hiệu ứng | Pha chế có chỗ: thời gian pha × **0,85** (`GameBalance.BrewSpotFactor`) |
| Thêm người | Mỗi quầy +1 chỗ thuê Pha chế (`StaffMaxCount(..., stations)`): đủ 3 khu 6 → 8 |
| Comfort/Style | 0 / 0 (không phải đồ trang trí) |

Cài đặt:
- `FurnitureDef` thêm `int brewSpots` (2) và `int shelfSlots` (dùng ở mục 2); không cần FurnitureKind mới (dùng `Decor`/`Counter` sẵn có, cờ theo trường).
- `GridSystem.BrewSpots()` trả danh sách điểm đứng theo thứ tự đặt. `StaffRoster` gán Pha chế theo thứ tự thuê vào chỗ: `Home = điểm chỗ`, không có chỗ thì giữ hàng cũ. Tính lại khi `LayoutChanged`; đang pha dở thì pha xong rồi mới đi tới Home mới.
- `BrewerBehaviour.TryStart`: `total = BrewSeconds(recipe.brewSeconds, level, Game.BrewTimeFactor * (HasSpot ? 0.85f : 1f))`.
- Không phạt người không có chỗ (save cũ không bị chậm đi).

Lý do: 6 Pha chế lv3 với 0,85 → +~17% thông lượng pha; +2 người nữa → +~33%. Bù 2 × 450 lương/ngày; lãi mỗi khách ~100 Xu (18k/176) nên +25–35 khách/ngày ≈ +2,5–3,5k/ngày, hoàn vốn 24k trong ~8 ngày.

## 2. Quầy line (`Pass_Counter_4x1`)
| Mục | Giá trị |
|---|---|
| Mở | Cấp 3 |
| Giá | 6.000 Xu |
| Tối đa | 2 quầy |
| Footprint | 4x1 ô (2 x 0,5 m) |
| Hiệu ứng | Sức chứa kệ giao +4 mỗi quầy: 6 → 10 → 14 |

Cài đặt:
- `ReadyShelf.Capacity` thành thuộc tính đọc từ `Func<int>` (mặc định 6); `HasRoom` dùng nó. Test cũ giữ nguyên với 6.
- Hình: mỗi ly trên kệ hiện 1 mesh ly trên mặt quầy line (tối đa `shelfSlots` ly/quầy), chỉ để nhìn.
- v1 Phục vụ VẪN lấy ly từ xa (không bắt đi tới quầy line) để không giảm thông lượng; v2 mới cân nhắc điểm lấy ly thật (khi đó phải bù tốc độ).

Lý do: chặn việc Pha chế tự đi giao lúc kệ đầy; rẻ hơn quầy pha vì chỉ có tác dụng khi có kệ đầy.

## 3. Đo bằng bot (≥ 2 lượt mỗi cấu hình, TB ngày 3–7)
Lệnh nền: `-autoplay -autoupgrade3 -cheatt3 -t3xu -autobuy -autozones -fullstaff -bestmenu -days 7 -timescale 4 -freshsave`.
Cờ mới: `-stations N` (bot đặt N quầy pha phụ khi đủ Xu), `-passes N` (quầy line), `-nostationslots` (tắt +1 chỗ thuê để tách hiệu ứng).

| Cấu hình | Cờ |
|---|---|
| A gốc | `-stations 0 -passes 0` |
| B chỉ tốc độ | `-stations 2 -nostationslots -passes 0` |
| C đủ quầy pha | `-stations 2 -passes 0` |
| D chỉ line | `-stations 0 -passes 2` |
| E đủ cả hai | `-stations 2 -passes 2` |

Log mới mỗi ngày: `QUẦY pha=.. line=.. kệĐầy=.. tựGiao=.. lyPha/NV=..` (`kệĐầy` = số lần `TryHandOffToServer` thất bại vì hết chỗ).

Đạt khi: E lãi ≥ A + 2.500/ngày và ≥ 20k/ngày; khách mất ≤ A. D phải giảm `tựGiao` ≥ 50%, nếu D < +500/ngày thì hạ giá line xuống 3.000 hoặc bỏ. Nếu C ≈ B thì bỏ +1 chỗ thuê (lương ăn hết).
