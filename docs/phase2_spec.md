# Giai đoạn 2 – Đặc tả "Cấp 2: Thuê mặt bằng" (bản 1, 2026-10-11)

> Mục tiêu: người chơi đủ ★2 + 18.000 Xu thì **thuê một căn nhà phố nhỏ**, và game đổi nhịp từ "bán nhanh ở vỉa hè" sang "vận hành quán":
> có bàn bẩn phải dọn, có công thức pha chế để khám phá, có nhân viên đầu tiên, có khách quen kể chuyện.
> Số liệu: `economy_monetization.md` §3.4, §5, §6, §2.1–2.3. Thiết kế: `GDD.md` §3.3, §4.2, §5, §7.

## 0. Chuẩn bị chung (dùng lại những gì đã có)
- Chủ quán, `OwnerController` + hàng việc (`OwnerTask`): thêm loại việc mới `CleanTable`, `WashCups` (không làm lại hệ thống).
- Build mode + cửa hàng (`BuildModeController`, `ShopPanel`, `ShopRules`): mở thêm danh mục cấp 2 qua `FurnitureDef` (cần thêm field `minTier`).
- Lưu: `SaveData` lên **v3** (tier, tiền cọc, nợ thuê, tuần, bàn bẩn không cần lưu, công thức đã biết, nhân viên, tiến độ khách quen). Phải `Migrate()` từ v2.
- Mọi số liệu cân bằng đặt trong `GameBalance`/ScriptableObject, không hard-code trong logic.

## 1. Việc 1 – Chuyển cấp & ngôi nhà thuê (`Scripts/Tier/`)
- `TierManager`: tier hiện tại (1..4), điều kiện lên cấp (RP ≥ 300 **và** Xu ≥ 18.000), nút "Thuê mặt bằng" xuất hiện trên HUD khi đủ điều kiện (thanh tiến độ hiện cả khi chưa đủ: "Xu 12.400/18.000 · ★1/2").
- Cảnh chuyển: màn đen mờ → dọn đồ → cảnh mới (không tải scene riêng; **xóa bố cục T1 rồi dựng phòng T2** bằng `EnvironmentBuilder`) → pháo giấy + khách quen chúc mừng. Giữ lại ghế/bàn đã mua (đưa vào kho `Inventory`, người chơi đặt lại bằng build mode, bố cục mặc định tự đặt sẵn để chơi được ngay).
- Phòng T2 (dựng bằng code từ các khối palette như `PrototypeSceneBuilder.Block`, 1 mặt tiền nhà ống ~6 × 8 m): sàn gạch bông (decal/ô palette 40–43), tường kem, cửa cuốn, quầy bar 4×2 (thay xe đẩy), kệ ly, quạt trần, cửa ra vào ở mặt tiền; vùng đặt đồ trong nhà; camera giới hạn theo phòng. Lưới 0.5 m rộng 12 × 16 ô.
- Tiền thuê: 7.000 Xu/tuần in-game (7 ngày), trừ tự động sáng Thứ Hai; thiếu tiền → ghi nợ, trừ 30% doanh thu mỗi ngày tới hết nợ (không mất quán). Tổng kết ngày hiện dòng "Thuê nhà" và "Nợ".
- Nhu cầu T2 = `1,5 × Demand_T1`, capacity `chỗ × 4,5 × (1 + 0,25·nhân viên)` (economy §3.2); chi phí cố định/ngày 150.
- Điều khiển `-autoplay`: bot phải chơi được tới lúc lên cấp (cho phép cheat `-cheatxu`/`-cheatrp` chỉ trong build Development).

## 2. Việc 2 – Bàn bẩn, dọn dẹp, bồn rửa (`Scripts/Cleaning/`)
- `Table` trạng thái: Sạch → Có khách → **Bẩn** (ly/đĩa/giấy, đôi khi vết nước) → Đang dọn → Sạch. Bàn bẩn **chặn ghế** thuộc bàn (không xếp khách). Chỉ áp dụng từ T2 (T1 giữ "thu ly").
- Chạm bàn bẩn → chủ quán đi tới, **giữ chạm** 1,2 s để lau (thanh tròn), ly bẩn vào kho "ly bẩn"; mang tới **bồn rửa** (đồ nội thất mới `SM_Sink_Counter` do Tech Artist làm) và rửa (giữ chạm 0,8 s/lượt, tối đa 4 ly) → ly sạch về `CupStock`. Hết ly sạch thì không pha được (đã có sẵn cơ chế `CupStock`).
- Chỉ số **Sạch sẽ (CI)** theo economy §5: `CI = 100 − 40·bàn_bẩn/tổng_bàn − 0,4·FloorDirt`; hiện icon cây chổi + % trên HUD; tác động tới hài lòng `C`, walk-away và tip; bàn bẩn > 26 s thực: −5 CI/bàn.
- FloorDirt: +0,6 mỗi khách; lau sàn là việc `MopFloor` (chủ quán hoặc nhân viên dọn).
- Hiệu ứng: ruồi nhỏ bay quanh bàn bẩn > 26 s (particle đơn giản), icon trên bàn.

## 3. Việc 3 – Công thức pha chế (`Scripts/Recipes/`)
- Thay luồng "chạm theo thứ tự cố định" của Tier 1 bằng **bảng pha**: người chơi chọn **dụng cụ** + thêm **nguyên liệu** theo thứ tự (phin, máy đánh trứng, bình lắc…); chuỗi hành động so khớp với `RecipeDef.steps`. Mini-game theo bước: **giữ** (rót, đủ lượng), **vuốt** (đổ), **chạm** (thả đá), **xoay vòng** (đánh/khuấy).
- Món T2 theo economy §2.1: Trà chanh + 3 món ẩn **Cà phê muối, Cà phê cốt dừa, Cà phê trứng**. Nguyên liệu mới: muối, kem muối, cốt dừa, trứng, chanh, đường.
- **Sổ công thức** (UI): món đã biết hiện đầy đủ; món ẩn hiện bóng + gợi ý ("Vị mặn bất ngờ từ Huế…"). Mở khóa bằng **thử nghiệm tự do** (đúng công thức ẩn → ăn mừng + thêm vào menu) hoặc **mua gợi ý 30 Hạt** (Hạt chưa có nguồn thu → tạm cấp 30 Hạt khi qua T2). Thử sai: "món lỗi" (bán 40% giá, khách chê nhẹ), mất nguyên liệu.
- **Chất lượng 1–3★** theo economy §2.3 (lượng rót, đúng thứ tự, dụng cụ cấp ≥ 2, "pha hoàn hảo" = canh thời gian). Dụng cụ nâng cấp 1.500/2.500/4.000 Xu.
- Menu hôm nay: người chơi bật/tắt món (tối đa 6 món/ngày ở T2), khách chỉ gọi món trong menu và đã mở khóa.
- Tương thích ngược: 4 món T1 vẫn dùng được, chuyển thành công thức dạng mới (dữ liệu trong SO).

## 4. Việc 4 – Nhân viên đầu tiên (`Scripts/Staff/`)
- Thuê **nhân viên dọn dẹp** (1.500 Xu, lương 300/ngày, Lv1 6,0 s/bàn) và **phục vụ** (1.500, 350/ngày). Nhân vật dùng lại rig khách/chủ quán (Tech Artist thêm đồng phục: tạp dề + nón; 2 kiểu nam/nữ, màu đồng phục cố định).
- Nhân viên tự nhận việc từ hàng đợi chung (`WorkQueue`: bàn bẩn, ly cần mang, ly cần rửa); chủ quán vẫn thao tác tay bất cứ lúc nào (tay chủ quán ưu tiên).
- Thuê/sa thải/nâng cấp kỹ năng trong panel "Nhân viên". Lương trả cuối ngày (thiếu thì ghi nợ giống tiền thuê).
- Tổng kết ngày: dòng "Lương".

## 5. Việc 5 – Khách quen có câu chuyện (`Scripts/Regulars/`)
- 3 khách quen đầu: **Bà Sáu** (trà đá, T1, người tin quán đầu tiên), **Minh** (sinh viên, bạc xỉu), **Chú Tư** (xe ôm, cà phê đen đá). Mỗi người 3 đoạn truyện ngắn (3–6 câu thoại), mở theo **thân thiết** (0–10).
- Xuất hiện theo lịch (xác suất/ngày), có icon ♥ trên đầu, ngồi ghế ưa thích nếu còn. Phục vụ đúng món ruột + ly sạch + nhanh → +thân thiết. Đạt mốc thì hiện hộp thoại truyện (clay UI, avatar, nút "Tiếp") và quà (nội thất độc quyền / công thức).
- Dữ liệu trong `CustomerDef` (SO) + file truyện `Data/Story/*.json` (tiếng Việt, i18n key).
- Biên kịch: giọng văn Việt tự nhiên, ấm áp, hài nhẹ; không dài dòng.

## 6. Thứ tự làm & ranh giới file (để các agent không đụng nhau)
| Agent | Model | Sở hữu | Phụ thuộc |
|---|---|---|---|
| Kiến trúc & tích hợp (Claude chính) | Opus | `SaveData` v3, `GameBootstrap`, `TierManager` khung, `GameBalance` | — |
| Tier + phòng T2 | Sonnet | `Scripts/Tier/*`, `Editor/RoomBuilder.cs` | khung TierManager |
| Cleaning | Sonnet | `Scripts/Cleaning/*`, sửa nhẹ `Customers/*`, `Owner/*` | Tier (cờ `tier>=2`) |
| Recipes | Sonnet | `Scripts/Recipes/*`, `UI/BrewPanel.cs`, `Brewing/*` | — |
| Staff | Sonnet | `Scripts/Staff/*`, `UI/StaffPanel.cs` | Cleaning, Owner |
| Biên kịch + Regulars | Sonnet | `Scripts/Regulars/*`, `Data/Story/*` | — |
| Tech Artist | Sonnet | `tools/gen_*.py` (bồn rửa, quầy bar, kệ, quạt, đèn thả, nhân viên) | — |
| Kinh tế (cân lại sau khi chạy mô phỏng thật) | Opus | `economy_monetization.md`, `GameBalance` | tất cả |

Mỗi agent: không chạy Unity (khóa project), chỉ sửa file thuộc phần mình bằng Edit nhỏ ở file chung, báo cáo rủi ro biên dịch. Claude chính biên dịch, chạy test tự động `-autoplay` và sửa.

## 7. Tiêu chí nghiệm thu Giai đoạn 2
- Bot `-autoplay -cheatxu` lên được T2, chơi trọn 3 ngày T2 không lỗi runtime.
- Bàn bẩn chặn ghế; CI thay đổi đúng công thức (log `[AutoPilot]` in CI cuối ngày).
- Khám phá được ít nhất 1 món ẩn bằng thử nghiệm; menu bật/tắt hoạt động.
- Thuê được 1 nhân viên dọn, thấy nhân viên tự dọn bàn.
- 1 đoạn truyện khách quen hiện đúng lúc đạt mốc thân thiết.
- Lưu/đọc giữa các ngày đúng (kể cả file v2 cũ).
- APK Android cài và chạy được trên điện thoại thật (bạn thử và báo lại fps, cảm giác chạm).
