# UI Style – "Clay Café" (bản 1, 2026-10-11)

> Phong cách **Claymorphism** (theo ui-ux-pro-max): khối mập, bo tròn lớn, viền dày, đổ bóng kép, nhấn có độ "lún".
> Màu lấy từ palette art_bible §3. Không dùng emoji hay ký tự đặc biệt (★…) làm icon, mọi icon là sprite.

## 1. Màu (token)
| Token | Hex | Dùng cho |
|---|---|---|
| `cream` | `#FFF4DC` | nền panel, thẻ |
| `cream_dk` | `#EFE3C8` | viền trong panel, ô trống |
| `coffee` | `#4A2C1E` | chữ chính, viền panel |
| `coffee_md` | `#6B4026` | chữ phụ, nút phụ |
| `red` | `#D9362B` | CTA chính (Ngày mới, Phục vụ), nhấn |
| `red_dk` | `#9E2119` | mép dưới nút đỏ |
| `green` | `#2FA05A` | đúng / thành công |
| `gold` | `#F2B93B` | xu, sao, thưởng |
| `gold_dk` | `#D9A441` | mép dưới nút vàng |
| `mint` | `#9CD3B5` | nguyên liệu lạnh (đá, sữa tươi) |
| `danger` | `#D62828` | kiên nhẫn sắp hết |
| `shadow` | `#2A1A12` @ 35% | bóng đổ |

Tương phản chữ ≥ 4.5:1: chữ `coffee` trên `cream` (≈ 11:1), chữ `cream`/trắng trên `red` (≈ 4.6:1, dùng chữ đậm ≥ 28px).

## 2. Font
| Vai trò | Font | Cỡ (ref 1920×1080) |
|---|---|---|
| Số lớn (Xu, giờ), tiêu đề | **Baloo 2 ExtraBold** | 44–64 |
| Nhãn nút | Baloo 2 ExtraBold | 30–36 |
| Chữ thường, mô tả | **Nunito Bold** | 24–28 (tối thiểu 22) |

File: `Art/Fonts/Baloo2-ExtraBold.ttf`, `Art/Fonts/Nunito-Bold.ttf` (OFL). Cả hai có đủ dấu tiếng Việt.
Chữ trên nền màu dùng `Outline`/`Shadow` component màu `coffee` 2px để nổi.

## 3. Hình khối
- Panel: bo **28px**, viền **4px** `coffee`, bóng đổ dưới 8px (`shadow`), có "highlight" sáng 1 dải trên cùng.
- Nút: bo **24px**, mép dưới dày **8px** màu tối hơn (hiệu ứng 3D). Khi nhấn: mặt nút lún xuống 6px, mép dưới mỏng lại, scale 0.96, 90ms ease-out. Thả ra: nảy về 1.0 trong 160ms (spring nhẹ).
- Chip (thanh trên): dạng viên thuốc, icon tròn lồi ra bên trái.
- Vùng chạm tối thiểu **96×96px** ở 1080p (≈ 48dp), khoảng cách giữa các nút ≥ 16px.

## 4. Bố cục màn hình ngang
```
┌──────────────────────────────────────────────────────────────────┐
│ (●1.791)  (★1 ▓▓░░░ 44/300)                    (◷ Ngày 1 · 12:28)│  ← chip, cách mép safe area 24px
│                                                                  │
│ ┌──────┐                                                         │
│ │ ĐƠN  │                                                         │
│ │ [ly] │← thẻ đơn: icon món + tên + vòng kiên nhẫn               │
│ │ [ly] │                                                         │
│ └──────┘                       QUÁN 3D                           │
│                                                                  │
│          ┌──────────────── BÀN PHA ────────────────┐            │
│          │ Cà phê sữa:  (phin)(sữa đặc)(đá) ← tiến độ│  (●Ly 6)  │
│          │ [Phin] [Sữa đặc] [Sữa tươi] [Đá] [Trà]  │            │
│          └──────────────────────────────────────────┘            │
└──────────────────────────────────────────────────────────────────┘
```
- Bàn pha **thu gọn** khi không có đơn được chọn (chỉ hiện 1 dải nhỏ "Chạm một đơn để pha"), trượt lên khi chọn đơn (220ms).
- Không có nút xoay camera: xoay bằng tay (xem §6).
- Toast (+43 Xu) bay lên từ vị trí khách trên màn hình, không phải giữa trên cùng.
- Bảng tổng kết: panel giữa màn hình, nền tối mờ 50% phía sau, ẩn bàn pha khi hiện.

## 5. Icon (sprite, vẽ bằng tools/gen_ui_sprites.py)
`coin`, `star`, `clock`, `cup`, `phin`, `sua_dac` (lon sữa), `sua_tuoi` (hộp sữa), `da` (viên đá), `tra` (ấm trà), `check`, `cross`, `heart` (hài lòng). Phong cách: khối tròn mập, viền `coffee` 3px, 1 mảng highlight trắng góc trên trái. Vẽ ở 4× rồi thu nhỏ (khử răng cưa), xuất 128×128.

## 6. Camera (cảm ứng)
| Thao tác | Kết quả |
|---|---|
| 1 ngón kéo ngang | **Xoay 360° tự do** quanh tâm quán, 0,14°/pixel; quán tính ngắn (văng tối đa ~60°, dừng sau ~0,5 giây) |
| 1 ngón kéo dọc | Nghiêng camera (pitch 30°–70°); vuốt chủ yếu theo chiều ngang thì không nghiêng |
| 2 ngón kéo | Di chuyển (pan) |
| 2 ngón chụm/mở | Zoom |
| 2 ngón xoay | Xoay (twist), chỉ khi đã xoay quá 10° (zoom / pan không tự xoay) |
| Cài đặt → "Giảm chuyển động" | Tắt quán tính, xoay chậm một nửa (≤ 150°/giây), thả tay thì dính góc 45°, không nghiêng, không xoay bằng 2 ngón. Lưu theo máy (PlayerPrefs `cafe.reducemotion`) |
| Chuột: trái kéo = xoay, phải/giữa kéo = pan, lăn = zoom | |
| Chạm nhanh (không kéo) | Chọn khách / ly / đơn |

## 7. Thuật ngữ (2026-10-16)
Mỗi từ chỉ MỘT nghĩa để người chơi không nhầm:
| Từ | Nghĩa | Hiển thị |
|---|---|---|
| **Sao ★** | hạng uy tín (1–5), mở khóa cấp quán và làm khách đông | số cạnh biểu tượng sao ở HUD; biểu ngữ "SAO N!" |
| **Lv** | level uy tín: bậc nhỏ giữa các sao (41 level) | "Lv N  RP/RP kế" dưới số sao; thẻ "Lv N: NHẬN +X Xu" |
| **Cấp quán** | tên riêng: **Vỉa hè / Mặt bằng / Nhà hàng / Chuỗi** (`Strings.TierShortName`) | không nói "cấp 2", "cấp 3" cho quán |
| **Cấp** (không kèm quán) | chỉ tay nghề nhân viên và dụng cụ ("Cấp 3 • Lương…") | bảng nhân viên, dụng cụ |
| Ly 3★ | chất lượng ly (nhiệm vụ "pha 5 ly đạt 3★") | khác hẳn sao uy tín |

