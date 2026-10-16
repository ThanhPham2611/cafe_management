# Review kỹ thuật các hệ giữ chân (e292e9c..7c4f09d) — 2026-10-16

Nguồn: agent Unity đọc code (không chạy game). Phần "Xử lý" ghi việc đã làm.

**Kết luận:** không có lỗi mức cao (không thưởng nhân đôi, không vòng lặp, không NRE). Có 4 điểm trung bình và nhiều điểm nhỏ.

## Trung bình
| # | Vấn đề | Xử lý |
|---|---|---|
| T1 | Thưởng Xu chương truyện khách quen mất khi vào lại giữa ngày (`NotifyRewardGranted(0, rp)` không dời `dayStartXu`) | Đã sửa: `RegularsManager.RewardXu` + dời mốc Xu theo thưởng; có test |
| T2 | Thẻ nhận thưởng level và dòng sự kiện vẽ đè lên Cửa hàng / MENU (16:9) | Đã sửa: xếp dưới `ShopPanel`/`MenuRoot` (`SendBelowPanels`); đã chụp xác nhận |
| T3 | Thẻ nhún làm HudCanvas dựng lại mỗi khung | Đã sửa: chỉ nhún 3 giây đầu |
| T4 | `ClaimedLevel++` chạy trước `Wallet.Earn`; `OnMoneyForFund` chạy trong `Customer.Pay` không bọc lỗi | Đã sửa: dời mốc + Hạt trước, Earn sau, lưu một lần; `AdvanceFund` bọc try/catch |

## Nhỏ
| # | Vấn đề | Xử lý |
|---|---|---|
| N1 | Thưởng level lớn hơn con số của review trước (đến Lv16 = 19.380 Xu); cần đo bot | Doc §22 đã sửa; **chờ bot** |
| N2 | Chữ nổi "LÊN LEVEL" đè lên thẻ; nhiều chữ cùng lúc | Đã sửa: bỏ chữ nổi lúc đạt; "Mở khóa" và "Danh hiệu mới" chuyển sang lúc nhận |
| N3 | Barks nhỏ, không kẹp màn hình; `dirty_table` chồng chữ đỏ có sẵn | Đã sửa: cỡ 36, kẹp x/y, bỏ bark `dirty_table` |
| N4 | Nhãn "Tuần 3/7" dễ hiểu nhầm | Đã sửa: nhãn "Dự án tuần", giá trị "96/110 khách" |
| N5 | `RaiseDayEnded` không bọc lỗi | Đã sửa: từng handler riêng |
| N6 | Chị Lan, Ken cần khám phá món ẩn mới ghé; bot không khám phá | Ghi nhận; sổ khách quen đã hiện "Cần công thức"; số đo §23.7 thiếu 2 người |
| N7 | `Order.rewardMult` không ai đọc; `regular_arrives` chưa dùng | Đã sửa `rewardMult` (Customer đọc từ đơn); `regular_arrives` để dành |
| N8 | `WeeklyProject` tạo mảng mỗi lần gọi | Đã sửa: mảng tĩnh |
| N9 | Save dev giữa các bản: `Level` có thể thấp hơn `RewardedLevel` | Bỏ qua (chưa phát hành) |
| N10 | Vạch quỹ mờ lại khi tiêu Xu | Bỏ qua (vô hại) |

## Test đã thêm theo review
Thưởng Xu chương (xu / mũ / loại khác), món ruột không cao hơn cấp của khách (trừ món ẩn), roundtrip `SaveData` với mọi trường mới.
