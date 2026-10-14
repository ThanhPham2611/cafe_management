#!/usr/bin/env python
"""Generate the game logo, app icons and tutorial hand pointer ("Clay Cafe" style, docs/ui_style.md).

Outputs into game/Assets/_Project/Art/UI/Logo/:
  logo_wide.png            2048x1024 transparent: emblem + game name
  logo_icon_1024.png       1024x1024 full-bleed app icon (emblem only, no text)
  icon_adaptive_fg_432.png 432x432 transparent, emblem inside the inner 66% safe zone
  icon_adaptive_bg_432.png 432x432 background gradient
  ui_hand_pointer.png      256x256 transparent cartoon pointing hand
                           FINGERTIP PIVOT: printed on every run and written to Logo/hand_pivot.json
  hand_pivot.json          (not an asset; Unity ignores it)

Run:   python tools/make_logo.py [--name "Quán Cà Phê\\nViệt"] [--emblem pillow|blender]
The emblem is DRAWN with Pillow shapes (the Blender low-poly cup render is a grey blob at logo size). The Blender
render is still available: blender --background --python tools/render_logo_emblem.py -> art_source/logo/, then
`--emblem blender` composites that instead. Use "\\n" in --name for a line break (last line is drawn bigger, gold).
Everything is drawn at 2x and downsampled with LANCZOS.
"""
import argparse
import json
import math
import os

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "game", "Assets", "_Project", "Art", "UI", "Logo")
FONT = os.path.join(ROOT, "game", "Assets", "_Project", "Art", "Fonts", "Baloo2-ExtraBold.ttf")
BLENDER_EMBLEM = os.path.join(ROOT, "art_source", "logo", "ICON_Cup_Phin_SuaDa.png")

DEFAULT_NAME = "Quán Cà Phê\nViệt"


def hx(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


CREAM = hx("FFF4DC")
CREAM_DK = hx("EBD7AE")
COFFEE = hx("4A2C1E")
COFFEE_MD = hx("7A4526")
MILK = hx("FFF1D6")
CARAMEL = hx("C48A52")
RED = hx("D9362B")
RED_DK = hx("9E2119")
GOLD = hx("F2B93B")
GOLD_DK = hx("D9A441")
SILVER = hx("E6EAEF")
SILVER_DK = hx("A3ABB6")
SKIN = hx("FFD6B0")
SKIN_DK = hx("E9A877")
SHADOW = hx("2A1A12")

# --------------------------------------------------------------------------- mask helpers


def new_mask(size):
    return Image.new("L", size, 0)


def shift(m, dx, dy):
    out = Image.new("L", m.size, 0)
    out.paste(m, (int(dx), int(dy)))
    return out


def dilate(m, r):
    """Round dilation (union of shifted copies)."""
    if r <= 0:
        return m.copy()
    out = m.copy()
    rr = float(r)
    while rr > 0.5:
        n = max(12, int(2 * math.pi * rr / 3.0))
        for i in range(n):
            a = 2 * math.pi * i / n
            out = ImageChops.lighter(out, shift(m, round(math.cos(a) * rr), round(math.sin(a) * rr)))
        rr -= 6.0
    return out


def blur(m, r):
    return m.filter(ImageFilter.GaussianBlur(r))


def roundish(m, r):
    """Round off corners of a mask (blur + threshold)."""
    return blur(m, r).point(lambda v: 255 if v > 128 else int(v * 2))


def rrect(size, box, r):
    m = new_mask(size)
    ImageDraw.Draw(m).rounded_rectangle(box, r, fill=255)
    return m


def ellipse(size, box):
    m = new_mask(size)
    ImageDraw.Draw(m).ellipse(box, fill=255)
    return m


def poly(size, pts):
    m = new_mask(size)
    ImageDraw.Draw(m).polygon(pts, fill=255)
    return m


def line(size, pts, w):
    m = new_mask(size)
    d = ImageDraw.Draw(m)
    d.line(pts, fill=255, width=int(w), joint="curve")
    r = w / 2.0
    for p in (pts[0], pts[-1]):
        d.ellipse((p[0] - r, p[1] - r, p[0] + r, p[1] + r), fill=255)
    return m


def bezier(p0, p1, p2, p3, n=48):
    pts = []
    for i in range(n + 1):
        t = i / n
        u = 1 - t
        pts.append((u ** 3 * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t ** 3 * p3[0],
                    u ** 3 * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t ** 3 * p3[1]))
    return pts


def star_pts(cx, cy, r, inner=0.45):
    pts = []
    for i in range(10):
        rr = r if i % 2 == 0 else r * inner
        a = -math.pi / 2 + i * math.pi / 5
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    return pts


def vgrad(size, stops, y0=0, y1=None):
    """Vertical gradient image (RGB) with color stops [(t, color)], t in 0..1 across y0..y1."""
    w, h = size
    y1 = h if y1 is None else y1
    ys = np.clip((np.arange(h) - y0) / max(1, (y1 - y0)), 0, 1)
    arr = np.zeros((h, w, 3), np.uint8)
    for c in range(3):
        col = np.interp(ys, [s[0] for s in stops], [s[1][c] for s in stops])
        arr[:, :, c] = col[:, None]
    return Image.fromarray(arr)


def paste_color(cv, mask, img_or_color, alpha=1.0):
    if isinstance(img_or_color, tuple):
        img_or_color = Image.new("RGB", cv.size, img_or_color)
    if alpha < 1.0:
        mask = mask.point(lambda v: int(v * alpha))
    cv.paste(img_or_color.convert("RGBA"), (0, 0), mask)


class Painter:
    """Clay-style part painter on an RGBA canvas: outline stroke + vertical gradient + soft highlight."""

    def __init__(self, size, unit):
        self.size = size
        self.u = unit
        self.cv = Image.new("RGBA", size, (0, 0, 0, 0))

    def part(self, mask, top, bot, stroke=9, shine=True):
        u = self.u
        if stroke:
            paste_color(self.cv, dilate(mask, stroke * u), COFFEE)
        bb = mask.getbbox()
        grad = vgrad(self.size, [(0, top), (1, bot)], bb[1], bb[3])
        paste_color(self.cv, mask, grad)
        if shine:
            inner = ImageChops.subtract(mask, shift(mask, 0, int(14 * u)))
            inner = ImageChops.subtract(inner, shift(mask, int(10 * u), 0))
            paste_color(self.cv, ImageChops.darker(blur(inner, 4 * u), mask), (255, 255, 255), 0.55)
            low = ImageChops.subtract(mask, shift(mask, 0, int(-16 * u)))
            paste_color(self.cv, ImageChops.darker(blur(low, 5 * u), mask), SHADOW, 0.18)
        return mask


def sticker(img, outline_px, shadow_dy, shadow_blur, shadow_alpha=0.35):
    """Add thick coffee outline (around the alpha silhouette) + soft drop shadow behind an RGBA image."""
    a = img.getchannel("A")
    ol = dilate(a, outline_px)
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sh = blur(shift(ol, 0, shadow_dy), shadow_blur).point(lambda v: int(v * shadow_alpha))
    out.paste(Image.new("RGBA", img.size, SHADOW + (255,)), (0, 0), sh)
    out.paste(Image.new("RGBA", img.size, COFFEE + (255,)), (0, 0), ol)
    out.alpha_composite(img)
    return out


# --------------------------------------------------------------------------- emblem (phin + iced milk coffee)


def draw_emblem():
    """RGBA emblem WITHOUT the outer sticker outline, drawn in a 1000x1100 unit space at 2x."""
    U = 2.0
    W, H = int(1000 * U), int(1100 * U)
    size = (W, H)
    P = Painter(size, U)

    def s(*v):
        return [x * U for x in v]

    def pt(points):
        return [(x * U, y * U) for x, y in points]

    # ---- glass (tapered tumbler)
    glass = roundish(poly(size, pt([(290, 520), (710, 520), (672, 1010), (328, 1010)])), 16 * U)
    P.part(glass, hx("FFFFFF"), hx("DDEBF0"), stroke=10, shine=False)
    inside = roundish(poly(size, pt([(306, 540), (694, 540), (660, 996), (340, 996)])), 14 * U)
    liq_top = 590
    liquid_grad = vgrad(size, [(0, hx("5A3219")), (0.45, COFFEE_MD), (0.62, CARAMEL), (0.80, hx("F2D3A0")), (1, MILK)],
                        liq_top * U, 1000 * U)
    liquid = ImageChops.darker(inside, rrect(size, s(0, liq_top, 1000, 1100), 0))
    paste_color(P.cv, liquid, liquid_grad)
    # ice cubes
    cubes = [((385, 575), 108, -10), ((520, 560), 118, 8), ((620, 640), 100, -6), ((430, 700), 96, 14)]
    for (cx, cy), sz, ang in cubes:
        cm = new_mask(size)
        ImageDraw.Draw(cm).rounded_rectangle(s(cx - sz / 2, cy - sz / 2, cx + sz / 2, cy + sz / 2), 22 * U, fill=255)
        cm = cm.rotate(ang, center=(cx * U, cy * U), resample=Image.BICUBIC)
        cm = ImageChops.darker(cm, inside)
        paste_color(P.cv, ImageChops.subtract(dilate(cm, 5 * U), cm), COFFEE, 0.55)
        paste_color(P.cv, cm, vgrad(size, [(0, hx("FFFFFF")), (1, hx("CFE7F0"))], (cy - sz / 2) * U, (cy + sz / 2) * U), 0.82)
        hl = rrect(size, s(cx - sz / 2 + 14, cy - sz / 2 + 12, cx - sz / 2 + 40, cy - sz / 2 + 24), 6 * U)
        hl = hl.rotate(ang, center=(cx * U, cy * U))
        paste_color(P.cv, ImageChops.darker(hl, cm), (255, 255, 255), 0.95)
    # glass shine
    shine = line(size, pt([(334, 650), (352, 930)]), 20 * U)
    paste_color(P.cv, ImageChops.darker(shine, inside), (255, 255, 255), 0.55)
    # red label band (curved) with gold star
    band_pts = []
    for i in range(41):
        x = 280 + i * (440 / 40)
        band_pts.append((x, 735 + 26 * (1 - ((x - 500) / 220) ** 2)))
    for i in range(40, -1, -1):
        x = 280 + i * (440 / 40)
        band_pts.append((x, 850 + 26 * (1 - ((x - 500) / 220) ** 2)))
    band = ImageChops.darker(poly(size, pt(band_pts)), glass)
    paste_color(P.cv, ImageChops.darker(dilate(band, 6 * U), glass), COFFEE)
    paste_color(P.cv, band, vgrad(size, [(0, hx("E8483A")), (1, RED_DK)], 735 * U, 880 * U))
    paste_color(P.cv, ImageChops.darker(blur(ImageChops.subtract(band, shift(band, 0, int(12 * U))), 3 * U), band),
                (255, 255, 255), 0.35)
    sm = roundish(poly(size, pt(star_pts(500, 812, 50, 0.45))), 3 * U)
    paste_color(P.cv, dilate(sm, 5 * U), COFFEE)
    paste_color(P.cv, sm, vgrad(size, [(0, hx("FFD96A")), (1, GOLD_DK)], 760 * U, 860 * U))
    for x in (385, 615):
        dm = ellipse(size, s(x - 12, 800, x + 12, 824))
        paste_color(P.cv, dilate(dm, 4 * U), COFFEE)
        paste_color(P.cv, dm, GOLD)

    # ---- phin
    P.part(rrect(size, s(205, 470, 795, 548), 38 * U), SILVER, SILVER_DK, stroke=10)                 # saucer plate
    chamber = rrect(size, s(318, 318, 682, 500), 34 * U)
    P.part(chamber, SILVER, SILVER_DK, stroke=10)                                                    # chamber
    ring = ImageChops.darker(rrect(size, s(318, 372, 682, 424), 0), chamber)
    paste_color(P.cv, ring, vgrad(size, [(0, hx("FFD96A")), (1, GOLD_DK)], 372 * U, 424 * U))        # gold ring
    paste_color(P.cv, ImageChops.darker(ImageChops.subtract(dilate(ring, 4 * U), ring), chamber), COFFEE, 0.9)
    P.part(rrect(size, s(342, 250, 658, 336), 40 * U), hx("F4F6F8"), hx("B6BDC7"), stroke=10)         # lid
    P.part(rrect(size, s(448, 196, 552, 270), 34 * U), hx("FFD96A"), GOLD_DK, stroke=10)               # knob
    # steam wisps (cream, coffee outline)
    wisps = [
        bezier((420, 170), (330, 120), (480, 80), (395, 20)),
        bezier((500, 165), (410, 105), (590, 70), (505, -10)),
        bezier((580, 170), (500, 125), (650, 85), (590, 30)),
    ]
    # shift everything down so steam fits in canvas
    steam = Painter(size, U)
    sm_all = new_mask(size)
    for w in wisps:
        sm_all = ImageChops.lighter(sm_all, line(size, pt([(x, y + 40) for x, y in w]), 30 * U))
    sm_all = roundish(sm_all, 4 * U)
    paste_color(steam.cv, dilate(sm_all, 9 * U), COFFEE)
    paste_color(steam.cv, sm_all, vgrad(size, [(0, hx("FFFFFF")), (1, CREAM_DK)], 0, 260 * U))
    em = steam.cv
    em.alpha_composite(P.cv)
    # gold sparkles
    for cx, cy, r in [(250, 360, 36), (770, 300, 30)]:
        sp = roundish(poly(size, pt([(cx, cy - r), (cx + r * .3, cy - r * .3), (cx + r, cy), (cx + r * .3, cy + r * .3),
                                     (cx, cy + r), (cx - r * .3, cy + r * .3), (cx - r, cy), (cx - r * .3, cy - r * .3)])), 2 * U)
        paste_color(em, dilate(sp, 7 * U), COFFEE)
        paste_color(em, sp, GOLD)
    return em


def load_emblem(kind):
    if kind == "blender" and os.path.exists(BLENDER_EMBLEM):
        return Image.open(BLENDER_EMBLEM).convert("RGBA")
    return draw_emblem()


def emblem_sticker(em, height):
    """Emblem with outer outline + shadow, total height ~= height."""
    em = em.crop(em.getbbox())
    scale = (height * 0.90) / em.height
    em = em.resize((round(em.width * scale), round(em.height * scale)), Image.LANCZOS)
    pad = int(height * 0.07)
    cv = Image.new("RGBA", (em.width + 2 * pad, em.height + 2 * pad), (0, 0, 0, 0))
    cv.alpha_composite(em, (pad, pad))
    return sticker(cv, max(4, int(height * 0.022)), int(height * 0.025), height * 0.012)


# --------------------------------------------------------------------------- title text


def title_text(name, width, ss=2):
    """RGBA name block: cream lines, last line (if >1) larger in gold, coffee outline + drop shadow."""
    lines = name.replace("\\n", "\n").split("\n")
    sizes = [1.0] * len(lines)
    colors = [(CREAM, CREAM_DK)] * len(lines)
    if len(lines) > 1:
        sizes[-1] = 1.45
        colors[-1] = (hx("FFD96A"), GOLD_DK)

    def f(px):
        return ImageFont.truetype(FONT, int(px * ss))

    base = 400
    while base > 40:
        widest = max(f(base * sz).getlength(t) / ss for t, sz in zip(lines, sizes))
        if widest <= width:
            break
        base -= 4
    outline = base * 0.10
    pad = int(outline * 2.5)
    line_h = [int(base * sz * 1.02) for sz in sizes]
    W = int((width + pad * 2) * ss)
    H = int((sum(line_h) + pad * 2 + base * 0.35) * ss)
    block = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    letters_all = new_mask((W, H))
    y = pad * ss
    for t, sz, (c1, c2), h in zip(lines, sizes, colors, line_h):
        font = f(base * sz)
        wl = font.getlength(t)
        m = new_mask((W, H))
        ImageDraw.Draw(m).text(((W - wl) / 2, y), t, font=font, fill=255)
        bb = m.getbbox()
        grad = vgrad((W, H), [(0, c1), (0.6, c1), (1, c2)], bb[1], bb[3])
        ol = dilate(m, outline * ss)
        paste_color(block, ol, COFFEE)
        paste_color(block, m, grad)
        hi = ImageChops.subtract(m, shift(m, 0, int(base * sz * 0.05 * ss)))
        paste_color(block, ImageChops.darker(blur(hi, base * 0.012 * ss), m), (255, 255, 255), 0.6)
        letters_all = ImageChops.lighter(letters_all, ol)
        y += int(h * ss * 0.93)
    sh = blur(shift(letters_all, 0, int(base * 0.07 * ss)), base * 0.03 * ss).point(lambda v: int(v * 0.35))
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out.paste(Image.new("RGBA", (W, H), SHADOW + (255,)), (0, 0), sh)
    out.alpha_composite(block)
    out = out.crop(out.getbbox())
    return out.resize((max(1, out.width // ss), max(1, out.height // ss)), Image.LANCZOS)


# --------------------------------------------------------------------------- outputs


def ribbon(w, h):
    S = 2
    size = (w * S, h * S)
    p = Painter(size, 2.0)
    p.part(rrect(size, (10, 10, w * S - 10, h * S - 10), h * S // 2 - 10), hx("E8483A"), RED_DK, stroke=4)
    d = ImageDraw.Draw(p.cv)
    cx, cy = w * S / 2, h * S / 2
    for dx, r in ((-1, 40), (0, 56), (1, 40)):
        d.polygon(star_pts(cx + dx * w * S * 0.2, cy, r), fill=GOLD, outline=COFFEE, width=4)
    return p.cv.resize((w, h), Image.LANCZOS)


def make_wide(em, name):
    W, H = 2048, 1024
    cv = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    emb = emblem_sticker(em, 940)
    cv.alpha_composite(emb, (30, (H - emb.height) // 2))
    tx0 = 30 + emb.width + 10
    avail = W - tx0 - 60
    text = title_text(name, avail)
    rib = ribbon(int(text.width * 0.7), 70)
    block = Image.new("RGBA", (text.width, text.height + 30 + rib.height), (0, 0, 0, 0))
    block.alpha_composite(text, (0, 0))
    block.alpha_composite(rib, ((text.width - rib.width) // 2, text.height + 24))
    cv.alpha_composite(block, (tx0 + (avail - block.width) // 2, (H - block.height) // 2))
    return cv


def icon_bg(size, disc=0.42):
    bg = vgrad((size, size), [(0, hx("FFF8E6")), (1, hx("F3DDB0"))]).convert("RGBA")
    if disc:
        m = ellipse((size, size), (size * (0.5 - disc), size * (0.5 - disc), size * (0.5 + disc), size * (0.5 + disc)))
        paste_color(bg, blur(m, size * 0.01), vgrad((size, size), [(0, hx("FFD96A")), (1, hx("F2B93B"))]), 0.5)
    return bg


def make_icon(em, size, emblem_frac, with_bg=True):
    base = icon_bg(size) if with_bg else Image.new("RGBA", (size, size), (0, 0, 0, 0))
    emb = emblem_sticker(em, int(size * emblem_frac))
    base.alpha_composite(emb, ((size - emb.width) // 2, (size - emb.height) // 2))
    return base


# --------------------------------------------------------------------------- hand pointer
# Drawn pointing straight UP on a 1024 canvas, rotated 28 deg CCW (fingertip points up-left), fitted into 256x256.
# The fingertip pivot is tracked through the same transform and printed / written to hand_pivot.json.


def make_hand():
    S = 1024
    size = (S, S)
    P = Painter(size, 1.8)

    def part(mask, top, bot, stroke=5):
        P.part(mask, top, bot, stroke=stroke, shine=True)

    cuff = rrect(size, (330, 800, 730, 1000), 60)
    part(cuff, hx("E8483A"), RED_DK)
    ring = ImageChops.darker(rrect(size, (330, 800, 730, 850), 0), cuff)
    paste_color(P.cv, ring, GOLD)
    part(rrect(size, (320, 470, 740, 880), 130), SKIN, SKIN_DK)
    for cx, cy, r in [(555, 520, 78), (638, 548, 76), (710, 590, 70)]:
        part(ellipse(size, (cx - r, cy - r, cx + r, cy + r)), SKIN, SKIN_DK)
    part(line(size, [(400, 720), (300, 590)], 125), SKIN, SKIN_DK)
    part(rrect(size, (395, 110, 535, 700), 70), SKIN, SKIN_DK)
    nail = rrect(size, (420, 135, 510, 235), 38)
    paste_color(P.cv, dilate(nail, 4), COFFEE, 0.6)
    paste_color(P.cv, nail, hx("FFF0E4"))
    pivot_layer = Image.new("L", size, 0)
    ImageDraw.Draw(pivot_layer).ellipse((465 - 6, 110 - 6, 465 + 6, 110 + 6), fill=255)
    ang = 28
    cv = P.cv.rotate(ang, resample=Image.BICUBIC, center=(S / 2, S / 2))
    pv = pivot_layer.rotate(ang, resample=Image.BICUBIC, center=(S / 2, S / 2))
    a = cv.getchannel("A")
    rim = dilate(a, 14)
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    out.paste(Image.new("RGBA", size, CREAM + (255,)), (0, 0), rim)
    out.alpha_composite(cv)
    sh = blur(shift(rim, 10, 16), 10).point(lambda v: int(v * 0.35))
    final = Image.new("RGBA", size, (0, 0, 0, 0))
    final.paste(Image.new("RGBA", size, SHADOW + (255,)), (0, 0), sh)
    final.alpha_composite(out)
    bb = final.getchannel("A").getbbox()
    m = 8
    k = (256 - 2 * m) / max(bb[2] - bb[0], bb[3] - bb[1])
    w2, h2 = round((bb[2] - bb[0]) * k), round((bb[3] - bb[1]) * k)
    crop = final.crop(bb).resize((w2, h2), Image.LANCZOS)
    res = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    res.alpha_composite(crop, (m, m))
    pb = pv.getbbox()
    pcx, pcy = (pb[0] + pb[2]) / 2, (pb[1] + pb[3]) / 2
    return res, (round(m + (pcx - bb[0]) * k), round(m + (pcy - bb[1]) * k))


# --------------------------------------------------------------------------- main


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--name", default=DEFAULT_NAME, help='game name; "\\n" = line break (default "Quán Cà Phê\\nViệt")')
    ap.add_argument("--emblem", choices=("pillow", "blender"), default="pillow")
    args = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    em = load_emblem(args.emblem)

    make_wide(em, args.name).save(os.path.join(OUT, "logo_wide.png"))
    make_icon(em, 1024, 0.80).save(os.path.join(OUT, "logo_icon_1024.png"))
    make_icon(em, 432, 0.60, with_bg=False).save(os.path.join(OUT, "icon_adaptive_fg_432.png"))
    icon_bg(432, disc=0).convert("RGB").save(os.path.join(OUT, "icon_adaptive_bg_432.png"))
    hand, pivot = make_hand()
    hand.save(os.path.join(OUT, "ui_hand_pointer.png"))
    with open(os.path.join(OUT, "hand_pivot.json"), "w") as f:
        json.dump({"file": "ui_hand_pointer.png", "size": [256, 256], "fingertip_pivot_px_from_top_left": list(pivot),
                   "pivot_normalized_unity_bottom_left": [pivot[0] / 256, 1 - pivot[1] / 256]}, f, indent=1)
    print("HAND_FINGERTIP_PIVOT_PX (from top-left of 256x256):", pivot)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
