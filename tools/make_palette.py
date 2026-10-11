"""Sinh T_Palette_256.png + palette.json từ bảng màu trong docs/art_bible.md (§3).

art_bible.md là nguồn sự thật duy nhất: sửa màu ở đó rồi chạy lại script này.

    python tools/make_palette.py
"""
import json
import re
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
BIBLE = ROOT / "docs" / "art_bible.md"
OUT_DIR = ROOT / "art_source" / "palette"
GRID = 8
CELL = 32

# | 24 | 3,0 | Nhựa đỏ | `#D9362B` |
ROW_RE = re.compile(r"^\|\s*(\d+)\s*\|\s*(\d+),(\d+)\s*\|\s*([^|]+?)\s*\|\s*`#([0-9A-Fa-f]{6})`\s*\|")


def parse_palette(text):
    swatches = {}
    for line in text.splitlines():
        m = ROW_RE.match(line.strip())
        if not m:
            continue
        idx, r, c, name, hexcode = int(m[1]), int(m[2]), int(m[3]), m[4], m[5].upper()
        if idx != r * GRID + c:
            sys.exit(f"Lỗi: idx {idx} không khớp ô ({r},{c})")
        swatches[idx] = {"idx": idx, "row": r, "col": c, "name": name, "hex": f"#{hexcode}"}
    missing = sorted(set(range(GRID * GRID)) - set(swatches))
    if missing:
        sys.exit(f"Lỗi: thiếu ô màu {missing}")
    return [swatches[i] for i in range(GRID * GRID)]


def main():
    palette = parse_palette(BIBLE.read_text(encoding="utf-8"))
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    img = Image.new("RGB", (GRID * CELL, GRID * CELL))
    for s in palette:
        rgb = tuple(int(s["hex"][i:i + 2], 16) for i in (1, 3, 5))
        x0, y0 = s["col"] * CELL, s["row"] * CELL  # row 0 = hàng trên cùng của ảnh
        img.paste(rgb, (x0, y0, x0 + CELL, y0 + CELL))
    img.save(OUT_DIR / "T_Palette_256.png")

    (OUT_DIR / "palette.json").write_text(
        json.dumps({"version": 1, "grid": GRID, "cell_px": CELL, "swatches": palette},
                   ensure_ascii=False, indent=2),
        encoding="utf-8")
    print(f"OK: {len(palette)} màu -> {OUT_DIR}")


if __name__ == "__main__":
    main()
