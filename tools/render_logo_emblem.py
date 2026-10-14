"""Render SM_Cup_Phin_SuaDa (phin + iced milk coffee) 3/4 view, transparent, 1024px -> art_source/logo/ICON_Cup_Phin_SuaDa.png
    blender --background --python tools/render_logo_emblem.py
Reuses tools/render_icons.py (toon material, camera framing, Standard view transform). No floor shadow.
"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy  # noqa: E402
import render_icons as ri  # noqa: E402
import gen_furniture as gf  # noqa: E402

ri.FILL = 0.96
ri.OUT_DIR = gf.EXPORT_DIR.parent / "logo"
ri.OUT_DIR.mkdir(parents=True, exist_ok=True)
ri.add_shadow = lambda radius: bpy.data.objects.new("ICON_Dummy", None)   # no floor shadow (logo composites its own)
scene = ri.clear_scene()
cam = ri.setup_render(scene, 1024)
ri.render_one(scene, cam, gf.EXPORT_DIR / "SM_Cup_Phin_SuaDa.fbx", 1024)
print("DONE")
