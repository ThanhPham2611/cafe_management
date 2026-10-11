# Kiến trúc kỹ thuật – Prototype Tier 1 (Vỉa hè)

> Unity 6 LTS + URP, project ở `game/`. Mọi code game nằm trong `game/Assets/_Project/`.
> Mục tiêu prototype: chơi trọn 1 ngày ở quán vỉa hè trên điện thoại Android (màn hình ngang).

## 1. Nguyên tắc
- **Ít phụ thuộc package:** runtime chỉ dùng module có sẵn của Unity (uGUI, Physics, AI/NavMeshAgent, NavMeshBuilder). Không Cinemachine, không Input System, không TextMeshPro ở prototype. Dùng `UnityEngine.Input` (cảm ứng + chuột) và `UnityEngine.UI.Text`.
- **Data-driven:** số liệu (giá, thời gian pha, nhu cầu khách) nằm trong ScriptableObject hoặc `GameBalance`, không hard-code trong logic.
- **Bootstrap bằng code:** scene chỉ có Camera, Light, mặt đất và 1 `GameBootstrap`. Bootstrap tự dựng lưới, đặt nội thất, bake NavMesh, tạo HUD. Nhờ vậy scene ít xung đột merge.
- **Sự kiện:** hệ thống giao tiếp qua `GameEvents` (static C# events), không gọi chéo trực tiếp nếu tránh được.

## 2. Assembly
| asmdef | Thư mục | Phụ thuộc | Ghi chú |
|---|---|---|---|
| `Cafe.Runtime` | `Scripts/` | không | Toàn bộ gameplay |
| `Cafe.Editor` | `Editor/` | `Cafe.Runtime` (+ URP qua versionDefine `CAFE_URP`) | Setup project, import model, dựng scene |

## 3. Thư mục `Scripts/`
| Thư mục | Lớp chính |
|---|---|
| `Core/` | `GameBootstrap`, `GameEvents`, `GameBalance`, `SaveSystem`, `TapInput` |
| `Data/` *(trong Scripts/Core/Data)* | `FurnitureDef`, `RecipeDef`, `LayoutEntry`, `BrewStep` |
| `Building/` | `GridSystem`, `PlacedFurniture`, `Seat`, `NavMeshBaker` |
| `Customers/` | `CustomerSpawner`, `Customer` (FSM), `CustomerVisual` |
| `Brewing/` | `BrewStation`, `OrderQueue`, `Order`, `CupStock`, `LeftoverCup` |
| `Economy/` | `Wallet`, `Reputation`, `DayCycle`, `DaySummary` |
| `UI/` | `HudBuilder`, `HudController`, `OrderBubble`, `BrewPanel`, `SummaryPanel`, `SafeArea` |
| `CameraRig/` | `CafeCameraController` |

## 4. Hợp đồng dữ liệu (đã viết sẵn, không đổi tên field)
- `FurnitureDef` (SO): `id`, `displayName`, `prefab`, `footprint` (Vector2Int, ô 0.5 m), `price`, `kind` (Seat/Table/Decor/BrewStation), `comfort`, `style`, `seatHeight`.
- `RecipeDef` (SO): `id`, `displayName`, `price`, `ingredientCost`, `brewSeconds`, `steps` (BrewStep[]), `unlockTier`, `unlockDay`, `orderWeight`.
- `LayoutEntry` (struct): `furnitureId`, `cell` (Vector2Int), `rotation` (0–3 = ×90°).
- `GameBootstrap` field serialize do Editor gán: `catalog`, `recipes`, `defaultLayout`, `toonMaterial`, `gridOrigin`, `gridSize`, `spawnPoint`, `exitPoint`.

## 5. Vòng chơi Tier 1 (prototype)
```
DayCycle.Open (210 s thực)
  CustomerSpawner sinh khách theo Demand_T1 = 18 + 9(★−1) + 0.5·day, rải đều trong ngày
  Customer: Arrive → FindSeat (không có ghế >10 s → bỏ đi)
          → WalkToSeat (NavMeshAgent) → Order (chọn RecipeDef theo orderWeight, đã mở khóa)
          → WaitOrder (kiên nhẫn 60 s; >60 s → bỏ đi, −2 RP)
          → Drink (8–15 s) → Pay (giá × hệ số chất lượng + tip) → Leave (để lại LeftoverCup trên bàn/ghế)
  Người chơi: chạm order trong hàng chờ → BrewPanel: chạm đúng thứ tự BrewStep
          → ly sẵn sàng (cần CupStock > 0) → chạm khách để phục vụ
          → chạm LeftoverCup để thu ly (+1 Xu, trả ly về CupStock). Ly để >30 s in-game chặn ghế đó.
DayCycle.Close → trừ phí vỉa hè 60 Xu → SummaryPanel (doanh thu, chi phí, khách, RP, ★) → "Ngày mới"
```
- Chất lượng: đúng thứ tự + nhanh (≤ brewSeconds) → 3★; đúng nhưng chậm → 2★; sai 1 bước → 1★; sai ≥2 → hỏng.
- Hài lòng T1: `S = 0.40·Q + 0.35·W + 0.10·C_ly + 0.15·F` (economy_monetization.md §4).
- RP: `clamp(1.5·(S−0.4)/0.3, −3, 3)`, ★2 ở 300 RP.
- Lưu: `SaveSystem` ghi JSON `persistentDataPath/save_v1.json` (Xu, ngày, RP, layout) khi đóng cửa và khi app pause.

## 6. Quy ước code
- C# namespace `Cafe`, PascalCase, field serialize `[SerializeField] private` (trừ hợp đồng dữ liệu ở §4 để Editor gán).
- Chuỗi hiển thị tiếng Việt để trong một chỗ (`Strings.cs`) để sau đổi sang Unity Localization.
- Không `FindObjectOfType` trong Update. Tham chiếu được truyền từ `GameBootstrap`.
- Mỗi lớp một file, file tên trùng tên lớp.
