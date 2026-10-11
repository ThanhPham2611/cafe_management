"""Kiểm tra asset theo art_bible.md §12. Fail = không được copy vào Unity.

    blender --background --python tools/blender_validate.py -- [file.blend|file.fbx ...]

Không truyền file thì kiểm tra art_source/blend/*.blend và art_source/export/*.fbx.
Ghi validate_report.json vào art_source/export/, exit code 1 nếu có ERROR.
"""
import json
import re
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parent.parent
GRID = 8
UV_MARGIN = 2 / 256  # 2 px trên texture 256
TRI_BUDGET = {"prop": 300, "furniture": 800, "counter": 3000, "building": 8000, "character": 5000}
ALLOWED_MATS = {"M_Toon_Palette", "M_Toon_Decal"}
ALLOWED_IMAGES = {"T_Palette_256", "T_Palette_256.png", "T_Decal_Atlas_512", "T_Decal_Atlas_512.png"}
NAME_RE = re.compile(r"^(SM|SK)_[A-Za-z0-9]+(_[A-Za-z0-9]+)*$")
TOL = 0.05
TOL_HANG = 0.10   # đồ treo trần (prop hang_top/mount_y): pivot = trục treo, bbox có thể lệch (quạt 3 cánh không đối xứng 2 trục)


class Report:
    def __init__(self, source, obj):
        self.entry = {"source": source, "object": obj, "errors": [], "warnings": []}

    def err(self, rule, msg):
        self.entry["errors"].append(f"#{rule} {msg}")

    def warn(self, rule, msg):
        self.entry["warnings"].append(f"#{rule} {msg}")


def world_bbox(obj):
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def check_uv(mesh, r):
    if len(mesh.uv_layers) != 1 or mesh.uv_layers[0].name != "UVMap":
        r.err(8, f"cần đúng 1 UV map tên 'UVMap', có {[u.name for u in mesh.uv_layers]}")
    if not mesh.uv_layers:
        return
    uv = mesh.uv_layers[0].data
    bad = 0
    for poly in mesh.polygons:
        cells = set()
        ok = True
        for li in poly.loop_indices:
            u, v = uv[li].uv
            if not (0 <= u <= 1 and 0 <= v <= 1):
                ok = False
                break
            col, row_from_bottom = int(min(u * GRID, GRID - 1e-6)), int(min(v * GRID, GRID - 1e-6))
            cells.add((col, row_from_bottom))
            fu, fv = u * GRID - col, v * GRID - row_from_bottom
            m = UV_MARGIN * GRID
            if not (m <= fu <= 1 - m and m <= fv <= 1 - m):
                ok = False
        if not ok or len(cells) != 1:
            bad += 1
    if bad:
        r.err(7, f"{bad} face có UV không nằm gọn trong 1 ô palette (margin 2px)")


def check_object(obj, source):
    r = Report(source, obj.name)
    mesh = obj.data
    is_sk = obj.name.startswith("SK_")

    # 2. transform
    if tuple(round(s, 4) for s in obj.scale) != (1, 1, 1):
        r.err(2, f"scale chưa apply: {tuple(obj.scale)}")
    if any(abs(a) > 1e-4 for a in obj.rotation_euler):
        r.err(2, f"rotation chưa apply: {tuple(obj.rotation_euler)}")

    # 3. pivot
    lo, hi = world_bbox(obj)
    if obj.location.length > 1e-4:
        r.err(3, f"origin không ở (0,0,0): {tuple(obj.location)}")
    if abs(lo.z) > 0.005:
        r.err(3, f"đáy không chạm sàn: min z = {lo.z:.3f}")
    cx, cy = (lo.x + hi.x) / 2, (lo.y + hi.y) / 2
    tol = TOL_HANG if obj.get("hang_top") is not None else TOL
    if abs(cx) > tol or abs(cy) > tol:
        r.err(3, f"tâm XY bbox lệch ({cx:.3f}, {cy:.3f})")
    if obj.get("hang_top") is not None and obj.get("mount_y") is not None:   # đồ treo: mount_y + cao = hang_top
        if abs(obj["mount_y"] + hi.z - obj["hang_top"]) > 0.02:
            r.err(18, f"mount_y {obj['mount_y']} + cao {hi.z:.3f} != hang_top {obj['hang_top']}")

    # 4. footprint (ô 0.5 m)
    fx, fz = obj.get("footprint_x"), obj.get("footprint_z")
    if fx is None or fz is None:
        if not is_sk:
            r.err(4, "thiếu custom prop footprint_x / footprint_z")
    else:
        if hi.x - lo.x > fx * 0.5 + TOL or hi.y - lo.y > fz * 0.5 + TOL:
            r.err(4, f"vượt footprint {fx}x{fz}: {hi.x - lo.x:.2f} x {hi.y - lo.y:.2f} m")

    # 5. tri budget
    tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
    cat = obj.get("category", "character" if is_sk else None)
    budget = TRI_BUDGET.get(cat)
    if budget is None:
        r.warn(5, f"category '{cat}' không rõ, bỏ qua budget ({tris} tris)")
    elif tris > budget:
        r.err(5, f"{tris} tris > budget {budget} ({cat})")
    elif tris > budget * 0.9:
        r.warn(5, f"{tris} tris > 90% budget {budget}")
    r.entry["tris"] = tris

    # 6. material
    mats = [m.name.split(".")[0] if m else None for m in mesh.materials]
    if len(mats) != 1 or mats[0] not in ALLOWED_MATS:
        r.err(6, f"cần đúng 1 material M_Toon_Palette, có {mats}")

    # 7, 8. UV
    check_uv(mesh, r)

    # 9. hình học cơ bản
    linked = set()
    for e in mesh.edges:
        linked.update(e.vertices)
    loose = len(mesh.vertices) - len(linked)
    if loose:
        r.warn(9, f"{loose} vertex lẻ")

    # 12. modifier
    if obj.modifiers:
        r.err(12, f"còn modifier chưa apply: {[m.name for m in obj.modifiers]}")

    # 13. naming
    if not NAME_RE.match(obj.name) or not obj.name.isascii():
        r.err(13, f"tên '{obj.name}' sai quy ước SM_/SK_ PascalCase ASCII")
    if mesh.name != obj.name and not source.endswith(".fbx"):  # importer FBX tự thêm .001
        r.warn(13, f"tên mesh '{mesh.name}' khác tên object")
    if source.endswith(".fbx") and Path(source).stem != obj.name:
        r.err(13, f"tên file '{Path(source).stem}' khác tên object")

    # 15. vertex color / shape key
    if getattr(mesh, "color_attributes", None) and len(mesh.color_attributes):
        r.warn(15, "có vertex color")
    if mesh.shape_keys and not is_sk:
        r.warn(15, "có shape key")

    # 16. hướng trước
    if obj.get("front") != "-Y" and not source.endswith(".fbx"):
        r.warn(16, "thiếu custom prop front='-Y'")

    # 18. chiều cao tham chiếu
    ref = obj.get("ref_height")
    if ref is not None and hi.z < ref - 0.03:
        r.warn(18, f"cao {hi.z:.2f} m < ref_height {ref}")
    return r.entry


CHAR_BUDGET = 3000
OWNER_BUDGET = 4500   # chủ quán + nhân viên (hero: ít nhân vật trên màn hình)
RECOLOR_PREFIXES = ("Shirt", "Hair", "Skin")   # runtime recolor: mỗi mesh đúng 1 ô palette
OWNER_TAG = re.compile(r"^(Owner|Staff)_[A-Z0-9]+_")    # tiền tố .blend của chủ quán / nhân viên (tag_children)
CHAR_PARTS = {"Hips", "Head", "Arm_L", "Arm_R", "Leg_L", "Leg_R"}


def is_char_root(o):
    return o.type == "EMPTY" and o.parent is None and o.name.split(".")[0].startswith("SM_Char_")


def descendants(o):
    out = []
    for c in o.children:
        out.append(c)
        out += descendants(c)
    return out


def check_character(root, source):
    """Hierarchy nhân vật: empty gốc ở (0,0,0), con Hips/Head/Arm_*/Leg_*, 1 material, UV palette, tri tổng."""
    r = Report(source, root.name)
    meshes = [o for o in descendants(root) if o.type == "MESH"]
    names = {o.name.split(".")[0] for o in descendants(root)}
    missing = [p for p in CHAR_PARTS if not any(n == p or n.endswith("_" + p) for n in names)]
    if missing:
        r.err(13, f"thiếu bộ phận: {sorted(missing)}")
    if root.location.length > 1e-4:
        r.err(3, f"root không ở (0,0,0): {tuple(root.location)}")
    if source.endswith(".fbx") and Path(source).stem != root.name.split(".")[0]:
        r.err(13, f"tên file '{Path(source).stem}' khác tên root '{root.name}'")
    # chủ quán (SM_Char_Owner_*) và nhân viên (SM_Char_Staff_*) cùng luật: HandSocket_R, mesh Shirt/Hair/Skin 1 ô màu
    is_owner = root.name.split(".")[0].startswith(("SM_Char_Owner_", "SM_Char_Staff_"))
    if is_owner:
        base = lambda o: OWNER_TAG.sub("", o.name.split(".")[0])
        allnames = {base(o) for o in descendants(root)}
        if "HandSocket_R" not in allnames:
            r.err(13, "owner thiếu empty HandSocket_R")
        sock = [o for o in descendants(root) if base(o) == "HandSocket_R"]
        if sock and (sock[0].type != "EMPTY" or not sock[0].parent or
                     not OWNER_TAG.sub("", sock[0].parent.name.split(".")[0]).startswith("Arm_R")):
            r.err(13, "HandSocket_R phải là empty con của Arm_R")
        for pre in RECOLOR_PREFIXES:
            if not any(base(o).startswith(pre) and o.type == "MESH" for o in descendants(root)):
                r.err(13, f"owner thiếu mesh có tên bắt đầu '{pre}'")
        for o in meshes:
            if base(o).startswith(RECOLOR_PREFIXES):
                uvd = o.data.uv_layers[0].data if o.data.uv_layers else None
                cells = set()
                for poly in o.data.polygons:
                    for li in poly.loop_indices:
                        u, v = uvd[li].uv
                        cells.add((int(u * GRID), int(v * GRID)))
                if len(cells) != 1:
                    r.err(7, f"{o.name}: phần recolor phải đúng 1 ô palette, có {len(cells)}")
    tris = 0
    pts = []
    for o in meshes:
        tris += sum(len(p.vertices) - 2 for p in o.data.polygons)
        mats = [m.name.split(".")[0] if m else None for m in o.data.materials]
        if len(mats) != 1 or mats[0] not in ALLOWED_MATS:
            r.err(6, f"{o.name}: cần đúng 1 material M_Toon_Palette, có {mats}")
        if o.modifiers:
            r.err(12, f"{o.name}: còn modifier")
        sub = Report(source, o.name)
        check_uv(o.data, sub)
        r.entry["errors"] += [f"{o.name} {e}" for e in sub.entry["errors"]]
        pts += [o.matrix_world @ Vector(c) for c in o.bound_box]
    if pts:
        lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
        hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        if abs(lo.z) > 0.01:
            r.err(3, f"bàn chân không chạm sàn: min z = {lo.z:.3f}")
        if abs((lo.x + hi.x) / 2) > 0.1 or abs((lo.y + hi.y) / 2) > 0.1:
            r.err(3, "tâm XY bbox lệch")
        if not 1.0 <= hi.z <= 1.3:
            r.warn(18, f"cao {hi.z:.2f} m ngoài 1.0–1.3 m")
    budget = OWNER_BUDGET if is_owner else CHAR_BUDGET
    if tris > budget:
        r.err(5, f"{tris} tris > budget {budget}")
    elif tris > budget * 0.9:
        r.warn(5, f"{tris} tris > 90% budget")
    r.entry["tris"] = tris
    return r.entry


def check_scene(source):
    entries = []
    scene = bpy.context.scene
    if scene.unit_settings.system != "METRIC" or abs(scene.unit_settings.scale_length - 1) > 1e-6:
        entries.append({"source": source, "object": "<scene>",
                        "errors": ["#1 scene phải Metric, Unit Scale 1.0"], "warnings": []})
    for img in bpy.data.images:
        if img.users and img.name not in ALLOWED_IMAGES and img.type == "IMAGE":
            entries.append({"source": source, "object": "<images>",
                            "errors": [f"#14 texture lạ: {img.name}"], "warnings": []})
    for obj in scene.objects:
        if is_char_root(obj):
            entries.append(check_character(obj, source))
    for obj in scene.objects:
        if obj.type == "MESH" and obj.parent is not None:
            continue  # phần của hierarchy nhân vật
        if obj.type == "MESH" and not obj.name.startswith("PREVIEW_"):
            entries.append(check_object(obj, source))
    return entries


def load(path):
    if path.suffix == ".blend":
        bpy.ops.wm.open_mainfile(filepath=str(path))
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.scene.unit_settings.system = "METRIC"
        bpy.ops.import_scene.fbx(filepath=str(path), axis_forward="-Z", axis_up="Y",
                                 bake_space_transform=not path.stem.startswith("SM_Char_"),
                                 use_custom_props=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    files = [Path(a).resolve() for a in argv]
    if not files:
        files = sorted((ROOT / "art_source" / "blend").glob("*.blend"))
        files += sorted((ROOT / "art_source" / "export").glob("*.fbx"))
        files += sorted((ROOT / "art_source" / "export" / "characters").glob("*.fbx"))

    results = []
    for f in files:
        load(f)
        results += check_scene(str(f.relative_to(ROOT)) if f.is_relative_to(ROOT) else str(f))

    n_err = sum(len(e["errors"]) for e in results)
    n_warn = sum(len(e["warnings"]) for e in results)
    for e in results:
        status = "FAIL" if e["errors"] else ("WARN" if e["warnings"] else "PASS")
        print(f"[{status}] {e['source']} :: {e['object']}  ({e.get('tris', '-')} tris)")
        for m in e["errors"]:
            print(f"     ERROR {m}")
        for m in e["warnings"]:
            print(f"     warn  {m}")

    out = ROOT / "art_source" / "export" / "validate_report.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\n{len(results)} object, {n_err} ERROR, {n_warn} warning -> {out}")
    sys.exit(1 if n_err else 0)


if __name__ == "__main__":
    main()
