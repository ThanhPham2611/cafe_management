# Hướng phát triển, kiếm tiền và lộ trình cho "Quán Cà Phê Việt"

## Context
Chủ game muốn biết: nên phát triển theo hướng nào, có kiếm tiền được không (hay phải đổi hệ thống), và cần lộ trình. Đã khảo sát code/tài liệu (2 agent Explore) và phân tích bằng agent game-designer + unity-game-developer. Điều kiện đã chốt với chủ game: **chưa rõ mục tiêu thu nhập, muốn thử thị trường; làm một mình bán thời gian (~10–20 giờ/tuần ≈ 2 ngày công/tuần); phát hành quốc tế (EN) ngay.**

Mọi con số doanh thu/CPI dưới đây là **ước tính từ kinh nghiệm thị trường, chưa đo**; chỉ số liệu soft-launch mới quyết định.

## 1. Kết luận ngắn

**Game đang ở đâu.** Game đơn, 3 cấp quán, vòng chơi chạm-vuốt-giữ có "tay", chất Việt thật (phin, cà phê muối, xe đẩy → nhà hàng), có bot cân bằng và ~330 test. Nhưng: **không có âm thanh, không analytics/crash, không ads/IAP, không cloud save, chỉ tiếng Việt, chưa từng build AAB/IPA hay chạy máy thật.** Phần kiếm tiền mới nằm trên giấy; gói IAP/Analytics chỉ được cài, chưa dùng; Hạt (gem) chỉ có một chỗ tiêu (gợi ý công thức 30 Hạt) nên bán Hạt hiện chưa mua được gì.

**Có kiếm tiền được không?** Được ở mức **thu nhập phụ**, chưa chắc là thu nhập chính. Không cần đổi hẳn hệ thống; nên **thu gọn** thiết kế F2P 8-vị-trí-quảng-cáo / 5 gói Hạt thành mô hình **"free-to-start hybrid"**:
- Free, không energy, không timer, không pay-to-win.
- 3 vị trí quảng cáo thưởng (opt-in): +30% doanh thu cuối ngày, rương Hạt, đơn VIP.
- 1 gói "Chủ Quán/Ủng hộ" (~$2.99–3.99: huy hiệu, bộ đồ độc quyền, 300 Hạt, slot bố cục thứ 2, tự nhận thưởng quảng cáo cùng giới hạn) + starter pack $0.99–1.99 + bộ nội thất định giá trực tiếp.
- Gói Hạt chỉ 2 mức ($0.99, $4.99), chỉ mở sau khi có ≥ 30 món cosmetic để tiêu Hạt.
- Event pass chỉ khi đã có mùa (giai đoạn P3). Hoãn: thu nhập offline (cần giờ server), 5 vị trí quảng cáo còn lại, subscription.

**Thực tế doanh thu (ước tính):** ARPDAU ~$0.015 / 0.025 / 0.045 (bi quan / cơ sở / lạc quan) ở VN/SEA; thị trường EN/iOS cao hơn (~$0.06–0.10) nhưng CPI cũng cao ($2–4) nên **mua người dùng trả phí gần như chắc lỗ** (LTV mỗi lượt cài chỉ ~$0.12–0.18 ở VN, ~$0.7–1.0 ở US/iOS). Tăng trưởng phải là **organic** (TikTok/Reels về cà phê Việt, cộng đồng, store featuring, mùa lễ). Ở 1k DAU ≈ $0.5–3k/tháng; 10k DAU ≈ $4.5–30k/tháng tuỳ thị trường. Nghĩa là: **đáng thử, nhưng đừng tính chuyện nghỉ việc trước khi có số liệu D7/ARPDAU thật.**

**Phương án dự phòng nếu soft-launch không đạt:** chuyển sang **premium một lần** ($4.99 mobile/ iOS + EN, $7.99–9.99 Steam) bỏ ads/gem, tier 4 là bản cập nhật miễn phí. Chỉ cân nhắc khi gate §4 không đạt; cần chiến dịch wishlist, không có đảm bảo doanh số.

## 2. Hướng phát triển (thứ tự ưu tiên)
**Đừng thêm hệ thống mới trước. Làm cho game "phát hành được → đo được → chỉnh theo dữ liệu".**

1. Âm thanh + haptics (game đang im lặng; đây là nâng cảm giác "cozy/đã tay" rẻ nhất).
2. Analytics + crash + lưu an toàn (không có thì mọi quyết định sau là đoán).
3. Lớp kiếm tiền tối thiểu (ads 3 vị trí + IAP + chỗ tiêu Hạt thật).
4. Tiếng Anh (vì chọn phát hành EN ngay) trước khi soft-launch.
5. Giữ chân giữa game: bức tường tiền cấp 2 (ngày 30–56), ngợp đơn cấp 3, ngày 70–130 lặp, khách quen hết truyện ở ngày ~25, sau ★4 chưa có nội dung (cấp 4 chưa làm).
6. Nội dung mùa (Tết/Trung thu/Mùa mưa), cấp 4 "chuỗi chi nhánh" **chỉ khi** dữ liệu cho thấy ≥ 5% đoàn người chơi tới ★4.

Lợi thế cạnh tranh nên khai thác: bản sắc văn hoá Việt rất ít đối thủ, vòng pha chế có "tay", cozy không ép nạp, chế độ ảnh/thẻ chia sẻ quán, sự kiện Tết/Trung thu.

## 3. Lộ trình (đã co lại cho 1 người bán thời gian, ~2 ngày công/tuần)

Ước lượng công (ngày công, 1 dev có kinh nghiệm): M1 ≈ 15–18, M2 ≈ 40–50, M3 ≈ 35–45. Với ~2 ngày công/tuần: **M1 ≈ 2 tháng, M2 ≈ 5 tháng nữa (tổng ~7 tháng tới soft-launch)**. Sự kiện Tết 2027 (≈6/2/2027, còn ~13 tuần) **không khả thi** với tốc độ này; nhắm Trung thu 2027 hoặc Tết 2028, còn Tết 2027 chỉ làm skin/cosmetic nhỏ nếu rảnh.

### P0 — Phát hành được & đo được (tuần 1–8, ≈ M1)
> **Tiến độ (2026-10-17):** đã xong *lưu game an toàn* (`SaveEnvelope`/`SaveStore`: ghi tmp→bak→main, SHA-256, đọc main→bak, cách ly file hỏng, khoá khi save của phiên bản mới hơn; 45 test) và *hệ thống âm thanh* (`Scripts/Audio/`: 25 SFX + 3 nhạc nền tạm sinh bằng `tools/gen_audio.py`, cài đặt nhạc/âm thanh/rung, rung Android/iOS chưa thử trên máy thật; xem `docs/audio_licenses.md`). Còn lại trong P0: tài khoản/ký ứng dụng, Firebase analytics + crash, đo máy thật, playtest.
- **Tuần 1 (việc lịch, không phải code):** tạo tài khoản Google Play Console + Apple Developer (ưu tiên tài khoản tổ chức; D-U-N-S mất thời gian), chốt bundle ID cuối cùng, kiểm tra luật Play hiện hành cho tài khoản cá nhân mới (closed test ~12 tester × 14 ngày), tên game chính thức (còn mở trong GDD), đăng ký chính sách quyền riêng tư (URL công khai).
- **Spike SDK 1–2 ngày (R1):** dự án trống trên Unity 6000.6.5f1 với Firebase + AdMob + IAP, build thử AAB và iOS archive. Nếu 6000.6 là bản tech-stream hoặc SDK không chạy, **chuyển sang bản LTS hiện hành trước khi làm tiếp**.
- Ký ứng dụng + build: keystore/Play App Signing, Apple team ID, icon thật (đã có nguồn `Assets/_Project/Art/UI/Logo/`), tắt splash nếu giấy phép cho phép, `BuildScript.Build(target)` qua `-executeMethod`, iPhone-only, gỡ package thừa (XR, vehicles, wind, cloth, terrain, video…).
- **Lưu an toàn:** ghi `.tmp` → `.bak` → move; bọc JSON bằng checksum; đọc thử main rồi `.bak`; hỏng thì đổi tên `.corrupt`, không tự ghi đè; save phiên bản mới hơn thì từ chối ghi đè; test golden-file v1–v3, file cụt, sai checksum, version tương lai. (`Core/SaveSystem.cs`; lưu ý `SaveSnapshot` hoàn Xu/RP về đầu ngày khi đang Open → mọi khoản thưởng Xu/RP mới phải qua `NotifyRewardGranted`.)
- **Firebase Analytics + Crashlytics** sau facade `IAnalytics` (Null impl khi chạy bot). ~20 event đầu từ `economy §13`; `AnalyticsBridge` đăng ký `GameEvents`, mọi handler bọc try/catch (chỉ `DayEnded`/`LevelUp` được bảo vệ sẵn; `MoneyChanged`/`CustomerServed` chạy trong `Customer.Pay`).
- Kiến trúc: static `Services` facade (reset bằng `RuntimeInitializeOnLoadMethod`), asmdef `Cafe.Services` tách SDK khỏi `Cafe.Runtime`, `PauseGate` đếm tham chiếu thay cho save/restore `timeScale` trong `StoryDialog`.
- **Âm thanh + haptics:** `Audio` MonoBehaviour tạo trong `BootLoader`, ~20–40 SFX, 2–3 nhạc nền, hook `GameEvents` + `ClayButton`, cài đặt nhạc/SFX, tắt tiếng khi xem quảng cáo. Nguồn: Kenney/Freesound CC0, Sonniss, hoặc thuê nhà soạn nhạc 2 loop; ghi LICENSES.
- **Đo máy thật:** ≥ 1 Android RAM 3 GB + 1 iPhone cũ, soak 30 phút bằng `DebugPerfProbe`, kiểm tra NavMesh bake đồng bộ (>100 ms thì chuyển async), shader variant bị strip, độ nóng, kích thước AAB/IPA. Preset Thấp phải tự chọn cho máy yếu.
- Đồng thời: chạy xong/đối chiếu bot `-nothrottle` (đang chạy) và xử lý hồi quy kinh tế cấp 3 (xem commit trước).
- **Playtest:** 8–10 phiên có quan sát 20 phút đầu (Zalo/Discord) để bắt lỗi FTUE (thuật ngữ cấp/★/Lv, level-up phải là thẻ chạm để nhận).
- **KPI/Exit P0:** crash-free ≥ 99.5%, 30 FPS máy yếu, hoàn thành ngày đầu ≥ 85% trong playtest, mọi event analytics kiểm chứng được.

### P1 — Bản soft-launch (tuần 9–~30, ≈ M2)
- **Tiếng Anh:** lớp `Loc` nhẹ (không dùng Unity Localization/Addressables trừ khi định ≥ 3 ngôn ngữ): đổi `const string` ở 5 file `*Strings.cs` (~330 chuỗi) thành thuộc tính; gom ~280 chuỗi rải rác (TierManager, GameBootstrap, StaffPanel, SettingsPanel, HudController, TutorialDirector, StoryDialog…); bảng JSON `vi`/`en`, thiếu thì rơi về `vi`; dữ liệu `FurnitureDef`/`RecipeDef`/`regulars.json`/barks cần khoá/trường theo ngôn ngữ; sửa test đang assert tiếng Việt; thêm test "mọi khoá `vi` đều có trong `en`". Không đổi tên node UI mà tutorial nhắm tới (`SummaryPanel/Card`, `NextDay`, `ShopButton`, `TierCta`…).
- **Đồng ý/pháp lý:** UMP + ATT, khai báo 13+ (không nhắm trẻ em), Data Safety/App Privacy/IARC, hỏi luật sư địa phương về luật dữ liệu cá nhân & giấy phép game Việt Nam nếu phát hành ở VN.
- **Quảng cáo thưởng:** AdMob sau facade `IAdService`; `AdPlacements` thuần C# (cap theo vị trí, cap chung 20, cooldown 90 s, không quảng cáo trong 10 phút chơi đầu, 2 lần lỗi → vẫn thưởng 50%, `remove_ads` đổi thành nút "Nhận ngay" cùng cap); chỉ 3 vị trí có sẵn hook (thưởng cuối ngày — an toàn vì tổng kết lưu `Wallet.Xu` thật —, rương Hạt, đổi nhiệm vụ). Nút đặt trong `SummaryPanel/Card`. Đo lại bot 100 ngày bật ads: Xu từ ads < 20% tổng.
- **IAP (Unity IAP 4.15 hoặc v5 — kiểm tra):** `starter_pack`, `supporter`, 2 gói Hạt; hiện giá cục bộ từ store; nút Restore (bắt buộc iOS); trình tự nhận quà an toàn: nhận giao dịch (xếp hàng đến khi `GameBootstrap.started`) → kiểm `processedTxIds` → `Gems.Add` → lưu đồng bộ (cần `SaveSnapshot` trả bool) → `ConfirmPendingPurchase`. Kiểm tra phía client trước; xác thực server sau nếu thấy gian lận.
- **Chỗ tiêu Hạt (bắt buộc trước khi bán Hạt):** thêm `gemPrice`/`premium` vào `FurnitureDef`, tab "Hạt" trong `ShopPanel`, ≥ 30 món cosmetic: ~12 biến thể đổi màu (đổi ô palette, không cần art mới), trang phục chủ quán/nhân viên (`OwnerLook`), biển hiệu (`ShopSign`), tường/sàn, đổi tên quán 50 Hạt, slot bố cục 150 Hạt; đồ premium hơn về Style, không hơn Comfort. Ngân sách Hạt: ~1 bộ cosmetic mỗi 4–6 tuần chơi free.
- **Giữ chân rẻ:** đường cong level lồi + thưởng Xu theo dải sao (đã đặc tả trong review), đăng nhập 7 ngày, nhiệm vụ tuần (dự án tuần đã có), nới bức tường tiền cấp 2, nhịp đơn cấp 3 đầu tiên, level mở cosmetic, chế độ ảnh + thẻ chia sẻ.
- **Soft-launch:** Android trước (rẻ hơn), nước EN chi phí thấp (vd. Philippines) song song VN; tải lên nội bộ → closed test → mở. Tạo 3–5 clip ngắn (xe đẩy, cà phê muối, nhà hàng) đăng TikTok/Reels/nhóm Facebook; thử $100–150 quảng cáo đo CPI.
- **Gate (500–1.000 lượt cài):** D1 ≥ 35%, D7 ≥ 10–12%, phiên trung vị ≥ 6 phút, ≥ 40% tới cấp 2 ở các phiên đầu, ≥ 35% DAU xem quảng cáo, ARPDAU ≥ $0.015, CPI (VN Android) ≤ $0.15 hoặc organic ≥ 100 cài/ngày ở tuần 3.

### P2 — Nội dung giữa game (sau gate đạt, 6–8 tuần)
Khách quen mỗi người 6–8 chương + 2–3 người mới (sửa Bà Sáu); 5–6 biến thể sự kiện ngày; đơn VIP chỉ chủ quán ở cấp 3; album công thức + ~40 thành tựu; trang phục/skin; sự kiện mùa v1 (khung dữ liệu hoá để dùng lại); Remote Config (cap ads, giá, tiền thuê cấp 2) + A/B.
KPI: D14 ≥ 8%, D30 ≥ 4%, "dead time" P90 ≤ 25 phút trong 3 giờ đầu. Exit: không có bước nào trong phễu (cấp 2 → ★3 → cấp 3) rớt > 25% đoàn.

### P3 — Live-ops & tăng trưởng
Event pass IAP + mùa Trung thu/Mùa mưa; iOS release (nếu chưa); cloud save (UGS Auth + Cloud Save, last-writer-wins); giờ tin cậy (HTTP `Date` header, thay 2 chỗ dùng `DateTime.UtcNow` trong `GameBootstrap.EnsureMissions`/`MissionPanel`) + thu nhập offline; thông báo cục bộ; trợ năng (cỡ chữ, kích thước chạm 48 dp); mở rộng ngôn ngữ nếu EN chạy tốt.
KPI: ARPDAU ≥ $0.03, IAP ≥ 35% doanh thu.

### P4 — Cấp 4 "Chuỗi chi nhánh"
Chỉ khi ≥ 5% đoàn tới ★4. Cắt phạm vi: 3 chi nhánh + 2 phong cách vùng, quản lý chi nhánh + idle + offline cap. Subscription "Cafe Club" chỉ khi > 50k DAU.

## 4. Quyết định đi/dừng (sau soft-launch)
- D1 < 25% → dừng làm tính năng, sửa FTUE/âm thanh.
- D7 < 12% → làm P2 giữ chân trước khi marketing.
- ARPDAU < $0.012 hoặc CPI quá cao và organic thấp → chuyển phương án premium một lần hoặc giữ làm sản phẩm portfolio.
- ARPDAU ≥ $0.03 và D7 ≥ 12% → mở rộng nội dung/mùa/iOS.

## 5. Rủi ro hàng đầu & cách giảm
1. **Không có nguồn người dùng** (CPI > LTV): sản xuất clip ngắn từ tuần 1, đo organic; không mua UA diện rộng.
2. **Tương thích SDK với Unity 6000.6.5f1 / quy tắc Play (16 KB page, target API)**: spike R1 trước M2.
3. **Hiệu năng 3D máy yếu / NavMesh hitch / shader variant**: đo máy thật ở P0.
4. **Mất tiền mua / mất save**: lưu giao dịch theo trình tự an toàn; save atomic + checksum.
5. **Kinh tế lệch sau khi thêm ads/IAP**: chạy lại bot 100 ngày có ads; hồi quy cấp 3 từ commit trước vẫn đang mở.
6. **Quá tải một người bán thời gian**: chỉ P0→P1 trước, mỗi sự kiện giới hạn 2 tuần, tách khung sự kiện theo dữ liệu.
7. **Review store**: nút Restore, chuỗi giá cục bộ, nhãn quyền riêng tư đúng, không đánh dấu hướng trẻ em, đưa TestFlight ngoài sớm.

## 6. Việc đầu tiên cần làm (2–4 tuần tới)
1. Tạo tài khoản Play/Apple + chính sách quyền riêng tư + chốt tên game & bundle ID.
2. Spike SDK trên Unity 6000.6.5f1 (Firebase + AdMob + IAP, build AAB + iOS).
3. Lưu an toàn + test, rồi Firebase Analytics/Crashlytics + facade `Services`.
4. Làm một vòng âm thanh tối thiểu (~15 SFX + 1 nhạc) để playtest có cảm giác thật.
5. Đưa bản build lên 2 điện thoại thật (1 yếu), đo FPS/nhiệt/kích thước.
6. Cắt 3 clip TikTok để đo phản ứng thị trường song song.

## 7. Kiểm chứng kế hoạch
- Mỗi mốc: chạy EditMode suite (tools/ runner đã có ở phiên trước: clone dự án sang thư mục tạm rồi `-runTests`), build non-Development AAB/IPA, kiểm tra `DebugAutoPilot`/`DebugPerfProbe` đã bị strip.
- Mỗi event analytics: kiểm bằng DebugView của Firebase trên máy thật.
- IAP: sandbox Apple/License testers Google; kịch bản kill-app giữa giao dịch, restore, refund.
- Ads: test ad unit, rồi kiểm cap/cooldown bằng test EditMode của `AdPlacements`.
- Kinh tế: bot 100 ngày (`-autoplay ... -days 100 -timescale 8`) trước/sau thêm ads, so với §23.7/§24.3 trong `docs/economy_monetization.md`; ghi kết quả vào tài liệu.
- Soft-launch: dashboard D1/D7/D30, phiên trung vị, % xem quảng cáo, ARPDAU, CPI so với gate ở §3 P1.

## Tệp tham chiếu chính
`docs/economy_monetization.md` (§9–15, §19–24), `docs/GDD.md`, `docs/game_designer_engagement_review_2026-10-16.md`, `docs/unity_level_review_2026-10-16.md`, `game/Packages/manifest.json`, `Core/SaveSystem.cs`, `Core/GameBootstrap.cs`, `Recipes/GemWallet.cs`, `Core/GameEvents.cs`, `Boot/BootLoader.cs`, `Missions/MissionCatalog.cs`.
