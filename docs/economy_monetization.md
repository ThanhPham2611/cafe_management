# Quán cà phê Việt — Economy & Monetization Design

> Phiên bản: v0.1 (draft để tune) · Phạm vi: tiền tệ, giá, pacing, uy tín (★), độ sạch, công thức, offline, ads/IAP, LiveOps, KPI, Remote Config.
> Mọi con số dưới đây là **giá trị khởi điểm**, đều phải nằm trong Remote Config (xem §15) và được kiểm chứng bằng mô phỏng (§16).

---

## 0. Nguyên tắc thiết kế (không thương lượng)

| # | Nguyên tắc | Hệ quả cụ thể |
|---|---|---|
| 1 | **Không pay-to-win** | Hạt cà phê vàng **không đổi ra Xu**, không mua tier, không mua ★, không tăng giá đồ uống/khách. IAP chủ yếu là cosmetic. |
| 2 | **Không energy, không timer chờ** | Không có "chờ 4h để nâng máy". Nâng cấp tức thì khi đủ Xu. |
| 3 | **Chỉ rewarded ads, opt-in** | Không interstitial, không banner. Ad luôn là *bonus*, không bao giờ là cổng chặn tiến trình. |
| 4 | **Cozy trước, tycoon sau** | Thua lỗ không bao giờ làm người chơi "phá sản": Xu không xuống âm; nếu thiếu tiền thuê thì nợ được trừ dần (§4.3). |
| 5 | **Tiến trình theo đúng 4 tier của owner** | Vỉa hè → Thuê mặt bằng → Mua nhà hàng → Chuỗi. Mỗi tier mở cơ chế mới, không chỉ "số to hơn". |

**Quy ước thời gian:** 1 ngày in-game ≈ **3,5 phút thực** (Remote Config `day_length_sec = 210`). Giờ mở cửa 06:00–22:00 (16 giờ in-game) → **1 giờ in-game ≈ 13 giây thực**. Cuối ngày có màn "Tổng kết ngày" (~10 s). *(Từ cấp 3 một ngày dài 300 s ≈ 5 phút, 1 giờ in-game ≈ 18,75 s thực — xem §19.2.)*

**Hương vị VND:** 1 Xu ≈ "100đ" về cảm giác — cà phê sữa 100 Xu ≈ "10 nghìn". Đủ quen thuộc với người Việt mà vẫn là số game.

---

## 1. Tiền tệ

| | **Xu** (soft) | **Hạt cà phê vàng** (hard) |
|---|---|---|
| Icon | đồng xu nhôm in hình phin | hạt cà phê vàng óng |
| Vai trò | vận hành & tiến trình (nguyên liệu, lương, thuê, mua nhà, nội thất thường) | cosmetic cao cấp, tiện ích không ảnh hưởng cân bằng |
| Tốc độ kiếm F2P | 1.200–2.500/ngày in-game (T1) → ~30.000/ngày (T3) | **~25–35 Hạt / ngày thực** (≈ 800–1.000/tháng) |
| Đổi chéo | Xu → Hạt: **không** · Hạt → Xu: **không** | |
| Hiển thị | số nguyên, dấu chấm phân cách (12.500) | số nguyên |

### 1.1 Xu — Sources & Sinks

| Nguồn (Source) | Lượng tham chiếu | Ghi chú |
|---|---|---|
| Bán đồ uống | 30–300 Xu/ly (giá × hệ số chất lượng) | ~85% tổng thu |
| Tip | 0–20% giá ly (theo ★ công thức + phong cách nội thất) | ~5–7% tổng thu |
| Đơn đặc biệt (khách VIP, đơn mang đi) | 1,5–2× giá thường | xuất hiện 1–3 lần/ngày từ T2 |
| Nhiệm vụ ngày | 150 × TierScale/ngày | TierScale = 1 / 4 / 12 / 30 cho T1..T4 |
| Thu nhập offline | §8 | chỉ khi có nhân viên (T2+) |
| Chi nhánh (idle) | §8.3 | T4 |
| Rewarded ad | +30% doanh thu ngày, x2 offline | §9 |
| Login, sự kiện, thành tựu | Xu × TierScale | |

| Sink | Lượng tham chiếu | Ghi chú |
|---|---|---|
| Nguyên liệu | 5–100 Xu/ly (~30–35% giá) | trừ tự động khi pha |
| Ghế/bàn/nội thất thường | xem §3.6 | sink chính T1–T2 |
| Tiền cọc + thuê mặt bằng | cọc 18.000; 7.000/tuần | T2 |
| Lương nhân viên | 300–2.500/ngày/người | T2+ |
| Mua nhà hàng | 250.000 | T3 |
| Nâng cấp máy móc / dụng cụ | 1.500–150.000 | T2–T3 |
| Mở khu (zone) mới | 40.000 / 90.000 / 180.000 | T3 |
| Chi nhánh | 2.000.000 (CN1), ×1,6 mỗi CN tiếp | T4 |
| Nâng cấp cap offline | 50.000 (10h) / 300.000 (12h) | |
| Chi phí vận hành (điện nước, phí vỉa hè) | 60 / 150 / 700 /ngày | |

### 1.2 Hạt cà phê vàng — Sources & Sinks

| Nguồn | Lượng | Tần suất / Cap |
|---|---|---|
| Hoàn thành 3/3 nhiệm vụ ngày (rương) | 10 | 1/ngày |
| Login 7 ngày | 5–30 (TB ~10/ngày) | 1/ngày |
| Rương Hạt (rewarded ad) | 3 | 3/ngày → 9 |
| Lên ★ | 50 / 100 / 200 / 400 (★2..★5) | 1 lần |
| Thành tựu (khám phá công thức ẩn, phục vụ 1.000 khách…) | 10–50 | ~60 thành tựu |
| Sự kiện (track free) | 150–250 / sự kiện | ~1 sự kiện/tháng |
| Mở tier mới | 100 | 1 lần/tier |
| IAP | §10 | — |

| Sink | Giá (Hạt) | Ghi chú |
|---|---|---|
| Nội thất premium lẻ | 40–400 | chỉ +Style (trang trí), Comfort ≤ đồ Xu cùng tier |
| Bộ nội thất chủ đề (set 8–12 món) | 1.200–2.000 | có bonus set "Style +15%" — chỉ ảnh hưởng tip/RP như set Xu tương đương |
| Trang phục nhân viên / chủ quán | 80–250 | thuần cosmetic |
| Biển hiệu, đèn, nhạc nền | 60–300 | cosmetic |
| Gợi ý công thức ẩn ("Mẹo bà nội") | 30 | tiện ích; công thức ẩn **luôn** có thể tự tìm ra |
| Slot layout lưu thêm (build mode) | 150 | tiện ích |
| Đổi tên quán / màu tường đặc biệt | 50 | |
| Premium track sự kiện (thay vì tiền thật) | 900 | ~1 tháng F2P tiết kiệm được → F2P vẫn "chạm" được pass |

> **Giới hạn:** Hạt **không** dùng để: mua Xu, trả lương, mua nhà, mua ★, tăng cap offline, bỏ qua tier.

---

## 2. Thực đơn, giá & biên lợi nhuận

### 2.1 Đồ uống (giá gốc tại chất lượng 2★)

| Đồ uống | Tier mở | Giá bán (Xu) | Nguyên liệu (Xu) | Biên (Xu) | Biên % | Thời gian pha (s thực) | Xu/giây pha chế | Ghi chú |
|---|---|---|---|---|---|---|---|---|
| Trà đá | T1 | 30 | 5 | 25 | 83% | 2 | 12,5 | khách "ngồi chơi", thường gọi kèm |
| Cà phê đen | T1 | 80 | 25 | 55 | 69% | 4 | 13,8 | |
| Cà phê sữa | T1 | 100 | 35 | 65 | 65% | 5 | 13,0 | đồ uống được gọi nhiều nhất |
| Bạc xỉu | T1 (ngày 3) | 120 | 45 | 75 | 63% | 6 | 12,5 | mở khóa tutorial ngày 3 |
| Trà chanh | T2 | 60 | 15 | 45 | 75% | 3 | 15,0 | đồ "rush hour" |
| Cà phê muối *(ẩn)* | T2 | 180 | 55 | 125 | 69% | 9 | 13,9 | |
| Cà phê cốt dừa *(ẩn)* | T2 | 220 | 70 | 150 | 68% | 12 | 12,5 | |
| Cà phê trứng *(ẩn)* | T2 | 250 | 80 | 170 | 68% | 14 | 12,1 | RP cao nhất T2 |
| Cold brew cam sả | T3 | 200 | 60 | 140 | 70% | 6* | 23,3* | *cần máy cold brew (ủ sẵn mẻ) |
| Bạc xỉu kem muối | T3 | 230 | 75 | 155 | 67% | 10 | 15,5 | |
| Cà phê phin Robusta Cầu Đất | T3 | 300 | 100 | 200 | 67% | 15 | 13,3 | đặc sản, khách VIP thích |

**Thiết kế chủ ý:** Xu/giây pha chế của mọi ly **xấp xỉ 12–15** → không có ly "tối ưu tuyệt đối". Khác biệt nằm ở:
- **Ly rẻ, nhanh**: thắng khi đông khách/rush hour (giảm thời gian chờ → W cao → RP giữ được).
- **Ly đắt, chậm**: thắng khi **chỗ ngồi** là nút thắt (mỗi ghế sinh nhiều Xu hơn), tip và RP cao hơn (khách thích đặc sản), nhưng gây xếp hàng nếu thiếu pha chế.
- Máy móc T3 (cold brew, máy xay 2 họng) phá vỡ thế cân bằng có chủ đích → phần thưởng cho đầu tư.

### 2.2 Chi phí nguyên liệu (per serving, mua tự động ở "chợ")

| Nguyên liệu | Xu/đơn vị dùng | Dùng trong |
|---|---|---|
| Cà phê bột Robusta (1 phin) | 20 | đen, sữa, bạc xỉu, muối, cốt dừa, trứng |
| Sữa đặc (1 muỗng) | 10 | sữa, bạc xỉu, muối |
| Đá viên | 2 | gần như tất cả |
| Trà (túi/nắm) | 3 | trà đá, trà chanh |
| Chanh | 8 | trà chanh |
| Kem muối | 25 | cà phê muối, bạc xỉu kem muối |
| Cốt dừa | 40 | cà phê cốt dừa |
| Lòng đỏ trứng + đường | 50 | cà phê trứng |

Nâng cấp **"Mối mua sỉ"** (T2: 8.000 Xu, T3: 60.000 Xu): −8% / −15% giá nguyên liệu.

### 2.3 Chất lượng công thức (1–3★) → giá & tip

Chất lượng ly quyết định bởi: đúng nguyên liệu, đúng dụng cụ, đúng thứ tự bước, và cấp dụng cụ.

| Chất lượng | Điều kiện | Hệ số giá | Xác suất tip | Cỡ tip (% giá) | Q (vào công thức ★) |
|---|---|---|---|---|---|
| 1★ | sai 1 bước hoặc dụng cụ cấp 1 cho món ẩn | ×0,85 | 5% | 5% | 0,0 |
| 2★ | đúng công thức | ×1,00 | 25% | 10% | 0,5 |
| 3★ | đúng + dụng cụ ≥ cấp 2 + "pha hoàn hảo" (mini-game timing hoặc nhân viên pha chế Lv ≥3) | ×1,20 | 50% | 15% | 1,0 |

- **Tip thực** = `price × qMult × TipSize(q) × (1 + 0,5·F)` với xác suất `TipChance(q) + 0,2·F` (F = điểm nội thất §3.4, 0..1).
- Thời gian pha 3★ dài hơn **+20%** (nếu người chơi tự pha mini-game); nhân viên pha chế Lv3+ cho 3★ không tốn thêm thời gian → giá trị của nâng cấp nhân viên.
- Sai thứ tự 2 bước trở lên → ly hỏng, mất nguyên liệu (không bán được). Món ẩn chưa khám phá: khách hỏi "có cà phê muối không?" (gợi ý tự nhiên).

---

## 3. Các tier — thông số chi tiết

### 3.1 Bảng tổng quan

| | **T1 Vỉa hè** | **T2 Thuê mặt bằng** | **T3 Mua nhà hàng** | **T4 Chuỗi** |
|---|---|---|---|---|
| Điều kiện mở | — | ★2 + **18.000 Xu** cọc | ★3 + **250.000 Xu** | ★4 + **2.000.000 Xu** (CN1) |
| Chỗ ngồi | 4 → 12 ghế nhựa | 8 → 24 (bàn 2 ghế) | 30 → 80 (3 zone) | mỗi CN 40 (tự vận hành) |
| Khách/ngày | 16 → 40 | 35 → 110 | 100 → 260 | CN: 80–200 (idle) |
| Giá TB/ly | ~75–80 | ~120 | ~150 | ~150 |
| Chi phí cố định/ngày | 60 | 150 + thuê 1.000 + lương | 700 + lương | lương quản lý + 15% doanh thu CN |
| Lãi ròng/ngày (TB) | 1.200–2.500 | 4.000–9.700 | 20.000–35.000 | CN: ~45% lãi quán chính |
| Nhân viên | 0 (người chơi tự làm) | 1–3 | 4–10 ở sảnh, chia ca; **mỗi khu phụ đã mở +3** (đủ 3 khu 17, §6, §19.6) | quản lý CN |
| Cơ chế mới | phục vụ, thu ly | dọn bàn, công thức, build mode, NV | zone, ca, nâng cấp máy, khu VIP | quản lý idle, offline lớn |
| Thời điểm mục tiêu | phút 0 | **phút 45–60** (ngày in-game ~13–16) | **ngày thực 5–10** (in-game ~55–90) | **tuần thực 3–4** (in-game ~170–230) |

> Dòng "Nhân viên" là dải thiết kế. Bản cài đặt (2026-10-14): T2 tối đa 2 (1 Dọn dẹp + 1 Phục vụ; Pha chế chỉ từ T3 vì đo không bù lương, §17); T3 sảnh 8 (3 Dọn dẹp + 2 Phục vụ + 3 Pha chế), mỗi khu phụ đã mở nới thêm 1 chỗ cho mỗi vai trò — bảng trần ở §6.

### 3.2 Nhu cầu khách (Demand)

```
Demand_T1(day) = 18 + 9·(★−1) + 0,5·day                (khách/ngày)
Demand_T2 = 1,5 × Demand_T1-formula                    (mặt bằng có biển hiệu, vị trí tốt hơn)
Demand_T3 = 2,6 × Demand_T1-formula  (+ zone bonus: × (1 + 0,2 · số khu phụ đã mở), xem §19)
Khách thực = min(Demand × eventMult × adBoost, Capacity)
Capacity_T1 = ghế × 4 lượt/ngày
Capacity_T2 = chỗ × 4,5 × (1 + 0,25·số NV)
Capacity_T3 = chỗ × 5   × (1 + 0,15·số NV)
```

Ý nghĩa: ở mỗi tier, ban đầu **Capacity** là nút thắt (→ mua ghế/bàn), sau đó **Demand/★** là nút thắt (→ nâng chất lượng, nội thất, độ sạch). Hai nút thắt thay phiên nhau = vòng lặp tycoon.

### 3.3 Tier 1 — Vỉa hè

| Hạng mục | Số |
|---|---|
| Xu khởi đầu | 500 |
| Ghế nhựa thứ n | `200 + 75·n` Xu (ghế thứ 5: 500, ghế thứ 12: 1.025) |
| Dù che nắng | 1.200 Xu (+Comfort, giảm walk-away trưa nắng 30%) |
| Bình trà đá to | 800 Xu (trà đá tự phục vụ, −1 s/ly) |
| Phí vỉa hè | 60 Xu/ngày |
| Thu ly | người chơi chạm ly → +1 Xu "ve chai" vui; ly để >30 s in-game: ghế đó bị chặn (nhẹ hơn dọn bàn T2) |

### 3.4 Tier 2 — Thuê mặt bằng

| Hạng mục | Số |
|---|---|
| Tiền cọc | **18.000 Xu** (hoàn lại 100% khi chuyển sang T3 — cảm giác "được thưởng") |
| Tiền thuê | **7.000 Xu/tuần** (trả tự động sáng thứ Hai in-game, ~17% doanh thu T2) |
| Thiếu tiền thuê | ghi nợ, trừ 30% doanh thu mỗi ngày cho đến hết; không phạt ★, không mất quán |
| Bàn + 2 ghế thứ k | `700 + 120·(k)` Xu (k = số bàn đã mua thêm) |
| Phí tuyển NV | 1.500 Xu |
| Dụng cụ cấp 2 (phin nhôm → phin inox, ấm, máy đánh trứng) | 1.500 / 2.500 / 4.000 Xu |
| Mở khóa công thức ẩn | khám phá bằng thử nghiệm (miễn phí) hoặc gợi ý 30 Hạt |

### 3.5 Tier 3 — Mua nhà hàng & Tier 4 — Chuỗi

| Hạng mục | Số |
|---|---|
| Giá mua nhà | **250.000 Xu** (cọc T2 18.000 được hoàn → thực trả 232.000) |
| Zone 2 (sân vườn) / Zone 3 (lầu, view) / Zone VIP | 40.000 / 90.000 / 180.000 Xu |
| Máy xay 2 họng / Máy cold brew / Máy pha espresso | 25.000 / 60.000 / 150.000 Xu |
| Ca làm (sáng/chiều/tối) | NV mỗi ca; thiếu ca → Capacity −30% trong ca đó |
| **Chi nhánh n** | `2.000.000 × 1,6^(n−1)` Xu (CN2 3,2M; CN3 5,12M); tối đa 6 CN ở launch |
| Quản lý CN | lương 2.500 Xu/ngày; Lv1–5 tăng hiệu suất CN 45% → 70% |

### 3.6 Nội thất — khoảng giá

| Loại | T1 | T2 | T3 | Comfort | Style |
|---|---|---|---|---|---|
| Ghế | 200–1.025 (nhựa) | 300–1.500 (gỗ, mây) | 1.500–8.000 (sofa, ghế bành) | 1–8 | 0–5 |
| Bàn | — | 700–3.000 | 3.000–15.000 | 0–3 | 1–6 |
| Trang trí (cây, tranh, đèn) | 150–600 | 400–4.000 | 3.000–30.000 | 0 | 2–15 |
| Quạt / máy lạnh | 600 (quạt) | 2.500–9.000 | 12.000–40.000 | 3–12 | 0–2 |
| Premium (Hạt) | 40–400 Hạt | | | ≤ đồ Xu cùng tier | +20–40% so với đồ Xu |

---

## 4. Uy tín (★) — công thức

### 4.1 Mức hài lòng mỗi khách (Satisfaction S, 0..1)

```
S = 0,35·Q + 0,30·W + 0,20·C + 0,15·F

Q = (chất lượng ly − 1) / 2                      # 1★→0, 2★→0,5, 3★→1
W = clamp01( 1 − (t_wait − 20) / 40 )            # t_wait: giây thực từ lúc ngồi/xếp hàng đến khi nhận ly
                                                 # ≤20 s → 1,0 ; ≥60 s → 0 ; >60 s → khách bỏ đi
C = CI_zone / 100                                # chỉ số sạch zone khách ngồi (§5)
F = clamp01( (ΣComfort_zone + ΣStyle_zone) / F_ref_tier )
    F_ref: T1 = 20, T2 = 200, T3 = 400 cho sảnh chính và 150 cho mỗi khu phụ, §19 (mỗi zone; đo lại 2026-10-12: Σ cộng trên mọi món kể cả ghế/bàn — bố cục mặc định cấp 2 ~61 điểm, +8 ghế/3 bàn ~116, +bộ trang trí ~160 — nên 60 làm F = 1 ngay), T4 = n/a
```

- **T1 (vỉa hè, chưa có dọn bàn):** trọng số đổi thành `0,40·Q + 0,35·W + 0,10·C_ly + 0,15·F`, với `C_ly = 1` nếu ly được thu trong 30 s in-game, giảm tuyến tính về 0 sau 2 giờ in-game.
- **Bỏ đi (walk-away):** không có chỗ > 10 s, hoặc t_wait > 60 s, hoặc CI < 60 (xác suất §5). Walk-away = −2 RP, không doanh thu.
- `F_ref` tăng theo tier → lên tier mới, quán "trống trơn" → động lực build mode.

### 4.2 Điểm uy tín (RP) và ★

```
RP_khach = clamp( 1,5 × (S − 0,4) / 0,3 , −3, +3 )   # S=0,7 → +1,5 ; S=0,4 → 0 ; S=0,2 → −1
RP += Σ RP_khach + RP_bonus (nhiệm vụ, sự kiện, khen trên "mạng xã hội" in-game)
```

| ★ | RP cần | Điều kiện giữ (TB S 3 ngày gần nhất) | Mở | Thưởng |
|---|---|---|---|---|
| ★1 | 0 | — | — | — |
| ★2 | **300** | ≥ 0,55 | đủ điều kiện T2 | 50 Hạt |
| ★3 | **4.000** | ≥ 0,65 | đủ điều kiện T3 | 100 Hạt |
| ★4 | **40.000** | ≥ 0,72 | đủ điều kiện T4 | 200 Hạt |
| ★5 | **120.000** | ≥ 0,80 | danh hiệu, khách du lịch nước ngoài | 400 Hạt |

- RP **không bao giờ giảm dưới ngưỡng ★ đã đạt** (sàn mềm). Nếu TB S < điều kiện giữ: ★ hiển thị "đang lung lay", Demand −10%, nhưng **tier đã mở không bị thu hồi** (cozy).
- Demand +9 khách/ngày/★ (×1,5 / ×2,6 theo tier) → ★ là đòn bẩy doanh thu, không chỉ là cổng.

---

## 5. Độ sạch (Cleanliness Index — CI)

Áp dụng từ T2. Mỗi zone có CI ∈ [0,100].

```
CI = 100 − 40·(bàn_bẩn / tổng_bàn) − 0,4·FloorDirt − 10·[thùng rác đầy]
FloorDirt += 0,6 mỗi khách phục vụ trong zone (+1,5 khi trời mưa / sự kiện Mùa mưa)
FloorDirt −= theo lau sàn (NV dọn: 8 điểm / lần, robot hút bụi T3: 1,5 điểm / giờ in-game)
Bàn bẩn: khi khách rời đi → bàn bị CHẶN (không xếp khách) đến khi dọn.
Bàn bẩn > 2 giờ in-game (~26 s thực): thêm −5 CI mỗi bàn (ruồi/kiến hiệu ứng).
```

| CI | Hậu quả |
|---|---|
| 80–100 | không phạt (C vẫn tính vào S) |
| 60–79 | chỉ giảm qua C trong S |
| 40–59 | walk-away +15% mỗi 10 điểm dưới 60; tip ×0,5 |
| < 40 | như trên + −1 RP mỗi giờ in-game; khách bình luận xấu (UI bubble) |

**Tốc độ dọn bàn**

| Ai dọn | Thời gian/bàn (s thực) | Giá nâng cấp (Xu) |
|---|---|---|
| Người chơi (giữ chạm) | 1,2 | — |
| NV dọn Lv1 | 6,0 | (tuyển 1.500) |
| Lv2 | 5,0 | 1.000 |
| Lv3 | 4,0 | 2.500 |
| Lv4 | 3,0 | 6.250 |
| Lv5 | 2,2 | 15.625 (yêu cầu T3) |
| Tốc độ di chuyển NV (giày tốt) Lv1–3 | +0 / +15% / +30% | 1.500 / 6.000 |
| Khay to (dọn 2 bàn/lượt) | — | 8.000 |
| Robot hút bụi (T3) | lau sàn tự động | 20.000 |

Giá nâng cấp NV dọn ở bảng trên là giá của công thức chung cho mọi vai trò ở §6 (không nhân TierScale), không còn là dải 2.000 / 5.000 / 12.000 / 30.000 của v0.1.

Mục tiêu cảm giác: T2 không có NV dọn → CI rơi xuống ~60 sau ~1 giờ in-game cao điểm nếu người chơi bận pha → **nhu cầu thuê NV đầu tiên rõ ràng** trong ~2 ngày đầu T2.

---

## 6. Nhân viên & lương

| Vai trò | Tier | Lương (Xu/ngày) | Phí tuyển | Kỹ năng (Lv1→5) |
|---|---|---|---|---|
| Phục vụ | T2 | 350 | 1.500 | tốc độ order/mang ly |
| Dọn dẹp | T2 | 300 | 1.500 | §5 |
| Pha chế | T3 (v0.1: T2) | 450 | 2.500 | Lv3+ cho 3★ không tốn thêm thời gian |
| Thu ngân | T3 | 400 | 3.000 | giảm hàng chờ thanh toán |
| Trưởng ca | T3 | 800 | 6.000 | +10% tốc độ cả ca, giảm 50% sự cố |
| Quản lý chi nhánh | T4 | 2.500 | 25.000 | hiệu suất CN 45% → 70% |

- Lương T3 ×1,2 cho ca tối (chưa có ca làm nên chưa áp dụng).
- Lương trả cuối ngày; nếu thiếu Xu, NV vẫn làm (ghi nợ như tiền thuê) — không có cơ chế NV bỏ việc gây stress.
- **Nâng cấp kỹ năng (mọi vai trò):** `Cost(Lv) = 1.000 × 2,5^(Lv−1)`, Lv là cấp hiện tại trước khi nâng, **không nhân TierScale** (`GameBalance.StaffUpgradeCost`). v0.1 ghi thêm `× TierScale` (×12 ở T3); bỏ theo quyết định 2026-10-14 (lý do ở §19.6). TierScale vẫn áp cho thưởng nhiệm vụ / login / sự kiện (§1.1), chỉ không áp cho giá nâng cấp NV. Cấp 5 chỉ nâng được từ cấp 4 khi quán ở cấp 3.

| Nâng từ → lên | Chi phí (Xu) | Cộng dồn từ Lv1 |
|---|---|---|
| Lv1 → Lv2 | 1.000 | 1.000 |
| Lv2 → Lv3 | 2.500 | 3.500 |
| Lv3 → Lv4 | 6.250 | 9.750 |
| Lv4 → Lv5 | 15.625 | 25.375 |

- **Trần số người** (`GameBalance.StaffMaxCount(role, tier, openZones)`): T2 chỉ 1 Dọn dẹp + 1 Phục vụ (đo ở §17: người thứ hai và Pha chế không bù lương). T3 sảnh 3 Dọn dẹp + 2 Phục vụ + 3 Pha chế = 8 (trong dải 4–10 của §3.1). **Mỗi khu phụ đã mở nới thêm 1 chỗ cho MỖI vai trò** (`StaffSlotsPerZone = 1`, +3 người mỗi khu), chỉ ở T3:

| Cấu hình | Dọn dẹp | Phục vụ | Pha chế | Tổng | Lương tối đa (Xu/ngày) |
|---|---|---|---|---|---|
| T2 | 1 | 1 | 0 | 2 | 650 |
| T3, chỉ sảnh | 3 | 2 | 3 | 8 | 2.950 |
| T3 + 1 khu | 4 | 3 | 4 | 11 | 4.050 |
| T3 + 2 khu | 5 | 4 | 5 | 14 | 5.150 |
| T3 + đủ 3 khu | 6 | 5 | 6 | 17 | 6.250 |

Lương tối đa = thuê đủ trần, lương cơ bản của bảng trên, chưa tính ca tối ×1,2. Lý do nới trần theo khu và hệ quả đầu tư: §19.6.

---

## 7. Pacing mục tiêu

### 7.1 Mốc chính

| Mốc | Ngày in-game | Phút chơi tích lũy | Ngày thực (người chơi TB: 60' ngày 1, ~30'/ngày sau đó) |
|---|---|---|---|
| Bạc xỉu mở | 3 | ~10' | phiên 1 |
| ★2 | ~11 | ~40' | phiên 1–2 |
| **T2 Thuê mặt bằng** | **~14** | **~49'** | **phiên 1–2, ngày thực 1** |
| Khám phá công thức ẩn đầu tiên | 15–20 | ~55–70' | ngày 1–2 |
| ★3 | ~42 | ~2,5h | ngày thực ~4 |
| **T3 Mua nhà hàng** | **~72** | **~4,2h** | **ngày thực ~7** (dải 5–10) |
| ★4 | ~130 | ~7,6h | ngày thực ~13 |
| **T4 Chi nhánh 1** | **~177** | **~10,3h** | **ngày thực ~20 (tuần 3–4)** |

Kết quả từ mô phỏng §16 (người chơi "trung bình": chi 35% lãi ròng vào nội thất từ T2, xem ad tổng kết 35% số ngày, chất lượng TB 2,2★).

### 7.2 Toán học từng tier

**T1 (ngày 1–13):** khách TB ~20, giá TB ~79 × 1,05 (chất lượng) ≈ 83 Xu, nguyên liệu ~27 → biên ~56/khách → 20 × 56 = 1.120 + tip ~100 + ad ~130 + nhiệm vụ 150 − phí 60 ≈ **1.450–1.800 Xu/ngày**. Cọc 18.000 ÷ ~1.600 + mua ghế ~2.000 ≈ **12–14 ngày**. ★2 (300 RP) ở ~1,4 RP/khách × 22 khách ≈ 30 RP/ngày → **ngày ~11** (★ đến trước tiền một chút → người chơi thấy "chỉ còn thiếu tiền", động lực rõ).

**T2 (ngày 14–72):** 55–110 khách × (126 − 40) ≈ 4.700–9.500 Xu − thuê 1.000 − lương 700–1.050 − phí 150 → **lãi ~4.200–9.700/ngày** (tăng dần theo ★ và nội thất), người chơi chi ~35% cho nội thất → tiết kiệm ~2.700–6.300/ngày → 250.000 sau ~58 ngày → **ngày ~72**. ★3 (4.000 RP) ~ngày 42 → lại ★ trước tiền.

**T3 (ngày 72–177):** ~200–240 khách × 104 biên ≈ 22.000–26.000 + tip/ad − lương ~1.700 − phí 700 → **lãi ~25.000–31.000/ngày**, tiết kiệm ~20.000/ngày → 2.000.000 sau ~105 ngày → **ngày ~177**.

### 7.3 Bảng 30 ngày đầu (output mô phỏng §16, mặc định)

Cột: *Thu* = bán + tip + bonus ad; *Chi* = nguyên liệu + cố định (thuê/7, lương, phí); *Lãi ròng* gồm nhiệm vụ; *Xu cuối ngày* sau khi mua ghế/bàn/NV/nội thất (T2: −35% lãi cho nội thất).

| Ngày | Tier | ★ | RP | Khách | Chỗ ngồi | NV | Thu (Xu) | Chi (Xu) | Lãi ròng | Xu cuối ngày | Sự kiện |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1 | 1 | 22 | 16 | 6 | 0 | 1.477 | 444 | 1.183 | 608 |  |
| 2 | 1 | 1 | 49 | 19 | 7 | 0 | 1.754 | 516 | 1.388 | 1.347 |  |
| 3 | 1 | 1 | 75 | 19 | 7 | 0 | 1.927 | 582 | 1.494 | 2.842 |  |
| 4 | 1 | 1 | 103 | 20 | 7 | 0 | 2.028 | 610 | 1.568 | 4.410 |  |
| 5 | 1 | 1 | 131 | 20 | 7 | 0 | 2.028 | 610 | 1.568 | 5.979 |  |
| 6 | 1 | 1 | 161 | 21 | 7 | 0 | 2.129 | 637 | 1.642 | 7.621 |  |
| 7 | 1 | 1 | 190 | 21 | 7 | 0 | 2.129 | 637 | 1.642 | 9.264 |  |
| 8 | 1 | 1 | 221 | 22 | 7 | 0 | 2.231 | 665 | 1.716 | 10.980 |  |
| 9 | 1 | 1 | 252 | 22 | 8 | 0 | 2.231 | 665 | 1.716 | 11.971 |  |
| 10 | 1 | 1 | 284 | 23 | 8 | 0 | 2.332 | 692 | 1.790 | 13.762 |  |
| 11 | 1 | 2 | 316 | 23 | 8 | 0 | 2.332 | 692 | 1.790 | 15.552 |  |
| 12 | 1 | 2 | 361 | 32 | 10 | 0 | 3.245 | 940 | 2.455 | 16.333 |  |
| 13 | 1 | 2 | 407 | 33 | 10 | 0 | 3.347 | 967 | 2.529 | 18.862 |  |
| 14 | 2 | 2 | 455 | 34 | 8 | 0 | 3.448 | 995 | 2.603 | 3.466 | **Thuê mặt bằng** (−18.000 cọc) |
| 15 | 2 | 2 | 516 | 36 | 10 | 1 | 5.284 | 2.590 | 2.844 | 3.115 | Thuê NV #1 |
| 16 | 2 | 2 | 605 | 52 | 12 | 1 | 7.633 | 3.580 | 4.203 | 4.907 |  |
| 17 | 2 | 2 | 698 | 53 | 12 | 1 | 7.779 | 3.620 | 4.309 | 7.708 |  |
| 18 | 2 | 2 | 794 | 54 | 12 | 1 | 7.926 | 3.660 | 4.416 | 10.579 |  |
| 19 | 2 | 2 | 892 | 54 | 12 | 1 | 7.926 | 3.660 | 4.416 | 13.450 |  |
| 20 | 2 | 2 | 993 | 55 | 12 | 1 | 8.073 | 3.700 | 4.523 | 16.390 |  |
| 21 | 2 | 2 | 1098 | 56 | 12 | 1 | 8.220 | 3.740 | 4.630 | 19.400 |  |
| 22 | 2 | 2 | 1206 | 57 | 12 | 2 | 8.367 | 3.780 | 4.737 | 19.479 | Thuê NV #2 |
| 23 | 2 | 2 | 1317 | 57 | 12 | 2 | 8.367 | 4.130 | 4.387 | 22.331 |  |
| 24 | 2 | 2 | 1431 | 58 | 12 | 2 | 8.513 | 4.170 | 4.493 | 25.251 |  |
| 25 | 2 | 2 | 1549 | 59 | 12 | 2 | 8.660 | 4.210 | 4.600 | 28.242 |  |
| 26 | 2 | 2 | 1671 | 60 | 12 | 2 | 8.807 | 4.250 | 4.707 | 31.302 |  |
| 27 | 2 | 2 | 1795 | 60 | 12 | 2 | 8.807 | 4.250 | 4.707 | 34.362 |  |
| 28 | 2 | 2 | 1923 | 61 | 12 | 2 | 8.954 | 4.290 | 4.814 | 37.491 |  |
| 29 | 2 | 2 | 2055 | 62 | 12 | 2 | 9.100 | 4.330 | 4.920 | 40.689 |  |
| 30 | 2 | 2 | 2192 | 63 | 12 | 2 | 9.247 | 4.370 | 5.027 | 43.957 |  |

Nhận xét: ngày 14 Xu tụt về ~3.500 sau cọc (khoảnh khắc "đầu tư lớn" đầu tiên), ngày 15–16 thuê NV + bàn → lãi nhảy từ ~2.600 lên ~4.200/ngày. Ngày 30: ~44.000 Xu ≈ 18% giá nhà hàng → mục tiêu T3 đã "nhìn thấy được".

---

## 8. Thu nhập offline

### 8.1 Công thức

```
Δt_h      = min( thời_gian_vắng_mặt_đã_xác_thực (giờ thực), Cap_h )
Offline   = Σ_quán  Δt_h × NetDay7_quán × k_tier × StaffCoverage
          + Σ_CN    Δt_h × BranchDaily × k_branch

NetDay7       = lãi ròng TB 7 ngày in-game gần nhất (không gồm bonus ad/nhiệm vụ)
k_tier        = T1: 0 (không NV → đóng cửa) · T2: 0,25 · T3: 0,30   (= "ngày in-game tương đương / giờ thực")
StaffCoverage = min(1, số_NV / số_vai_trò_cần)   # T2 cần 2, T3 cần 4 (ít NV → offline ít)
BranchDaily   = 0,45..0,70 × NetDay7_quán_chính (theo Lv quản lý)
k_branch      = 0,50   # chi nhánh là trụ cột idle của T4
Cap_h         = 8h (mặc định) → 10h (50.000 Xu, ★3) → 12h (300.000 Xu, ★4)
```

| Ví dụ | NetDay7 | 8h offline | x2 bằng ad |
|---|---|---|---|
| T2, đủ 2 NV | 4.500 | 9.000 (≈ 2 ngày lãi) | 18.000 |
| T3, đủ NV | 28.000 | 67.200 | 134.400 |
| T4, quán chính + 2 CN (Lv quản lý 1) | 30.000 + 2×13.500 | 72.000 + 108.000 = 180.000 | 360.000 |

So sánh: 1 giờ chơi chủ động ≈ 17 ngày in-game ≈ 17× lãi ngày → offline 8h chỉ ≈ 12–15% của 1h chơi → **offline là quà "chào mừng quay lại", không thay thế chơi chủ động** (trừ T4 nơi idle là chủ đích).

### 8.2 Chống chỉnh giờ (anti clock-cheat)

1. **Nguồn thời gian chuẩn = server** (timestamp từ backend/Firebase lúc save & lúc resume). Không tin `DateTime.Now` của thiết bị.
2. Lưu kèm `elapsedRealtime`/monotonic clock lúc save; khi resume trong cùng boot → Δ = monotonic delta (không bị ảnh hưởng chỉnh giờ).
3. Nếu `Δ_device − Δ_server > 5 phút` hoặc giờ thiết bị lùi → dùng `Δ_server`, log event `clock_anomaly`.
4. **Không có mạng:** offline tối đa **2h** được trả ngay; phần còn lại "giữ chờ" (pending) cho đến khi xác thực với server.
5. Offline chỉ tính từ **lần save cuối đã đồng bộ**; không cộng dồn nhiều lần claim trong cùng khoảng.
6. Remote Config `offline_trust_mode` (strict / lenient) để nới lỏng ở thị trường mạng kém.

---

## 9. Rewarded Ads (chỉ rewarded, không interstitial)

| Vị trí | Phần thưởng | Cap/ngày thực | Mở từ | Ghi chú UX |
|---|---|---|---|---|
| Tổng kết ngày | +30% doanh thu ngày đó | 5 | ngày in-game 2 | nút phụ, nút chính là "Tiếp tục" |
| Nhận offline | ×2 offline | 3 (mỗi lần claim 1) | T2 | |
| Rương Hạt (quầy) | 3 Hạt | 3 | ngày 3 | |
| "Giờ cao điểm" | +50% Demand trong 2 ngày in-game | 2 | T1 ngày 5 | không chồng với sự kiện boost |
| Xe hàng sỉ | −50% nguyên liệu trong 1 ngày | 2 | T2 | |
| Đổi nhiệm vụ ngày | reroll 1 nhiệm vụ | 2 | ngày 2 | |
| Khách VIP bất ngờ | đơn ×3 giá + 30 RP | 2 | T2 | xuất hiện ngẫu nhiên, có thể bỏ qua |
| Token sự kiện | +50% token 1 ngày | 2 | trong sự kiện | |
| **Tổng cap** | | **~19** (thực tế người chơi xem 3–6) | | `ads_global_daily_cap = 20` |

**Luật:**
- Không có ad trong 10 phút đầu (FTUE), ngoại trừ người chơi tự bấm.
- Cooldown tối thiểu 90 s giữa 2 ad.
- Ad fail/không có fill → vẫn thưởng 50% sau lần thứ 2 thất bại liên tiếp (giữ niềm tin).
- Mua gói **"Không quảng cáo"** → mọi placement thành nút "Nhận ngay" (vẫn giữ **cùng cap**), để không thành pay-to-win.

---

## 10. IAP Catalog

Bảng giá VND theo tier giá store tại Việt Nam (tham chiếu; map lại theo price tier chính thức của Google Play / App Store khi lên store).

| SKU | USD | VND | Nội dung | Giới hạn |
|---|---|---|---|---|
| `starter_pack` | 1,99 | 49.000 | 300 Hạt + bộ "Ghế nhựa xanh dương hoài niệm" (6 món) + khung avatar + 3.000 Xu | 1 lần, hiện từ ngày in-game 3 đến hết 72h thực |
| `remove_ads` | 4,99 | 119.000 | Nhận thưởng ad không cần xem (cùng cap) + 300 Hạt + biển hiệu "Quán quen" | 1 lần |
| `hat_xs` | 0,99 | 25.000 | 80 Hạt | |
| `hat_s` | 4,99 | 119.000 | 450 Hạt (+12%) | |
| `hat_m` | 9,99 | 249.000 | 1.000 Hạt (+25%) | |
| `hat_l` | 19,99 | 499.000 | 2.200 Hạt (+37%) | |
| `hat_xl` | 49,99 | 1.199.000 | 6.000 Hạt (+50%) | |
| `set_hoian` | 4,99 | 119.000 | Bộ Hội An (đèn lồng, bàn gỗ, mái ngói) 10 món | cũng mua được bằng 1.500 Hạt |
| `set_dalat` | 4,99 | 119.000 | Bộ Đà Lạt vintage (lò sưởi giả, ghế mây, hoa) 10 món | 1.500 Hạt |
| `set_saigonxua` | 7,99 | 199.000 | Bộ Sài Gòn xưa (quạt trần, gạch bông, radio) 12 món | 2.000 Hạt |
| `set_indochine` | 9,99 | 249.000 | Bộ Indochine cao cấp T3 (14 món + trang phục NV) | 2.400 Hạt |
| `event_pass` | 4,99 | 119.000 | Premium track sự kiện (§11.3) | mỗi sự kiện; 900 Hạt thay thế |
| `event_pass_plus` | 9,99 | 249.000 | Premium track + nhảy 10 bậc + 1 món độc quyền thêm | mỗi sự kiện |

### 10.1 Luật công bằng (fairness rules)

1. **Xu trong mọi gói ≤ 1 ngày lãi ròng của tier hiện tại** (starter pack 3.000 Xu ≈ 2 ngày T1 — ngoại lệ duy nhất, có chủ đích).
2. Đồ premium: Comfort ≤ đồ Xu tốt nhất cùng tier; chỉ hơn về **Style (+20–40%)**, và Style bị giới hạn bởi `F = clamp01(...)` nên người chơi F2P cũng chạm trần F bằng đồ Xu (chỉ cần nhiều món hơn).
3. Mọi bộ nội thất IAP đều có giá **Hạt** tương đương → F2P tiết kiệm ~2 tháng là có.
4. Không loot box trả tiền; mọi gói hiện rõ nội dung. Không bán theo "đếm ngược gây áp lực" ngoài starter pack.
5. Không popup mua hàng khi người chơi vừa thất bại/đang bận; tối đa 1 offer tự bật mỗi phiên, không trong 3 phiên đầu.
6. Hỗ trợ khóa mua hàng/giới hạn chi tiêu cho người dùng dưới 18 theo chính sách store.

---

## 11. LiveOps

### 11.1 Nhiệm vụ ngày (reset 04:00 giờ VN)

3 nhiệm vụ / ngày, chọn ngẫu nhiên từ pool theo tier. Hoàn thành mỗi nhiệm vụ: Xu; đủ 3/3: rương 10 Hạt + 20 RP.

| Nhiệm vụ mẫu | Tier | Thưởng Xu (× TierScale) |
|---|---|---|
| Phục vụ 30 khách | T1+ | 50 |
| Bán 10 ly cà phê sữa | T1+ | 50 |
| Thu 20 ly trong 10 giây | T1 | 50 |
| Pha 5 ly 3★ | T2+ | 80 |
| Giữ CI ≥ 80 cả ngày | T2+ | 80 |
| Bán 3 món ẩn | T2+ | 100 |
| Không để khách nào bỏ đi trong 1 ngày | T1+ | 80 |
| Đặt 2 món nội thất mới | T2+ | 50 |

Nhiệm vụ tuần (7 nhiệm vụ): thưởng 1 món trang trí độc quyền tuần + 40 Hạt.

### 11.2 Login 7 ngày (lặp chu kỳ, không reset nếu lỡ — chỉ tạm dừng)

| Ngày | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---|---|---|---|---|---|---|
| Thưởng | 300 Xu × TierScale | 5 Hạt | Chậu cây trầu bà | 10 Hạt | Phiếu "Xe hàng sỉ" (−50% NL 1 ngày) | 15 Hạt | 30 Hạt + món độc quyền tuần (vd: Đèn ông sao) |

### 11.3 Sự kiện mùa

Cấu trúc chung: **21 ngày thực**, token sự kiện kiếm từ khách đặc biệt & món theo mùa; **30 bậc**, mỗi bậc 100 token → cần 3.000 token. F2P kiếm ~140–160 token/ngày → hoàn thành track free nếu chơi ~19/21 ngày (premium +20% token).

| Sự kiện | Thời điểm | Token | Món theo mùa | Cơ chế đặc biệt |
|---|---|---|---|---|
| **Tết** | 2 tuần trước → 1 tuần sau Mùng 1 | Bao lì xì | Trà mứt gừng, cà phê hạt dưa | Khách "xông đất" ngày đầu (đơn ×5), mâm ngũ quả trang trí +Style |
| **Trung thu** | Rằm tháng 8 âm lịch ±10 ngày | Đèn lồng | Trà sen + bánh trung thu, cà phê cốm | Đêm rước đèn: tối Demand +40% |
| **Mùa mưa** | Tháng 6–8 dương | Giọt mưa | Ca cao nóng, cà phê gừng | Mưa rào ngẫu nhiên: khách trú mưa (+Demand), sàn bẩn ×2.5 → kiểm tra hệ thống dọn |

| Bậc | Track Free | Track Premium |
|---|---|---|
| 1–5 | Xu, 10 Hạt, công thức theo mùa (miễn phí cho tất cả) | +1 món trang trí sự kiện, 30 Hạt |
| 6–10 | 1 món trang trí, nhân vật khách đặc biệt | trang phục NV sự kiện |
| 11–20 | Xu, 40 Hạt, 2 món trang trí | 100 Hạt, 3 món, biển hiệu sự kiện |
| 21–29 | 60 Hạt, khung avatar | bộ tường/sàn sự kiện, 150 Hạt |
| 30 | **Món trung tâm free** (vd: cây mai nhỏ) | **Món trung tâm premium** (vd: cổng hoa Tết lớn) + 200 Hạt |
| Tổng Hạt | ~150–250 | thêm ~480 → premium "hoàn lại" > 50% giá nếu quy đổi Hạt |

Công thức theo mùa **luôn ở track free** (gameplay không bị khóa sau tiền). Đồ sự kiện quay lại sau 1 năm trong "Chợ đồ cũ" (giá Hạt) → giảm FOMO.

---

## 12. KPI mục tiêu

| KPI | Mục tiêu launch | Tốt (benchmark cozy/tycoon) |
|---|---|---|
| D1 retention | ≥ 38% | 45% |
| D7 retention | ≥ 14% | 20% |
| D30 retention | ≥ 5% | 8% |
| Session length TB | 8–12 phút (≈ 2–3 ngày in-game) | |
| Sessions/DAU | 3–4 | |
| Playtime/DAU | 25–35 phút | |
| % đạt T2 trong phiên 1 | ≥ 55% | 70% |
| % đạt T3 trước D10 (trong người còn chơi D10) | ≥ 50% | |
| Ad views/DAU | 3,5–5 | 6 |
| % DAU xem ≥1 ad | 45–60% | |
| Payer conversion (D30 cumulative) | 1,5–2,5% | 3% |
| Starter pack attach (trong payers) | ≥ 40% | |
| ARPDAU (blended, VN/SEA) | 0,03–0,06 USD | 0,08 |
| IAP : Ad revenue | ~40 : 60 | |

---

## 13. Analytics events

Tham số chung trên mọi event: `user_id, session_id, tier, stars, rp, xu_balance, hat_balance, game_day, build_version, rc_variant`.

| Nhóm | Event | Tham số chính |
|---|---|---|
| Vòng đời | `session_start` / `session_end` | duration_s, days_played_in_session |
| FTUE | `tutorial_step` | step_id, time_since_install_s |
| Tiến trình | `day_end` | customers, walkaways, revenue, tips, costs, net, avg_S, avg_wait, avg_CI |
| | `tier_unlocked` | tier, game_day, minutes_played_total |
| | `star_up` / `star_shaky` | new_star, rp |
| | `recipe_discovered` | recipe_id, attempts, used_hint |
| | `drink_served` (sampled 10%) | drink_id, quality, wait_s, price, tip |
| Kinh tế | `currency_earn` / `currency_spend` | currency, amount, source/sink_id |
| | `item_purchased` | item_id, currency, price, category |
| | `staff_hired` / `staff_upgraded` | role, level, cost |
| | `rent_paid` / `debt_started` / `debt_cleared` | amount |
| Offline | `offline_claimed` | hours_raw, hours_capped, amount, doubled |
| | `clock_anomaly` | device_delta, server_delta |
| Ads | `ad_offer_shown` / `ad_started` / `ad_completed` / `ad_failed` | placement, reward, network, ecpm_bucket |
| IAP | `iap_offer_shown` / `iap_initiated` / `iap_completed` / `iap_failed` | sku, price_local, currency, trigger |
| LiveOps | `mission_completed`, `login_reward_claimed`, `event_tier_reached`, `event_pass_bought` | ids, tier |
| Build mode | `build_mode_enter` / `build_layout_saved` | items_placed, comfort, style |

Funnel cần dashboard: Install → T1 ngày 3 → ★2 → T2 → công thức ẩn đầu tiên → ★3 → T3 → T4.

---

## 14. Rủi ro cân bằng & cách theo dõi

| Rủi ro | Dấu hiệu | Núm chỉnh |
|---|---|---|
| T2 quá chậm (bỏ game phiên 1) | % đạt T2 phiên 1 < 50% | `t2_deposit`, `demand_base`, `rp_star_thresholds[1]` |
| T2 → T3 "grind" (giữa D3–D7 rơi) | D7 < 14%, tier_unlocked T3 median > ngày thực 10 | `t3_price`, giá máy móc, sự kiện bonus |
| Lạm phát Xu T3 (không còn gì để mua) | xu_balance median > 3× giá món đắt nhất | thêm zone/máy, `branch_cost` |
| Ad quá mạnh | xu từ ad > 20% tổng Xu | `ad_day_bonus_frac`, caps |
| Hạt dư thừa F2P | hat_balance median > 2.000 ở D30 | giảm rương ad, thêm sink cosmetic |

---

## 15. Tunables bắt buộc trên Remote Config

| Key | Mặc định | Ghi chú |
|---|---|---|
| `day_length_sec` | 210 | |
| `start_xu` | 500 | |
| `drink_table` (JSON) | §2.1 | giá, NL, prep_s, tier mở |
| `quality_price_mult` | [0.85, 1.0, 1.2] | |
| `quality_tip_chance` / `quality_tip_size` | [0.05,0.25,0.5] / [0.05,0.10,0.15] | |
| `demand_base` / `demand_per_star` / `demand_per_day` | 18 / 9 / 0.5 | |
| `demand_tier_mult` | [1, 1.5, 2.6, 2.6] | |
| `turnover_per_seat` | [4, 4.5, 5] | |
| `staff_capacity_bonus` | [0, 0.25, 0.15] | |
| `t1_stool_cost_base` / `_step` | 200 / 75 | |
| `t2_deposit` / `t2_rent_week` / `t2_star_req` | 18000 / 7000 / 2 | |
| `t2_table_cost_base` / `_step` | 700 / 120 | |
| `t3_price` / `t3_star_req` / `zone_costs` | 250000 / 3 / [40k, 90k, 180k] | |
| `branch_cost_base` / `branch_cost_growth` / `t4_star_req` | 2000000 / 1.6 / 4 | |
| `staff_wages` (JSON) | §6 | |
| `upkeep_per_day` | [60, 150, 700] | |
| `sat_weights` / `sat_weights_t1` | [0.35,0.30,0.20,0.15] / [0.40,0.35,0.10,0.15] | |
| `wait_ok_s` / `wait_max_s` | 20 / 60 | |
| `f_ref_per_tier` | [20, 200, 400] | |
| `rp_star_thresholds` | [0, 300, 4000, 40000, 120000] | |
| `rp_per_customer_scale` | 1.5 | |
| `star_hold_avg_s` | [0.55, 0.65, 0.72, 0.80] | |
| `ci_weights` (dirty_table, floor, bin) | 40 / 0.4 / 10 | |
| `floor_dirt_per_customer` / `rain_mult` | 0.6 / 2.5 | |
| `clean_time_by_level` | [6, 5, 4, 3, 2.2] | |
| `offline_k_tier` / `offline_k_branch` | [0, 0.25, 0.30] / 0.5 | |
| `offline_cap_hours` | [8, 10, 12] | |
| `offline_unverified_max_h` / `offline_trust_mode` | 2 / strict | |
| `ad_caps` (JSON theo placement) / `ads_global_daily_cap` | §9 / 20 | |
| `ad_day_bonus_frac` / `ad_cooldown_s` / `ad_ftue_block_min` | 0.30 / 90 / 10 | |
| `mission_pool` / `mission_rewards` / `tier_scale` | JSON / JSON / [1,4,12,30] | |
| `login_rewards` (JSON) | §11.2 | |
| `event_config` (ngày, token/bậc, rewards) | §11.3 | |
| `iap_offers_enabled` / `starter_pack_window_h` | true / 72 | |
| `rent_debt_repay_frac` | 0.30 | |

---

## 16. Mô phỏng 30 ngày (Python, chuyển sang `tools/economy_sim.py` sau)

Mô phỏng tất định cho một "người chơi trung bình": mua ghế/bàn tham lam khi Capacity < Demand, chuyển tier ngay khi đủ ★ + Xu, từ T2 chi 35% lãi ròng cho nội thất (tăng S). Chạy: `python economy_sim.py` → in bảng 30 ngày và ngày đạt T2/T3/T4. Tham số trong `P` khớp với các key Remote Config §15 để có thể đọc thẳng từ file JSON export.

```python
"""economy_sim.py - mo phong 30 ngay dau cua Quan ca phe Viet (deterministic, 'average player')."""

P = {
    "days": 30,
    "start_xu": 500,
    # Tier 1 - Via he
    "t1_menu": {  # ten: (gia ban, gia nguyen lieu, ti le goi mon, ngay mo khoa)
        "tra_da":    (30, 5, 0.25, 1),
        "cf_den":    (80, 25, 0.35, 1),
        "cf_sua":    (100, 35, 0.40, 1),
        "bac_xiu":   (120, 45, 0.20, 3),
    },
    "t1_seats_start": 4, "t1_seats_max": 12, "t1_turnover": 4,       # khach/ghe/ngay
    "t1_stool_cost": lambda n: 200 + 75 * n,                          # n = so ghe hien co
    "t1_upkeep": 60,                                                   # phi "ve sinh via he"/ngay
    # Tier 2 - Thue mat bang
    "t2_deposit": 18000, "t2_rent_week": 7000, "t2_star_req": 2,
    "t2_seats_start": 8, "t2_seats_max": 24, "t2_turnover": 4.5,
    "t2_table_cost": lambda n: 700 + 120 * (n - 8),                   # 1 ban + 2 ghe = +2 cho
    "t2_avg_price": 120, "t2_avg_cost": 40,                            # gom cong thuc an
    "t2_staff_wage": 350, "t2_staff_cost": 1500,                      # luong/ngay, phi tuyen
    "t2_upkeep": 150,
    "decor_spend_frac": 0.35,     # % loi nhuan rong ngay tier>=2 nguoi choi chi cho noi that/may moc
    "decor_sat_per_1k": 0.004, "sat_cap": 0.86,
    # Tier 3 - Mua nha hang
    "t3_price": 250000, "t3_star_req": 3,
    "t3_seats": 30, "t3_turnover": 5.0, "t3_staff": 4, "t3_staff_wage": 420,
    "t3_avg_price": 150, "t3_avg_cost": 46, "t3_upkeep": 700, "t3_demand_mult": 2.6,
    # Tier 4 - Chuoi (chi mo phong moc dat duoc)
    "t4_branch_cost": 2000000, "t4_star_req": 4,
    # Chung
    "demand_base": 18, "demand_per_star": 9, "demand_per_day": 0.5,
    "quality_mult": 1.05,   # TB nguoi choi ~2.2 sao cong thuc
    "tip_rate": 0.06,
    "mission_xu_per_day": 150,
    "ad_double_day_rate": 0.35,   # ti le ngay nguoi choi xem ad "Doanh thu x1.3" o man tong ket
    "ad_bonus_frac": 0.30,        # ad tong ket cong +30% doanh thu ngay
    "rp_per_customer": 3.0, "sat_t1": 0.68, "sat_t2": 0.74,
    "star_thresholds": [0, 300, 4000, 40000, 120000],   # ★1..★5 (RP)
    "reserve": 300,
}

def stars(rp, th):
    s = 1
    for i, t in enumerate(th):
        if rp >= t:
            s = i + 1
    return s

def simulate(p=P, verbose=True):
    xu, rp, tier = p["start_xu"], 0.0, 1
    seats, staff = p["t1_seats_start"], 0
    sat_bonus = 0.0
    rows = []
    for d in range(1, p["days"] + 1):
        st = stars(rp, p["star_thresholds"])
        demand = p["demand_base"] + p["demand_per_star"] * (st - 1) + p["demand_per_day"] * d
        if tier == 1:
            menu = [(pr, c, w) for pr, c, w, u in p["t1_menu"].values() if d >= u]
            tw = sum(w for _, _, w in menu)
            avg_p = sum(pr * w for pr, _, w in menu) / tw
            avg_c = sum(c * w for _, c, w in menu) / tw
            cap = seats * p["t1_turnover"]
            cust = int(min(demand, cap))
            fixed = p["t1_upkeep"]
            sat = p["sat_t1"]
        elif tier >= 3:
            avg_p, avg_c = p["t3_avg_price"], p["t3_avg_cost"]
            cap = p["t3_seats"] * p["t3_turnover"] * (1 + 0.15 * p["t3_staff"])
            cust = int(min(demand * p["t3_demand_mult"], cap))
            fixed = p["t3_upkeep"] + p["t3_staff"] * p["t3_staff_wage"]
        else:
            avg_p, avg_c = p["t2_avg_price"], p["t2_avg_cost"]
            cap = seats * p["t2_turnover"] * (1 + 0.25 * staff)
            cust = int(min(demand * 1.5, cap))
            fixed = p["t2_upkeep"] + staff * p["t2_staff_wage"] + p["t2_rent_week"] / 7
        revenue = cust * avg_p * p["quality_mult"]
        tips = revenue * p["tip_rate"]
        ingredients = cust * avg_c
        ad = revenue * p["ad_bonus_frac"] * p["ad_double_day_rate"]
        net = revenue + tips + ad - ingredients - fixed + p["mission_xu_per_day"]
        xu += net
        if tier >= 2:
            sat = min(p["sat_cap"], p["sat_t2"] + sat_bonus)
            if net > 0:
                spend = net * p["decor_spend_frac"]
                xu -= spend
                sat_bonus += spend / 1000 * p["decor_sat_per_1k"]
        rp += cust * p["rp_per_customer"] * (sat - 0.4) / 0.3 * 0.5
        event = ""
        # chuyen tier
        if tier == 1 and stars(rp, p["star_thresholds"]) >= p["t2_star_req"] \
                and xu >= p["t2_deposit"] + p["t2_rent_week"] / 7:
            xu -= p["t2_deposit"]
            tier, seats = 2, p["t2_seats_start"]
            event = "THUE MAT BANG"
        if tier == 2 and stars(rp, p["star_thresholds"]) >= p["t3_star_req"] and xu >= p["t3_price"]:
            xu -= p["t3_price"]; tier = 3; event = "MUA NHA HANG"
        elif tier == 3 and stars(rp, p["star_thresholds"]) >= p["t4_star_req"] and xu >= p["t4_branch_cost"]:
            xu -= p["t4_branch_cost"]; tier = 4; event = "CHI NHANH 1"
        # dau tu (greedy)
        bought = 0
        if tier == 1:
            while seats < p["t1_seats_max"] and xu - p["t1_stool_cost"](seats) > p["reserve"] \
                    and seats * p["t1_turnover"] < demand + 6:
                xu -= p["t1_stool_cost"](seats); seats += 1; bought += 1
        elif tier == 2:
            if staff == 0 and xu > p["t2_staff_cost"] + 2000:
                xu -= p["t2_staff_cost"]; staff = 1; event += " +NV1"
            elif staff == 1 and xu > p["t2_staff_cost"] * 2 + 6000 and d >= 22:
                xu -= p["t2_staff_cost"] * 2; staff = 2; event += " +NV2"
            while seats < p["t2_seats_max"] and xu - p["t2_table_cost"](seats) > 3000 \
                    and cap < demand * 1.5 + 10:
                xu -= p["t2_table_cost"](seats); seats += 2; bought += 1
                cap = seats * p["t2_turnover"] * (1 + 0.25 * staff)
        rows.append((d, tier, stars(rp, p["star_thresholds"]), int(rp), cust, seats, staff,
                     int(revenue + tips + ad), int(ingredients + fixed), int(net), int(xu), event.strip()))
    if verbose:
        print("ngay|tier|sao|RP|khach|cho|NV|thu|chi|rong|Xu|su kien")
        for r in rows[:30]:
            print("|".join(str(x) for x in r))
    return rows

if __name__ == "__main__":
    rows = simulate()
    long = simulate(dict(P, days=260), verbose=False)
    for tier in (2, 3, 4):
        d = next((r[0] for r in long if r[1] >= tier), None)
        print(f"Tier {tier} dat o ngay in-game: {d}  (~{d*3.5/60:.1f}h choi)" if d else f"Tier {tier}: chua dat trong 260 ngay")
```

Output mặc định (rút gọn):

```
Tier 2 dat o ngay in-game: 14  (~0.8h choi)
Tier 3 dat o ngay in-game: 72  (~4.2h choi)
Tier 4 dat o ngay in-game: 177  (~10.3h choi)
```

**Giới hạn của mô phỏng:** không mô phỏng từng khách (wait time, CI), dùng S trung bình; offline income, sự kiện và hoàn cọc T2 khi lên T3 không được tính → pacing thực tế sẽ **nhanh hơn ~5–10%** với người chơi quay lại thường xuyên. Bước tiếp theo: Monte Carlo theo persona (casual 15'/ngày, mid 30', core 90') và bản agent-based theo từng khách cho T2.

---

## 17. Số liệu đo từ bản chạy thật (autoplay, cấp 2) — 2026-10-12

Bot autoplay `-autoplay -autobuy -autohidden -upgradestaff` chạy 10–12 ngày cấp 2 (có tiền thuê 7.000/tuần, 3 nhân viên). Các sửa lỗi đi kèm: số ly tăng theo ghế (`GameBalance.CupsFor`), Phục vụ cầm ly đi ngay, chủ quán không bỏ ly pha dở, nhân viên Dọn dẹp lau sàn.

| Chỉ số | Thiết kế (§3.4, §7) | Đo được |
|---|---|---|
| Khách/ngày (14–16 ghế) | 52–63 | ổn định 33–47, TB ~38 (Demand cấp 2 ~48–52 là trần) |
| Doanh thu/ngày | ~7.600 | 4.400–6.900 |
| Lãi/ngày sau lương, trước thuê | 4.200+ | 2.200–3.700 |
| Lãi tuần sau thuê 7.000 | ~14.700 | +12.700 đến +17.000 |
| Pha chế (lương 450/ngày) | bù lương bằng 3★ | chưa bù lương ở 14–16 ghế (cổ chai là chủ quán + dọn dẹp) |

Kết luận: cấp 2 có lãi và ổn định; mua nhà hàng 250.000 Xu mất khoảng 110–130 ngày in-game thay vì ~58. Núm chỉnh nên thử trước: Demand cấp 2 (hệ số 1,5), giá bán món ẩn, tiền thuê, giá cấp 3 (`t3_price`).

---

## 18. Số liệu đo từ bản chạy thật (autoplay, cấp 3) — 2026-10-13

Bot `-autoplay -autoupgrade3 -cheatt3` (lên cấp 2 rồi 3 bằng cheat Xu/RP, chơi 4–6 ngày ở nhà hàng; chi tiết cấu hình và lỗi đã sửa ở `phase3_spec.md` §0.6). Nhà hàng 10 × 12 m, mua nhà 232.000 Xu (đã trừ cọc hoàn), phí 700/ngày, 8 nhân viên (trần lúc đó, chưa nới theo khu; lương 2.950/ngày), máy xay + cold brew.

| Chỉ số | Thiết kế (§3.1, §7) | Đo được |
|---|---|---|
| Khách/ngày | 100 → 260 | 53–88 (ổn định ~80 khi ~26–47 ghế) |
| Quay vòng chỗ ngồi | 5 lượt/ngày | **~3,2 khách/ghế/ngày** (đi bộ + chờ ly + uống + chờ lau bàn so với cửa sổ sinh khách ~165 s) |
| Doanh thu/ngày | ~22.000–26.000 | 11.000–13.600 |
| Lãi ròng/ngày | 20.000–35.000 | **4.600–7.400** |
| Pha chế (lương 450) | T3 | ~4 ly/ngày/người khi tự đi giao; 8–13 ly/ngày/người sau khi thêm quầy giao |

Kết luận: cấp 3 chạy được và có lãi hơn cấp 2 khoảng 2–3 lần (~2.500 → ~5.000–7.000/ngày), nhưng **thấp hơn mục tiêu 3–4 lần**; cộng với cấp 2 ~2.500/ngày (§17) thì mua nhà hàng mất ~90–130 ngày và T3→T4 ~290 ngày. Hai núm có tác dụng lớn nhất vì chạm đúng nút thắt đo được: (1) **số khách mỗi ngày** — độ dài ngày cấp 3 hoặc tốc độ đi bộ / giao ly, hay Demand cấp 3; (2) **biên mỗi ly** — giá 3 món cấp 3 (200/230/300) và hệ số bán món ẩn. Giá nhà hàng 250.000 nên chỉnh sau khi chốt hai núm trên.

---

## 19. Ba khu của nhà hàng & ngày cấp 3 (2026-10-14)

Cập nhật theo bản đã chạy thật trong Unity (147 test EditMode đạt: 142 lúc thêm khu, 147 sau khi thêm trần nhân viên theo khu; chụp màn hình cả 3 khu mở được, bot mở khu không lỗi). Chi tiết kỹ thuật ở `phase3_spec.md` §0.8; mục này ghi các quyết định kinh tế, lý do và phát hiện đo. Mọi con số vẫn là giá trị khởi điểm. Các chỗ cần số đo mà chưa có được đánh dấu riêng để điền sau (gom lại ở §19.5; số đo mới nhất nằm ở `phase3_spec.md` §0.7 mục 6). Trần nhân viên theo khu và chi phí nâng cấp nhân viên: §19.6.

### 19.1 Giá & hiệu ứng từng khu

Nhà hàng cấp 3 là lưới 44 × 24 ô (ô 0,5 m) chia thành bốn khu; mọi ô thuộc đúng một khu. Khu chưa mở là bãi đất rào, không đi vào và không đặt đồ được. Ba khu phụ mở theo thứ tự tùy ý, giá không phụ thuộc thứ tự.

| Khu | Giá mở (Xu) | Diện tích | Nhu cầu | Giá ly & tip | F_ref | CI (phần bàn bẩn) |
|---|---|---|---|---|---|---|
| Sảnh chính | có sẵn | 10 × 12 m (20 × 24 ô) | — | ×1 | 400 | riêng |
| Sân vườn | 40.000 | 6 × 12 m (12 × 24 ô) | +20% | ×1 | 150 | riêng |
| Lầu (gác gỗ cùng mặt sàn) | 90.000 | 6 × 6 m (12 × 12 ô) | +20% | ×1 | 150 | riêng |
| VIP | 180.000 | 6 × 6 m (12 × 12 ô) | +20% | ×1,5 | 150 | riêng |

Tổng giá ba khu = 310.000 Xu, nhiều hơn cả giá mua nhà (250.000).

- **Nhu cầu.** `Demand_T3 = 2,6 × Demand_T1-formula × (1 + 0,2·n)`, n = số khu phụ đã mở (0–3). Cộng chứ không nhân dồn: 1 khu ×1,2 · 2 khu ×1,4 · 3 khu ×1,6. Đây là "zone bonus" của §3.2 (`GameBalance.ZoneDemandMult`); nhu cầu được cập nhật ngay khi mua khu.
- **VIP ×1,5.** Áp cho cả giá ly và tip của khách ngồi khu VIP; bằng đáy dải 1,5–2× của đơn đặc biệt (§1.1).
- **F_ref theo khu.** §4.1 ghi F_ref T3 = 400 "mỗi zone". Nay sảnh giữ 400, mỗi khu phụ (nhỏ hơn sảnh) dùng 150; khách tính tip và hài lòng theo điểm nội thất F của chính khu mình ngồi: `F = clamp01((ΣComfort_khu + ΣStyle_khu) / F_ref_khu)`.
- **CI theo khu.** Phần `40·(bàn_bẩn / tổng_bàn)` của §5 tính riêng từng khu; phần `0,4·FloorDirt` vẫn dùng chung vì lau sàn là việc cả quán. Khách tính tỉ lệ bỏ đi vì bẩn, tip và thành phần C của S theo CI khu mình ngồi. CI chung hiện trên HUD và phạt RP theo CI (§5) vẫn tính trên mọi bàn.
- **Khi mua.** Trừ Xu một lần, tặng 2 bộ bàn ghế (mỗi bộ = bàn + 2 ghế, dùng đồ trong kho trước), bake lại NavMesh ngay trong ngày. UI chạm hai lần (chọn rồi mua) để tránh bấm nhầm.

### 19.2 Vì sao ngày cấp 3 dài 300 giây

Từ cấp 3 một ngày in-game dài **300 giây thực** thay vì 210 (`GameBalance.DayRealSecondsFor(tier)`, hằng số `Tier3DayRealSeconds`; cấp 1–2 giữ 210). Giờ mở cửa vẫn 06:00–22:00 nên 1 giờ in-game ≈ 18,75 s thực (so với ≈ 13 s ở §0). `DayCycle` tự đổi độ dài khi lên cấp giữa ngày và giữ nguyên giờ đồng hồ.

**Lý do.** Công thức Capacity_T3 (§3.2) coi mỗi ghế quay 5 lượt/ngày, nhưng bot đo được thấp hơn nhiều (§18). Một lượt khách ~55 s (đi bộ ~10 s + chờ ly ~25 s + uống 8–15 s + chờ lau bàn ~10 s) tính bằng giây thực nên không đổi theo độ dài ngày, trong khi cửa sổ sinh khách chỉ ~165 s ở ngày 210 s. Ngày dài hơn thì cửa sổ sinh khách dài hơn, mỗi ghế quay được nhiều lượt hơn:

| | Ngày 210 s | Ngày 300 s |
|---|---|---|
| Quay vòng thực (khách/ghế/ngày) | ~3,2 (đo) | ~4,5 (ước) |
| Công thức §3.2 | 5 | 5 |

**Đo lại ở ngày 300 s (2026-10-14):** chỗ ngồi không còn là nút thắt nên quay vòng thực không đo tách bạch được: phục vụ/ghế chỉ ~2–3 vì nhân viên mới là giới hạn (mất khách vì hết chỗ còn ≤ 16/ngày ở sảnh 30–44 ghế, so với hết kiên nhẫn 8–22). Ước ~4,5 chưa kiểm chứng được; chỉ chắc chắn là hết chỗ không còn chiếm đa số mất khách.

**Hệ quả cần nhớ.**
- Chi phí cố định (phí vận hành 700, lương) tính theo ngày in-game nên ngày dài hơn có nhiều khách hơn để chia chi phí đó: lãi mỗi ngày tăng, còn lãi mỗi phút chơi cần so lại.
- Pacing (§7): cùng số ngày in-game, cấp 3 tốn nhiều phút chơi hơn (300 / 210 ≈ 1,43 lần mỗi ngày). Các mốc "phút chơi tích lũy" từ T3 trở đi ở §7.1 chưa tính lại.
- Remote Config: `day_length_sec` ở §15 hiện là một số duy nhất, cần tách theo cấp (§19.4).

### 19.3 Phát hiện đo được: nút thắt chuyển từ "hết chỗ" sang "hết kiên nhẫn"

Bot cấp 3, 8 nhân viên (trần lúc đó; nay đã nới theo khu, §19.6), chưa nâng cấp nhân viên trừ khi ghi khác. Mỗi dòng là một lượt chạy riêng nên là xu hướng, chưa phải số chốt.

| Cấu hình | Đo được |
|---|---|
| Ngày 300 s, sảnh 44–48 ghế | "Hết chỗ" = 0; mất khách chuyển hoàn toàn sang **"hết kiên nhẫn" ~28–35/ngày** |
| Mở đủ 3 khu (nhu cầu ×1,6, ~78 ghế), nhân viên còn cấp 1 | Quá tải: **mất 100–130 khách/ngày, lãi ~0** |
| Menu giá cao (giá TB 148 thay vì ~103) + nâng cấp nhân viên + sảnh 26 ghế | ~94 khách/ngày, doanh thu ~14.000, **lãi ~6.400–6.600/ngày** |

- **Nút thắt dời chỗ.** `phase3_spec.md` §0.6 ghi lý do mất khách chính là hết chỗ (khớp quay vòng ~3,2 ở §18). Với ngày 300 s và đủ ghế, "hết chỗ" về 0 và khách mất vì hết kiên nhẫn: nút thắt nay là **thông lượng Pha chế / Phục vụ**, không còn là chỗ ngồi. §3.2 mô tả hai nút thắt thay phiên (Capacity rồi Demand / ★); ở cấp 3 có thêm nút thắt thứ ba là nhân viên.
- **Mở khu mà nhân viên còn cấp 1 thì quá tải.** Nhu cầu ×1,6 cộng với ~78 ghế làm đơn dồn lại, mất 100–130 khách/ngày và lãi gần 0. Thứ tự đầu tư nên là nhân viên trước hoặc cùng lúc với khu mới (đề xuất; nên gợi ý trong UI / `DayAdvisor`).
- **Nâng cấp nhân viên là phần thưởng đầu tư chính của cấp 3.** `Cost(Lv) = 1.000 × 2,5^(Lv−1)`: Lv1→2 = 1.000, →3 = 2.500, →4 = 6.250, →5 = 15.625, tức 25.375 Xu để đưa một người lên Lv5; cả 8 người ở sảnh 203.000 Xu, đủ 17 người (đủ 3 khu) ≈ 431.000 Xu (so với 310.000 cho ba khu). Đã chốt (2026-10-14): **không nhân TierScale**, sửa §6 cho khớp với bản cài đặt; lý do và con số ở §19.6.
- **Lãi vẫn thấp hơn mục tiêu.** Dòng cuối (giá TB 148, sát ~150 ở §3.1) cho lãi 6.400–6.600/ngày, nằm trong dải 4.600–7.400 đo ở §18 (cấu hình khác) và thấp hơn dải 20.000–35.000 của §3.1 khoảng 3 lần. Vì mỗi lượt khác cấu hình, chưa tách được ảnh hưởng riêng của ngày 300 s, của khu và của nâng cấp nhân viên lên lãi.

### 19.4 Tunables mới (đưa vào §15 khi làm Remote Config)

| Key (đề xuất) | Mặc định | Hằng số trong code | Ghi chú |
|---|---|---|---|
| `day_length_sec` | [210, 210, 300] theo cấp | `DayRealSeconds`, `Tier3DayRealSeconds` | thay số đơn 210 ở §15 |
| `zone_costs` | [40000, 90000, 180000] | `ZoneGardenCost`, `ZoneTerraceCost`, `ZoneVipCost` | đã có ở §15; thứ tự sân vườn, lầu, VIP |
| `zone_demand_bonus` | 0.20 | `ZoneDemandBonus` | cộng cho mỗi khu phụ đã mở |
| `zone_vip_price_mult` | 1.5 | `VipPriceMult` | giá ly và tip của khách ngồi VIP |
| `f_ref_zone` | 150 | `FRefZone` | khu phụ; sảnh vẫn theo `f_ref_per_tier` (400) |
| `zone_starter_sets` | 2 | `ZoneStarterSets` | bộ bàn ghế tặng khi mở mỗi khu |
| `staff_slots_per_zone` | 1 | `StaffSlotsPerZone` | chỗ nhân viên thêm cho MỖI vai trò với mỗi khu phụ đã mở (chỉ cấp 3); trần cơ bản theo vai trò xem §6, §19.6 |

### 19.5 Số đo còn thiếu

Các chỗ này cần chạy bot rồi điền; chưa có thì không suy ra từ các lượt đo riêng lẻ ở §19.3.

1. **Đã đo (bot, 7 ngày, TB ngày 3–7, một lượt mỗi cấu hình).** Cờ bot: `-autoplay -autoupgrade3 -cheatt3 -autostaff -fullstaff -autobuy -bestmenu -autohidden -upgradestaff` (+ `-autozones` cho hai dòng dưới); menu 8 món giá TB 148 (dòng giữa 127 vì không mua máy cold brew); 18–22 lần nâng cấp nhân viên (Pha chế cấp 3, Dọn dẹp cấp 3–5, Phục vụ cấp 3); không có lỗi / exception. **Mỗi cấu hình chỉ một lượt** (ngày lẻ dao động ±2.000), và bot mua bàn ở sảnh trước nên **mỗi khu phụ chỉ có 4 ghế tặng**.

| Cấu hình (bot, 7 ngày, TB ngày 3–7, lãi in ra) | Nhân viên | Ghế cuối | Nhu cầu | Phục vụ/ngày | Mất: hết chỗ / hết kiên nhẫn | Doanh thu | Lãi ròng/ngày |
|---|---|---|---|---|---|---|---|
| Chỉ sảnh chính (2 lượt) | 8 | 44–45 | ×1,0 | 88–101 | 2–4 / 9–20 | 14.300–15.600 | **7.249 / 8.949** |
| Sân vườn + lầu (200.000 Xu dư; 2 lượt) | 14 | 38–44 | ×1,4 | 145–148 | 8–11 / 0 | 18.000–19.000 | **9.165 / 9.612** |
| Đủ 3 khu (500.000 Xu dư; 4 lượt) | 17 | 66–74 | ×1,6 | 174–178 | 0–3 / 0–1 | 28.400–28.600 | **17.240 / 18.197 / 18.532 / 18.145** |

   Lãi **~18.000/ngày** khi đủ 3 khu + 17 nhân viên (thấp hơn dải 20.000–35.000 của §3.1 một chút; ~16.000–16.500 nếu trừ lương bot trả thiếu); chỉ sảnh 8 người ~7.200–8.900. Phát hiện và sửa kèm theo (A/B `avoidancePriority`, sửa dựng lại cả đội): xem `phase3_spec.md` §0.8. Các núm còn lại (giá / biên 3 món cấp 3, Demand cấp 3, lương, giá nhà 250.000) có thể chỉnh sau; chưa cần vì cấu hình đủ khu đã gần dải mục tiêu.
2. **Hoàn vốn khu (ước tính thô).** Đủ 3 khu hơn chỉ sảnh ~9.000–10.000/ngày ⇒ ~31–35 ngày cho 310.000 Xu (~39–44 nếu trừ lương trả thiếu, chưa tính 9 người thêm và nâng cấp); sân vườn + lầu hơn chỉ sảnh 0–2.400/ngày (trong sai số, chưa kết luận). Khu phụ mới có 4 ghế tặng nên là cận dưới; cần đo lại khi khu có đủ bàn (gồm ×1,5 của VIP).
3. **Cấp nhân viên tối thiểu — không còn là vấn đề với trần theo khu.** Với 17 người (Pha chế cấp 3, Dọn dẹp cấp 3–5, Phục vụ cấp 3 sau ~7 ngày) mất khách vì hết kiên nhẫn về ~0; chưa cần chặn mở khu khi nhân viên yếu.
4. Quay vòng thực ở ngày 300 s: đã đánh dấu ở §19.2.
5. **Đo lại sau khi nới trần nhân viên theo khu** (đủ 3 khu, thuê đủ 17 người, nâng cấp nhân viên): số đo chờ điền nằm ở một chỗ duy nhất, `phase3_spec.md` §0.7 mục 6 (lãi ròng/ngày so với 8.760, khách mất vì hết kiên nhẫn so với ~39/ngày); điền xong thì cập nhật lại mục 1 và 3 ở trên.

### 19.6 Trần nhân viên theo khu & chi phí nâng cấp nhân viên (2026-10-14)

Hai quyết định đi cùng nhau vì cùng trả lời một câu hỏi: người chơi cấp 3 cần bao nhiêu nhân viên, mạnh đến đâu, để nhu cầu +20% mỗi khu (§19.1) không biến thành khách bỏ về. Đã chạy thật: 147 test EditMode đạt.

**Quyết định 1 — mỗi khu phụ đã mở nới thêm 1 chỗ cho MỖI vai trò.** `GameBalance.StaffMaxCount(role, tier, openZones)` với `StaffSlotsPerZone = 1`:

| Số khu phụ đã mở | Dọn dẹp | Phục vụ | Pha chế | Tổng |
|---|---|---|---|---|
| 0 (chỉ sảnh) | 3 | 2 | 3 | 8 |
| 1 | 4 | 3 | 4 | 11 |
| 2 | 5 | 4 | 5 | 14 |
| 3 | 6 | 5 | 6 | **17** |

- **Cấp 2 không đổi** (1 Dọn dẹp + 1 Phục vụ, không Pha chế; §6, §17). Số khu âm không làm giảm trần.
- **Lý do.** Bot đo ở trần cũ (8 người): mở đủ 3 khu (nhu cầu ×1,6) chỉ làm khách mất vì hết kiên nhẫn tăng từ ~13 lên ~39/ngày (§19.5 mục 1), vì thông lượng Pha chế / Phục vụ là giới hạn (§19.3). Nhu cầu tăng theo khu thì chỗ làm việc cũng phải tăng theo khu, nếu không khu mới chỉ thêm khách mất.
- **Trần ở UI và save.** `StaffRoster` nhận `zonesProvider` (số khu đã mở). Nạp save dùng trần lớn nhất (đủ 3 khu) nên không cắt bớt người đã thuê. Bảng "Đội ngũ quán" dựng sẵn số hàng lớn nhất và cuộn được (`ScrollRect`, panel cao tối đa 1.040 px), chân bảng nhắc "Mỗi khu mở thêm 1 chỗ cho mỗi vai trò".
- **Lương tăng theo.** Thuê đủ trần: 2.950 Xu/ngày (8 người) lên 4.050 / 5.150 / 6.250 (1 / 2 / 3 khu), tức đủ 17 người tốn thêm 3.300/ngày so với 8 người (bảng ở §6). Đã đo: doanh thu giữ lại thêm ~13.000/ngày (nguyên liệu +~3.000) vượt xa lương thêm +3.300, nên thuê đủ trần có lời khi mở đủ khu (bot trả thiếu lương vì tiêu hết Xu, xem §19.5).
- **Công thức Capacity_T3 (§3.2)** cộng 0,15 cho mỗi nhân viên đang có (`CapacityFor`): 8 người ×2,2; 17 người ×3,55. Trần nhân viên mới vì thế cũng nới trần sức chứa theo công thức. Chưa kiểm chứng đây có phải cận trên thực tế của số khách sinh ra.
- **Đã đo lại với trần mới:** ~18.000/ngày khi đủ 3 khu (§19.5 mục 1).

**Quyết định 2 — giữ nguyên giá nâng cấp trong code, không nhân TierScale.** `Cost(Lv) = 1.000 × 2,5^(Lv−1)` (Lv là cấp trước khi nâng; `GameBalance.StaffUpgradeCost`):

| Nâng | Xu | Cộng dồn từ Lv1 |
|---|---|---|
| Lv1 → 2 | 1.000 | 1.000 |
| Lv2 → 3 | 2.500 | 3.500 |
| Lv3 → 4 | 6.250 | 9.750 |
| Lv4 → 5 | 15.625 | **25.375** |

- **Lý do bỏ TierScale.** v0.1 ghi `× TierScale` (×12 ở T3), tức 25.375 × 12 ≈ 305.000 Xu cho một người lên Lv5. Lãi cấp 3 đo được chỉ ~7–9k/ngày (§19.5), nên con số đó bằng hơn 30 ngày in-game lãi (305.000 / 8.760 ≈ 35 ngày) cho riêng một người, còn nâng cả đội thì ngoài tầm: không khả thi. Quyết định theo yêu cầu "nới" của chủ dự án.
- **Quy mô đầu tư khi không nhân TierScale.** Một người Lv1→5 = 25.375 Xu; 8 người ở sảnh = 203.000; 17 người đều Lv5 ≈ **431.000** Xu (17 × 25.375 = 431.375), cùng bậc với giá ba khu (310.000, §19.1), bằng khoảng 1,4 lần. Vẫn là khoản đầu tư lớn, chỉ không còn nằm ngoài tầm với.
- **Cấp 5 vẫn chỉ nâng được từ cấp 4 khi quán ở cấp 3** (`StaffRoster.Upgrade`).
- **Phạm vi TierScale.** Vẫn áp cho thưởng nhiệm vụ / login / sự kiện (§1.1); chỉ không áp cho giá nâng cấp nhân viên. Giá cơ sở 1.000 và hệ số 2,5 hiện viết thẳng trong `StaffUpgradeCost`, chưa là tunable trong §19.4.
