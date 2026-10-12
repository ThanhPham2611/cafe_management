"""Render thumbnail cửa hàng cho mọi SM_*.fbx nội thất/prop: 256x256 PNG nền trong suốt, toon, góc 3/4.

    blender --background --python tools/render_icons.py -- [--only Stool_Plastic_Red ...] [--tier 1|2] [--size 256]

Kết quả: art_source/export/icons/ICON_<id>.png  (<id> = tên FBX bỏ "SM_", vd ICON_Board_Menu_A.png)
Đọc FBX từ art_source/export/ (không đụng characters/). Vật thể phủ ~80% khung, có bóng tiếp xúc mềm
(đồ treo trần — có custom prop mount_y/hang_top — không có bóng sàn). `--tier 2` = chỉ icon nội thất Tier 2
(danh sách lấy từ gen_furniture.ASSETS_T2 + ASSETS_T2B). Bảng tổng hợp xem nhanh: python tools/make_icon_sheet.py [--tier 2].
"""
import argparse
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import gen_furniture as gf  # noqa: E402

OUT_DIR = gf.EXPORT_DIR / "icons"
FILL = 0.80
VIEW_DIR = Vector((0.45, -1.0, 0.85)).normalized()   # cùng góc 3/4 với camera game / preview_tier1


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    return scene


def setup_render(scene, size):
    gf._set_eevee(scene)
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x = scene.render.resolution_y = size
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    world = bpy.data.worlds.new("ICON_World")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.62, 0.78, 0.90, 1)
    bg.inputs["Strength"].default_value = 0.5
    scene.world = world
    sd = bpy.data.lights.new("ICON_Sun", "SUN")
    sd.energy, sd.color, sd.angle = 3.2, (1.0, 0.86, 0.68), math.radians(4)
    sun = bpy.data.objects.new("ICON_Sun", sd)
    sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
    scene.collection.objects.link(sun)
    cd = bpy.data.cameras.new("ICON_Cam")
    cd.type = "ORTHO"
    cam = bpy.data.objects.new("ICON_Cam", cd)
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def shadow_material():
    mat = bpy.data.materials.new("ICON_Shadow")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    coord = nt.nodes.new("ShaderNodeTexCoord")
    grad = nt.nodes.new("ShaderNodeTexGradient")
    grad.gradient_type = "SPHERICAL"
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Location"].default_value = (-0.5, -0.5, 0)   # tâm gradient ở giữa tấm
    mapping.inputs["Scale"].default_value = (1, 1, 1)
    nt.links.new(coord.outputs["UV"], mapping.inputs["Vector"])
    nt.links.new(mapping.outputs["Vector"], grad.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    # fac gradient: 0.5 ở mép tấm -> 1 ở tâm; Mix Fac = độ trong suốt (1 = trong hẳn)
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = 0.5, (1, 1, 1, 1)
    ramp.color_ramp.elements[1].position, ramp.color_ramp.elements[1].color = 1.0, (0.80, 0.80, 0.80, 1)
    nt.links.new(grad.outputs["Fac"], ramp.inputs["Fac"])
    emis = nt.nodes.new("ShaderNodeEmission")
    emis.inputs["Color"].default_value = (0.12, 0.08, 0.08, 1)
    transp = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(ramp.outputs["Color"], mix.inputs["Fac"])
    nt.links.new(emis.outputs["Emission"], mix.inputs[1])
    nt.links.new(transp.outputs["BSDF"], mix.inputs[2])
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])
    try:
        mat.surface_render_method = "BLENDED"
    except Exception:
        mat.blend_method = "BLEND"
    return mat


def add_shadow(radius):
    """Đĩa bóng mềm dưới vật thể (alpha giảm dần ra mép)."""
    bpy.ops.mesh.primitive_plane_add(size=2 * radius, location=(0, 0, 0.002))
    plane = bpy.context.active_object
    plane.name = "ICON_ShadowPlane"
    # UV plane mặc định 0..1: mapping dịch -0.5 để gradient spherical tâm ở giữa (bán kính 0.5)
    plane.data.materials.append(shadow_material())
    plane.visible_shadow = False
    return plane


def world_corners(objs):
    pts = []
    for o in objs:
        pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
    return pts


def frame(cam, pts):
    """Đặt camera ortho sao cho bbox các điểm phủ FILL khung, canh giữa."""
    rot = (-VIEW_DIR).to_track_quat("-Z", "Y")
    cam.rotation_euler = rot.to_euler()
    right = rot @ Vector((1, 0, 0))
    up = rot @ Vector((0, 1, 0))
    xs = [p.dot(right) for p in pts]
    ys = [p.dot(up) for p in pts]
    w, h = max(xs) - min(xs), max(ys) - min(ys)
    cam.data.ortho_scale = max(w, h) / FILL
    cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
    center = right * cx + up * cy
    cam.location = center + VIEW_DIR * 20
    cam.data.clip_start, cam.data.clip_end = 0.1, 60


def render_one(scene, cam, fbx, size):
    for o in list(bpy.data.objects):
        if o.name not in ("ICON_Cam", "ICON_Sun"):
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx), axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, use_custom_props=True)
    meshes = [o for o in scene.objects if o.type == "MESH" and not o.name.startswith("ICON_")]
    for o in meshes:  # bỏ material FBX (trùng tên M_Toon_Palette nhưng không có node) để dựng lại bản toon
        o.data.materials.clear()
    for m in list(bpy.data.materials):
        if not m.name.startswith("ICON_"):
            bpy.data.materials.remove(m)
    toon = gf.get_material()
    for o in meshes:
        o.data.materials.clear()
        o.data.materials.append(toon)
        for p in o.data.polygons:
            p.use_smooth = True
    pts = world_corners(meshes)
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), 0))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), 0))
    # đồ treo trần (quạt, đèn thả, chậu treo): không có bóng tiếp xúc dưới sàn, vật thể "lơ lửng"
    hanging = any(o.get("mount_y") is not None or o.get("hang_top") is not None for o in meshes)
    if not hanging:
        shadow_r = max(hi.x - lo.x, hi.y - lo.y) * 0.55
        plane = add_shadow(shadow_r)
        plane.location.x, plane.location.y = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2
    bpy.context.view_layer.update()
    frame(cam, pts)   # chỉ khung theo vật thể (bóng nằm trong/sát mép)
    icon_id = fbx.stem[3:] if fbx.stem.startswith("SM_") else fbx.stem
    out = OUT_DIR / f"ICON_{icon_id}.png"
    scene.render.filepath = str(out)
    bpy.ops.render.render(write_still=True)
    return icon_id


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", nargs="*", help="chỉ render các id này (vd Board_Menu_A)")
    ap.add_argument("--tier", type=int, choices=(1, 2), help="chỉ render icon của tier này")
    ap.add_argument("--size", type=int, default=256)
    args = ap.parse_args(argv)

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    files = sorted(gf.EXPORT_DIR.glob("SM_*.fbx"))
    if args.only:
        files = [f for f in files if f.stem[3:] in args.only]
    if args.tier:
        group = gf.ASSETS_T1 if args.tier == 1 else {**gf.ASSETS_T2, **gf.ASSETS_T2B}
        files = [f for f in files if f.stem in group]
    scene = clear_scene()
    cam = setup_render(scene, args.size)
    done = []
    for f in files:
        done.append(render_one(scene, cam, f, args.size))
        print(f"  ICON_{done[-1]}.png")
    print("ICONS:", ", ".join(done))


if __name__ == "__main__":
    main()
