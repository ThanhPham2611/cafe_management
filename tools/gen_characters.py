"""Sinh khách chibi modular cho "Quán Cà Phê Việt" (art_bible §8.3, bản đơn giản: hierarchy mesh rời, không skin).

Chạy headless:
    blender --background --python tools/gen_characters.py -- [--render] [--only Student NonLa Staff_M1 ...]

Kết quả:
    art_source/export/characters/SM_Char_Customer_<Variant>.fbx   (cả hierarchy)
    art_source/export/characters/SM_Char_Owner_*.fbx, SM_Char_Staff_*.fbx
    art_source/export/characters/preview_characters*.png, preview_owners*.png, preview_staff*.png (nếu --render)
    art_source/blend/characters_tier1.blend (khách + chủ quán), characters_tier2.blend (nhân viên)
    (--only KHÔNG ghi lại .blend để khỏi mất nhân vật khác)

Hierarchy (pivot ở khớp, để Unity xoay transform):
    SM_Char_Customer_<V> (empty, ở giữa bàn chân, mặt hướng -Y => +Z Unity)
      Hips (pivot = khớp hông, cao HIP_Z; thân + chậu + phụ kiện lưng/ngực)
        Head   (pivot = cổ; đầu, tóc, mũ, mắt, má hồng, miệng)
        Arm_L / Arm_R (pivot = vai)
        Leg_L / Leg_R (pivot = hông)
Tỉ lệ: cao ~1.12–1.2 m, đầu ≈ 40% chiều cao. Ngồi: Hips hạ xuống SEAT_HIP_Z (0.08) trên mặt ghế 0.27 m.
"""
import argparse
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gen_furniture as gf  # noqa: E402
from gen_furniture import Builder, C, ROOT, get_material  # noqa: E402

EXPORT_DIR = ROOT / "art_source" / "export" / "characters"
BLEND_OUT = ROOT / "art_source" / "blend" / "characters_tier1.blend"
BLEND_OUT_T2 = ROOT / "art_source" / "blend" / "characters_tier2.blend"
SHARP_ANGLE = math.radians(40)

# ---- kích thước (m) ----
HIP_Z = 0.34          # khớp hông khi đứng
SEAT_HIP_Z = 0.08     # khớp hông (so với mặt ghế) khi ngồi
NECK_Z = 0.66
HEAD_C = Vector((0, 0, 0.89))
HEAD_R = (0.255, 0.235, 0.225)   # bán kính x,y,z -> cao 0.45 m (≈40%)
SHOULDER_X, SHOULDER_Z = 0.205, 0.625
ARM_LEN = 0.25
ARM_OUT = math.radians(12)
LEG_X = 0.085

SCALE = 1.0           # hệ số phóng cả nhân vật khi finish() (owner = OWNER_SCALE)
OWNER_SCALE = 1.05    # chủ quán cao ~1.25 m (khách ~1.12 m)

# ---- ô palette ----
SKIN = {"light": 52, "mid": 53, "brown": 54, "dark": 55}
BLACK_HAIR, BROWN_HAIR, WHITE_HAIR = 62, 7, 14
EYE, BLUSH, MOUTH, WHITE = 62, 58, 25, 63

VARIANTS = {
    # Sinh viên nam: khăn quàng đỏ, balo xanh
    "Student": dict(skin=53, hair=BLACK_HAIR, hair_style="short", top=31, bottom=27, shoes=31,
                    bottom_type="pants", extras=["backpack:26", "scarf:56"], sleeves="short"),
    # Sinh viên nữ: tóc đuôi ngựa, váy xanh, balo đỏ
    "StudentF": dict(skin=52, hair=BLACK_HAIR, hair_style="pony", top=31, bottom=27, shoes=31,
                     bottom_type="skirt", extras=["backpack:24"], sleeves="short"),
    # Dân văn phòng nam: sơ mi trắng, cà vạt đỏ
    "Office": dict(skin=52, hair=BROWN_HAIR, hair_style="short", top=31, bottom=51, shoes=0,
                   bottom_type="pants", extras=["tie:56"], sleeves="long"),
    # Dân văn phòng nữ: blouse hồng, váy than, tóc búi
    "OfficeF": dict(skin=53, hair=BLACK_HAIR, hair_style="bun", top=58, bottom=51, shoes=0,
                    bottom_type="skirt", extras=[], sleeves="short"),
    # Chú xe ôm: mũ bảo hiểm trắng, áo khoác xanh lá
    "Xeom": dict(skin=54, hair=BLACK_HAIR, hair_style="none", top=28, bottom=27, shoes=51,
                 bottom_type="pants", extras=["helmet:31"], sleeves="long"),
    # Cô bán hàng rong: nón lá, áo bà ba
    "NonLa": dict(skin=53, hair=BLACK_HAIR, hair_style="none", top=33, bottom=51, shoes=6,
                  bottom_type="pants", extras=["nonla:21"], sleeves="long"),
    # Cặp đôi teen (nam): hoodie cam
    "TeenBoy": dict(skin=53, hair=BROWN_HAIR, hair_style="short", top=59, bottom=51, shoes=31,
                    bottom_type="pants", extras=[], sleeves="long"),
    # Cặp đôi teen (nữ): hai bím tóc, áo hồng, váy tím
    "TeenGirl": dict(skin=52, hair=BLACK_HAIR, hair_style="twin", top=58, bottom=61, shoes=31,
                     bottom_type="skirt", extras=[], sleeves="short"),
    # Du khách: nón lưỡi trai cam, áo teal, quần short, máy ảnh
    "Tourist": dict(skin=52, hair=BROWN_HAIR, hair_style="short", top=60, bottom=4, shoes=31,
                    bottom_type="shorts", extras=["cap:59", "camera"], sleeves="short"),
    # Người lớn tuổi: tóc bạc, ria trắng, gile be
    "Elder": dict(skin=54, hair=WHITE_HAIR, hair_style="white", top=12, bottom=6, shoes=0,
                  bottom_type="pants", extras=["mustache"], sleeves="long"),
    # Công nhân: mũ bảo hộ vàng, áo phản quang cam
    "Worker": dict(skin=54, hair=BLACK_HAIR, hair_style="none", top=59, bottom=27, shoes=6,
                   bottom_type="pants", extras=["hardhat:30", "stripes"], sleeves="long", sleeve_col=26),
}


# ---------------------------------------------------------------- builder mở rộng

class CB(Builder):
    """Builder cho nhân vật: thêm blob (ellipsoid), tính vào object với pivot tuỳ ý."""

    def blob(self, center, radii, color, u=12, v=8, cut=None, rot=None, drop=None):
        """Ellipsoid bán kính `radii`. cut=(a,b): bỏ face có z <= a*(-y)+b (toạ độ cầu đơn vị) -> tóc/mũ."""
        pb = bmesh.new()
        bmesh.ops.create_uvsphere(pb, u_segments=u, v_segments=v, radius=1.0)
        if cut:
            a, b = cut
            dead = []
            for f in pb.faces:
                c = f.calc_center_median()
                if c.z <= a * (-c.y) + b:
                    dead.append(f)
            bmesh.ops.delete(pb, geom=dead, context="FACES")
        if drop:  # drop(c) -> True thì bỏ face (toạ độ cầu đơn vị), ví dụ chỉ giữ nửa trước
            bmesh.ops.delete(pb, geom=[f for f in pb.faces if drop(f.calc_center_median())], context="FACES")
        bmesh.ops.scale(pb, vec=Vector(radii), verts=pb.verts)
        m = Matrix.Translation(Vector(center)) @ (rot or Matrix.Identity(4))
        self._absorb(pb, color, m)

    def face_dot(self, x, dz, radii, color, push=0.965, u=8, v=6):
        """Chi tiết mặt dẹt dán lên mặt trước đầu (ellipsoid HEAD_R), xoay theo pháp tuyến."""
        rx, ry, rz = HEAD_R
        t = max(1 - (x / rx) ** 2 - (dz / rz) ** 2, 0.02)
        y = -ry * math.sqrt(t)
        n = Vector((x / rx ** 2, y / ry ** 2, dz / rz ** 2)).normalized()
        q = Vector((0, 1, 0)).rotation_difference(n).to_matrix().to_4x4()
        c = HEAD_C + Vector((x, y * push, dz))
        self.blob(c, radii, color, u=u, v=v, rot=q)

    def finish(self, name, pivot, parent, props=None):
        """Tịnh tiến mesh về pivot, tạo object (location = pivot, thế giới) rồi gắn parent giữ nguyên vị trí."""
        bm = self.bm
        pivot = Vector(pivot) * SCALE
        if SCALE != 1.0:
            bmesh.ops.scale(bm, vec=Vector((SCALE,) * 3), verts=bm.verts, space=Matrix.Identity(4))
        bmesh.ops.translate(bm, vec=-pivot, verts=bm.verts)
        bm.normal_update()
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        for f in bm.faces:
            f.smooth = True
        for e in bm.edges:
            e.smooth = len(e.link_faces) == 2 and e.calc_face_angle(math.pi) < SHARP_ANGLE
        if not bm.verts:  # khớp rỗng (empty)
            obj = bpy.data.objects.new(name, None)
            obj.empty_display_type = "PLAIN_AXES"
            obj.empty_display_size = 0.05
        else:
            mesh = bpy.data.meshes.new(name)
            bm.to_mesh(mesh)
            mesh.materials.append(get_material())
            obj = bpy.data.objects.new(name, mesh)
        bm.free()
        bpy.context.scene.collection.objects.link(obj)
        obj.location = pivot
        bpy.context.view_layer.update()
        if parent is not None:
            obj.parent = parent
            obj.matrix_parent_inverse = parent.matrix_world.inverted()
        for k, v in (props or {}).items():
            obj[k] = v
        return obj


# ---------------------------------------------------------------- phần cơ thể

def build_head(s):
    b = CB()
    b.blob(HEAD_C, HEAD_R, s["skin"], u=16, v=10)
    for sx in (-1, 1):  # tai
        b.blob(HEAD_C + Vector((sx * 0.245, 0.0, -0.03)), (0.04, 0.03, 0.05), s["skin"], u=6, v=4)
    # Mặt: mắt dot to + highlight, má hồng, miệng nhỏ
    for sx in (-1, 1):
        b.face_dot(sx * 0.098, -0.03, (0.032, 0.015, 0.042), EYE, u=8, v=6)
        b.face_dot(sx * 0.098 + 0.011, -0.012, (0.011, 0.01, 0.013), WHITE, push=0.99, u=6, v=4)
        b.face_dot(sx * 0.165, -0.085, (0.04, 0.012, 0.025), BLUSH, u=8, v=4)
    b.face_dot(0.0, -0.115, (0.03, 0.011, 0.014), MOUTH, u=8, v=4)
    ex = s["extras"]
    if "mustache" in ex:
        b.face_dot(0.0, -0.088, (0.07, 0.014, 0.018), WHITE_HAIR, push=0.985, u=8, v=4)
    if "brows" in ex:
        for sx in (-1, 1):
            b.face_dot(sx * 0.098, 0.035, (0.045, 0.012, 0.011), WHITE_HAIR, push=0.985, u=6, v=4)
    _hair(b, s)
    for e in ex:
        kind, _, col = e.partition(":")
        col = int(col) if col else 0
        if kind == "cap":
            _cap(b, col)
        elif kind == "helmet":
            _helmet(b, col)
        elif kind == "hardhat":
            _hardhat(b, col)
        elif kind == "nonla":
            _nonla(b, col)
    return b


def _hair(b, s):
    st, col = s["hair_style"], s["hair"]
    hc = HEAD_C
    hr = HEAD_R
    k = 1.07
    if st == "none":
        return
    if st == "white":
        b.blob(hc + Vector((0, 0.03, 0.01)), (hr[0] * 1.05, hr[1] * 1.05, hr[2] * 1.05), col, u=14, v=8,
               cut=(0.25, 0.30))
        return
    b.blob(hc + Vector((0, 0.02, 0.015)), (hr[0] * k, hr[1] * k, hr[2] * k), col, u=16, v=9, cut=(0.4, 0.06))
    if st == "bun":
        b.blob(hc + Vector((0, 0.06, 0.255)), (0.085, 0.085, 0.075), col, u=10, v=6)
    elif st == "pony":
        b.blob(hc + Vector((0, 0.235, 0.06)), (0.07, 0.07, 0.07), col, u=8, v=6)
        b.blob(hc + Vector((0, 0.29, -0.06)), (0.06, 0.06, 0.13), col, u=8, v=6)
        b.blob(hc + Vector((0, 0.225, 0.05)), (0.05, 0.03, 0.05), 56, u=6, v=4)  # chun buộc đỏ
    elif st == "twin":
        for sx in (-1, 1):
            b.blob(hc + Vector((sx * 0.26, 0.06, -0.01)), (0.075, 0.075, 0.075), col, u=8, v=6)
            b.blob(hc + Vector((sx * 0.29, 0.07, -0.13)), (0.065, 0.065, 0.12), col, u=8, v=6)
            b.blob(hc + Vector((sx * 0.265, 0.045, 0.045)), (0.04, 0.04, 0.03), 56, u=6, v=4)
    elif st == "bob":
        b.blob(hc + Vector((0, 0.12, -0.12)), (0.24, 0.14, 0.19), col, u=12, v=7)


def _cap(b, col):
    hc, hr = HEAD_C, HEAD_R
    k = 1.1
    b.blob(hc + Vector((0, 0.015, 0.02)), (hr[0] * k, hr[1] * k, hr[2] * k), col, u=16, v=9, cut=(0.55, 0.20))
    b.rounded_slab(0.34, 0.26, hc.z + 0.115, hc.z + 0.145, col, corner=0.11, edge=0.01, cx=0.0, cy=-0.255)
    b.cylinder(0.025, 0.025, hc.z + 0.235, hc.z + 0.255, 57, segs=8)  # núm trên mũ


def _helmet(b, col):
    hc, hr = HEAD_C, HEAD_R
    k = 1.13
    b.blob(hc + Vector((0, 0.015, 0.025)), (hr[0] * k, hr[1] * k, hr[2] * k), col, u=16, v=9, cut=(0.3, -0.12))
    b.rounded_slab(0.36, 0.2, hc.z + 0.085, hc.z + 0.115, 51, corner=0.09, edge=0.01, cx=0.0, cy=-0.25)  # kính che
    b.rbox((0.06, 0.4, 0.03), (0, 0.0, hc.z + 0.255), 24, r=0.01, segs=1)  # sọc đỏ trên mũ


def _hardhat(b, col):
    hc, hr = HEAD_C, HEAD_R
    k = 1.1
    b.blob(hc + Vector((0, 0.01, 0.025)), (hr[0] * k, hr[1] * k, hr[2] * k), col, u=16, v=9, cut=(0.45, 0.12))
    b.rounded_slab(0.62, 0.62, hc.z + 0.085, hc.z + 0.112, col, corner=0.28, edge=0.01, cy=-0.02)
    b.rbox((0.05, 0.3, 0.035), (0, 0.0, hc.z + 0.25), col, r=0.012, segs=1)


def _nonla(b, col):
    """Nón lá: nón nón cao 0.2 m, đường kính 0.84 m (cường điệu để đọc rõ từ trên xuống) + quai hồng."""
    hc = HEAD_C
    z0 = hc.z + 0.125
    b.cylinder(0.43, 0.02, z0, z0 + 0.21, col, segs=16, round_r=0.0)
    b.cylinder(0.435, 0.435, z0 - 0.012, z0 + 0.012, 5, segs=16, round_r=0.006)  # vành sậm màu caramel
    b.cylinder(0.04, 0.025, z0 + 0.2, z0 + 0.235, 5, segs=8, round_r=0.01)       # chóp
    for sx in (-1, 1):  # quai
        b.rod((sx * 0.215, -0.03, z0 - 0.02), (sx * 0.075, -0.19, hc.z - 0.165), 0.011, 58)


def build_hips(s):
    b = CB()
    top, bottom = s["top"], s["bottom"]
    b.blob((0, 0, 0.5), (0.18, 0.145, 0.19), top, u=12, v=8)                 # thân
    b.blob((0, 0, 0.355), (0.165, 0.13, 0.095), bottom, u=12, v=6)          # chậu / quần
    if s["bottom_type"] == "skirt":
        b.cylinder(0.215, 0.16, 0.20, 0.42, bottom, segs=14, round_r=0.02, round_top=False)
    for e in s["extras"]:
        kind, _, col = e.partition(":")
        col = int(col) if col else 0
        if kind == "backpack":
            b.rbox((0.27, 0.13, 0.31), (0, 0.185, 0.52), col, r=0.04, segs=2)
            b.rbox((0.17, 0.05, 0.12), (0, 0.255, 0.46), 30 if col != 30 else 24, r=0.02, segs=1)
            for sx in (-1, 1):  # quai balo
                b.rod((sx * 0.1, 0.1, 0.66), (sx * 0.11, -0.11, 0.6), 0.016, col)
        elif kind == "tie":
            b.rbox((0.05, 0.02, 0.17), (0, -0.147, 0.49), col, r=0.008, segs=1)
            b.rbox((0.06, 0.025, 0.04), (0, -0.14, 0.60), col, r=0.01, segs=1)
        elif kind == "scarf":
            b.cylinder(0.125, 0.115, 0.625, 0.69, col, segs=12, round_r=0.02)
            b.rbox((0.07, 0.03, 0.14), (0, -0.115, 0.58), col, r=0.012, segs=1)
        elif kind == "camera":
            b.rbox((0.15, 0.07, 0.095), (0.05, -0.17, 0.46), 51, r=0.02, segs=1)
            b.cylinder(0.034, 0.034, -0.03, 0.03, 48, segs=10, center=(0.05, -0.21, 0.46),
                       axis=Matrix.Rotation(math.radians(90), 4, "X"), round_r=0.01)
            b.rod((-0.1, -0.1, 0.66), (0.0, -0.15, 0.49), 0.011, 51)
            b.rod((0.1, -0.1, 0.66), (0.05, -0.15, 0.50), 0.011, 51)
        elif kind == "stripes":
            for sx in (-1, 1):
                b.rbox((0.055, 0.03, 0.2), (sx * 0.075, -0.14, 0.5), 31, r=0.012, segs=1)
            b.rbox((0.3, 0.03, 0.045), (0, -0.138, 0.43), 31, r=0.012, segs=1)
    return b


def build_arm(s, sx):
    b = CB()
    shoulder = Vector((sx * SHOULDER_X, 0, SHOULDER_Z))
    d = Vector((sx * math.sin(ARM_OUT), 0, -math.cos(ARM_OUT)))
    end = shoulder + d * ARM_LEN
    sleeve_col = s.get("sleeve_col", s["top"])
    skin = s["skin"]
    b.blob(shoulder, (0.072, 0.07, 0.07), sleeve_col, u=8, v=6)
    if s["sleeves"] == "long":
        b.leg(shoulder, end, 0.06, 0.052, sleeve_col, segs=8)
    else:
        mid = shoulder + d * ARM_LEN * 0.5
        b.leg(shoulder, mid, 0.06, 0.056, sleeve_col, segs=8)
        b.leg(mid, end, 0.056, 0.05, skin, segs=8)
    b.blob(end, (0.062, 0.062, 0.062), skin, u=8, v=6)
    return b, shoulder


def build_leg(s, sx):
    b = CB()
    hip = Vector((sx * LEG_X, 0, HIP_Z))
    ankle = Vector((sx * LEG_X, 0, 0.065))
    bt = s["bottom_type"]
    skin, pants = s["skin"], s["bottom"]
    b.blob(hip, (0.085, 0.085, 0.085), skin if bt == "skirt" else pants, u=8, v=6)
    if bt == "pants":
        b.leg(hip, ankle, 0.078, 0.062, pants, segs=8)
    elif bt == "shorts":
        mid = hip + (ankle - hip) * 0.45
        b.leg(hip, mid, 0.08, 0.075, pants, segs=8)
        b.leg(mid, ankle, 0.07, 0.058, skin, segs=8)
    else:  # skirt: chân trần + tất
        mid = hip + (ankle - hip) * 0.7
        b.leg(hip, mid, 0.07, 0.062, skin, segs=8)
        b.leg(mid, ankle, 0.062, 0.058, 31, segs=8)
    b.blob(Vector((sx * LEG_X, -0.035, 0.05)), (0.072, 0.115, 0.05), s["shoes"], u=10, v=6)
    return b, hip


def build_character(variant):
    s = VARIANTS[variant]
    fname = f"SM_Char_Customer_{variant}"
    root = bpy.data.objects.new(fname, None)
    root.empty_display_type = "PLAIN_AXES"
    bpy.context.scene.collection.objects.link(root)
    root["category"] = "character"
    root["front"] = "-Y"
    root["ref_height"] = 1.1
    root["hip_height"] = HIP_Z
    root["seat_hip_height"] = SEAT_HIP_Z
    bpy.context.view_layer.update()

    hips = build_hips(s).finish("Hips", (0, 0, HIP_Z), root)
    build_head(s).finish("Head", (0, 0, NECK_Z), hips)
    for sx, side in ((1, "L"), (-1, "R")):  # nhân vật nhìn -Y: bên trái của nó là +X
        ab, sp = build_arm(s, sx)
        ab.finish(f"Arm_{side}", sp, hips)
        lb, hp = build_leg(s, sx)
        lb.finish(f"Leg_{side}", hp, hips)
    return root


# ---------------------------------------------------------------- chủ quán (owner / người chơi)
# Mỗi phần có thể recolor lúc chạy là một mesh RIÊNG, MỘT màu: "Shirt*" (áo), "Hair*" (tóc), "Skin*" (da).
# Tạp dề, mắt, miệng, giày... là object riêng màu cố định. Khớp (Hips/Head/Arm_*/Leg_*) là empty;
# HandSocket_R là empty con của Arm_R ở bàn tay phải (chỗ cầm ly).

OWNER_APRON, OWNER_APRON_POCKET = 6, 3     # cacao + cà phê sữa (túi)

OWNERS = {
    # Nam 1: tóc đen rẽ ngôi lệch, áo trắng, quần than
    "Owner_M1": dict(skin=53, hair=BLACK_HAIR, hair_style="sidepart", shirt=31, pants=51, shoes=31,
                     bottom_type="pants", sleeves="short", extras=[]),
    # Nam 2: tóc nâu xù bồng, kính, áo teal xắn tay, quần jean
    "Owner_M2": dict(skin=52, hair=BROWN_HAIR, hair_style="quiff", shirt=60, pants=42, shoes=6,
                     bottom_type="pants", sleeves="rolled", extras=["glasses"]),
    # Nữ 1: tóc đen đuôi ngựa dài + chun cam, áo hồng, quần short be
    "Owner_F1": dict(skin=52, hair=BLACK_HAIR, hair_style="pony", shirt=58, pants=12, shoes=31,
                     bottom_type="shorts", sleeves="short", extras=["scrunchie"]),
    # Nữ 2: tóc nâu bob mái ngang + kẹp hoa, áo kem, chân váy jean, giày đỏ
    "Owner_F2": dict(skin=53, hair=BROWN_HAIR, hair_style="bob", shirt=10, pants=42, shoes=24,
                     bottom_type="skirt", sleeves="short", extras=["clip"]),
}


# ---------------------------------------------------------------- nhân viên (Tier 2)
# Cùng rig/hierarchy/mesh tách màu với chủ quán (build_owner dùng chung), nhưng đồng phục CỐ ĐỊNH để đọc rõ là
# nhân viên: polo teal (mesh "Shirt", runtime vẫn recolor được), tạp dề NGẮN quấn eo xanh lá tối + túi (khác tạp
# dề yếm nâu của chủ), nón lưỡi trai có logo (M1 / F1, F1 đuôi ngựa thò ra sau) hoặc băng đô (M2 / F2 có nơ vàng).
# Quần dài than + giày trắng cho cả 4 (chủ quán không ai mặc quần dài than + tạp dề xanh).
STAFF_SCALE = 1.02
STAFF_SHIRT, STAFF_UNI, STAFF_UNI_LT, STAFF_ACCENT, STAFF_COLLAR = 60, 29, 33, 57, 14

STAFF = {
    # Nam 1: tóc đen cắt ngắn dưới nón, da nâu
    "Staff_M1": dict(skin=54, hair=BLACK_HAIR, hair_style="crop", shirt=STAFF_SHIRT, pants=51, shoes=31,
                     bottom_type="pants", sleeves="short", extras=[], headwear="cap", female=False),
    # Nam 2: tóc nâu vuốt ngược, băng đô xanh lá
    "Staff_M2": dict(skin=52, hair=BROWN_HAIR, hair_style="swept", shirt=STAFF_SHIRT, pants=51, shoes=31,
                     bottom_type="pants", sleeves="short", extras=[], headwear="headband", female=False),
    # Nữ 1: tóc đen đuôi ngựa thấp thò sau nón, chun vàng
    "Staff_F1": dict(skin=53, hair=BLACK_HAIR, hair_style="lowpony", shirt=STAFF_SHIRT, pants=51, shoes=31,
                     bottom_type="pants", sleeves="short", extras=["staff_tie"], headwear="cap", female=True),
    # Nữ 2: tóc nâu hai búi, băng đô + nơ vàng
    "Staff_F2": dict(skin=55, hair=BROWN_HAIR, hair_style="buns", shirt=STAFF_SHIRT, pants=51, shoes=31,
                     bottom_type="pants", sleeves="short", extras=[], headwear="headband", female=True),
}


def _ry(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Y")


def _rx(deg):
    return Matrix.Rotation(math.radians(deg), 4, "X")


def _rz(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Z")


def owner_skin_head(s):
    b = CB()
    b.blob(HEAD_C, HEAD_R, s["skin"], u=14, v=9)
    for sx in (-1, 1):
        b.blob(HEAD_C + Vector((sx * 0.245, 0.0, -0.03)), (0.04, 0.03, 0.05), s["skin"], u=6, v=4)
    b.blob(HEAD_C + Vector((0, -0.222, -0.065)), (0.028, 0.02, 0.022), s["skin"], u=6, v=4)   # mũi
    b.blob(Vector((0, 0, NECK_Z + 0.02)), (0.06, 0.055, 0.05), s["skin"], u=8, v=4)            # cổ
    return b


def owner_eyes(s):
    b = CB()
    female = s.get("female", s["hair_style"] in ("pony", "bob"))
    for sx in (-1, 1):
        b.face_dot(sx * 0.098, -0.03, (0.037, 0.016, 0.049), EYE, u=8, v=6)
        if female:  # mi cong
            b.face_dot(sx * 0.137, -0.005, (0.022, 0.012, 0.008), EYE, push=0.98, u=6, v=4)
    return b


def owner_shine():
    b = CB()
    for sx in (-1, 1):
        b.face_dot(sx * 0.098 + 0.012, -0.010, (0.012, 0.01, 0.015), WHITE, push=0.99, u=6, v=4)
        b.face_dot(sx * 0.098 - 0.01, -0.052, (0.006, 0.006, 0.007), WHITE, push=0.99, u=5, v=3)
    return b


def owner_blush():
    b = CB()
    for sx in (-1, 1):
        b.face_dot(sx * 0.168, -0.085, (0.042, 0.012, 0.026), BLUSH, u=8, v=4)
    return b


def owner_mouth():
    b = CB()
    b.face_dot(0.0, -0.118, (0.034, 0.011, 0.016), MOUTH, u=8, v=4)
    return b


def owner_hair(s):
    st, col = s["hair_style"], s["hair"]
    b = CB()
    hc, hr = HEAD_C, HEAD_R
    k = 1.07
    b.blob(hc + Vector((0, 0.02, 0.015)), (hr[0] * k, hr[1] * k, hr[2] * k), col, u=14, v=8, cut=(0.4, 0.06))
    for sx in (-1, 1):  # lông mày (cùng màu tóc)
        b.face_dot(sx * 0.098, 0.068, (0.03, 0.01, 0.007), col, push=0.985, u=6, v=3)

    def fringe(x, dz, rx, rz, rotz=0.0):
        t = max(1 - (x / hr[0]) ** 2 - (dz / hr[2]) ** 2, 0.05)
        y = -hr[1] * math.sqrt(t) * 0.97
        b.blob(hc + Vector((x, y, dz)), (rx, 0.05, rz), col, u=8, v=5, rot=_ry(rotz))

    if st == "sidepart":
        for x, dz, rx, rz, rot in ((-0.17, 0.13, 0.085, 0.06, 28), (-0.07, 0.15, 0.085, 0.06, 18),
                                   (0.03, 0.155, 0.08, 0.05, 8), (0.12, 0.14, 0.07, 0.05, -5)):
            fringe(x, dz, rx, rz, rot)
        b.blob(hc + Vector((-0.1, -0.1, 0.235)), (0.12, 0.1, 0.06), col, u=8, v=5, rot=_ry(-14))
    elif st == "quiff":
        b.blob(hc + Vector((0, -0.1, 0.235)), (0.15, 0.13, 0.085), col, u=10, v=6, rot=_rx(18))
        for x, dz, rx, rz, rot in ((-0.13, 0.12, 0.07, 0.06, 20), (0.0, 0.135, 0.07, 0.07, 0),
                                   (0.13, 0.12, 0.07, 0.06, -20)):
            fringe(x, dz, rx, rz, rot)
        for sx in (-1, 1):
            b.blob(hc + Vector((sx * 0.232, 0.0, -0.01)), (0.032, 0.05, 0.075), col, u=6, v=4)
        b.blob(hc + Vector((0.0, 0.2, 0.0)), (0.17, 0.08, 0.15), col, u=8, v=5)
    elif st == "pony":
        for x, dz, rx, rz, rot in ((-0.15, 0.12, 0.075, 0.06, 20), (-0.05, 0.145, 0.07, 0.055, 8),
                                   (0.05, 0.145, 0.07, 0.055, -8), (0.15, 0.12, 0.075, 0.06, -20)):
            fringe(x, dz, rx, rz, rot)
        for sx in (-1, 1):  # lọn tóc ôm mặt
            b.blob(hc + Vector((sx * 0.235, -0.05, -0.06)), (0.035, 0.045, 0.1), col, u=6, v=4)
        b.blob(hc + Vector((0, 0.235, 0.075)), (0.075, 0.07, 0.07), col, u=8, v=6)
        b.blob(hc + Vector((0, 0.3, -0.04)), (0.075, 0.07, 0.17), col, u=8, v=6, rot=_rx(-12))
        b.blob(hc + Vector((0, 0.335, -0.2)), (0.06, 0.05, 0.1), col, u=8, v=5, rot=_rx(-18))
    elif st == "bob":
        for x, dz, rx, rz, rot in ((-0.17, 0.115, 0.075, 0.075, 15), (-0.06, 0.13, 0.07, 0.07, 4),
                                   (0.06, 0.13, 0.07, 0.07, -4), (0.17, 0.115, 0.075, 0.075, -15)):
            fringe(x, dz, rx, rz, rot)
        b.blob(hc + Vector((0, 0.1, -0.1)), (0.255, 0.16, 0.2), col, u=12, v=7)
        for sx in (-1, 1):
            b.blob(hc + Vector((sx * 0.235, -0.04, -0.085)), (0.045, 0.075, 0.14), col, u=6, v=5)
    elif st == "crop":        # nhân viên: tóc ngắn gọn, phần lộ dưới nón là mai + gáy
        for sx in (-1, 1):
            b.blob(hc + Vector((sx * 0.232, 0.0, -0.02)), (0.032, 0.05, 0.075), col, u=6, v=4)
        b.blob(hc + Vector((0, 0.15, -0.095)), (0.2, 0.11, 0.115), col, u=10, v=6)
    elif st == "swept":       # nhân viên: tóc vuốt ngược (không mái), băng đô che chân tóc
        b.blob(hc + Vector((0, -0.04, 0.215)), (0.165, 0.15, 0.055), col, u=10, v=6, rot=_rx(10))
        for sx in (-1, 1):
            b.blob(hc + Vector((sx * 0.232, 0.0, -0.02)), (0.032, 0.05, 0.075), col, u=6, v=4)
        b.blob(hc + Vector((0, 0.15, -0.09)), (0.2, 0.11, 0.11), col, u=10, v=6)
    elif st == "lowpony":     # nhân viên: đuôi ngựa thấp thò ra sau nón
        for sx in (-1, 1):
            b.blob(hc + Vector((sx * 0.235, -0.03, -0.05)), (0.035, 0.045, 0.1), col, u=6, v=4)
        b.blob(hc + Vector((0, 0.15, -0.095)), (0.2, 0.11, 0.115), col, u=10, v=6)
        b.blob(hc + Vector((0, 0.29, -0.03)), (0.07, 0.065, 0.07), col, u=8, v=5)
        b.blob(hc + Vector((0, 0.335, -0.15)), (0.065, 0.055, 0.15), col, u=8, v=6, rot=_rx(-12))
    elif st == "buns":        # nhân viên: hai búi tròn trên đầu, băng đô ở dưới búi
        for sx in (-1, 1):
            b.blob(hc + Vector((sx * 0.232, 0.0, -0.03)), (0.034, 0.05, 0.09), col, u=6, v=4)
            b.blob(hc + Vector((sx * 0.135, 0.03, 0.215)), (0.078, 0.078, 0.072), col, u=8, v=6)
        b.blob(hc + Vector((0, 0.15, -0.09)), (0.2, 0.11, 0.11), col, u=10, v=6)
    return b


def owner_accessories(s):
    """Phụ kiện màu cố định -> dict tên object -> CB (con của Head)."""
    out = {}
    hc = HEAD_C
    for e in s["extras"]:
        if e == "glasses":
            b = CB()
            fy = -0.236
            for sx in (-1, 1):
                cx, cz = sx * 0.098, hc.z - 0.03
                w, h, t = 0.125, 0.1, 0.011
                b.rbox((w, 0.014, t), (cx, fy, cz + h / 2), 51, r=0)
                b.rbox((w, 0.014, t), (cx, fy, cz - h / 2), 51, r=0)
                b.rbox((t, 0.014, h), (cx - w / 2, fy, cz), 51, r=0)
                b.rbox((t, 0.014, h), (cx + w / 2, fy, cz), 51, r=0)
                b.rod((sx * 0.158, fy + 0.012, cz + 0.02), (sx * 0.25, -0.03, hc.z - 0.01), 0.007, 51, segs=4)
            b.rbox((0.04, 0.014, 0.011), (0, fy, hc.z - 0.005), 51, r=0)
            out["Glasses"] = b
        elif e == "scrunchie":
            b = CB()
            b.blob(hc + Vector((0, 0.275, 0.05)), (0.092, 0.08, 0.06), 59, u=10, v=6)
            out["Scrunchie"] = b
        elif e == "clip":
            b = CB()
            c = hc + Vector((0.185, -0.15, 0.105))
            for i in range(5):
                a = 2 * math.pi * i / 5
                b.blob(c + Vector((math.cos(a) * 0.036, -0.012, math.sin(a) * 0.036)), (0.026, 0.014, 0.026),
                       58, u=6, v=4)
            out["Clip"] = b
            b2 = CB()
            b2.blob(c + Vector((0, -0.024, 0)), (0.018, 0.012, 0.018), 57, u=6, v=4)
            out["ClipCenter"] = b2
    return out


def owner_shirt(s):
    b = CB()
    b.blob((0, 0, 0.5), (0.18, 0.145, 0.19), s["shirt"], u=12, v=8)
    b.blob((0, 0, 0.665), (0.105, 0.09, 0.04), s["shirt"], u=10, v=5)   # cổ áo
    return b


def owner_pants(s):
    b = CB()
    b.blob((0, 0, 0.355), (0.165, 0.13, 0.095), s["pants"], u=12, v=6)
    return b


def owner_skirt(s):
    b = CB()
    b.cylinder(0.225, 0.165, 0.19, 0.42, s["pants"], segs=14, round_r=0.025, round_top=False)
    return b


def owner_apron():
    b = CB()
    b.blob(Vector((0, -0.02, 0.45)), (0.192, 0.165, 0.155), OWNER_APRON, u=12, v=7,
           drop=lambda f: f.y > 0.12)
    for sx in (-1, 1):  # dây đeo cổ
        b.rod((sx * 0.085, -0.14, 0.58), (sx * 0.05, 0.02, 0.705), 0.013, OWNER_APRON, segs=5)
        b.rod((sx * 0.05, 0.02, 0.705), (sx * 0.02, 0.07, 0.69), 0.013, OWNER_APRON, segs=5)
    for sx in (-1, 1):  # nơ buộc sau lưng
        b.blob((sx * 0.06, 0.16, 0.42), (0.06, 0.03, 0.04), OWNER_APRON, u=6, v=4, rot=_ry(sx * 20))
        b.blob((sx * 0.065, 0.165, 0.37), (0.025, 0.02, 0.07), OWNER_APRON, u=6, v=4)
    return b


def owner_apron_pocket():
    b = CB()
    b.rbox((0.13, 0.03, 0.075), (0, -0.188, 0.40), OWNER_APRON_POCKET, r=0.012, segs=1)
    return b


def owner_apron_logo():
    b = CB()
    b.cylinder(0.034, 0.034, -0.01, 0.01, 57, segs=12, center=(0, -0.2, 0.5),
               axis=Matrix.Rotation(math.radians(90), 4, "X"), round_r=0.004)
    return b


def _arm_dir(sx):
    return Vector((sx * math.sin(ARM_OUT), 0, -math.cos(ARM_OUT)))


def owner_sleeve(s, sx):
    b = CB()
    shoulder = Vector((sx * SHOULDER_X, 0, SHOULDER_Z))
    frac = 0.72 if s["sleeves"] == "rolled" else 0.5
    mid = shoulder + _arm_dir(sx) * ARM_LEN * frac
    b.blob(shoulder, (0.074, 0.072, 0.072), s["shirt"], u=8, v=6)
    b.leg(shoulder, mid, 0.062, 0.057, s["shirt"], segs=8)
    return b, mid


def owner_skin_arm(s, sx, mid):
    b = CB()
    end = Vector((sx * SHOULDER_X, 0, SHOULDER_Z)) + _arm_dir(sx) * ARM_LEN
    b.leg(mid, end, 0.056, 0.05, s["skin"], segs=8)
    b.blob(end, (0.064, 0.064, 0.064), s["skin"], u=8, v=6)
    return b, end


# ---- đồng phục nhân viên (chỉ dùng cho STAFF) ----

def _ellipse_band(b, ctr, radii, z0, z1, color, k=1.05, segs=16):
    """Vành/dải bao quanh một ellipsoid (tâm ctr, bán kính radii) từ độ cao tuyệt đối z0..z1, nở ra k lần
    (băng đô quanh tóc, đai tạp dề quanh thân). Bán kính đổi theo độ cao nên ôm đúng đường cong."""
    pb = bmesh.new()
    bmesh.ops.create_cone(pb, cap_ends=True, cap_tris=False, segments=segs, radius1=1.0, radius2=1.0,
                          depth=z1 - z0)
    zc = (z0 + z1) / 2
    for v in pb.verts:
        z = zc + v.co.z
        t = math.sqrt(max(1 - ((z - ctr[2]) / radii[2]) ** 2, 0.02))
        v.co = Vector((ctr[0] + v.co.x * radii[0] * t * k, ctr[1] + v.co.y * radii[1] * t * k, z))
    b._absorb(pb, color)


def staff_collar():
    """Cổ polo kem (2 cánh) + nẹp áo; màu cố định."""
    b = CB()
    for sx in (-1, 1):
        b.blob((sx * 0.06, -0.086, 0.652), (0.058, 0.022, 0.034), STAFF_COLLAR, u=8, v=5, rot=_ry(30 * sx))
    b.rbox((0.018, 0.012, 0.1), (0, -0.126, 0.6), STAFF_COLLAR, r=0.004, segs=1)
    return b


def staff_apron():
    """Tạp dề ngắn quấn eo xanh lá tối: tấm trước hơi loe + đai eo + nơ sau lưng."""
    b = CB()
    b.rbox((0.29, 0.034, 0.21), (0, -0.143, 0.335), STAFF_UNI, r=0.025, segs=1, rot=_rx(-6))
    _ellipse_band(b, (0, 0, 0.5), (0.18, 0.145, 0.19), 0.425, 0.465, STAFF_UNI, k=1.05, segs=16)
    for sx in (-1, 1):
        b.blob((sx * 0.06, 0.155, 0.44), (0.06, 0.03, 0.04), STAFF_UNI, u=6, v=4, rot=_ry(sx * 20))
        b.blob((sx * 0.065, 0.16, 0.39), (0.025, 0.02, 0.065), STAFF_UNI, u=6, v=4)
    return b


def staff_apron_pocket():
    b = CB()
    b.rbox((0.13, 0.026, 0.07), (0, -0.168, 0.30), STAFF_UNI_LT, r=0.01, segs=1)
    return b


def staff_badge():
    """Huy hiệu vàng + bảng tên kem trên ngực."""
    b = CB()
    b.cylinder(0.026, 0.026, -0.008, 0.008, STAFF_ACCENT, segs=10, center=(0.078, -0.124, 0.575),
               axis=Matrix.Rotation(math.radians(90), 4, "X"), round_r=0.003)
    b.rbox((0.06, 0.012, 0.028), (-0.075, -0.1235, 0.575), STAFF_COLLAR, r=0.004, segs=1)
    return b


def staff_headwear(s):
    """Phụ kiện đầu cố định màu -> dict tên object -> CB (con của Head)."""
    out = {}
    hc, hr = HEAD_C, HEAD_R
    if s["headwear"] == "cap":
        b = CB()                      # nón lưỡi trai: chỏm tròn trước thấp sau cao + lưỡi trai
        k = 1.1
        b.blob(hc + Vector((0, 0.015, 0.02)), (hr[0] * k, hr[1] * k, hr[2] * k), STAFF_UNI, u=14, v=9,
               cut=(0.55, 0.20))
        b.rounded_slab(0.34, 0.26, hc.z + 0.115, hc.z + 0.145, STAFF_UNI, corner=0.11, edge=0.01, cx=0.0,
                       cy=-0.255)
        out["Cap"] = b
        lg = CB()                     # logo tròn vàng ở mặt trước nón + núm chóp
        lg.cylinder(0.037, 0.037, -0.007, 0.007, STAFF_ACCENT, segs=10, center=(0, -0.162, hc.z + 0.2045),
                    axis=Matrix.Rotation(math.radians(39.2), 4, "X"), round_r=0.003)
        lg.cylinder(0.026, 0.02, hc.z + 0.255, hc.z + 0.285, STAFF_SHIRT, segs=8, center=(0, 0.015), round_r=0.006)
        out["CapLogo"] = lg
    else:
        b = CB()                      # băng đô: dải ôm chân tóc (theo ellipsoid của tóc, nở 5%)
        ctr = hc + Vector((0, 0.02, 0.015))
        radii = (hr[0] * 1.07, hr[1] * 1.07, hr[2] * 1.07)
        _ellipse_band(b, ctr, radii, hc.z + 0.10, hc.z + 0.152, STAFF_UNI, k=1.05, segs=18)
        out["Headband"] = b
        if s["female"]:               # nơ vàng bên trái (+X) của nhân vật
            bw = CB()
            c = hc + Vector((0.205, -0.15, 0.127))
            ang = _rz(46)
            for sg in (-1, 1):
                bw.blob(c + ang @ Vector((sg * 0.05, 0, 0.0)) + Vector((0, 0, 0.012)),
                        (0.05, 0.021, 0.03), STAFF_ACCENT, u=6, v=4, rot=ang)
            bw.blob(c, (0.026, 0.026, 0.026), STAFF_ACCENT, u=6, v=4)
            out["Bow"] = bw
    for e in s["extras"]:
        if e == "staff_tie":          # chun vàng buộc đuôi ngựa
            t = CB()
            t.blob(hc + Vector((0, 0.285, -0.03)), (0.09, 0.075, 0.06), STAFF_ACCENT, u=10, v=6)
            out["Scrunchie"] = t
    return out


def build_owner(key):
    """Chủ quán / nhân viên: Root > Hips > (Head, Arm_L/R, Leg_L/R); các mesh con tách theo màu (xem đầu mục)."""
    global SCALE
    is_staff = key in STAFF
    SCALE = STAFF_SCALE if is_staff else OWNER_SCALE
    try:
        s = STAFF[key] if is_staff else OWNERS[key]
        root = bpy.data.objects.new(f"SM_Char_{key}", None)
        root.empty_display_type = "PLAIN_AXES"
        bpy.context.scene.collection.objects.link(root)
        root["category"] = "character"
        root["front"] = "-Y"
        root["ref_height"] = 1.2 if is_staff else 1.25
        root["hip_height"] = HIP_Z * SCALE
        root["seat_hip_height"] = SEAT_HIP_Z
        bpy.context.view_layer.update()

        hips = CB().finish("Hips", (0, 0, HIP_Z), root)
        head = CB().finish("Head", (0, 0, NECK_Z), hips)
        arms, legs = {}, {}
        for sx, side in ((1, "L"), (-1, "R")):
            arms[side] = CB().finish(f"Arm_{side}", (sx * SHOULDER_X, 0, SHOULDER_Z), hips)
            legs[side] = CB().finish(f"Leg_{side}", (sx * LEG_X, 0, HIP_Z), hips)

        def put(builder, name, parent, pivot):
            if not builder.bm.verts:
                builder.bm.free()
                return None
            return builder.finish(name, pivot, parent)

        hp = (0, 0, HIP_Z)
        put(owner_shirt(s), "Shirt", hips, hp)
        put(owner_pants(s), "Pants", hips, hp)
        if s["bottom_type"] == "skirt":
            put(owner_skirt(s), "Skirt", hips, hp)
        if is_staff:
            put(staff_collar(), "Collar", hips, hp)
            put(staff_apron(), "Apron", hips, hp)
            put(staff_apron_pocket(), "ApronPocket", hips, hp)
            put(staff_badge(), "Badge", hips, hp)
        else:
            put(owner_apron(), "Apron", hips, hp)
            put(owner_apron_pocket(), "ApronPocket", hips, hp)
            put(owner_apron_logo(), "ApronLogo", hips, hp)

        hdp = (0, 0, NECK_Z)
        put(owner_skin_head(s), "Skin_Head", head, hdp)
        put(owner_hair(s), "Hair", head, hdp)
        put(owner_eyes(s), "Eyes", head, hdp)
        put(owner_shine(), "EyeShine", head, hdp)
        put(owner_blush(), "Blush", head, hdp)
        put(owner_mouth(), "Mouth", head, hdp)
        for nm, bld in owner_accessories(s).items():
            put(bld, nm, head, hdp)
        if is_staff:
            for nm, bld in staff_headwear(s).items():
                put(bld, nm, head, hdp)

        bt = s["bottom_type"]
        for sx, side in ((1, "L"), (-1, "R")):
            sp = (sx * SHOULDER_X, 0, SHOULDER_Z)
            sb, mid = owner_sleeve(s, sx)
            put(sb, f"Shirt_Sleeve_{side}", arms[side], sp)
            ab, end = owner_skin_arm(s, sx, mid)
            put(ab, f"Skin_Arm{side}", arms[side], sp)
            if side == "R":  # chỗ cầm ly: ở bàn tay, hơi nhô ra trước (-Y)
                sock = CB().finish("HandSocket_R", tuple(end + Vector((0, -0.03, 0.0))), arms["R"])
                sock.empty_display_size = 0.06

            lp = (sx * LEG_X, 0, HIP_Z)
            hip, ankle = Vector(lp), Vector((sx * LEG_X, 0, 0.065))
            pants_b, skin_b, sock_b = CB(), CB(), CB()
            if bt == "skirt":
                skin_b.blob(hip, (0.085, 0.085, 0.085), s["skin"], u=8, v=6)
                m = hip + (ankle - hip) * 0.62
                skin_b.leg(hip, m, 0.068, 0.062, s["skin"], segs=8)
                sock_b.leg(m, ankle, 0.062, 0.057, 31, segs=8)
            else:
                pants_b.blob(hip, (0.085, 0.085, 0.085), s["pants"], u=8, v=6)
                if bt == "pants":
                    pants_b.leg(hip, ankle, 0.078, 0.062, s["pants"], segs=8)
                else:  # shorts
                    m = hip + (ankle - hip) * 0.42
                    pants_b.leg(hip, m, 0.082, 0.077, s["pants"], segs=8)
                    skin_b.leg(m, ankle, 0.064, 0.056, s["skin"], segs=8)
            put(pants_b, f"Pants{side}", legs[side], lp)
            put(skin_b, f"Skin_Leg{side}", legs[side], lp)
            put(sock_b, f"Sock{side}", legs[side], lp)
            shoe = CB()
            shoe.blob(Vector((sx * LEG_X, -0.035, 0.05)), (0.074, 0.118, 0.052), s["shoes"], u=10, v=6)
            put(shoe, f"Shoe{side}", legs[side], lp)
        return root
    finally:
        SCALE = 1.0


# ---------------------------------------------------------------- tiện ích

def descendants(obj):
    out = []
    for c in obj.children:
        out.append(c)
        out += descendants(c)
    return out


def tri_count(root):
    return sum(len(p.vertices) - 2 for o in descendants(root) if o.type == "MESH" for p in o.data.polygons)


def export_fbx(root):
    EXPORT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for o in descendants(root):
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(EXPORT_DIR / f"{root.name}.fbx"),
        use_selection=True, object_types={"EMPTY", "MESH"},
        axis_forward="-Z", axis_up="Y",
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        # bake_space_transform=True làm HỎNG transform của node con khi có hierarchy (đã thử: round-trip
        # Blender ra con có xoay 90° & vị trí sai). False = round-trip chính xác; Unity tự xử lý PreRotation.
        bake_space_transform=False, use_mesh_modifiers=True,
        mesh_smooth_type="EDGE", use_triangles=True,
        path_mode="STRIP", embed_textures=False, add_leaf_bones=False,
        use_custom_props=True,
    )


def tag_children(root, variant):
    """Sau export: đổi tên con để nhiều nhân vật cùng tồn tại trong một scene (preview/.blend)."""
    for o in descendants(root):
        o.name = f"{variant}_{o.name}"


def child(root, name):
    return next(o for o in descendants(root) if o.name.endswith("_" + name) or o.name == name)



def pose_seated(root, arms=True):
    """Ngồi trên ghế đẩu: root ở mặt ghế (0.27), hông hạ, chân duỗi về trước (-Y)."""
    root.location.z = 0.27
    child(root, "Hips").location.z = SEAT_HIP_Z
    for n in ("Leg_L", "Leg_R"):
        child(root, n).rotation_euler[0] = math.radians(-88)
    if arms:
        child(root, "Arm_L").rotation_euler[0] = math.radians(-25)
        child(root, "Arm_R").rotation_euler[0] = math.radians(-110)  # tay đưa lên uống


def pose_walk(root):
    child(root, "Leg_L").rotation_euler[0] = math.radians(-30)
    child(root, "Leg_R").rotation_euler[0] = math.radians(30)
    child(root, "Arm_L").rotation_euler[0] = math.radians(30)
    child(root, "Arm_R").rotation_euler[0] = math.radians(-30)


# ---------------------------------------------------------------- preview

def setup_stage(size=(2400, 1100)):
    scene = bpy.context.scene
    gf._set_eevee(scene)
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x, scene.render.resolution_y = size
    world = bpy.data.worlds.new("PREVIEW_World")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.62, 0.78, 0.90, 1)
    bg.inputs["Strength"].default_value = 0.5
    scene.world = world
    sd = bpy.data.lights.new("PREVIEW_Sun", "SUN")
    sd.energy, sd.color, sd.angle = 3.2, (1.0, 0.86, 0.68), math.radians(4)
    sun = bpy.data.objects.new("PREVIEW_Sun", sd)
    sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
    scene.collection.objects.link(sun)
    fl = Builder()
    fl.rounded_slab(16.0, 8.0, -0.03, 0.0, 45, corner=0, edge=0)
    fl.to_object("PREVIEW_Floor", {})
    return scene


def render(path, target, ortho, pitch, yaw, dist=18, persp_lens=None):
    scene = bpy.context.scene
    cd = bpy.data.cameras.new("PREVIEW_Cam")
    if persp_lens:
        cd.lens = persp_lens
    else:
        cd.type, cd.ortho_scale = "ORTHO", ortho
    cam = bpy.data.objects.new("PREVIEW_Cam", cd)
    scene.collection.objects.link(cam)
    direction = Vector((math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    cam.location = Vector(target) + direction * dist
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)


def render_previews(roots):
    setup_stage()
    names = list(roots)
    n = len(names)
    cols = 6
    for i, name in enumerate(names):
        r = roots[name]
        row, col = divmod(i, cols)
        r.location = (-3.3 + col * 1.15, -0.2 - row * 1.4 if row == 0 else 1.3, 0)
        r.rotation_euler[2] = math.radians(-20)  # hơi xoay để thấy mặt từ camera 3/4
    # nhóm ngồi bên phải: ghế đỏ + bàn xanh + nhân vật ngồi (Office) và một người đang đi (TeenGirl copy không cần)
    stool = gf.plastic_stool("PREVIEW_Stool_A", gf.C["red"], gf.C["red_dk"])
    stool.location = (3.9, 0.2, 0)
    table = gf.low_table("PREVIEW_Table", gf.C["blue"], gf.C["blue_dk"])
    table.location = (3.9, -0.65, 0)
    stool2 = gf.plastic_stool("PREVIEW_Stool_B", gf.C["blue"], gf.C["blue_dk"])
    stool2.location = (5.0, -0.65, 0)
    sitter = roots["TeenGirl"]
    sitter.location = (3.9, 0.2, 0)
    sitter.rotation_euler[2] = math.radians(-10)
    pose_seated(sitter)
    sitter2 = roots["Xeom"]
    sitter2.location = (5.0, -0.65, 0)
    sitter2.rotation_euler[2] = math.radians(90 - 15)
    pose_seated(sitter2, arms=False)
    pose_walk(roots["Tourist"])
    bpy.context.view_layer.update()
    render(EXPORT_DIR / "preview_characters.png", (1.3, 0.9, 0.4), 10.5, math.radians(50), math.radians(20))
    # cận mặt: camera thấp, tele
    for r in roots.values():
        r.rotation_euler[2] = 0
    sitter.rotation_euler[2] = 0
    sitter2.rotation_euler[2] = math.radians(90)
    render(EXPORT_DIR / "preview_characters_front.png", (1.2, 0.2, 0.55), 9.6, math.radians(18), math.radians(0))


def render_owner_previews(roots):
    """preview_owners.png: 4 chủ quán đứng hàng ngang (3/4), 2 người cầm ly ở HandSocket_R."""
    if "PREVIEW_Sun" not in bpy.data.objects:
        setup_stage((2400, 1000))
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = 2400, 1000
    for r in bpy.context.scene.objects:  # cất khách ra ngoài khung
        if r.parent is None and r.type == "EMPTY" and r.name.startswith("SM_Char_Customer"):
            r.location = (0, 40, 0)
    keys = list(roots)
    cups = []
    for i, key in enumerate(keys):
        r = roots[key]
        r.location = (-2.1 + i * 1.4, 0.0, 0)
        r.rotation_euler[2] = math.radians(-18 if i < 2 else -6)
        if i in (1, 2):  # cầm ly
            arm = child(r, "Arm_R")
            arm.rotation_euler[0] = math.radians(-70)
            sock = child(r, "HandSocket_R")
            cup = gf.phin_cup("PREVIEW_Cup_" + key)
            cup.parent = sock
            cup.matrix_parent_inverse.identity()
            cup.rotation_euler[0] = math.radians(70)  # triệt xoay tay -> ly thẳng đứng
            cups.append(cup)
    bpy.context.view_layer.update()
    render(EXPORT_DIR / "preview_owners.png", (0.0, 0.0, 0.62), 6.2, math.radians(14), math.radians(14))
    for r in roots.values():  # nhìn từ sau (dây tạp dề)
        r.rotation_euler[2] = math.radians(180 - 18)
    render(EXPORT_DIR / "preview_owners_back.png", (0.0, 0.0, 0.62), 6.2, math.radians(14), math.radians(180 - 14 + 28))
    for c in cups:
        bpy.data.objects.remove(c, do_unlink=True)
    for key, r in roots.items():
        r.rotation_euler[2] = 0
        child(r, "Arm_R").rotation_euler[0] = 0
        r.location = (0, 0, 0)


def render_staff_previews(staff, ref_owner):
    """preview_staff.png: 4 nhân viên đứng ngang + 1 chủ quán (bên phải, để so đồng phục); M2 và F1 cầm ly.
    preview_staff_back.png: nhìn từ sau (nón, nơ tạp dề, đuôi tóc)."""
    if "PREVIEW_Sun" not in bpy.data.objects:
        setup_stage((2400, 1000))
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = 2400, 1000
    show = set(staff.values()) | {ref_owner}
    for r in scene.objects:  # cất các nhân vật khác ra ngoài khung
        if r.parent is None and r.type == "EMPTY" and r.name.startswith("SM_Char_") and r not in show:
            r.location = (0, 40, 0)
    keys = list(staff)
    xs = {k: -2.8 + 1.4 * i for i, k in enumerate(keys)}
    cups = []
    for i, key in enumerate(keys):
        r = staff[key]
        r.location = (xs[key], 0.0, 0)
        r.rotation_euler[2] = math.radians(-18 if i < 2 else -6)
        if key in ("Staff_M2", "Staff_F1"):
            child(r, "Arm_R").rotation_euler[0] = math.radians(-70)
            cup = gf.phin_cup("PREVIEW_Cup_" + key)
            cup.parent = child(r, "HandSocket_R")
            cup.matrix_parent_inverse.identity()
            cup.rotation_euler[0] = math.radians(70)
            cups.append(cup)
    ref_owner.location = (3.3, 0.0, 0)
    ref_owner.rotation_euler[2] = math.radians(-6)
    bpy.context.view_layer.update()
    render(EXPORT_DIR / "preview_staff.png", (0.25, 0.0, 0.62), 8.4, math.radians(14), math.radians(14))
    # nhìn từ sau: giữ nguyên hướng nhân vật (mặt -Y), đưa camera sang phía +Y
    render(EXPORT_DIR / "preview_staff_back.png", (0.25, 0.0, 0.62), 8.4, math.radians(14), math.radians(180 + 14))
    for c in cups:
        bpy.data.objects.remove(c, do_unlink=True)
    for r in list(staff.values()) + [ref_owner]:
        r.rotation_euler[2] = 0
        r.location = (0, 0, 0)
    for key in keys:
        child(staff[key], "Arm_R").rotation_euler[0] = 0


def _purge_previews():
    """Xóa object/camera/đèn PREVIEW_* để .blend sạch."""
    for o in list(bpy.data.objects):
        if o.name.startswith("PREVIEW_"):
            bpy.data.objects.remove(o, do_unlink=True)


def _save_blends(roots):
    """characters_tier1.blend = khách + chủ quán; characters_tier2.blend = nhân viên."""
    BLEND_OUT.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    for path, group in ((BLEND_OUT, lambda k: k not in STAFF), (BLEND_OUT_T2, lambda k: k in STAFF)):
        keep = [k for k in roots if group(k)]
        if not keep:
            continue
        hidden = []
        for k, r in roots.items():
            if not group(k):
                hidden += [r] + descendants(r)
        for o in hidden:
            scene.collection.objects.unlink(o)
        bpy.ops.wm.save_as_mainfile(filepath=str(path))
        for o in hidden:
            scene.collection.objects.link(o)
        print(f"  blend: {path.name} ({len(keep)} nhân vật)")


# ---------------------------------------------------------------- main

def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--render", action="store_true")
    ap.add_argument("--only", nargs="*")
    args = ap.parse_args(argv)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    roots = {}
    for v in args.only or (list(VARIANTS) + list(OWNERS) + list(STAFF)):
        root = build_owner(v) if (v in OWNERS or v in STAFF) else build_character(v)
        export_fbx(root)
        tag_children(root, v)
        roots[v] = root
        bb = [o for o in descendants(root) if o.type == "MESH"]
        zs = [(o.matrix_world @ Vector(c)).z for o in bb for c in o.bound_box]
        print(f"  {root.name:34s} {tri_count(root):5d} tris  height={max(zs):.3f}  minz={min(zs):.3f}")

    if args.render:
        # render cần đủ nhân vật cho bố cục
        need = {"TeenGirl", "Xeom", "Tourist"}
        cust = {k: v for k, v in roots.items() if k in VARIANTS}
        owners = {k: v for k, v in roots.items() if k in OWNERS}
        staff = {k: v for k, v in roots.items() if k in STAFF}
        if need <= set(cust):
            render_previews(cust)
        else:
            print("bỏ qua render khách: cần đủ TeenGirl, Xeom, Tourist")
        if owners:
            render_owner_previews(owners)
        if staff:
            ref = owners.get("Owner_M1")
            tmp = None
            if ref is None:  # chủ quán tham chiếu dựng tạm (không export)
                tmp = ref = build_owner("Owner_M1")
                tag_children(ref, "Owner_M1")
            render_staff_previews(staff, ref)
            if tmp is not None:
                for o in descendants(tmp) + [tmp]:
                    bpy.data.objects.remove(o, do_unlink=True)

    for rt in roots.values():  # trả về pose gốc để .blend sạch
        rt.location, rt.rotation_euler = (0, 0, 0), (0, 0, 0)
        for o in descendants(rt):
            o.rotation_euler = (0, 0, 0)
            if o.name.endswith("_Hips"):
                o.location.z = rt.get("hip_height", HIP_Z)
    if args.only:
        print("(--only: bỏ qua ghi .blend)")
    else:
        _purge_previews()
        _save_blends(roots)
    print(f"OK: {len(roots)} nhân vật -> {EXPORT_DIR}")


if __name__ == "__main__":
    main()
