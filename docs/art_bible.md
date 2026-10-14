# ART BIBLE — "Quán cà phê Việt"

> Phiên bản 1.0 · Tech Art / Art Director · Unity 6 URP · Mobile landscape (Android trước, iOS sau)
> Mọi asset (nội bộ, mua, AI, thuê ngoài) PHẢI tuân thủ tài liệu này. Validator `tools/blender_validate.py` sẽ chặn asset sai chuẩn.

---

## 1. Tầm nhìn nghệ thuật

- **Từ khóa:** ấm áp · cozy · Việt Nam đời thường · low-poly dễ thương · "golden hour".
- **Hình khối:** mập, bo nhẹ, tỉ lệ hơi cường điệu (chibi), ít chi tiết nhỏ, silhouette đọc rõ ở màn hình 6".
- **Màu:** bảng màu duy nhất, bão hòa vừa, ấm; tương phản bằng *sáng/tối* chứ không bằng texture.
- **Không dùng:** texture photoreal, normal map, PBR, specular gắt, post-process nặng.
- **Camera:** góc isometric-ish phối cảnh (pitch ~50°, FOV 30–35°), xoay giới hạn. Mọi asset phải đẹp từ góc nhìn trên-chéo.

---

## 2. Quy ước đơn vị & lưới

| Mục | Giá trị |
|---|---|
| Đơn vị | **1 unit = 1 m** (Blender Unit Scale 1.0, Unity scale 1) |
| Ô lưới (grid cell) | **0.5 m × 0.5 m** |
| Trục | Unity: Y up, **mặt trước (front) hướng +Z** |
| Pivot | **Bottom-center** của footprint (X,Z giữa, Y = 0 là mặt sàn) |
| Rotation đặt | Bội số 90° quanh Y |
| Scale | Luôn (1,1,1) sau khi export; không scale âm |

### 2.1 Footprint (đơn vị ô)

| Loại | Footprint (X×Z ô) | Kích thước thực |
|---|---|---|
| Ghế đẩu nhựa / ghế | 1×1 | 0.5 × 0.5 m |
| Bàn nhỏ | 1×1 hoặc 2×2 | 0.5² / 1.0² m |
| Bàn dài | 4×2 | 2.0 × 1.0 m |
| Quầy (counter) | 4×2 | 2.0 × 1.0 m |
| Máy pha / tủ lạnh | 2×2 / 2×1 | 1.0² / 1.0×0.5 m |
| Cây cảnh lớn | 1×1 hoặc 2×2 | |
| Xe đẩy vỉa hè | 3×2 | 1.5 × 1.0 m |

Mesh phải **nằm gọn trong footprint** (tolerance +0.05 m). Footprint khai báo qua custom property `footprint_x`, `footprint_z` trên object root.

### 2.2 Chiều cao tham chiếu

| Vật | Cao (m) | Ghi chú |
|---|---|---|
| Ghế đẩu nhựa vỉa hè (mặt ngồi) | **0.25** | Thấp kiểu Việt Nam! Tổng cao ~0.28 |
| Bàn thấp vỉa hè | **0.45** | |
| Ghế thường (mặt ngồi) | 0.45 | Tổng cao ~0.85 nếu có lưng |
| Bàn thường | 0.75 | |
| Quầy bar | 1.05 | |
| Máy pha cà phê (trên quầy) | 0.4–0.5 | |
| Nhân vật chibi | **1.6** | ~3 đầu (đầu ≈ 0.53 m) |
| Cửa ra vào | 2.0 | |
| Trần nhà | 3.2 | |
| Dù/ô vỉa hè | 2.2 | |
| Ly phin / ly cà phê | 0.08–0.12 | |

---

## 3. Bảng màu (Palette) — `T_Palette_256.png`

- Texture 256×256, lưới **8×8**, mỗi ô **32×32 px**, filter **Point**, no mipmaps (hoặc mip với padding), sRGB, compression ASTC 6x6 (Android) / không alpha.
- **Index = `row*8 + col`**, row 0 là hàng **trên cùng**, col 0 là cột trái.
- **UV tâm ô:** `u = (col + 0.5)/8`, `v = 1 - (row + 0.5)/8` (Unity/Blender: v=0 là đáy).
- Gradient/shading *không* nằm trong texture: shading do toon shader đảm nhiệm.
- Ô là màu **phẳng**; muốn 2 sắc độ thì chọn 2 ô.

### Row 0 — Cà phê & nâu (coffee browns)
| Idx | (r,c) | Tên | Hex |
|---|---|---|---|
| 0 | 0,0 | Espresso đen | `#2A1A12` |
| 1 | 0,1 | Cà phê đậm | `#4A2C1E` |
| 2 | 0,2 | Cà phê phin | `#6B4026` |
| 3 | 0,3 | Cà phê sữa | `#A06A42` |
| 4 | 0,4 | Bạc xỉu nhạt | `#C9976A` |
| 5 | 0,5 | Caramel | `#D9A441` |
| 6 | 0,6 | Cacao | `#5A3A2E` |
| 7 | 0,7 | Tóc nâu | `#3B2A22` |

### Row 1 — Kem / sữa (cream & milk)
| Idx | (r,c) | Tên | Hex |
|---|---|---|---|
| 8 | 1,0 | Sữa đặc | `#FFF4DC` |
| 9 | 1,1 | Kem trứng | `#FBE29A` |
| 10 | 1,2 | Kem muối | `#F3E9D2` |
| 11 | 1,3 | Tường kem | `#F2DDB8` |
| 12 | 1,4 | Be ấm | `#E6C99A` |
| 13 | 1,5 | Vàng trứng | `#F2B93B` |
| 14 | 1,6 | Foam trắng | `#FFFBF0` |
| 15 | 1,7 | Giấy/menu | `#EFE3C8` |

### Row 2 — Gỗ (wood)
| Idx | (r,c) | Tên | Hex |
|---|---|---|---|
| 16 | 2,0 | Gỗ sáng | `#D8A96B` |
| 17 | 2,1 | Gỗ mật ong | `#B97F45` |
| 18 | 2,2 | Gỗ trung | `#8F5B31` |
| 19 | 2,3 | Gỗ óc chó | `#5E3920` |
| 20 | 2,4 | Gỗ tối cũ | `#3F2616` |
| 21 | 2,5 | Tre/mây | `#C8A24E` |
| 22 | 2,6 | Gỗ sơn trắng | `#E8DCC4` |
| 23 | 2,7 | Gỗ xanh sơn cũ | `#6E8F7A` |

### Row 3 — Nhựa (ghế đẩu, biển, đồ nhựa)
| Idx | (r,c) | Tên | Hex |
|---|---|---|---|
| 24 | 3,0 | Nhựa đỏ | `#D9362B` |
| 25 | 3,1 | Nhựa đỏ tối | `#9E2119` |
| 26 | 3,2 | Nhựa xanh dương | `#2E6FD1` |
| 27 | 3,3 | Nhựa xanh dương tối | `#1D4A97` |
| 28 | 3,4 | Nhựa xanh lá | `#2FA05A` |
| 29 | 3,5 | Nhựa xanh lá tối | `#1D6D3C` |
| 30 | 3,6 | Nhựa vàng | `#F2C230` |
| 31 | 3,7 | Nhựa trắng | `#F4F1EA` |

### Row 4 — Xanh phố Việt (cây, mái hiên, rêu)
| Idx | (r,c) | Tên | Hex |
|---|---|---|---|
| 32 | 4,0 | Lá sáng | `#8CC152` |
| 33 | 4,1 | Lá trung | `#4FA043` |
| 34 | 4,2 | Lá tối | `#2C6B3A` |
| 35 | 4,3 | Lá bàng/rêu | `#5B7F3A` |
| 36 | 4,4 | Xanh bạc hà tường | `#9CD3B5` |
| 37 | 4,5 | Xanh ngọc cửa | `#3E9E8C` |
| 38 | 4,6 | Xanh dù | `#2F8F7B` |
| 39 | 4,7 | Xanh rêu tường cũ | `#7A8F5E` |

### Row 5 — Gạch (tile / gạch bông / tường)
| Idx | (r,c) | Tên | Hex |
|---|---|---|---|
| 40 | 5,0 | Gạch bông kem | `#E9D9B5` |
| 41 | 5,1 | Gạch bông đỏ gạch | `#B5472F` |
| 42 | 5,2 | Gạch bông xanh | `#3B6E8F` |
| 43 | 5,3 | Gạch bông vàng | `#D9A93A` |
| 44 | 5,4 | Gạch men trắng | `#F1F0EA` |
| 45 | 5,5 | Gạch xám sàn | `#B9B2A6` |
| 46 | 5,6 | Gạch nung | `#C4683E` |
| 47 | 5,7 | Vữa/grout | `#8E877B` |

### Row 6 — Kim loại/xám (0–3) + Da (4–7)
| Idx | (r,c) | Tên | Hex |
|---|---|---|---|
| 48 | 6,0 | Inox sáng | `#B8BEC4` |
| 49 | 6,1 | Inox trung | `#8A939B` |
| 50 | 6,2 | Inox tối | `#4F575E` |
| 51 | 6,3 | Than/đen | `#2B2D31` |
| 52 | 6,4 | Da sáng | `#F6D7BE` |
| 53 | 6,5 | Da trung | `#E3B48E` |
| 54 | 6,6 | Da nâu | `#C48B63` |
| 55 | 6,7 | Da sẫm | `#8D5A3B` |

### Row 7 — Nhấn (accents)
| Idx | (r,c) | Tên | Hex |
|---|---|---|---|
| 56 | 7,0 | Đỏ cờ | `#D62828` |
| 57 | 7,1 | Vàng sao | `#FFD23F` |
| 58 | 7,2 | Hồng sen | `#F28DB2` |
| 59 | 7,3 | Cam | `#F4801F` |
| 60 | 7,4 | Teal | `#2A9D9B` |
| 61 | 7,5 | Tím | `#7B5EA7` |
| 62 | 7,6 | Tóc đen | `#1A1514` |
| 63 | 7,7 | Trắng tinh | `#FFFFFF` |

### 3.1 Quy tắc UV vào palette
- Mỗi face được unwrap và **thu nhỏ/đặt trọn vào 1 ô** (UV island nằm trong ô, cách mép ô ≥ 2 px = 0.0078 UV để chống bleed).
- Face đơn sắc: scale island về ~0 và đặt tại tâm ô (cách nhanh nhất; script `assign_palette(obj, face, idx)`).
- Không dùng cell 63 (trắng tinh) cho diện tích lớn — dùng 14/31/44.
- Muốn thêm màu: **không** thêm; thay đổi bảng phải qua Art Director và bump version palette (`T_Palette_256_v2`) + migrate toàn bộ.

### 3.2 Decal atlas (tùy chọn)
- `T_Decal_Atlas_512.png` (512², lưới 4×4 ô 128 px): gạch bông (4 mẫu), vết ố, logo quán, chữ biển hiệu, hoa văn rèm.
- Dùng cho **sàn/tường** dạng quad riêng (material `M_Toon_Decal`, cùng shader, 1 texture khác) — tối đa **1 material decal/scene**.
- Mesh thường vẫn chỉ dùng `M_Toon_Palette`.

---

## 4. Material & Shader

**Một material duy nhất:** `M_Toon_Palette` (shader `Shader Graph/Toon_Palette`, URP, SRP Batcher compatible).

| Thuộc tính | Giá trị mặc định |
|---|---|
| Base Map | `T_Palette_256` |
| Bands | 3 (cho phép 2) — ngưỡng 0.35 / 0.7 |
| Band softness | 0.04 (viền mềm nhẹ) |
| Shadow tint | màu tím-nâu ấm `#6A4A5A` nhân với base ở vùng tối |
| Rim light | Soft, color `#FFD9A0`, power 3, intensity 0.35, chỉ ở phía ngược sáng |
| Specular | Không |
| Emission | Có 1 slot (cho đèn, màn hình, đèn neon) dùng mask theo ô palette |
| Receive shadow | Có (main light), cast shadow theo renderer |
| Alpha | Opaque. Không alpha blend (trừ FX riêng) |

Quy tắc:
- Mọi `MeshRenderer` dùng `M_Toon_Palette` (shared, không instance material). Đổi màu runtime dùng **MaterialPropertyBlock** hoặc vertex color / UV offset ô, không clone material.
- Đổi màu ghế (đỏ/xanh/lá) = đổi UV ô qua script `PaletteSwatchApplier` (MPB `_SwatchOffset`) — không cần mesh mới.
- Outline: **không** dùng inverted hull trên toàn cảnh; chỉ cho object được chọn (highlight pass).

---

## 5. Ngân sách đa giác & mesh

| Loại | Tri budget | Ví dụ |
|---|---|---|
| Props nhỏ | 100–300 | ly phin, cốc, chai, bình hoa, thực đơn |
| Nội thất | 300–800 | ghế, bàn, kệ, tủ, dù |
| Quầy / máy | 1.500–3.000 | quầy bar, máy pha, xe đẩy |
| Nhân vật | 3.000–5.000 | khách, nhân viên, chủ quán |
| Vỏ nhà (tier) | ≤ 8.000 | khung shophouse, tường, mái |
| **Cả scene** | **< 150.000 tris** | tính lúc camera nhìn đầy đủ |

- **Bo tròn ("chunky", đã duyệt 2026-10-11):** mặt bàn/ghế bo góc khi nhìn từ trên (bán kính 0.07–0.12 m) + bo mép **2 nấc** (0.01–0.03 m); chân tròn thuôn, đầu chân bo. Chi tiết bị che hoặc nhỏ < 0.15 m dùng 0–1 nấc để giữ tri budget.
- Không có n-gon khi export (triangulate), không loose verts, không faces ẩn bên trong/đáy (xóa mặt đáy).
- Shading: **smooth toàn bộ + cạnh sắc khi góc > 40°** (mặt phẳng vẫn phẳng, phần bo trông mềm). FBX export `mesh_smooth_type=EDGE`.
- Không dùng vertex color, không normal map, không blend shape (ngoại trừ mặt nhân vật nếu cần).
- LOD: không cần cho props; nhân vật/nhà không LOD (camera cố định). Dùng culling theo khu vực (zone).
- Draw call mục tiêu: **< 150** batches/frame, SetPass **< 20**.

---

## 6. Ánh sáng & Render (URP, mobile)

### 6.1 Lighting
- **1 Directional Light** "golden hour": màu `#FFC98A`, intensity 1.3, rotation (X 38°, Y -35°). Đây là nguồn chính.
- **Ambient:** Gradient — Sky `#BFD3E6`, Equator `#E8C9A0`, Ground `#7A5A4A`. Intensity 1.0.
- **Shadow:** Soft, **1 cascade**, distance 18 m, resolution 1024 (512 trên máy yếu), depth bias 0.05, normal bias 0.4.
- **Baked:** không dùng lightmap. Đèn trong quán dùng *emission + quad glow* (fake), không realtime point light (tối đa 1 additional light có shadow tắt).
- Ban ngày/đêm: lerp màu directional + ambient theo giờ; đêm → bật emission đèn lồng/đèn neon.

### 6.2 Perf rules
- **SRP Batcher: ON**, **GPU Instancing: ON** cho prop lặp lại (ghế, bàn, ly).
- Static Batching: ON cho vỏ nhà/sàn. Dynamic batching OFF.
- MSAA: **Off** hoặc 2x trên máy mạnh; dùng FXAA/SMAA low tuỳ quality tier. Render scale 0.85–1.0.
- Post-processing: **tối đa** Color Adjustments nhẹ (hoặc LUT 16³). **Không** Bloom/SSAO/DoF/Motion Blur. Bloom giả = sprite glow.
- HDR off, depth texture off (trừ khi cần), opaque texture off.
- Target: 30 fps ổn định (60 fps cho máy mạnh), nhiệt độ thấp, RAM < 1.2 GB.
- Texture: chỉ 2 texture world (`T_Palette_256`, `T_Decal_Atlas_512`) + UI atlas.

### 6.3 Ba mức đồ họa (Thấp / Vừa / Cao)

Người chơi chọn trong **Cài đặt đồ họa** (nút "ĐỒ HỌA" ở cột phải HUD); đổi có hiệu lực ngay và được nhớ theo máy (PlayerPrefs `cafe.graphics`, không đi theo save game). Lần chạy đầu tự chọn theo cấu hình máy (`GraphicsPresets.Recommend`): máy tính = Cao; điện thoại RAM ≤ 3,2 GB hoặc ≤ 4 nhân = Thấp, RAM ≥ 5,5 GB và ≥ 8 nhân = Cao, còn lại = Vừa.

| | **Thấp** (máy yếu) | **Vừa** | **Cao** |
|---|---|---|---|
| URP asset | `URP_Mobile_Low` | `URP_Mobile_Medium` | `URP_Mobile` |
| Bóng đổ | **tắt** (không có lượt vẽ bóng) | cứng, bản đồ bóng 512, tới 14 m | mềm, bản đồ bóng 1024, tới 18 m |
| Độ nét (render scale) | 70% (≈ 49% số điểm ảnh) | 85% (≈ 72%) | 100% |
| Khử răng cưa | không | không | MSAA 2x |
| FPS tối đa trên điện thoại | **30** | 60 | 60 |
| Viền chữ UI (Outline) | tắt | bật | bật |

Mỗi mức = một mức chất lượng của Unity (`ProjectSettings/QualitySettings` có đúng 3 mức Low / Medium / High) trỏ tới URP asset riêng; `UrpSetup.Ensure` (menu Cafe) tạo lại 3 asset nếu mất. UI (Screen Space Overlay) luôn vẽ ở độ phân giải gốc nên chữ vẫn nét khi giảm độ nét cảnh 3D. Thêm cờ chạy thử: `-gfx low|medium|high` (bản Development).

---

## 7. Quy ước đặt tên

| Tiền tố | Loại | Ví dụ |
|---|---|---|
| `SM_` | Static Mesh | `SM_Stool_Plastic_Red`, `SM_Counter_Wood_4x2` |
| `SK_` | Skinned Mesh | `SK_Char_Base`, `SK_Hair_Bun` |
| `M_` | Material | `M_Toon_Palette`, `M_Toon_Decal` |
| `T_` | Texture | `T_Palette_256`, `T_Decal_Atlas_512` |
| `A_` | Animation clip | `A_Idle_Customer`, `A_Walk`, `A_Serve` |
| `AC_` | Animator Controller | `AC_Customer` |
| `FX_` | VFX/Particle | `FX_Steam_Cup`, `FX_Coin_Pop` |
| `UI_` | UI sprite/prefab | `UI_Icon_Coin`, `UI_Btn_Primary` |
| `PF_` | Prefab | `PF_Stool_Plastic_Red` |
| `SO_` | ScriptableObject | `SO_Furniture_StoolRed` |
| `AU_` | Audio | `AU_Sfx_Pour`, `AU_Bgm_Tier1` |

Quy tắc: `PascalCase_Với_Gạch_Dưới`, ASCII không dấu, không khoảng trắng; hậu tố biến thể ở cuối (`_Red`, `_L`, `_4x2`). Tên object trong Blender = tên file FBX (không `.001`).

### 7.1 Cấu trúc thư mục

```
D:\cafe_store\
├─ docs\                         (art_bible.md ...)
├─ tools\                        (blender_validate.py, gen_furniture.py)
├─ art_source\                   (NGOÀI Unity — nguồn gốc)
│  ├─ blend\                     (.blend theo category)
│  ├─ palette\                   (T_Palette_256.psd/.aseprite, palette.json)
│  ├─ reference\                 (ảnh tham khảo)
│  ├─ purchased\                 (pack Kenney/Synty gốc, chưa sửa)
│  ├─ ai_raw\                    (output Meshy/Tripo trước retopo)
│  └─ export\                    (FBX đã validate → copy vào Unity)
└─ game\Assets\_Project\Art\
   ├─ Materials\                 (M_Toon_Palette, M_Toon_Decal)
   ├─ Textures\                  (T_Palette_256, T_Decal_Atlas_512)
   ├─ Models\
   │  ├─ Furniture\  Props\  Counters\  Building\  Nature\
   │  └─ Characters\             (SK_*)
   ├─ Animations\                (A_*, AC_*)
   ├─ Prefabs\                   (PF_*)
   ├─ VFX\                       (FX_*)
   ├─ UI\                        (Atlas, Fonts, Icons)
   └─ Audio\                     (Sfx, Bgm)
```

---

## 8. Pipeline mô hình

### 8.1 Export từ Blender (FBX)
- Blender: Z-up. Unity: Y-up, left-handed.
- Export FBX settings: **Forward = -Z Forward, Up = Y Up**, Apply Scalings = **FBX Units Scale**, `Apply Transform` ON, Scale 1.0, Triangulate Faces ON, Smoothing = Face, Only Selected.
- Kết quả: front của model (Blender -Y) → **+Z trong Unity**. Luôn thiết kế mặt trước nhìn về -Y trong Blender.
- Trước export: Ctrl+A Apply All Transforms; origin ở bottom-center (Object ▸ Set Origin ▸ 3D Cursor tại (0,0,0) với model đặt trên sàn).
- Unity Import: Scale Factor 1, Convert Units OFF (vì đã đúng m), Read/Write OFF, Generate Colliders OFF, Normals = Import, Mesh Compression Medium, Optimize Mesh ON, Materials = **Use External → M_Toon_Palette** (remap).

### 8.2 Nguồn asset & cách chuẩn hóa

| Nguồn | Chất lượng | Cách chuẩn hóa |
|---|---|---|
| **Blender procedural `gen_furniture.py`** | Chuẩn, lặp lại được | Ưu tiên số 1. Script nhận param (kích thước, ô màu), dựng primitive + bevel, gán palette UV, export FBX. Đã đúng chuẩn mặc định. |
| **Kenney / Synty packs** | Style khác, texture riêng | Import vào Blender → scale về mét → xóa chi tiết thừa → decimate/ sửa tri → **re-UV vào ô palette** (chọn màu gần nhất) → gộp 1 material → đổi tên `SM_` → validate. Giữ license file trong `art_source/purchased/`. |
| **AI-3D (Meshy/Tripo)** | Topology xấu, texture photoreal | **Bắt buộc**: retopo (Quad Remesher/Decimate + tay) về budget → bỏ texture AI → bake/gán lại bằng palette (vẽ face theo màu gần nhất) → sửa scale/pivot → validate. Không đưa thẳng vào Unity. Ghi nguồn + prompt trong `ai_raw/`. |
| **Thuê artist ngoài** | Biến thiên | Gửi **art_bible.md + palette.json + file mẫu .blend (`TEMPLATE_asset.blend`)**. Nghiệm thu = pass `blender_validate.py` + review thủ công 3 góc camera. |

### 8.3 Nhân vật
- **1 humanoid base rig** (`SK_Char_Base`): ~22 xương Unity-Humanoid-compatible (Hips, Spine, Chest, Neck, Head, Shoulder/Arm/Forearm/Hand, UpLeg/Leg/Foot) + ngón tay tối giản (tuỳ chọn).
- **Modular parts** dùng chung 1 skeleton: `SK_Body_{M,F}`, `SK_Hair_*`, `SK_Top_*`, `SK_Bottom_*`, `SK_Hat_*` (nón lá!), `SK_Apron`. Ghép bằng SkinnedMeshRenderer chia sẻ `rootBone`; mỗi bộ phận vẫn dùng `M_Toon_Palette` (đổi màu bằng ô UV/MPB).
- Tỉ lệ chibi: đầu ≈ 1/3 chiều cao (1.6 m), tay chân ngắn mập; mặt đơn giản — mắt = 2 chấm (ô 62), miệng = quad nhỏ (decal tuỳ chọn).
- Tri: body 1.5–2k, hair 300–600, top/bottom 400–800 mỗi cái, tổng ≤ 5k.
- **Animation:** Mixamo (FBX without skin, 30 fps) → Unity **Humanoid retarget** (Animation Type = Humanoid, Avatar từ `SK_Char_Base`). Clip cần: Idle, Walk, Sit_Idle, Drink, Carry_Tray, Serve, Clean, Brew (đứng thao tác), Wave, Celebrate. Đổi tên `A_*`. Loop pose bật khi cần.
- Root motion OFF (di chuyển bằng NavMesh/Pathing).

---

## 9. Mood & vật dụng theo cấp (progression)

### Tier 1 — Vỉa hè (Sidewalk stall)
- **Mood:** buổi sáng sớm Hà Nội/Sài Gòn, nắng vàng xiên, vỉa hè gạch đỏ cũ, tiếng xe máy; ấm, hơi lộn xộn dễ thương.
- **Palette chủ đạo:** nhựa đỏ/xanh (r3), gạch xám (45), xanh lá cây (r4), inox (r6).
- **Props:** ghế đẩu nhựa thấp (0.25 m), bàn thấp 0.45 m, dù che nắng, xe đẩy cà phê, bình trà đá, phích nước, ly thủy tinh, thùng xốp, cây cảnh chậu, xe máy làm nền, biển hiệu giấy.

### Tier 2 — Thuê mặt bằng (Rented shophouse)
- **Mood:** nhà ống nhỏ gọn, cửa xếp kéo, tường vàng kem, gạch bông, đèn vàng ấm; cảm giác "quán quen".
- **Palette:** kem/be (r1), gỗ (r2), gạch bông (r5), xanh bạc hà (36).
- **Props:** quầy bar 4×2, máy pha phin set, kệ ly, bàn ghế gỗ, quạt trần, quạt cây, đèn thả, rèm, cửa cuốn, menu bảng đen, tranh treo, cây trầu bà.

### Tier 3 — Mua nhà hàng (Multi-zone restaurant)
- **Mood:** rộng rãi, nhiều khu: indoor, sân vườn, quầy bếp, khu nhạc; sang hơn nhưng vẫn ấm, đèn lồng, mây tre.
- **Palette:** gỗ tối (19–20), tre/mây (21), xanh lá tối (34), caramel (5), nhấn đỏ cờ.
- **Props:** sofa/ghế bành, bàn dài 4×2, bar island, máy pha espresso lớn, tủ trưng bánh, hồ cá/koi, đèn lồng, giàn lan, piano nhỏ, quầy thu ngân, ô-văng sân vườn.

**Môi trường ba khu (sân vườn / lầu / VIP, 2026-10-13).** Dựng bằng khối trong code (`ZoneEnvironment`), chỉ dùng `M_Toon_Palette` và các ô có sẵn, không import model và không thêm ô mới (§3.1). Sân vườn là cỏ ô sáng / tối 1 m + lối đá, hàng rào cây thấp, đèn dây; lầu là gác gỗ cùng mặt sàn (không phải tầng thật) có lan can trắng, cột pergola; VIP là sàn gỗ tối, thảm đỏ viền vàng, tường cao có tranh. Khu chưa mở là bãi đất nung rào chắn cam / trắng kèm biển đỏ "chờ xây".

| Nhóm | Ô palette (idx) |
|---|---|
| Cỏ sân vườn (ô sáng / tối) | 32 lá sáng · 33 lá trung |
| Cây, hàng rào cây | 34 lá tối |
| Lối đá | 45 gạch xám sàn |
| Bãi đất khu chưa mở | 46 gạch nung |
| Rào chắn, biển "chờ xây" | 59 cam · 31 nhựa trắng · 56 đỏ cờ |
| Gỗ (sàn gác lầu, sàn VIP, khung) | tông sáng 16 · 17 · 21 · 22; tông tối 19 · 20 (VIP dùng gỗ tối) |
| Thảm VIP + viền | 25 nhựa đỏ tối + 57 vàng sao |
| Tường cao khu VIP | 38 xanh dù |

### Tier 4 — Chuỗi cửa hàng (Chain: Hà Nội / Huế / Đà Lạt / Sài Gòn)
Chung: logo thương hiệu nhất quán (cờ vàng-đỏ nhấn), mỗi chi nhánh đổi *tông + kiến trúc*, dùng cùng palette.

| Chi nhánh | Mood | Màu nhấn | Props đặc trưng |
|---|---|---|---|
| **Hà Nội** | Phố cổ, mưa phùn, vàng rêu | Vàng kem tường (11), rêu (39), đỏ gạch (41) | Ban công sắt, hoa sữa, ghế đẩu, tàu điện nền, tranh phố cổ |
| **Huế** | Cung đình, trầm lặng, tím-vàng | Tím (61), vàng (57), gỗ tối | Nón lá, đèn lồng, cổng vòm, bình gốm, áo dài mannequin |
| **Đà Lạt** | Sương, thông, hồng-xanh | Hồng sen (58), lá (33), kem (8) | Lò sưởi, hoa, cây thông, len, kính ấm, sương FX |
| **Sài Gòn** | Nắng gắt, nhộn nhịp, hiện đại | Cam (59), teal (60), xanh nhựa | Xe máy, dây điện, quạt, mái tôn, cafe chung cư, graffiti decal |

---

## 10. UI (Landscape)

### 10.1 Thông số
- **Reference resolution:** 1920×1080, Canvas Scaler *Scale With Screen Size*, Match = **0.5** (hoặc 1.0 Height trên máy 21:9).
- **Safe area:** áp `Screen.safeArea` cho root container (notch, gesture bar). Lề tối thiểu 24 px (ref).
- **Touch target ≥ 48 dp** (~ 96 px ở ref 1920 trên máy xhdpi; thực tế ≥ 120×120 ref cho nút chính). Khoảng cách giữa nút ≥ 8 dp.
- Tỉ lệ hỗ trợ: 16:9 → 21:9; 4:3 (tablet) với anchoring linh hoạt.
- Không đặt thông tin quan trọng trong góc bị che bởi ngón (xem thumb-zone).

### 10.2 Font
- **Chính (tiêu đề + nút):** *Be Vietnam Pro* (Bold/SemiBold).
- **Phụ (body, số):** *Nunito* (Regular/Bold, rounded, thân thiện).
- Bắt buộc font **hỗ trợ đầy đủ dấu tiếng Việt** (ă â ê ô ơ ư đ + thanh điệu) — test chuỗi: "Quán cà phê Việt – Ướt át, Ếch ngồi đáy giếng, Đường Nguyễn Huệ".
- TMP Font Asset Dynamic hoặc Static với Vietnamese charset (Unicode U+00C0–1EF9). Có fallback font.
- Cỡ tối thiểu 28 px (ref) body, 36+ px số tiền/nút; line spacing 1.15.

### 10.3 Phong cách UI & icon
- Bo góc lớn (radius 24–32 px ref), panel màu kem (`#FFF4DC`) viền nâu (`#6B4026`), nút chính caramel/cam, nút nguy hiểm đỏ cờ.
- Màu UI bám palette §3 (cùng hex) — không thêm màu lạ.
- **Icon:** flat, 2 tông + 1 viền nâu đậm 4 px, góc bo, canvas 128×128 (export 128/64), padding 8 px, đồ vật nhìn từ phía trước/3-4 nhẹ, không gradient/gloss. Một bộ stroke width thống nhất.
- Atlas: Sprite Atlas v2, ASTC, không mipmap, 9-slice cho panel/nút.
- Chuyển cảnh: scale-pop 0.15 s ease-out; tiền "ting" bay lên HUD.

### 10.4 Layout HUD (landscape, 1920×1080)

```
┌──────────────────────────────────────────────────────────────────────────┐
│ [≡]  Lv.3  ★ Danh tiếng ▓▓▓▓░░          🪙 12.450     💎 25      [⚙]    │ ← TOP BAR (info, ít chạm)
│ ┌─Nhiệm vụ──┐                                                            │
│ │▸ Pha 5 phin│                                                  ┌──────┐ │
│ │▸ Lau bàn 3 │             [ VÙNG CHƠI 3D ]                     │Đơn   │ │
│ └───────────┘        (kéo = pan, 2 ngón = zoom)                 │hàng  │ │
│                                                                  │ ☕☕🥛│ │
│                                                                  └──────┘ │
│                                                                           │
│ ( ◉ )                                                       ┌────┐┌────┐ │
│  Joystick/                                                   │ 🧹 ││ ☕ │ │ ← THUMB ZONE
│  Di chuyển                                                   │Lau ││Pha │ │   (phải: hành động)
│                  [🪑 Xây dựng]  [📦 Kho]  [🏪 Chi nhánh]     └────┘└────┘ │
└──────────────────────────────────────────────────────────────────────────┘
 ↑ thumb trái: di chuyển                         thumb phải: hành động ↑
```

- **Thumb zone:** góc dưới-trái (di chuyển) và dưới-phải (hành động chính) — nút lớn ≥ 140 px ref.
- **Top bar:** thông tin chỉ đọc (tiền, level, cài đặt) — nút nhỏ 96 px, ít dùng.
- **Giữa dưới:** menu chế độ (Xây dựng/Kho/Chi nhánh), tab ngang, ẩn khi đang thao tác.
- **Build mode:** thanh nội thất cuộn ngang ở đáy, preview lưới 0.5 m hiện chấm, xanh = hợp lệ / đỏ = vướng; nút Xoay/Xác nhận/Hủy ở cạnh phải (thumb).
- Panel lớn (shop, nâng cấp) mở dạng modal 70% màn hình, có nút X ≥ 96 px, đóng bằng chạm ngoài.

---

## 11. Audio & Haptic (ngắn gọn)

**Âm thanh**
- Tông: lo-fi acoustic, guitar nylon + piano mềm + tiếng phố (xe máy, rao hàng) nền nhỏ. Mỗi tier 1 BGM loop (60–90 s) với biến thể ngày/đêm.
- SFX cozy, ngắn (< 1.2 s), mix thấp: rót phin (nhỏ giọt), khuấy ly, đặt ly (tock gỗ), chạm bàn, xoay ghế, tiền "ting", lau bàn (swish), mở cửa chuông gió, đặt nội thất (pop mềm).
- Format: OGG Vorbis q4, mono cho SFX, stereo cho BGM; Load = Compressed in Memory (SFX), Streaming (BGM). Loudness: BGM −20 LUFS, SFX −14 LUFS peak −3 dBFS.
- Ducking: BGM giảm 4 dB khi popup/cutscene.
- Đa ngôn ngữ: voice-over không bắt buộc; dùng tiếng "ừm/ồ" ngắn (gibberish) cho khách.

**Haptic**
- Nhẹ & hiếm: `Light` khi đặt nội thất/chạm nút; `Medium` khi hoàn thành đơn / lên cấp; `Success` pattern khi mở khóa tier.
- Không rung liên tục khi kéo. Có công tắc tắt Haptic trong Settings. Dùng Android VibrationEffect (10–30 ms), iOS UIImpactFeedbackGenerator.

---

## 12. Validation checklist — `tools/blender_validate.py`

Script chạy (Blender headless) trên mọi `.blend` trong `art_source/blend/` và mọi FBX trong `art_source/export/`. **Fail = không được copy vào Unity.**

| # | Kiểm tra | Điều kiện PASS | Mức |
|---|---|---|---|
| 1 | Scene unit | Metric, Unit Scale = 1.0 | ERROR |
| 2 | Scale applied | `obj.scale == (1,1,1)`, rotation = (0,0,0), không scale âm | ERROR |
| 3 | Pivot | Origin ở (0,0,0), bbox min.z(Blender up)=0 → đáy chạm sàn; tâm XY của bbox trong ±0.05 m | ERROR |
| 4 | Footprint | bbox X/Y ≤ `footprint × 0.5 + 0.05`; khớp custom prop | ERROR |
| 5 | Tri count | Theo danh mục (§5); vượt budget | ERROR; >90% budget | WARN |
| 6 | Material | Đúng **1 slot**, tên chính xác `M_Toon_Palette` (hoặc `M_Toon_Decal` cho decal quad) | ERROR |
| 7 | UV palette | **Mọi** UV vertex nằm trong 1 ô duy nhất mỗi face (island không vắt qua 2 ô), bên trong ô trừ margin 0.0078; không UV ngoài [0,1] | ERROR |
| 8 | UV set | Chỉ 1 UV map, tên `UVMap` | ERROR |
| 9 | Geometry | Không n-gon (hoặc sẽ triangulate), không loose vert/edge, không double verts, không non-manifold (trừ tấm mỏng), không mặt đáy thừa | WARN/ERROR |
| 10 | Normals | Normal hướng ra ngoài (không flipped), không custom split lung tung | ERROR |
| 11 | Bevel | Không modifier chưa apply; (kiểm tra gián tiếp: không geometry vi phạm tri budget) | WARN |
| 12 | Modifiers | Không modifier còn lại (đã apply) | ERROR |
| 13 | Naming | Theo §7 (`SM_/SK_/…`), ASCII, không `.001`, tên object = tên mesh data = tên file | ERROR |
| 14 | Texture | Không có image texture nào khác ngoài palette/decal; không embedded image | ERROR |
| 15 | Vertex color / shape key | Không có (trừ SK_ được cho phép) | WARN |
| 16 | Direction | Mặt trước hướng -Y Blender (front marker empty `FRONT` hoặc custom prop `front="-Y"`) | WARN |
| 17 | Rig (SK_) | Skeleton khớp `SK_Char_Base` (tên xương), ≤ 4 influences/vertex, root xương ở 0,0,0 | ERROR |
| 18 | Bounds | Tồn tại giá trị chiều cao hợp lý theo loại (ví dụ ghế đẩu seat 0.25±0.03) nếu có tag `ref_height` | WARN |

Output: `validate_report.json` + console PASS/FAIL, exit code ≠ 0 nếu có ERROR. Có chế độ `--fix` an toàn (apply scale, set origin, triangulate) nhưng **không** tự sửa UV.

### 12.1 Checklist nghiệm thu thủ công (người)
- [ ] Đặt vào scene thử trên lưới, không lệch/chìm sàn.
- [ ] Nhìn từ camera chuẩn: silhouette đọc được ở 40% kích thước thật.
- [ ] Màu chỉ lấy từ palette, tương phản đủ với nền tier tương ứng.
- [ ] Không z-fighting, không lộ mặt trong; shadow không rò.
- [ ] Draw call không tăng (dùng chung material; GPU instancing hoạt động).
- [ ] Thử trên thiết bị mid-range (Snapdragon 6-series) vẫn ≥ 30 fps.

---

## 13. Quy trình làm một asset (tóm tắt)

1. Chọn nguồn (§8.2) → dựng/nhập trong Blender, đơn vị mét.
2. Chốt footprint + chiều cao tham chiếu (§2).
3. Model low-poly, bo tròn theo §5, xóa mặt thừa.
4. Gán palette UV (`assign_palette`) theo ô §3.
5. Đặt tên, pivot, apply transform, thêm custom props (`footprint_x/z`, `ref_height`, `front`).
6. Chạy `python tools/blender_validate.py` → sửa đến khi PASS.
7. Export FBX (§8.1) → `art_source/export/` → copy vào `game/Assets/_Project/Art/Models/...`.
8. Unity: tạo prefab `PF_*`, gán `M_Toon_Palette`, collider đơn giản, footprint data (`SO_Furniture_*`), bật GPU Instancing.
9. Review 3 góc + thiết bị thật → merge.

---

## 14. Phụ lục — Quyết định đã khoá (không đổi nếu không có Art Director duyệt)

- Palette 64 ô 8×8, `T_Palette_256.png`; 1 material `M_Toon_Palette`.
- 1 m = 1 unit; lưới 0.5 m; pivot bottom-center; +Z front.
- Tri budget theo §5; scene < 150k.
- Humanoid single rig + Mixamo retarget.
- Landscape 1920×1080 ref, touch ≥ 48 dp, font Be Vietnam Pro / Nunito.
