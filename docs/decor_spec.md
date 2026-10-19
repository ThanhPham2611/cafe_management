# Trang trí quán: sơn tường + sàn (v1)

Mã: `Scripts/Tier/DecorCatalog.cs` (danh mục + `DecorState`), `Scripts/UI/DecorPanel.cs` (bảng), `TierManager.PreviewDecor / BuyOrApplyDecor`, `EnvironmentBuilder.ApplyDecor`. Test: `Tests/EditMode/DecorCatalogTests.cs`. Bot: cờ `-paint` (log `[AutoPilot] PAINT`).

## Cách chơi
- Từ cấp 2. Mở bằng MENU → **TRANG TRÍ**. Bảng nằm nửa dưới màn hình để vẫn thấy quán.
- Chạm một mẫu là quán đổi ngay để **xem thử**. Bấm **MUA** (Xu hoặc Hạt) hoặc **DÙNG** nếu đã có. Bấm **Đóng** để quay lại mẫu đang dùng.
- Mua một lần là sở hữu vĩnh viễn ở mọi cấp; đổi qua lại giữa các mẫu đã có thì miễn phí.
- Lựa chọn lưu **theo từng cấp quán**: lên nhà hàng thì phòng mới bắt đầu bằng kiểu mặc định của cấp 3.
- v1 chỉ trang trí phòng cấp 2 và **sảnh** cấp 3. Các khu phụ (sân vườn, lầu, VIP) giữ kiểu riêng.
- Mua được bất cứ lúc nào, trừ lúc đang chuyển cấp. Tiền mua dời theo mốc Xu đầu ngày (`NotifyShopTransaction`), nên thoát rồi vào lại không được hoàn tiền.

## Danh mục

| Id | Tên | Họ | Giá | Mở |
|---|---|---|---|---|
| w_cream | Vôi ngà (mặc định) | — | miễn phí | — |
| w_mint | Bạc hà Tập thể | Hoài cổ | 1.500 Xu | Lv7 |
| w_hoian | Vàng Hội An | Đông Dương | 2.500 Xu | Lv9 |
| w_cement | Xi măng Sài Gòn | Hiện đại | 3.000 Xu | Lv12 |
| w_jade | Ngọc Đông Dương | Đông Dương | 8.000 Xu | cấp 3, Lv17 |
| w_brick | Gạch thẻ trần | Hoài cổ | 150 Hạt | cấp 2 |
| f_encaustic | Gạch bông Sài Gòn (mặc định cấp 2) | — | miễn phí | — |
| f_parquet | Sàn gỗ ghép (mặc định cấp 3) | — | miễn phí | cấp 3 |
| f_terracotta | Gạch tàu đỏ | Hoài cổ | 2.000 Xu | Lv8 |
| f_terrazzo | Đá mài granito | Hiện đại | 3.500 Xu | Lv10 |
| f_hue | Gạch men lam Huế | Đông Dương | 200 Hạt | cấp 2 |

## Hiệu ứng
- Điểm Style cộng vào sảnh (`GridSystem.DecorBonus`, tính trong F của sảnh, F vẫn chặn ở 1):
  - mỗi mặt (tường / sàn) dùng kiểu có họ: cấp 2 **+10**, cấp 3 **+20**;
  - tường và sàn **cùng họ** ("hợp tông"): thêm cấp 2 **+5**, cấp 3 **+10**.
- Phần lớn là trang trí cho đẹp. Mẫu bán bằng Hạt cho điểm **bằng** mẫu bán bằng Xu, nên không pay-to-win (§10.1).

## Kỹ thuật
- Chỉ dùng 64 ô palette có sẵn. Đổi màu bằng cách dựng lại mesh tường/sàn với ô khác, nên không thêm vật liệu và không thêm draw call.
- Tường và sàn không có collider, nên đổi chúng không phải bake lại NavMesh.
- Mesh cũ bị hủy kèm theo, không rò bộ nhớ khi xem thử nhiều lần.
- Save: `decorOwned` + `decorChoices` (chỉ thêm trường, không đổi version). Save cũ đọc ra kiểu mặc định.

## Để v2
- Sơn/sàn riêng cho từng khu cấp 3.
- Thẻ phong cách cho `FurnitureDef`, kèm bonus bộ (tường + sàn + ≥4 đồ cùng họ).
- Đồ treo tường theo các vị trí cố định của tranh.
- Biển hiệu tùy chỉnh.
- Sơn theo sự kiện (Tết, Trung thu).
- Gói chủ đề (`set_hoian`, `set_saigonxua`).
