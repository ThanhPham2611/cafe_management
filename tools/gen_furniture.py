"""Sinh nội thất low-poly Tier 1 (vỉa hè) + Tier 2 (nhà phố thuê) đúng chuẩn art_bible.md, phong cách "chunky & bo tròn".

Chạy headless:
    blender --background --python tools/gen_furniture.py -- [--render] [--only SM_Stool_Plastic_Red ...]

Kết quả:
    art_source/blend/furniture_tier1.blend   (nguồn Tier 1)
    art_source/blend/furniture_tier2.blend   (nguồn Tier 2)       -- chỉ ghi khi KHÔNG dùng --only
    art_source/export/SM_*.fbx               (sẵn sàng copy vào Unity)
    art_source/export/preview_tier1.png      (nếu --render, render EEVEE toon)
    art_source/export/preview_tier2.png      (nếu --render, góc quán nhà phố)

Quy ước (art_bible §2, §3, §5, §8.1): Blender Z-up, mặt trước nhìn -Y, pivot ở (0,0,0) = giữa đáy,
mọi face đặt UV vào tâm 1 ô của T_Palette_256, 1 material M_Toon_Palette.
Bo tròn: bevel 2–3 nấc, shade smooth + cạnh sắc khi góc > 40°.

Đồ treo trần (SM_Fan_Ceiling, SM_Lamp_Pendant, SM_Plant_Pothos_Hanging): pivot vẫn ở ĐÁY model (z=0 = điểm
thấp nhất) để hợp validator; custom prop `hang_top` = độ cao (m) điểm treo trên trần, `mount_y` = độ cao Y
cần đặt pivot trong Unity (= hang_top - chiều cao model).
"""
import argparse
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parent.parent
PALETTE_PNG = ROOT / "art_source" / "palette" / "T_Palette_256.png"
BLEND_OUT = ROOT / "art_source" / "blend" / "furniture_tier1.blend"
BLEND_OUT_T2 = ROOT / "art_source" / "blend" / "furniture_tier2.blend"
EXPORT_DIR = ROOT / "art_source" / "export"
MAT_NAME = "M_Toon_Palette"
GRID = 8
SHARP_ANGLE = math.radians(40)

# Chỉ số ô palette hay dùng (art_bible §3)
C = {
    "red": 24, "red_dk": 25, "blue": 26, "blue_dk": 27, "green": 28, "green_dk": 29,
    "yellow": 30, "white_pl": 31,
    "inox": 48, "inox_mid": 49, "inox_dk": 50, "charcoal": 51,
    "wood_honey": 17, "wood_mid": 18, "wood_dark": 19,
    "paper": 15, "foam": 14, "milk": 8, "bacxiu": 4, "coffee": 2, "espresso": 0,
    "umbrella": 38, "leaf_dk": 34, "mint": 36, "flag_red": 56, "star": 57,
    # --- Tier 2
    "caramel": 5, "cacao": 6, "egg": 9, "salt": 10, "wall": 11, "beige": 12, "wood_light": 16,
    "wood_white": 22, "wood_sage": 23, "leaf_lt": 32, "leaf_md": 33, "leaf_moss": 35, "door_teal": 37,
    "tile_cream": 40, "tile_red": 41, "tile_blue": 42, "tile_yellow": 43, "tile_white": 44,
    "tile_grey": 45, "grout": 47, "terracotta": 46, "teal": 60, "pink": 58, "orange": 59,
    "soil": 1, "bamboo": 21,
}

HANG_TOP = 2.8   # độ cao điểm treo trên trần (m) cho đồ treo (quạt trần, đèn thả, chậu treo)


# ---------------------------------------------------------------- palette / material

def cell_uv(idx):
    row, col = divmod(idx, GRID)
    return Vector(((col + 0.5) / GRID, 1.0 - (row + 0.5) / GRID))


def _socket(sockets, name, kind):
    return next(s for s in sockets if s.name == name and s.type == kind)


def get_material():
    """M_Toon_Palette: trong Blender dựng node toon (3 bậc sáng + rim) để preview giống game.
    Unity chỉ đọc tên material, shader thật nằm ở Shader Graph Toon_Palette."""
    mat = bpy.data.materials.get(MAT_NAME)
    if mat:
        return mat
    mat = bpy.data.materials.new(MAT_NAME)
    mat.use_nodes = True
    nt = mat.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()

    tex = nodes.new("ShaderNodeTexImage")
    tex.interpolation = "Closest"
    if PALETTE_PNG.exists():
        tex.image = bpy.data.images.load(str(PALETTE_PNG), check_existing=True)
        tex.image.name = "T_Palette_256"

    diffuse = nodes.new("ShaderNodeBsdfDiffuse")
    to_rgb = nodes.new("ShaderNodeShaderToRGB")
    links.new(diffuse.outputs["BSDF"], to_rgb.inputs["Shader"])

    # 3 bậc sáng, vùng tối ngả tím-nâu ấm (art_bible §4: shadow tint #6A4A5A)
    bands = nodes.new("ShaderNodeValToRGB")
    ramp = bands.color_ramp
    ramp.interpolation = "CONSTANT"
    ramp.elements[0].position, ramp.elements[0].color = 0.0, (0.52, 0.42, 0.50, 1)
    ramp.elements[1].position, ramp.elements[1].color = 0.12, (0.82, 0.76, 0.78, 1)
    mid = ramp.elements.new(0.35)
    mid.color = (1.0, 1.0, 1.0, 1)
    links.new(to_rgb.outputs["Color"], bands.inputs["Fac"])

    shade = nodes.new("ShaderNodeMix")
    shade.data_type, shade.blend_type = "RGBA", "MULTIPLY"
    _socket(shade.inputs, "Factor", "VALUE").default_value = 1.0
    links.new(tex.outputs["Color"], _socket(shade.inputs, "A", "RGBA"))
    links.new(bands.outputs["Color"], _socket(shade.inputs, "B", "RGBA"))

    # Rim light ấm nhẹ ở viền
    lw = nodes.new("ShaderNodeLayerWeight")
    lw.inputs["Blend"].default_value = 0.35
    rim_ramp = nodes.new("ShaderNodeValToRGB")
    rr = rim_ramp.color_ramp
    rr.interpolation = "CONSTANT"
    rr.elements[0].color = (0, 0, 0, 1)
    rr.elements[1].position, rr.elements[1].color = 0.82, (0.35, 0.27, 0.16, 1)
    links.new(lw.outputs["Facing"], rim_ramp.inputs["Fac"])
    rim = nodes.new("ShaderNodeMix")
    rim.data_type, rim.blend_type = "RGBA", "ADD"
    _socket(rim.inputs, "Factor", "VALUE").default_value = 1.0
    links.new(_socket(shade.outputs, "Result", "RGBA"), _socket(rim.inputs, "A", "RGBA"))
    links.new(rim_ramp.outputs["Color"], _socket(rim.inputs, "B", "RGBA"))

    emit = nodes.new("ShaderNodeEmission")
    links.new(_socket(rim.outputs, "Result", "RGBA"), emit.inputs["Color"])
    out = nodes.new("ShaderNodeOutputMaterial")
    links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


# ---------------------------------------------------------------- part builders
# Mỗi part là một bmesh riêng: dựng hình, bo tròn, rồi tô 1 ô màu cho mọi face và gộp vào mesh chính.

def _flat_edges(bm, eps=1e-6):
    """Các cạnh nằm ngang (2 đầu cùng z) = viền nắp trụ / viền trên-dưới của tấm."""
    return [e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) < eps]


def _bevel(bm, edges, offset, segs):
    if offset > 0 and edges:
        bmesh.ops.bevel(bm, geom=edges, offset=offset, segments=segs, profile=0.5,
                        affect="EDGES", clamp_overlap=True)


def _rr_pts(w, d, corner, segs, cx=0.0, cy=0.0):
    """Điểm viền hình chữ nhật bo góc (ngược chiều kim đồng hồ), 4*(segs+1) điểm khi corner>0, 4 khi corner=0."""
    corner = max(0.0, min(corner, w / 2 - 1e-3, d / 2 - 1e-3))
    pts = []
    for sx, sy, a0 in ((1, 1, 0), (-1, 1, 90), (-1, -1, 180), (1, -1, 270)):
        ccx, ccy = sx * (w / 2 - corner), sy * (d / 2 - corner)
        n = segs if corner > 0 else 0
        for k in range(n + 1):
            a = math.radians(a0 + 90 * k / max(n, 1))
            pts.append((cx + ccx + corner * math.cos(a), cy + ccy + corner * math.sin(a)))
    return pts


class Builder:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def _absorb(self, part_bm, color, matrix=None):
        """Chép part_bm (có thể biến đổi bằng `matrix`) vào bm chính, gán UV ô `color`."""
        if matrix is not None:
            bmesh.ops.transform(part_bm, matrix=matrix, verts=part_bm.verts)
        vmap = {v: self.bm.verts.new(v.co) for v in part_bm.verts}
        uv = cell_uv(color)
        for f in part_bm.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
            except ValueError:  # face trùng (hiếm)
                continue
            for loop in nf.loops:
                loop[self.uv].uv = uv
        part_bm.free()

    def rounded_slab(self, w, d, z0, z1, color, corner=0.05, edge=0.015, cx=0.0, cy=0.0, segs=3):
        """Tấm chữ nhật bo góc khi nhìn từ trên (corner) và bo mép trên/dưới (edge)."""
        pb = bmesh.new()
        corner = max(0.0, min(corner, w / 2 - 1e-3, d / 2 - 1e-3))
        pts = []
        for sx, sy, a0 in ((1, 1, 0), (-1, 1, 90), (-1, -1, 180), (1, -1, 270)):
            ccx, ccy = sx * (w / 2 - corner), sy * (d / 2 - corner)
            n = segs if corner > 0 else 0
            for k in range(n + 1):
                a = math.radians(a0 + 90 * k / max(n, 1))
                pts.append((cx + ccx + corner * math.cos(a), cy + ccy + corner * math.sin(a)))
        bot = [pb.verts.new((x, y, z0)) for x, y in pts]
        top = [pb.verts.new((x, y, z1)) for x, y in pts]
        pb.faces.new(list(reversed(bot)))
        pb.faces.new(top)
        for i in range(len(pts)):
            j = (i + 1) % len(pts)
            pb.faces.new((bot[i], bot[j], top[j], top[i]))
        _bevel(pb, _flat_edges(pb), min(edge, (z1 - z0) / 2 - 1e-4), 2)
        self._absorb(pb, color)

    def rbox(self, size, center, color, r=0.015, segs=2, rot=None):
        """Hộp bo đều mọi cạnh (biển hiệu, sọc, chi tiết nhỏ). `rot` = Matrix xoay quanh tâm hộp."""
        pb = bmesh.new()
        bmesh.ops.create_cube(pb, size=1.0)
        bmesh.ops.scale(pb, vec=Vector(size), verts=pb.verts)
        _bevel(pb, list(pb.edges), min(r, min(size) / 2 - 1e-4), segs)
        self._absorb(pb, color, Matrix.Translation(center) @ (rot or Matrix()))

    def blob(self, center, radii, color, u=6, v=4, rot=None):
        """Ellipsoid thô (lá, tán cây), bán kính `radii`; `rot` = Matrix xoay quanh tâm."""
        pb = bmesh.new()
        bmesh.ops.create_uvsphere(pb, u_segments=u, v_segments=v, radius=1.0)
        bmesh.ops.scale(pb, vec=Vector(radii), verts=pb.verts)
        self._absorb(pb, color, Matrix.Translation(Vector(center)) @ (rot or Matrix()))

    def leaf(self, base, direction, length, width, color, thick=0.35, u=6, v=4):
        """Lá/phiến dẹt: ellipsoid dài `length` bắt đầu ở `base` theo `direction`."""
        d = Vector(direction).normalized()
        rot = Vector((0, 1, 0)).rotation_difference(d).to_matrix().to_4x4()
        self.blob(Vector(base) + d * length / 2, (width / 2, length / 2, width / 2 * thick), color,
                  u=u, v=v, rot=rot)

    def cylinder(self, r_bottom, r_top, z0, z1, color, segs=10, center=(0, 0), axis=None,
                 round_r=0.0, round_top=True, round_bottom=True):
        """Trụ/nón cụt từ z0 tới z1, bo viền nắp `round_r`. Dời theo `center` (x,y) hoặc (x,y,z);
        `axis` = Matrix xoay áp trước khi dời (trụ nằm ngang như bánh xe)."""
        pb = bmesh.new()
        bmesh.ops.create_cone(pb, cap_ends=True, cap_tris=False, segments=segs,
                              radius1=r_bottom, radius2=r_top, depth=z1 - z0)
        h = (z1 - z0) / 2
        rims = [e for e in _flat_edges(pb)
                if (round_top and e.verts[0].co.z > 0) or (round_bottom and e.verts[0].co.z < 0)]
        if r_top < 1e-4:
            rims = [e for e in rims if e.verts[0].co.z < 0]
        _bevel(pb, rims, min(round_r, h - 1e-4, max(r_bottom, r_top) * 0.9), 2)
        m = Matrix.Translation(Vector((*center, 0)[:3])) @ (axis or Matrix()) @ \
            Matrix.Translation((0, 0, (z0 + z1) / 2))
        self._absorb(pb, color, m)

    def leg(self, top, foot, r_top, r_foot, color, segs=8):
        """Chân tròn thuôn nối `top` → `foot`, đầu chân bo tròn."""
        top, foot = Vector(top), Vector(foot)
        d = top - foot
        pb = bmesh.new()
        bmesh.ops.create_cone(pb, cap_ends=True, cap_tris=False, segments=segs,
                              radius1=r_foot, radius2=r_top, depth=d.length)
        bmesh.ops.translate(pb, vec=Vector((0, 0, d.length / 2)), verts=pb.verts)
        foot_rim = [e for e in _flat_edges(pb) if e.verts[0].co.z < 1e-4]
        _bevel(pb, foot_rim, r_foot * 0.6, 2)
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        self._absorb(pb, color, Matrix.Translation(foot) @ rot)

    def rod(self, a, b, r, color, segs=6):
        """Thanh tròn đều nối a → b (thanh giằng, tay đẩy)."""
        a, b = Vector(a), Vector(b)
        d = b - a
        pb = bmesh.new()
        bmesh.ops.create_cone(pb, cap_ends=True, cap_tris=False, segments=segs,
                              radius1=r, radius2=r, depth=d.length)
        rot = Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        self._absorb(pb, color, Matrix.Translation((a + b) / 2) @ rot)

    def frame_slab(self, w, d, hw, hd, hcx, hcy, z0, z1, color, corner=0.04, hcorner=0.03, edge=0.01,
                   segs=2, cx=0.0, cy=0.0, top_only=True):
        """Tấm bo góc có LỖ chữ nhật bo góc (bồn rửa, khay có gờ). Lỗ (hw×hd, tâm hcx,hcy) phải nằm trong tấm."""
        pb = bmesh.new()
        outer = _rr_pts(w, d, corner, segs, cx, cy)
        inner = _rr_pts(hw, hd, hcorner, segs, hcx, hcy)
        n = len(outer)
        assert n == len(inner)
        to_ = [pb.verts.new((x, y, z1)) for x, y in outer]
        ti = [pb.verts.new((x, y, z1)) for x, y in inner]
        bo = [pb.verts.new((x, y, z0)) for x, y in outer]
        bi = [pb.verts.new((x, y, z0)) for x, y in inner]
        for i in range(n):
            j = (i + 1) % n
            pb.faces.new((to_[i], to_[j], ti[j], ti[i]))      # mặt trên (vành)
            pb.faces.new((bi[i], bi[j], bo[j], bo[i]))        # mặt dưới
            pb.faces.new((bo[i], bo[j], to_[j], to_[i]))      # thành ngoài
            pb.faces.new((ti[i], ti[j], bi[j], bi[i]))        # thành trong lỗ
        bmesh.ops.recalc_face_normals(pb, faces=pb.faces)
        flat = _flat_edges(pb)
        if top_only:  # chỉ bo viền mặt trên (mặt dưới bị che)
            flat = [e for e in flat if e.verts[0].co.z > (z0 + z1) / 2]
        _bevel(pb, flat, min(edge, (z1 - z0) / 2 - 1e-4), 2)
        self._absorb(pb, color)

    def ground(self):
        """Hạ cả model xuống để điểm thấp nhất chạm z=0 (đồ treo trần: pivot ở đáy)."""
        z = min(v.co.z for v in self.bm.verts)
        bmesh.ops.translate(self.bm, vec=Vector((0, 0, -z)), verts=self.bm.verts)

    def height(self):
        return max(v.co.z for v in self.bm.verts)

    def to_object(self, name, props):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        for v in bm.verts:  # chân xòe sẽ lún dưới sàn: cắt phẳng đáy
            v.co.z = max(v.co.z, 0.0)
        bm.normal_update()
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        # Shade smooth + giữ cạnh sắc: bo tròn trông mềm, mặt phẳng vẫn phẳng
        for f in bm.faces:
            f.smooth = True
        for e in bm.edges:
            e.smooth = len(e.link_faces) == 2 and e.calc_face_angle(math.pi) < SHARP_ANGLE
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(get_material())
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        for k, v in props.items():
            obj[k] = v
        obj["front"] = "-Y"
        return obj


# ---------------------------------------------------------------- assets

def plastic_stool(name, main, dark):
    """Ghế đẩu nhựa vỉa hè: mặt ngồi 0.27 m dày, bo tròn, chân tròn mập xòe, có lỗ tay cầm."""
    b = Builder()
    seat_top = 0.27
    b.rounded_slab(0.34, 0.34, 0.22, seat_top, main, corner=0.08, edge=0.02)
    b.rounded_slab(0.29, 0.29, 0.19, 0.22, main, corner=0.07, edge=0.0, segs=2)  # váy dưới mặt (bị che)
    b.cylinder(0.045, 0.045, seat_top - 0.003, seat_top + 0.002, dark, segs=8)  # lỗ tay cầm
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.leg((sx * 0.105, sy * 0.105, 0.20), (sx * 0.155, sy * 0.155, 0.0), 0.03, 0.036, main)
    # Vòng giằng tròn ở 0.07 m
    z = 0.07
    t = (0.20 - z) / 0.20
    ring = 0.105 + (0.155 - 0.105) * t
    corners = [(-ring, -ring), (ring, -ring), (ring, ring), (-ring, ring)]
    for i in range(4):
        (x0, y0), (x1, y1) = corners[i], corners[(i + 1) % 4]
        b.rod((x0, y0, z), (x1, y1, z), 0.014, dark)
    return b.to_object(name, {"category": "furniture", "footprint_x": 1, "footprint_z": 1,
                              "ref_height": seat_top, "seat_height": seat_top})


def low_table(name, top, leg):
    """Bàn thấp vỉa hè 0.45 m, mặt dày bo tròn 0.74×0.74, footprint 2×2."""
    b = Builder()
    h = 0.45
    b.rounded_slab(0.74, 0.74, h - 0.055, h, top, corner=0.12, edge=0.022)
    b.rounded_slab(0.62, 0.62, h - 0.09, h - 0.055, leg, corner=0.09, edge=0.01)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.leg((sx * 0.25, sy * 0.25, h - 0.08), (sx * 0.29, sy * 0.29, 0.0), 0.032, 0.04, leg)
    return b.to_object(name, {"category": "furniture", "footprint_x": 2, "footprint_z": 2,
                              "ref_height": h})


def coffee_cart(name):
    """Xe đẩy cà phê vỉa hè 1.5×1.0 m (footprint 3×2), mái che sọc, mặt trước (-Y) có biển hiệu."""
    b = Builder()
    # Thân tủ gỗ + mặt quầy dày
    b.rounded_slab(1.28, 0.70, 0.30, 0.90, C["wood_honey"], corner=0.08, edge=0.03)
    b.rounded_slab(1.42, 0.82, 0.90, 0.97, C["wood_dark"], corner=0.10, edge=0.025)
    # Biển hiệu trước + sọc đỏ
    b.rbox((1.04, 0.03, 0.34), (0, -0.355, 0.62), C["paper"], r=0.012, segs=1)
    b.rbox((1.04, 0.035, 0.06), (0, -0.36, 0.40), C["red"], r=0.012, segs=1)
    # (logo sao đã bỏ: tên quán do người chơi đặt hiện ở giữa biển, xem ShopSign.cs)
    # Tủ kính trưng (khung inox, "kính" bạc hà)
    b.rounded_slab(0.78, 0.48, 0.97, 1.24, C["mint"], corner=0.05, edge=0.015, cx=0.22)
    b.rounded_slab(0.84, 0.54, 1.24, 1.29, C["inox"], corner=0.06, edge=0.02, cx=0.22)
    # Cột + mái che sọc đỏ/trắng
    for px in (-0.62, 0.62):
        b.rod((px, 0.30, 0.97), (px, 0.30, 1.78), 0.018, C["inox_mid"])
    stripes = 6
    sw = 1.46 / stripes
    for i in range(stripes):
        col = C["red"] if i % 2 == 0 else C["foam"]
        x = -0.73 + sw * (i + 0.5)
        b.rbox((sw, 0.90, 0.06), (x, -0.02, 1.81), col, r=0.02, segs=1)
        # diềm lượn trước mái
        b.cylinder(sw / 2, sw / 2, -0.015, 0.015, col, segs=8, center=(x, -0.47, 1.78),
                   axis=Matrix.Rotation(math.radians(90), 4, "X"))
    # Bánh xe lớn (lốp + mâm) hai bên, trục ngang
    to_y = Matrix.Rotation(math.radians(90), 4, "X")
    for wy in (-0.40, 0.40):
        b.cylinder(0.24, 0.24, -0.04, 0.04, C["charcoal"], segs=12, center=(-0.30, wy, 0.24),
                   axis=to_y, round_r=0.03)
        b.cylinder(0.11, 0.11, -0.046, 0.046, C["inox"], segs=8, center=(-0.30, wy, 0.24),
                   axis=to_y)
    # Chân chống bên phải
    for py in (-0.25, 0.25):
        b.leg((0.52, py, 0.31), (0.55, py, 0.0), 0.03, 0.035, C["inox_dk"])
    # Tay đẩy bên +X
    b.rod((0.66, -0.30, 0.92), (0.77, -0.30, 1.02), 0.02, C["inox_mid"])
    b.rod((0.66, 0.30, 0.92), (0.77, 0.30, 1.02), 0.02, C["inox_mid"])
    b.rod((0.77, -0.32, 1.02), (0.77, 0.32, 1.02), 0.022, C["red_dk"])
    # Bộ ly phin + bình trà đá trên mặt quầy
    for x in (-0.52, -0.39, -0.26):
        b.cylinder(0.035, 0.042, 0.97, 1.06, C["foam"], segs=8, center=(x, -0.18))
        b.cylinder(0.04, 0.036, 1.06, 1.11, C["inox"], segs=8, center=(x, -0.18), round_r=0.01,
                   round_bottom=False)
    b.cylinder(0.10, 0.10, 0.97, 1.22, C["red"], segs=12, center=(-0.40, 0.18), round_r=0.03)
    b.cylinder(0.08, 0.06, 1.22, 1.27, C["white_pl"], segs=12, center=(-0.40, 0.18), round_r=0.02)
    return b.to_object(name, {"category": "counter", "footprint_x": 3, "footprint_z": 2,
                              "ref_height": 0.97})


def street_umbrella(name):
    """Dù che vỉa hè cao 2.3 m, tán vòm 12 múi Ø1.9 m, viền xen màu (footprint 4×4)."""
    b = Builder()
    b.cylinder(0.22, 0.17, 0.0, 0.10, C["charcoal"], segs=12, round_r=0.04, round_bottom=False)
    b.cylinder(0.026, 0.026, 0.10, 2.25, C["inox_mid"], segs=6)
    # Tán vòm: 2 tầng nón cho cảm giác phồng
    b.cylinder(0.95, 0.60, 1.95, 2.13, C["umbrella"], segs=12, round_r=0.04, round_top=False)
    b.cylinder(0.60, 0.07, 2.13, 2.28, C["umbrella"], segs=12, round_bottom=False)
    b.cylinder(0.05, 0.03, 2.28, 2.34, C["foam"], segs=8, round_r=0.02)   # chóp
    b.cylinder(0.97, 0.97, 1.89, 1.96, C["foam"], segs=12, round_r=0.025)  # dải viền trắng
    return b.to_object(name, {"category": "furniture", "footprint_x": 4, "footprint_z": 4,
                              "ref_height": 2.2})


def phin_cup(name):
    """Ly cà phê sữa đá + phin trên miệng (prop nhỏ, bo mềm)."""
    b = Builder()
    b.cylinder(0.031, 0.040, 0.0, 0.10, C["bacxiu"], segs=10, round_r=0.008, round_top=False)
    b.cylinder(0.054, 0.054, 0.10, 0.107, C["inox_mid"], segs=10)
    b.cylinder(0.032, 0.036, 0.107, 0.155, C["inox"], segs=10)
    b.cylinder(0.038, 0.030, 0.155, 0.168, C["inox_dk"], segs=10, round_r=0.005, round_bottom=False)
    return b.to_object(name, {"category": "prop", "footprint_x": 1, "footprint_z": 1,
                              "ref_height": 0.168})


def plant_pot_small(name):
    """Chậu trầu bà nhỏ cao ~0.55 m: chậu đất nung bo tròn + cụm lá bầu mập."""
    b = Builder()
    b.cylinder(0.105, 0.155, 0.0, 0.21, 46, segs=10, round_r=0.02, round_top=False)       # thân chậu
    b.cylinder(0.172, 0.172, 0.19, 0.24, 41, segs=10, round_r=0.015)                      # viền miệng
    b.cylinder(0.145, 0.145, 0.235, 0.255, 1, segs=10)                                    # đất
    greens = [32, 33, 34]
    n = 9
    for i in range(n):
        a = 2 * math.pi * i / n + 0.3
        tilt = 0.55 + 0.35 * (i % 3) / 2
        d = (math.cos(a) * math.sin(tilt) * 1.0, math.sin(a) * math.sin(tilt), math.cos(tilt))
        base = (math.cos(a) * 0.05, math.sin(a) * 0.05, 0.25)
        b.leaf(base, d, 0.25 + 0.04 * (i % 3), 0.17, greens[i % 3], thick=0.3, u=7, v=4)
    b.leaf((0, 0, 0.25), (0.05, 0.02, 1), 0.28, 0.15, 32, thick=0.3, u=7, v=4)           # lá giữa
    return b.to_object(name, {"category": "furniture", "footprint_x": 1, "footprint_z": 1,
                              "ref_height": 0.55})


def areca_tall(name):
    """Cây cau tiểu trâm cao ~1.3 m: chậu men + cụm thân mảnh, tàu lá cong rủ."""
    b = Builder()
    b.cylinder(0.12, 0.17, 0.0, 0.30, 37, segs=10, round_r=0.025, round_top=False)       # chậu men xanh ngọc
    b.cylinder(0.185, 0.185, 0.27, 0.33, 38, segs=10, round_r=0.018)                      # viền miệng
    b.cylinder(0.15, 0.15, 0.325, 0.345, 1, segs=10)                                      # đất
    fronds = 8
    for i in range(fronds):
        a = 2 * math.pi * i / fronds + 0.2
        ca, sa = math.cos(a), math.sin(a)
        h = 0.60 + 0.28 * (i % 3) / 2           # chiều cao tới điểm uốn
        out = 0.04 + 0.03 * (i % 2)
        p0 = Vector((ca * 0.03, sa * 0.03, 0.34))
        p1 = Vector((ca * out, sa * out, 0.34 + h * 0.9))
        b.rod(p0, p1, 0.011, 34, segs=4)
        # tàu lá cong rủ ra ngoài: 2 phiến dài mỗi bên
        tip = Vector((ca * (out + 0.10), sa * (out + 0.10), p1.z + 0.10))
        perp = Vector((-sa, ca, 0)) * 0.05
        for sgn in (-1, 1):
            b.leaf(p1, (tip - p1) + perp * sgn * 2, 0.27, 0.09, 33 if sgn < 0 else 34, thick=0.3,
                   u=5, v=3)
        if i % 2 == 0:
            b.leaf(p1, Vector((ca * 0.5, sa * 0.5, 0.8)), 0.2, 0.085, 32, thick=0.3, u=5, v=3)
    return b.to_object(name, {"category": "furniture", "footprint_x": 1, "footprint_z": 1,
                              "ref_height": 1.3})


def menu_board_a(name):
    """Bảng menu chữ A vỉa hè cao ~0.85 m. Mặt bảng than ở phía trước (-Y), nét phấn kem + ly cà phê."""
    b = Builder()
    ang = math.atan2(0.22, 0.85)               # độ nghiêng mỗi tấm
    L = math.hypot(0.22, 0.85) + 0.02
    rf = Matrix.Rotation(-ang, 4, "X")         # tấm trước: đỉnh ngả về +Y
    rb = Matrix.Rotation(ang, 4, "X")          # tấm sau
    cf = Vector((0, -0.11, 0.425))
    cb = Vector((0, 0.11, 0.425))
    b.rbox((0.46, 0.045, L), cf, C["wood_honey"], r=0.012, segs=1, rot=rf)
    b.rbox((0.44, 0.04, L - 0.02), cb, C["wood_mid"], r=0.012, segs=1, rot=rb)
    n = rf @ Vector((0, -1, 0))               # pháp tuyến mặt trước
    face_c = cf + n * 0.016

    def on_face(lx, lz, off=0.0):
        return face_c + rf @ Vector((lx, -off, lz))

    b.rbox((0.37, 0.025, 0.66), face_c + rf @ Vector((0, 0, 0.0)) + n * 0.006, C["charcoal"], r=0.008,
           segs=1, rot=rf)
    cream = C["paper"]
    # tiêu đề + dòng "menu" bằng nét phấn
    b.rbox((0.22, 0.012, 0.028), on_face(0, 0.26, 0.02) + n * 0.006, cream, r=0, rot=rf)
    b.rbox((0.30, 0.012, 0.012), on_face(0, 0.215, 0.02) + n * 0.006, cream, r=0, rot=rf)
    for k, w in enumerate((0.25, 0.20, 0.23)):
        z = 0.12 - k * 0.07
        b.rbox((w, 0.012, 0.016), on_face(-0.04 + (w - 0.25) / 2 * -1, z, 0.02) + n * 0.006, cream, r=0,
               rot=rf)
        b.rbox((0.045, 0.012, 0.016), on_face(0.125, z, 0.02) + n * 0.006, C["yellow"], r=0, rot=rf)
    # ly cà phê doodle
    cx, cz = 0.0, -0.15
    b.rbox((0.085, 0.012, 0.075), on_face(cx, cz, 0.02) + n * 0.006, cream, r=0.004, segs=1, rot=rf)
    b.rbox((0.05, 0.013, 0.04), on_face(cx, cz - 0.003, 0.02) + n * 0.007, C["bacxiu"], r=0, rot=rf)
    b.rbox((0.03, 0.012, 0.03), on_face(cx + 0.058, cz + 0.005, 0.02) + n * 0.006, cream, r=0.004,
           segs=1, rot=rf)
    for sx in (-0.02, 0.02):                   # hơi nước
        b.rbox((0.01, 0.012, 0.04), on_face(sx, cz + 0.07, 0.02) + n * 0.006, cream, r=0, rot=rf)
    # thanh giằng + bản lề
    b.rbox((0.40, 0.03, 0.025), (0, 0, 0.84), C["wood_dark"], r=0.008, segs=1)
    b.rbox((0.42, 0.025, 0.02), (0, 0, 0.0 + 0.012), C["wood_dark"], r=0, rot=None)
    return b.to_object(name, {"category": "furniture", "footprint_x": 1, "footprint_z": 1,
                              "ref_height": 0.85})


# ================================================================ TIER 2 — nhà phố thuê (nhà ống, gạch bông)

def _tile(b, x, z, y, s, motif, accent):
    """Một viên gạch bông trên mặt phẳng XZ nhìn về -Y (y = mặt nền tường): nền kem + hình thoi + tâm."""
    d45 = Matrix.Rotation(math.radians(45), 4, "Y")
    b.rbox((s, 0.012, s), (x, y - 0.006, z), C["tile_cream"], r=0)
    b.rbox((s * 0.64, 0.008, s * 0.64), (x, y - 0.0145, z), motif, r=0, rot=d45)
    b.rbox((s * 0.26, 0.008, s * 0.26), (x, y - 0.0215, z), accent, r=0, rot=d45)


def counter_bar(name):
    """Quầy bar 4×2 (2.0×1.0×1.05 m): thân gỗ nan đứng, mặt kem, dải gạch bông ở mặt trước (-Y),
    đầu +X là tủ bánh kính (cửa sổ kính bạc hà + tủ trưng khung inox trên mặt quầy), thanh gác chân inox."""
    b = Builder()
    FY = -0.43                                       # mặt trước thân tủ
    b.rounded_slab(1.80, 0.76, 0.0, 0.09, C["wood_dark"], corner=0.04, edge=0.0, segs=2)      # chân thụt
    b.rounded_slab(1.90, 0.86, 0.09, 0.91, C["wood_mid"], corner=0.05, edge=0.012, segs=2)    # thân tủ
    b.rounded_slab(1.94, 0.90, 0.91, 0.95, C["wood_dark"], corner=0.07, edge=0.0, segs=2)     # đường chỉ
    b.rounded_slab(2.00, 1.00, 0.95, 1.05, C["salt"], corner=0.09, edge=0.022)                # mặt kem
    # --- phần gỗ + gạch bông (x -0.93 .. 0.30)
    for i in range(9):
        x = -0.86 + 0.14 * i
        b.rbox((0.085, 0.014, 0.23), (x, FY - 0.007, 0.765), C["wood_honey"], r=0)           # nan trên
        b.rbox((0.085, 0.014, 0.22), (x, FY - 0.007, 0.215), C["wood_honey"], r=0)           # nan dưới
    for z in (0.616, 0.344):                                                                  # khung dải gạch
        b.rbox((1.25, 0.024, 0.032), (-0.315, FY - 0.012, z), C["wood_dark"], r=0)
    motifs = [(C["tile_red"], C["tile_blue"]), (C["tile_blue"], C["tile_yellow"])]
    for i in range(6):
        _tile(b, -0.80 + 0.2 * i, 0.48, FY, 0.2, *motifs[i % 2])
    b.rbox((0.045, 0.026, 0.76), (0.32, FY - 0.013, 0.50), C["wood_dark"], r=0)               # trụ ngăn
    # --- cửa sổ tủ bánh (x 0.35 .. 0.92): nền kính bạc hà, khung inox, 2 kệ + bánh
    gx = 0.635
    b.rbox((0.50, 0.01, 0.60), (gx, FY - 0.005, 0.54), C["mint"], r=0)
    b.rbox((0.56, 0.026, 0.035), (gx, FY - 0.013, 0.865), C["inox"], r=0)
    b.rbox((0.56, 0.026, 0.035), (gx, FY - 0.013, 0.215), C["inox"], r=0)
    for sx in (-1, 1):
        b.rbox((0.035, 0.026, 0.69), (gx + sx * 0.2625, FY - 0.013, 0.54), C["inox"], r=0)
    for z, cols in ((0.405, (C["caramel"], C["pink"], C["egg"])), (0.62, (C["cacao"], C["egg"], C["caramel"]))):
        b.rbox((0.50, 0.075, 0.014), (gx, FY - 0.0375, z), C["inox"], r=0)
        for k, col in enumerate(cols):
            b.blob((gx - 0.15 + 0.15 * k, FY - 0.04, z + 0.034), (0.052, 0.036, 0.03), col, u=7, v=4)
    for k, col in enumerate((C["pink"], C["caramel"], C["egg"])):                             # hàng dưới cùng
        b.blob((gx - 0.15 + 0.15 * k, FY - 0.04, 0.215 + 0.04), (0.052, 0.036, 0.03), col, u=7, v=4)
    # --- tủ trưng trên mặt quầy (khung hở nhìn thấy bánh, kính bạc hà phía sau/hai bên)
    cx, cy = 0.63, -0.04
    b.rounded_slab(0.54, 0.46, 1.05, 1.075, C["inox"], corner=0.05, edge=0.0, segs=2, cx=cx, cy=cy)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.rod((cx + sx * 0.255, cy + sy * 0.215, 1.075), (cx + sx * 0.255, cy + sy * 0.215, 1.325), 0.012,
                  C["inox_mid"], segs=6)
    b.rounded_slab(0.60, 0.50, 1.31, 1.36, C["mint"], corner=0.06, edge=0.012, segs=2, cx=cx, cy=cy)
    b.rbox((0.52, 0.012, 0.235), (cx, cy + 0.215, 1.1925), C["mint"], r=0)
    for sx in (-1, 1):
        b.rbox((0.012, 0.43, 0.235), (cx + sx * 0.255, cy, 1.1925), C["mint"], r=0)
    b.cylinder(0.075, 0.075, 1.075, 1.105, C["caramel"], segs=10, center=(cx - 0.13, cy - 0.06), round_r=0.01)
    b.cylinder(0.078, 0.078, 1.105, 1.118, C["foam"], segs=10, center=(cx - 0.13, cy - 0.06), round_r=0.004)
    b.blob((cx + 0.12, cy - 0.10, 1.107), (0.075, 0.04, 0.032), C["caramel"], u=7, v=4,
           rot=Matrix.Rotation(math.radians(25), 4, "Z"))                                    # croissant
    b.cylinder(0.034, 0.028, 1.075, 1.13, C["pink"], segs=7, center=(cx + 0.14, cy + 0.08))   # cupcake
    b.blob((cx + 0.14, cy + 0.08, 1.14), (0.04, 0.04, 0.03), C["foam"], u=7, v=4)
    b.blob((cx + 0.14, cy + 0.08, 1.168), (0.011, 0.011, 0.011), C["flag_red"], u=5, v=3)
    # --- thanh gác chân inox
    b.rod((-0.88, -0.475, 0.15), (0.88, -0.475, 0.15), 0.015, C["inox_mid"], segs=6)
    for x in (-0.7, 0.0, 0.7):
        b.rod((x, FY - 0.02, 0.15), (x, -0.475, 0.15), 0.012, C["inox_mid"], segs=5)
    return b.to_object(name, {"category": "counter", "footprint_x": 4, "footprint_z": 2,
                              "ref_height": 1.05})


def sink_counter(name):
    """Quầy rửa 2×1 (1.0×0.5 m, mặt làm việc 0.95 m): tủ xanh sage + cửa kem sơn, mặt inox có bồn rửa LÕM thật,
    vòi cổ ngỗng + 2 núm nóng/lạnh, giá úp ly (chốt + ly úp nhiều màu) ở phần ráo nước, ốp tường gạch trắng."""
    b = Builder()
    TOP, TH = 0.95, 0.06
    hw, hd, hcx, hcy = 0.38, 0.28, -0.24, -0.02            # lỗ bồn
    sage, door, inox, inox_m = C["wood_sage"], C["wood_white"], C["inox"], C["inox_mid"]
    b.rounded_slab(0.86, 0.40, 0.0, 0.07, C["wood_dark"], corner=0.03, edge=0.0, segs=1)
    b.rounded_slab(0.94, 0.46, 0.07, 0.72, sage, corner=0.04, edge=0.0, segs=1)                # tủ dưới bồn
    b.frame_slab(0.94, 0.46, hw, hd, hcx, hcy, 0.72, TOP - TH, sage, corner=0.04, hcorner=0.03, edge=0.0,
                 segs=1)
    b.frame_slab(1.00, 0.50, hw, hd, hcx, hcy, TOP - TH, TOP, inox, corner=0.05, hcorner=0.03, edge=0.012,
                 segs=1)
    # lòng bồn: thành inox + đáy tối + lỗ thoát
    b.frame_slab(hw, hd, hw - 0.024, hd - 0.024, hcx, hcy, 0.72, TOP, inox_m, corner=0.03, hcorner=0.018,
                 edge=0.0, cx=hcx, cy=hcy, segs=1)
    b.rbox((hw - 0.022, hd - 0.022, 0.014), (hcx, hcy, 0.727), C["inox_dk"], r=0)
    b.cylinder(0.03, 0.03, 0.733, 0.739, C["charcoal"], segs=8, center=(hcx, hcy))
    # cửa tủ + tay nắm
    for sx in (-1, 1):
        b.rbox((0.435, 0.02, 0.50), (sx * 0.235, -0.238, 0.40), door, r=0.008, segs=1)
        b.rbox((0.018, 0.024, 0.13), (sx * 0.043, -0.252, 0.55), inox_m, r=0)
    # ốp tường sau + vòi
    b.rbox((1.0, 0.03, 0.15), (0, 0.235, TOP + 0.075), C["tile_white"], r=0)
    fx, fy = hcx, 0.185
    b.cylinder(0.045, 0.04, TOP, TOP + 0.04, inox_m, segs=8, center=(fx, fy), round_r=0.01)
    b.rod((fx, fy, TOP + 0.035), (fx, fy, TOP + 0.20), 0.019, inox, segs=6)
    b.blob((fx, fy, TOP + 0.205), (0.026, 0.026, 0.026), inox, u=6, v=3)
    b.rod((fx, fy, TOP + 0.205), (fx, fy - 0.11, TOP + 0.22), 0.018, inox, segs=6)
    b.blob((fx, fy - 0.11, TOP + 0.22), (0.025, 0.025, 0.025), inox, u=6, v=3)
    b.rod((fx, fy - 0.11, TOP + 0.22), (fx, fy - 0.115, TOP + 0.13), 0.017, inox, segs=6)
    for sx, col in ((-1, C["blue"]), (1, C["red"])):                                           # núm lạnh/nóng
        b.cylinder(0.013, 0.013, TOP, TOP + 0.03, inox_m, segs=6, center=(fx + sx * 0.085, fy - 0.01))
        b.cylinder(0.026, 0.022, TOP + 0.03, TOP + 0.058, col, segs=6, center=(fx + sx * 0.085, fy - 0.01),
                   round_r=0.006, round_bottom=False)
    # giá úp ly: khay có gờ, 6 chốt, 6 ly úp
    rx, ry = 0.26, -0.01
    b.frame_slab(0.34, 0.24, 0.30, 0.20, rx, ry, TOP, TOP + 0.022, inox_m, corner=0.04, hcorner=0.03,
                 edge=0.0, cx=rx, cy=ry, segs=1)
    b.rounded_slab(0.30, 0.20, TOP, TOP + 0.01, C["inox_dk"], corner=0.03, edge=0.0, cx=rx, cy=ry, segs=1)
    cols = [C["red"], C["blue"], C["yellow"], C["green"], C["pink"], C["white_pl"]]
    k = 0
    for py in (ry - 0.055, ry + 0.055):
        for px in (rx - 0.095, rx, rx + 0.095):
            b.rod((px, py, TOP + 0.01), (px, py, TOP + 0.12), 0.008, inox_m, segs=4)
            b.cylinder(0.036, 0.028, TOP + 0.02, TOP + 0.115, cols[k], segs=7, center=(px, py))
            k += 1
    return b.to_object(name, {"category": "counter", "footprint_x": 2, "footprint_z": 1,
                              "ref_height": TOP})


def shelf_cups(name):
    """Kệ ly 2×1 (0.96×0.38×1.6 m): tủ thấp 2 cánh + 3 tầng kệ hở lưng gỗ tối, hàng ly nhiều màu / ly thủy tinh /
    bộ phin. Tựa tường phía +Y (lưng kệ sát mép ô)."""
    b = Builder()
    cy = 0.05                                          # tâm kệ lệch về sau: lưng ở y=+0.24, mặt ở y=-0.14
    FY = cy - 0.19
    b.rbox((0.96, 0.38, 0.42), (0, cy, 0.21), C["wood_mid"], r=0.012, segs=1)                   # tủ dưới
    for sx in (-1, 1):
        b.rbox((0.45, 0.018, 0.34), (sx * 0.235, FY - 0.009, 0.22), C["wood_honey"], r=0)
        b.rbox((0.016, 0.02, 0.1), (sx * 0.035, FY - 0.02, 0.27), C["inox_mid"], r=0)
    b.rbox((0.98, 0.40, 0.04), (0, cy, 0.44), C["wood_light"], r=0.01, segs=1)                  # mặt tủ / tầng 0
    for sx in (-1, 1):                                                                        # hai cột bên
        b.rbox((0.04, 0.38, 1.14), (sx * 0.47, cy, 1.03), C["wood_mid"], r=0)
    b.rbox((0.90, 0.025, 1.14), (0, cy + 0.1775, 1.03), C["wood_dark"], r=0)                    # lưng kệ
    for z in (0.855, 1.215):                                                                  # ván kệ
        b.rbox((0.90, 0.36, 0.03), (0, cy, z), C["wood_light"], r=0)
    b.rbox((1.0, 0.40, 0.06), (0, cy, 1.57), C["wood_mid"], r=0.01, segs=1)                    # nắp kệ
    ty = cy + 0.03                                     # đồ đặt lùi về lưng
    # tầng 0: 6 ly sứ nhiều màu
    cols = [C["red"], C["blue"], C["green"], C["yellow"], C["pink"], C["orange"]]
    for i, col in enumerate(cols):
        b.cylinder(0.034, 0.04, 0.46, 0.55, col, segs=6, center=(-0.325 + 0.13 * i, ty))
    # tầng 1: 5 ly thủy tinh cao (kem / bạc hà nhạt xen kẽ)
    for i in range(5):
        b.cylinder(0.03, 0.037, 0.87, 0.98, C["foam"] if i % 2 == 0 else C["mint"], segs=6,
                   center=(-0.30 + 0.15 * i, ty))
    # tầng 2: 4 bộ phin (ly + phin inox)
    for i in range(4):
        x = -0.27 + 0.18 * i
        b.cylinder(0.034, 0.04, 1.23, 1.30, C["bacxiu"], segs=6, center=(x, ty))
        b.cylinder(0.044, 0.036, 1.30, 1.385, C["inox"], segs=6, center=(x, ty))
    return b.to_object(name, {"category": "furniture", "footprint_x": 2, "footprint_z": 1,
                              "ref_height": 1.6})


def _hang(b):
    """Đồ treo: hạ pivot về đáy, trả về props hang_top/mount_y/ref_height."""
    b.ground()
    h = round(b.height(), 3)
    return {"ref_height": h, "hang_top": HANG_TOP, "mount_y": round(HANG_TOP - h, 3)}


def ceiling_fan(name):
    """Quạt trần 3 cánh (≈0.45×0.48 m, cao 0.32 m): thân động cơ kem, 3 cánh gỗ mật ong bầu tròn, ty ngắn + chụp
    trần. Pivot = trục ty, đáy (z=0) = đáy động cơ; treo ở HANG_TOP (xem mount_y)."""
    b = Builder()
    b.cylinder(0.05, 0.085, 0.0, 0.09, C["salt"], segs=10, round_r=0.02)                       # chụp dưới động cơ
    b.cylinder(0.088, 0.088, 0.09, 0.115, C["inox_dk"], segs=10, round_r=0.0)                   # vòng giữ cánh
    b.cylinder(0.06, 0.045, 0.115, 0.17, C["salt"], segs=8)
    b.rod((0, 0, 0.17), (0, 0, 0.28), 0.016, C["inox_mid"], segs=6)                          # ty
    b.cylinder(0.062, 0.046, 0.26, 0.315, C["salt"], segs=10, round_r=0.01, round_bottom=False)  # chụp trần
    for k in range(3):
        th = math.radians(120 * k)
        rz = Matrix.Rotation(th, 4, "Z")
        b.rbox((0.075, 0.04, 0.012), (0.105 * math.cos(th), 0.105 * math.sin(th), 0.1), C["inox_dk"], r=0,
               rot=rz)                                                                       # tay cánh
        b.blob((0.158 * math.cos(th), 0.158 * math.sin(th), 0.098), (0.12, 0.078, 0.017), C["wood_honey"],
               u=8, v=4, rot=rz @ Matrix.Rotation(math.radians(9), 4, "X"))
    props = _hang(b)
    return b.to_object(name, {"category": "furniture", "footprint_x": 1, "footprint_z": 1, **props})


def pendant_lamp(name):
    """Đèn thả: chụp chuông vàng sao (ô 57 = emission) viền caramel, bóng kem nhô dưới vành, dây đen, hoa trần.
    Cao 0.92 m; pivot ở đáy bóng đèn; treo ở HANG_TOP (xem mount_y)."""
    b = Builder()
    b.blob((0, 0, 0.05), (0.052, 0.052, 0.05), C["foam"], u=8, v=5)                           # bóng đèn
    b.cylinder(0.205, 0.075, 0.06, 0.25, C["star"], segs=14, round_r=0.02, round_top=False)   # chụp
    b.cylinder(0.215, 0.21, 0.052, 0.088, C["caramel"], segs=14, round_r=0.008)                # viền vành
    b.cylinder(0.062, 0.042, 0.245, 0.30, C["caramel"], segs=10, round_r=0.01)                 # đầu chụp
    b.rod((0, 0, 0.30), (0, 0, 0.90), 0.008, C["charcoal"], segs=5)                            # dây
    b.cylinder(0.055, 0.045, 0.875, 0.92, C["inox"], segs=10, round_r=0.008)                   # hoa trần
    props = _hang(b)
    return b.to_object(name, {"category": "furniture", "footprint_x": 1, "footprint_z": 1, **props})


def pothos_hanging(name):
    """Chậu trầu bà treo (0.8 m): chậu đất nung, 3 dây treo mây, tán lá + 4 dây lá rủ xuống."""
    b = Builder()
    pot_top, pot_bot = 0.62, 0.46
    b.cylinder(0.085, 0.118, pot_bot, pot_top, C["terracotta"], segs=10, round_r=0.015, round_top=False)
    b.cylinder(0.128, 0.128, pot_top - 0.025, pot_top + 0.012, C["tile_red"], segs=10)        # viền miệng
    b.cylinder(0.108, 0.108, pot_top + 0.005, pot_top + 0.02, C["soil"], segs=10)
    for k in range(3):                                                                         # dây treo
        a = math.radians(120 * k + 30)
        b.rod((0.115 * math.cos(a), 0.115 * math.sin(a), pot_top), (0, 0, 0.86), 0.006, C["bamboo"], segs=4)
    b.cylinder(0.026, 0.02, 0.85, 0.89, C["inox_mid"], segs=6)                                 # móc / khoen
    # tán lá trên chậu
    greens = [C["leaf_lt"], C["leaf_md"], C["leaf_moss"]]
    for i in range(5):
        a = 2 * math.pi * i / 5 + 0.4
        d = (math.cos(a) * 0.75, math.sin(a) * 0.75, 0.6)
        b.leaf((math.cos(a) * 0.04, math.sin(a) * 0.04, pot_top + 0.015), d, 0.15, 0.1, greens[i % 3],
               thick=0.3, u=5, v=3)
    # 4 dây lá rủ: mỗi dây = que mảnh + 4 lá xen kẽ
    lens = (0.52, 0.40, 0.33, 0.45)
    for i, ln in enumerate(lens):
        a = math.radians(90 * i + 50)
        px, py = math.cos(a) * 0.12, math.sin(a) * 0.12
        outx, outy = math.cos(a), math.sin(a)
        top = pot_top - 0.02
        bot = top - ln
        b.rod((px, py, top), (px + outx * 0.045, py + outy * 0.045, bot), 0.005, C["leaf_dk"], segs=3)
        for j in range(4):
            t = 0.12 + j * 0.22
            z = top - ln * t
            ox = px + outx * 0.045 * t
            oy = py + outy * 0.045 * t
            side = 1 if j % 2 == 0 else -1
            tang = (-outy * side, outx * side, 0)
            d = (tang[0] * 0.6 + outx * 0.25, tang[1] * 0.6 + outy * 0.25, -0.7)
            b.leaf((ox, oy, z), d, 0.135, 0.095, greens[(i + j) % 3], thick=0.3, u=5, v=3)
    props = _hang(b)
    return b.to_object(name, {"category": "furniture", "footprint_x": 1, "footprint_z": 1, **props})


def wood_table(name):
    """Bàn gỗ vuông 2×2 (0.94×0.94, cao 0.75 m) cho 2 chỗ ngồi: mặt dày bo tròn, váy gỗ, 4 chân mập thuôn,
    giữa mặt bàn khảm một viên gạch bông."""
    b = Builder()
    h = 0.75
    b.rounded_slab(0.94, 0.94, h - 0.07, h, C["wood_honey"], corner=0.12, edge=0.022)
    b.rounded_slab(0.80, 0.80, h - 0.15, h - 0.07, C["wood_mid"], corner=0.09, edge=0.0)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.leg((sx * 0.35, sy * 0.35, h - 0.1), (sx * 0.385, sy * 0.385, 0.0), 0.05, 0.04, C["wood_mid"])
    d45 = Matrix.Rotation(math.radians(45), 4, "Z")
    b.rbox((0.30, 0.30, 0.012), (0, 0, h + 0.0005), C["tile_cream"], r=0.003, segs=1)         # viên gạch khảm
    b.rbox((0.19, 0.19, 0.01), (0, 0, h + 0.0035), C["tile_red"], r=0, rot=d45)
    b.rbox((0.075, 0.075, 0.01), (0, 0, h + 0.0055), C["tile_blue"], r=0, rot=d45)
    return b.to_object(name, {"category": "furniture", "footprint_x": 2, "footprint_z": 2,
                              "ref_height": h, "seats": 2})


def wood_chair(name):
    """Ghế gỗ 1×1 (0.44×0.44, mặt ngồi 0.45 m, tổng cao 0.86 m): mặt ngồi dày + đệm gạch nung, 4 chân mập,
    lưng tựa 2 thanh về phía +Y (mặt trước/chỗ ngồi hướng -Y)."""
    b = Builder()
    seat = 0.45
    b.rounded_slab(0.40, 0.40, seat - 0.07, seat - 0.03, C["wood_honey"], corner=0.07, edge=0.0, segs=2)
    b.rounded_slab(0.37, 0.37, seat - 0.03, seat, C["tile_red"], corner=0.08, edge=0.012, segs=2)
    for sx in (-1, 1):
        b.leg((sx * 0.155, -0.155, seat - 0.07), (sx * 0.18, -0.18, 0.0), 0.034, 0.042, C["wood_mid"], segs=6)
        b.leg((sx * 0.165, 0.19, 0.86), (sx * 0.18, 0.185, 0.0), 0.03, 0.042, C["wood_mid"], segs=6)  # chân sau + trụ lưng
        b.rod((sx * 0.172, -0.17, 0.17), (sx * 0.172, 0.185, 0.17), 0.012, C["wood_dark"], segs=4)
    b.rbox((0.30, 0.036, 0.14), (0, 0.2, 0.69), C["wood_honey"], r=0.012, segs=1)           # tấm lưng dưới
    b.rbox((0.37, 0.046, 0.06), (0, 0.205, 0.83), C["wood_honey"], r=0.015, segs=1)          # thanh trên cùng
    return b.to_object(name, {"category": "furniture", "footprint_x": 1, "footprint_z": 1,
                              "ref_height": seat, "seat_height": seat, "total_height": 0.86})


def rolling_shutter(name):
    """Khung cửa cuốn 3×1 (1.5 m rộng, 2.43 m cao), cửa kéo lên một nửa: 2 ray bên + hộp cuộn xanh ngọc,
    tấm lá sóng inox (xen 2 sắc) kéo tới 1.2 m, thanh đáy + khóa, ngưỡng cửa gạch xám."""
    b = Builder()
    b.rounded_slab(1.50, 0.34, 0.0, 0.03, C["tile_grey"], corner=0.03, edge=0.008, segs=2)    # ngưỡng
    for sx in (-1, 1):
        b.rbox((0.12, 0.17, 2.18), (sx * 0.69, 0.0, 1.12), C["door_teal"], r=0.015, segs=1)  # ray bên
    b.rbox((1.50, 0.26, 0.26), (0, 0.0, 2.30), C["door_teal"], r=0.035, segs=1)                 # hộp cuộn
    b.rbox((1.40, 0.04, 0.05), (0, -0.14, 2.20), C["teal"], r=0.008, segs=1)                   # gờ viền hộp
    n, y0 = 15, 2.15
    pitch = 0.063
    for i in range(n):
        z = y0 - pitch * (i + 0.5)
        b.rbox((1.28, 0.022, 0.056), (0, 0.007 * (i % 2) - 0.0035, z), C["inox"] if i % 2 == 0 else C["inox_mid"],
               r=0)
    zb = y0 - pitch * n - 0.04
    b.rbox((1.34, 0.055, 0.07), (0, 0.0, zb), C["inox_dk"], r=0.012, segs=1)                    # thanh đáy
    b.rbox((0.1, 0.03, 0.05), (0.32, -0.04, zb), C["charcoal"], r=0.008, segs=1)               # khóa
    b.rbox((0.16, 0.03, 0.02), (-0.32, -0.04, zb), C["charcoal"], r=0)                         # tay kéo
    return b.to_object(name, {"category": "furniture", "footprint_x": 3, "footprint_z": 1,
                              "ref_height": 2.4, "shutter_open_height": round(zb - 0.035, 3)})


def trash_bin(name):
    """Thùng rác nhỏ có nắp (Ø0.32, cao 0.43 m): thân xanh dù thuôn, nắp inox vòm, núm cầm, bàn đạp trước (-Y)."""
    b = Builder()
    b.cylinder(0.128, 0.152, 0.0, 0.33, C["umbrella"], segs=8, round_r=0.014, round_top=False)
    b.cylinder(0.158, 0.158, 0.31, 0.345, C["inox_mid"], segs=8)                               # vành
    b.cylinder(0.155, 0.12, 0.345, 0.39, C["inox"], segs=8, round_r=0.012, round_bottom=False)  # nắp
    b.cylinder(0.028, 0.022, 0.39, 0.425, C["charcoal"], segs=6)                              # núm
    b.rbox((0.1, 0.08, 0.02), (0, -0.165, 0.035), C["charcoal"], r=0)                          # bàn đạp
    b.rbox((0.06, 0.03, 0.05), (0, 0.158, 0.33), C["inox_dk"], r=0)                           # bản lề
    b.rbox((0.15, 0.012, 0.045), (0, -0.147, 0.2), C["yellow"], r=0)                          # nhãn vàng
    return b.to_object(name, {"category": "furniture", "footprint_x": 1, "footprint_z": 1,
                              "ref_height": 0.43})


ASSETS_T1 = {
    "SM_Stool_Plastic_Red": lambda n: plastic_stool(n, C["red"], C["red_dk"]),
    "SM_Stool_Plastic_Blue": lambda n: plastic_stool(n, C["blue"], C["blue_dk"]),
    "SM_Stool_Plastic_Green": lambda n: plastic_stool(n, C["green"], C["green_dk"]),
    "SM_Table_Low_Blue_2x2": lambda n: low_table(n, C["blue"], C["blue_dk"]),
    "SM_Table_Low_Inox_2x2": lambda n: low_table(n, C["inox"], C["inox_dk"]),
    "SM_Cart_Coffee_3x2": coffee_cart,
    "SM_Umbrella_Street_4x4": street_umbrella,
    "SM_Cup_Phin_SuaDa": phin_cup,
    "SM_Plant_Pot_Small": plant_pot_small,
    "SM_Plant_Areca_Tall": areca_tall,
    "SM_Board_Menu_A": menu_board_a,
}

ASSETS_T2 = {
    "SM_Counter_Bar_4x2": counter_bar,
    "SM_Sink_Counter": sink_counter,
    "SM_Shelf_Cups": shelf_cups,
    "SM_Fan_Ceiling": ceiling_fan,
    "SM_Lamp_Pendant": pendant_lamp,
    "SM_Plant_Pothos_Hanging": pothos_hanging,
    "SM_Table_Wood_2Seat": wood_table,
    "SM_Chair_Wood": wood_chair,
    "SM_Door_Rolling_Shutter": rolling_shutter,
    "SM_Trash_Bin": trash_bin,
}

ASSETS = {**ASSETS_T1, **ASSETS_T2}


# ---------------------------------------------------------------- export / preview

def export_fbx(obj):
    EXPORT_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=str(EXPORT_DIR / f"{obj.name}.fbx"),
        use_selection=True, object_types={"MESH"},
        axis_forward="-Z", axis_up="Y",
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=True, use_mesh_modifiers=True,
        mesh_smooth_type="EDGE", use_triangles=True,
        path_mode="STRIP", embed_textures=False, add_leaf_bones=False,
        use_custom_props=True,
    )


# Bố cục ảnh preview (Blender coords, mặt trước nhìn -Y): tên -> (x, y, z)
PREVIEW_LAYOUT = {
    "SM_Cart_Coffee_3x2": (-0.2, 1.1, 0),
    "SM_Umbrella_Street_4x4": (1.9, 1.3, 0),
    "SM_Stool_Plastic_Red": (-1.9, -0.7, 0),
    "SM_Stool_Plastic_Blue": (-1.35, -0.7, 0),
    "SM_Stool_Plastic_Green": (-0.8, -0.7, 0),
    "SM_Table_Low_Blue_2x2": (0.2, -0.7, 0),
    "SM_Table_Low_Inox_2x2": (1.2, -0.7, 0),
    "SM_Cup_Phin_SuaDa": (0.2, -0.75, 0.45),  # đặt trên bàn xanh
    "SM_Plant_Pot_Small": (-2.7, -0.6, 0),
    "SM_Plant_Areca_Tall": (-2.7, 0.6, 0),
    "SM_Board_Menu_A": (2.1, -0.7, 0),
}

# Bố cục preview Tier 2 — một góc quán nhà phố: (asset, x, y, z, xoay Z độ). z=None -> dùng `mount_y` (đồ treo).
# Một asset có thể xuất hiện nhiều lần (bản sao liên kết). Tường sau ở y=2.5; cửa cuốn đặt trong tường sau.
PREVIEW_LAYOUT_T2 = [
    ("SM_Shelf_Cups", -2.55, 2.26, 0, 0),
    ("SM_Sink_Counter", -1.15, 2.25, 0, 0),
    ("SM_Counter_Bar_4x2", -1.0, 0.75, 0, 0),
    ("SM_Lamp_Pendant", -1.5, -0.1, None, 0),
    ("SM_Lamp_Pendant", -0.5, -0.1, None, 0),
    ("SM_Table_Wood_2Seat", 1.7, 0.3, 0, 0),
    ("SM_Chair_Wood", 1.7, 1.15, 0, 0),
    ("SM_Chair_Wood", 1.7, -0.55, 0, 180),
    ("SM_Fan_Ceiling", 1.7, 0.3, None, 0),
    ("SM_Door_Rolling_Shutter", 3.2, 2.5, 0, 0),
    ("SM_Trash_Bin", 4.15, 2.15, 0, 0),
    ("SM_Plant_Pothos_Hanging", -3.5, 1.4, None, 0),
]


def _set_eevee(scene):
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = engine
            return
        except TypeError:
            continue


def _stage_common(scene):
    """Engine/world/sun giống nhau cho mọi preview. Trả về sun."""
    _set_eevee(scene)
    scene.view_settings.view_transform = "Standard"  # giữ đúng màu palette (AgX làm nhạt màu)
    scene.render.resolution_x, scene.render.resolution_y = 1920, 1080
    world = bpy.data.worlds.new("PREVIEW_World")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.62, 0.78, 0.90, 1)
    bg.inputs["Strength"].default_value = 0.5
    scene.world = world
    sun_data = bpy.data.lights.new("PREVIEW_Sun", "SUN")
    sun_data.energy = 3.2
    sun_data.color = (1.0, 0.86, 0.68)  # nắng vàng buổi sáng (art_bible §6)
    sun_data.angle = math.radians(4)
    sun = bpy.data.objects.new("PREVIEW_Sun", sun_data)
    sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
    scene.collection.objects.link(sun)
    return sun


def _stage_camera(scene, target, ortho):
    cam_data = bpy.data.cameras.new("PREVIEW_Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho
    cam = bpy.data.objects.new("PREVIEW_Cam", cam_data)
    scene.collection.objects.link(cam)
    target = Vector(target)
    cam.location = target + Vector((0.45, -1.0, 0.85)).normalized() * 15  # góc 3/4 như camera game
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    return cam


def _isolate(keep):
    """Ẩn khỏi render mọi mesh không thuộc `keep` (model của tier kia nằm chồng ở gốc toạ độ). Trả về danh sách đã ẩn."""
    keep, hidden = set(keep), []
    for o in bpy.context.scene.objects:
        if o.type == "MESH" and o not in keep and not o.name.startswith("PREVIEW_") and not o.hide_render:
            o.hide_render = True
            hidden.append(o)
    return hidden


def render_preview(objs):
    """Tier 1: xếp các model thành một góc quán vỉa hè rồi render EEVEE toon."""
    for o in objs:
        o.location = PREVIEW_LAYOUT.get(o.name, (0, -2.0, 0))
    hidden = _isolate(objs)
    scene = bpy.context.scene
    sun = _stage_common(scene)
    floor = Builder()
    floor.rounded_slab(9.0, 6.0, -0.03, 0.0, 45, corner=0, edge=0)  # gạch xám vỉa hè
    floor = floor.to_object("PREVIEW_Floor", {})
    cam = _stage_camera(scene, (0.0, 0.15, 0.75), 7.6)
    scene.render.filepath = str(EXPORT_DIR / "preview_tier1.png")
    bpy.ops.render.render(write_still=True)

    # Dọn: trả model về gốc, xóa đồ preview để .blend sạch
    for o in hidden:
        o.hide_render = False
    for o in objs:
        o.location = (0, 0, 0)
    for o in (floor, cam, sun):
        bpy.data.objects.remove(o, do_unlink=True)


def _stage_room_t2():
    """Sàn gạch bông + 2 bức tường (sau & trái) sơn kem, chân tường bạc hà; tường sau có ô cửa cho cửa cuốn."""
    made = []
    fl = Builder()
    fl.rounded_slab(10.0, 5.2, -0.03, -0.006, C["grout"], corner=0, edge=0, cy=0.2)
    d45 = Matrix.Rotation(math.radians(45), 4, "Z")
    for i in range(-9, 9):
        for j in range(-4, 6):
            x, y = 0.25 + i * 0.5, 0.25 + j * 0.5
            if not (-4.6 <= x <= 4.6 and -2.1 <= y <= 2.5):
                continue
            fl.rbox((0.47, 0.47, 0.006), (x, y, -0.003), C["tile_cream"], r=0)
            fl.rbox((0.27, 0.27, 0.004), (x, y, -0.0015), C["beige"] if (i + j) % 2 else C["tile_cream"], r=0,
                    rot=d45)                                          # sàn êm mắt để nổi đồ nội thất
            fl.rbox((0.1, 0.1, 0.006), (x, y, -0.001), C["terracotta"] if (i + j) % 2 else C["mint"], r=0,
                    rot=d45)
    floor = fl.to_object("PREVIEW_Floor", {})
    floor.visible_shadow = False   # nhiều lớp gạch mỏng dễ sinh nhiễu bóng, sàn chỉ cần NHẬN bóng
    made.append(floor)

    w = Builder()
    for (x0, x1) in ((-4.7, 2.45), (3.95, 4.7)):                      # tường sau, chừa ô cửa 2.45..3.95
        w.rbox((x1 - x0, 0.16, 0.95), ((x0 + x1) / 2, 2.58, 0.475), C["mint"], r=0)
        w.rbox((x1 - x0, 0.16, 2.05), ((x0 + x1) / 2, 2.58, 1.975), C["wall"], r=0)
        w.rbox((x1 - x0, 0.17, 0.05), ((x0 + x1) / 2, 2.58, 0.975), C["wood_honey"], r=0)
    w.rbox((1.5, 0.16, 0.6), (3.2, 2.58, 2.7), C["wall"], r=0)                                   # lanh tô
    w.rbox((0.16, 7.4, 0.95), (-4.62, 0.15, 0.475), C["mint"], r=0)                             # tường trái
    w.rbox((0.16, 7.4, 2.05), (-4.62, 0.15, 1.975), C["wall"], r=0)
    w.rbox((0.17, 7.4, 0.05), (-4.62, 0.15, 0.975), C["wood_honey"], r=0)
    made.append(w.to_object("PREVIEW_Walls", {}))
    return made


def render_preview_tier2(objs):
    """Tier 2: một góc quán nhà phố — sàn gạch bông, tường kem/bạc hà, quầy, bàn ghế, đồ treo đúng mount_y."""
    by_name = {o.name: o for o in objs}
    hidden = _isolate(objs)
    scene = bpy.context.scene
    sun = _stage_common(scene)
    room = _stage_room_t2()
    extras, used = [], set()

    def place(obj, x, y, z, rot):
        z = obj.get("mount_y", 0.0) if z is None else z
        obj.location = (x, y, z)
        obj.rotation_euler = (0, 0, math.radians(rot))

    for name, x, y, z, rot in PREVIEW_LAYOUT_T2:
        src = by_name.get(name)
        if src is None:
            continue
        if name in used:  # bản sao liên kết (chung mesh) — tên PREVIEW_ để validator bỏ qua
            o = bpy.data.objects.new(f"PREVIEW_{name}_{len(extras)}", src.data)
            scene.collection.objects.link(o)
            for k in src.keys():
                o[k] = src[k]
            extras.append(o)
        else:
            o = src
            used.add(name)
        place(o, x, y, z, rot)
    # đồ nhỏ tham khảo tỷ lệ: ly phin trên bàn, chậu cây góc phải
    cup = phin_cup("PREVIEW_Cup")
    cup.location = (1.7, 0.3, 0.76)
    plant = plant_pot_small("PREVIEW_Plant")
    plant.location = (4.1, 0.3, 0)
    extras += [cup, plant]
    cam = _stage_camera(scene, (0.35, 1.0, 1.0), 10.2)
    scene.render.filepath = str(EXPORT_DIR / "preview_tier2.png")
    bpy.ops.render.render(write_still=True)

    for o in hidden:
        o.hide_render = False
    for o in objs:
        o.location, o.rotation_euler = (0, 0, 0), (0, 0, 0)
    for o in room + extras + [cam, sun]:
        bpy.data.objects.remove(o, do_unlink=True)


def _save_blends(objs):
    """Mỗi tier một .blend: furniture_tier1.blend / furniture_tier2.blend (chỉ chứa object của tier đó)."""
    BLEND_OUT.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    for path, group in ((BLEND_OUT, ASSETS_T1), (BLEND_OUT_T2, ASSETS_T2)):
        keep = [o for o in objs if o.name in group]
        if not keep:
            continue
        hidden = [o for o in objs if o.name not in group]
        for o in hidden:
            scene.collection.objects.unlink(o)
        bpy.ops.wm.save_as_mainfile(filepath=str(path))
        for o in hidden:
            scene.collection.objects.link(o)
        print(f"  blend: {path.name} ({len(keep)} asset)")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--render", action="store_true", help="render preview_tier1.png / preview_tier2.png")
    ap.add_argument("--only", nargs="*", help="chỉ sinh các asset này (KHÔNG ghi lại .blend để khỏi mất asset khác)")
    args = ap.parse_args(argv)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    names = args.only or list(ASSETS)
    objs = []
    for n in names:
        obj = ASSETS[n](n)
        export_fbx(obj)
        objs.append(obj)
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        print(f"  {n:28s} {tris:5d} tris  dims={tuple(round(d, 3) for d in obj.dimensions)}")

    if args.render:
        t1 = [o for o in objs if o.name in ASSETS_T1]
        t2 = [o for o in objs if o.name in ASSETS_T2]
        if t1:
            render_preview(t1)
        if t2:
            render_preview_tier2(t2)

    if args.only:
        print("(--only: bỏ qua ghi .blend)")
    else:
        _save_blends(objs)
    print(f"OK: {len(objs)} asset -> {EXPORT_DIR}")


if __name__ == "__main__":
    main()
