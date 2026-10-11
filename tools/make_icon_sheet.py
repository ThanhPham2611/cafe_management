"""Ghép ICON_*.png thành một contact sheet để xem nhanh (Pillow, chạy bằng python thường, không cần Blender).

    python tools/make_icon_sheet.py [--tier 2] [--cols 5] [--out art_source/export/icons_contact_tier2.png]

--tier 2 chỉ lấy icon của nội thất Tier 2 (tên khớp ASSETS_T2 trong gen_furniture.py, đọc bằng regex, không import bpy).
Nền kem (#FFF4DC) + tên icon bên dưới mỗi ô; 256 px/ô.
"""
import argparse
import re
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
ICON_DIR = ROOT / "art_source" / "export" / "icons"
BG = (255, 244, 220)


def tier2_ids():
    src = (ROOT / "tools" / "gen_furniture.py").read_text(encoding="utf-8")
    block = src[src.index("ASSETS_T2 = {"):]
    block = block[:block.index("}")]
    return [m[3:] for m in re.findall(r'"(SM_[A-Za-z0-9_]+)"', block)]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tier", type=int, choices=(1, 2))
    ap.add_argument("--cols", type=int, default=5)
    ap.add_argument("--out")
    args = ap.parse_args()

    files = sorted(ICON_DIR.glob("ICON_*.png"))
    if args.tier == 2:
        keep = tier2_ids()
        files = [ICON_DIR / f"ICON_{i}.png" for i in keep if (ICON_DIR / f"ICON_{i}.png").exists()]
    if not files:
        raise SystemExit("không có icon nào")
    cell, label = 256, 28
    rows = (len(files) + args.cols - 1) // args.cols
    sheet = Image.new("RGB", (args.cols * cell, rows * (cell + label)), BG)
    d = ImageDraw.Draw(sheet)
    for i, f in enumerate(files):
        im = Image.open(f).convert("RGBA")
        x, y = (i % args.cols) * cell, (i // args.cols) * (cell + label)
        sheet.paste(im, (x, y), im)
        d.text((x + 8, y + cell + 6), f.stem[5:], fill=(74, 44, 30))
    out = Path(args.out) if args.out else ICON_DIR.parent / (
        "icons_contact_tier2.png" if args.tier == 2 else "icons_contact.png")
    sheet.save(out)
    print(f"{len(files)} icon -> {out}")


if __name__ == "__main__":
    main()
