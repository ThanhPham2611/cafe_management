#!/usr/bin/env python
"""Generate the "Clay Cafe" UI sprites (docs/ui_style.md) with Pillow.

Outputs PNGs + ui_sprites.json into game/Assets/_Project/Art/UI/ and a contact sheet into
art_source/export/ui_contact_sheet.png.   Run:  python tools/gen_ui_sprites.py
Everything is drawn at 4x and downsampled with LANCZOS.
"""
import json
import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT = os.path.join(ROOT, "game", "Assets", "_Project", "Art", "UI")
SHEET = os.path.join(ROOT, "art_source", "export", "ui_contact_sheet.png")
SS = 4


def hx(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def mul(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


CREAM = hx("FFF4DC")
CREAM_DK = hx("EFE3C8")
COFFEE = hx("4A2C1E")
COFFEE_MD = hx("6B4026")
RED = hx("D9362B")
RED_DK = hx("9E2119")
GREEN = hx("2FA05A")
GREEN_DK = hx("1F7440")
GOLD = hx("F2B93B")
GOLD_DK = hx("D9A441")
STAR = hx("FFD23F")
MINT = hx("9CD3B5")
SHADOW = hx("2A1A12")
WHITE = (255, 255, 255)

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
        n = max(10, int(2 * math.pi * rr / 3.0))
        for i in range(n):
            a = 2 * math.pi * i / n
            out = ImageChops.lighter(out, shift(m, round(math.cos(a) * rr), round(math.sin(a) * rr)))
        rr -= 5.0
    return out


def erode(m, r):
    if r <= 0:
        return m.copy()
    pad = int(r) + 2
    w, h = m.size
    big = Image.new("L", (w + 2 * pad, h + 2 * pad), 0)
    big.paste(m, (pad, pad))
    big = ImageChops.invert(dilate(ImageChops.invert(big), r))
    return big.crop((pad, pad, pad + w, pad + h))


def msub(a, b):
    return ImageChops.subtract(a, b)


def madd(*ms):
    out = ms[0]
    for m in ms[1:]:
        out = ImageChops.lighter(out, m)
    return out


def mand(a, b):
    return ImageChops.darker(a, b)


def smooth(m, r_open, r_close=0):
    out = m
    if r_open:
        out = dilate(erode(out, r_open), r_open)
    if r_close:
        out = erode(dilate(out, r_close), r_close)
    return out


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


def rect(size, box):
    m = new_mask(size)
    ImageDraw.Draw(m).rectangle(box, fill=255)
    return m


def line(size, pts, w):
    m = new_mask(size)
    d = ImageDraw.Draw(m)
    d.line(pts, fill=255, width=w, joint="curve")
    r = w / 2.0
    for p in (pts[0], pts[-1]):
        d.ellipse((p[0] - r, p[1] - r, p[0] + r, p[1] + r), fill=255)
    return m


def blur(m, r):
    return m.filter(ImageFilter.GaussianBlur(r))


def scale_mask(m, k):
    return m.point(lambda v: int(v * k))


# --------------------------------------------------------------------------- painting


def canvas(size):
    return Image.new("RGBA", size, COFFEE + (0,))


def paint(cv, mask, color, alpha=1.0):
    m = scale_mask(mask, alpha) if alpha < 1.0 else mask
    cv.paste(color + (255,), (0, 0), m)


def part(cv, mask, base, outline=12, outline_color=COFFEE, shade=0.80, rim=0.55, facet=None):
    """Chunky clay part: outline, base, lower shade crescent, top-left rim light."""
    size = cv.size
    if outline:
        paint(cv, dilate(mask, outline), outline_color)
    layer = Image.new("RGBA", size, base + (255,))
    d = 14
    sh = blur(msub(mask, shift(mask, -int(d * 0.5), -d)), 3)
    layer.paste(mul(base, shade) + (255,), (0, 0), mand(sh, mask))
    if facet:
        fm = erode(mask, facet[0])
        layer.paste(facet[1] + (255,), (0, 0), blur(fm, 2))
        layer.paste(mul(base, shade) + (255,), (0, 0), mand(blur(msub(fm, shift(fm, -4, -10)), 3), fm))
    hl = blur(msub(mask, shift(mask, d, d)), 3)
    layer.paste(WHITE + (255,), (0, 0), scale_mask(mand(hl, mask), rim))
    cv.paste(layer, (0, 0), mask)


def spec(cv, clip, box, alpha=0.85):
    m = ellipse(cv.size, box)
    m = blur(m, 2)
    paint(cv, mand(m, clip), WHITE, alpha)


def down(img, size):
    return img.resize(size, Image.LANCZOS)


# --------------------------------------------------------------------------- UI shapes


def clay_shape(W, H, sil_box, radius, ow, face, lip_col, lip_h, shadow_h, hl=0.5, shadow_alpha=0.35):
    """Generic clay shape on a W x H final-pixel canvas (sil_box in final px)."""
    size = (W * SS, H * SS)
    cv = canvas(size)
    b = tuple(v * SS for v in sil_box)
    sil = rrect(size, b, radius * SS)
    if shadow_h > 0:
        sh = blur(shift(sil, 0, shadow_h * SS), max(2, shadow_h * SS * 0.45))
        cv.paste(SHADOW + (255,), (0, 0), scale_mask(sh, shadow_alpha))
    paint(cv, sil, COFFEE)
    inner = erode(sil, ow * SS)
    if lip_h > 0:
        paint(cv, inner, lip_col)
        faceM = mand(inner, shift(inner, 0, -lip_h * SS))
    else:
        faceM = inner
    layer = Image.new("RGBA", size, face + (255,))
    if hl > 0:
        fi = erode(faceM, 6 * SS)
        strip = blur(msub(fi, shift(fi, 0, 9 * SS)), 2)
        layer.paste(WHITE + (255,), (0, 0), scale_mask(mand(strip, faceM), hl))
    cv.paste(layer, (0, 0), faceM)
    return cv


def make_panel():
    return down(clay_shape(128, 128, (0, 0, 128, 116), 28, 4, CREAM, CREAM_DK, 6, 12, hl=0.75), (128, 128))


def make_button(face, lip, pressed=False):
    if not pressed:
        cv = clay_shape(128, 128, (0, 0, 128, 116), 28, 4, face, lip, 8, 12, hl=0.45)
    else:
        cv = clay_shape(128, 128, (0, 6, 128, 122), 28, 4, face, lip, 2, 6, hl=0.25, shadow_alpha=0.3)
    return down(cv, (128, 128))


def make_chip():
    return down(clay_shape(96, 88, (0, 0, 96, 76), 38, 4, CREAM, CREAM_DK, 6, 12, hl=0.7), (96, 88))


def make_card():
    return down(clay_shape(96, 96, (0, 0, 96, 86), 24, 4, CREAM, CREAM_DK, 6, 10, hl=0.7), (96, 96))


def make_bar_track():
    W, H = 64, 32
    cv = clay_shape(W, H, (0, 0, 64, 32), 16, 3, CREAM_DK, CREAM_DK, 0, 0, hl=0.0)
    size = cv.size
    sil = rrect(size, (0, 0, W * SS, H * SS), 16 * SS)
    inner = erode(sil, 3 * SS)
    top = blur(msub(inner, shift(inner, 0, 6 * SS)), 3)
    paint(cv, mand(top, inner), mul(CREAM_DK, 0.82), 1.0)
    return down(cv, (W, H))


def make_bar_fill():
    W, H = 64, 32
    size = (W * SS, H * SS)
    cv = canvas(size)
    sil = rrect(size, (0, 0, W * SS, H * SS), 16 * SS)
    layer = Image.new("RGBA", size, WHITE + (255,))
    low = blur(msub(sil, shift(sil, 0, -9 * SS)), 4)
    layer.paste((205, 205, 205, 255), (0, 0), low)
    fi = erode(sil, 4 * SS)
    hlm = blur(msub(fi, shift(fi, 0, 6 * SS)), 2)
    layer.paste((255, 255, 255, 255), (0, 0), hlm)
    cv.paste(layer, (0, 0), sil)
    return down(cv, (W, H))


def make_ring(fill_only):
    W = 128
    size = (W * SS, W * SS)
    cv = canvas(size)
    c = W * SS / 2.0
    R_out, R_gin_o, R_gin_i = 64 * SS, 58 * SS, 38 * SS
    if not fill_only:
        disc = ellipse(size, (c - R_out, c - R_out, c + R_out, c + R_out))
        paint(cv, disc, COFFEE)
        paint(cv, erode(disc, 4 * SS), CREAM)
        groove = msub(ellipse(size, (c - R_gin_o, c - R_gin_o, c + R_gin_o, c + R_gin_o)),
                      ellipse(size, (c - R_gin_i, c - R_gin_i, c + R_gin_i, c + R_gin_i)))
        paint(cv, groove, CREAM_DK)
        ink = blur(msub(groove, shift(groove, 0, 5 * SS)), 2)
        paint(cv, mand(ink, groove), mul(CREAM_DK, 0.8))
    else:
        r1, r2 = 57 * SS, 39 * SS
        ring = msub(ellipse(size, (c - r1, c - r1, c + r1, c + r1)), ellipse(size, (c - r2, c - r2, c + r2, c + r2)))
        layer = Image.new("RGBA", size, WHITE + (255,))
        low = blur(msub(ring, shift(ring, 0, -5 * SS)), 2)
        layer.paste((200, 200, 200, 255), (0, 0), mand(low, ring))
        hl = blur(msub(ring, shift(ring, 3 * SS, 5 * SS)), 2)
        layer.paste((255, 255, 255, 255), (0, 0), mand(hl, ring))
        cv.paste(layer, (0, 0), ring)
    return down(cv, (W, W))


def make_disc():
    W = 128
    size = (W * SS, W * SS)
    cv = canvas(size)
    sh = blur(shift(ellipse(size, (8 * SS, 8 * SS, 120 * SS, 120 * SS)), 0, 5 * SS), 6)
    cv.paste(SHADOW + (255,), (0, 0), scale_mask(sh, 0.3))
    d = ellipse(size, (4 * SS, 2 * SS, 124 * SS, 118 * SS))
    paint(cv, d, COFFEE)
    inner = erode(d, 4 * SS)
    paint(cv, inner, (215, 215, 215))
    face = mand(inner, shift(inner, 0, -6 * SS))
    cv.paste(WHITE + (255,), (0, 0), face)
    return down(cv, (W, W))


def make_bubble():
    W, H = 128, 128
    return down(clay_shape(W, H, (0, 0, 128, 116), 34, 4, CREAM, CREAM_DK, 0, 12, hl=0.6), (W, H))


def make_bubble_tail():
    W, H = 56, 40
    size = (W * SS, H * SS)
    cv = canvas(size)
    sil = poly(size, [(4 * SS, 0), (52 * SS, 0), (28 * SS, 34 * SS)])
    sil = smooth(sil, 4 * SS)
    sh = blur(shift(sil, 0, 5 * SS), 8)
    cv.paste(SHADOW + (255,), (0, 0), scale_mask(sh, 0.3))
    paint(cv, sil, COFFEE)
    inner = erode(sil, 4 * SS)
    paint(cv, inner, CREAM)
    paint(cv, rect(size, (14 * SS, 0, 42 * SS, 9 * SS)), CREAM)
    return down(cv, (W, H))


def make_dim():
    return Image.new("RGBA", (4, 4), (255, 255, 255, 255))


# --------------------------------------------------------------------------- icons

N = 128 * SS
SZ = (N, N)


def drop_mask(cx, cy, r):
    c = ellipse(SZ, (cx - r, cy - r * 0.7, cx + r, cy + r * 1.3))
    t = poly(SZ, [(cx, cy - r * 2.0), (cx - r * 0.93, cy + r * 0.1), (cx + r * 0.93, cy + r * 0.1)])
    return smooth(madd(c, t), 0, 8)


def icon_coin():
    cv = canvas(SZ)
    m = ellipse(SZ, (34, 34, 478, 478))
    part(cv, m, GOLD, facet=(30, hx("FFD66B")))
    ring = msub(ellipse(SZ, (88, 88, 424, 424)), ellipse(SZ, (104, 104, 408, 408)))
    paint(cv, ring, mul(GOLD_DK, 0.92))
    dia = poly(SZ, [(256, 150), (362, 256), (256, 362), (150, 256)])
    dia = dilate(erode(dia, 10), 22)
    part(cv, dia, GOLD_DK, outline=8, outline_color=hx("A8741A"), shade=0.85, rim=0.35)
    hole = dilate(erode(poly(SZ, [(256, 200), (312, 256), (256, 312), (200, 256)]), 6), 14)
    paint(cv, hole, hx("8A5A1E"))
    spec(cv, m, (92, 78, 190, 128), 0.9)
    spec(cv, m, (70, 140, 96, 168), 0.9)
    return cv


def star_points(cx, cy, R, r, rot=-90):
    pts = []
    for i in range(10):
        a = math.radians(rot + 36 * i)
        rad = R if i % 2 == 0 else r
        pts.append((cx + rad * math.cos(a), cy + rad * math.sin(a)))
    return pts


def icon_star():
    cv = canvas(SZ)
    m = poly(SZ, star_points(256, 280, 196, 100))
    m = dilate(m, 30)
    m = erode(dilate(m, 22), 22)
    part(cv, m, STAR, facet=(36, hx("FFE585")))
    spec(cv, m, (150, 150, 200, 190), 0.9)
    return cv


def icon_clock():
    cv = canvas(SZ)
    part(cv, rrect(SZ, (212, 22, 300, 96), 24), RED_DK, outline=12, rim=0.4)
    body = ellipse(SZ, (46, 64, 466, 484))
    part(cv, body, RED)
    face = ellipse(SZ, (94, 112, 418, 436))
    part(cv, face, hx("FFFBEF"), outline=8, shade=0.9, rim=0.2)
    paint(cv, line(SZ, [(256, 274), (256, 168)], 34), COFFEE)
    paint(cv, line(SZ, [(256, 274), (336, 316)], 34), COFFEE)
    paint(cv, ellipse(SZ, (230, 248, 282, 300)), RED)
    for ang in (0, 90, 180, 270):
        a = math.radians(ang)
        px, py = 256 + 138 * math.sin(a), 274 - 138 * math.cos(a)
        paint(cv, ellipse(SZ, (px - 9, py - 9, px + 9, py + 9)), COFFEE_MD)
    spec(cv, body, (80, 96, 170, 130), 0.55)
    return cv


def wisp(x, y0, y1, amp, w, phase=0.0):
    pts = []
    steps = 14
    for i in range(steps + 1):
        t = i / float(steps)
        pts.append((x + amp * math.sin(t * math.pi * 2.0 + phase), y0 + (y1 - y0) * t))
    return line(SZ, pts, w)


def icon_cup():
    cv = canvas(SZ)
    for x, ph in ((196, 0.0), (318, 3.1)):
        w = wisp(x, 150, 34, 20, 26, ph)
        part(cv, w, hx("FFFFFF"), outline=8, shade=0.92, rim=0.0)
    part(cv, ellipse(SZ, (46, 398, 466, 478)), hx("E9D9B5"), outline=12, shade=0.85)
    handle = msub(ellipse(SZ, (336, 236, 470, 376)), ellipse(SZ, (372, 270, 432, 342)))
    part(cv, handle, hx("FFF8EA"), shade=0.85)
    body = poly(SZ, [(98, 196), (390, 196), (346, 410), (142, 410)])
    body = dilate(erode(body, 40), 40)
    part(cv, body, hx("FFF8EA"))
    band = mand(rect(SZ, (0, 280, N, 340)), body)
    paint(cv, band, RED)
    paint(cv, mand(rect(SZ, (0, 280, N, 290)), body), mul(RED, 1.12))
    paint(cv, mand(rect(SZ, (0, 330, N, 340)), body), RED_DK)
    part(cv, ellipse(SZ, (92, 166, 396, 250)), hx("FFF8EA"), outline=12, shade=0.88, rim=0.2)
    paint(cv, ellipse(SZ, (116, 180, 372, 238)), COFFEE_MD)
    paint(cv, ellipse(SZ, (140, 186, 270, 214)), lerp(COFFEE_MD, WHITE, 0.25), 0.9)
    return cv


def icon_phin():
    cv = canvas(SZ)
    glass = dilate(erode(poly(SZ, [(148, 330), (364, 330), (344, 468), (168, 468)]), 18), 18)
    part(cv, glass, hx("D6EEF6"), shade=0.88, rim=0.7)
    liquid = mand(erode(glass, 8), rect(SZ, (0, 408, N, N)))
    paint(cv, liquid, COFFEE_MD)
    paint(cv, mand(liquid, rect(SZ, (0, 408, N, 424))), lerp(COFFEE_MD, WHITE, 0.2))
    paint(cv, ellipse(SZ, (240, 346, 272, 392)), COFFEE_MD)
    spec(cv, glass, (176, 346, 196, 440), 0.75)
    steel = hx("C3CDD3")
    part(cv, rrect(SZ, (92, 284, 420, 346), 28), hx("D7DFE3"), shade=0.78)
    part(cv, rrect(SZ, (170, 140, 342, 300), 26), steel, shade=0.74)
    part(cv, rrect(SZ, (150, 84, 362, 156), 34), hx("DCE3E7"), shade=0.78)
    part(cv, ellipse(SZ, (222, 22, 290, 90)), RED, shade=0.78)
    spec(cv, rrect(SZ, (170, 140, 342, 300), 26), (190, 170, 214, 270), 0.55)
    return cv


def icon_sua_dac():
    cv = canvas(SZ)
    blue = hx("3F86C9")
    silver = hx("CBD3D9")
    body = rrect(SZ, (118, 100, 394, 446), 44)
    part(cv, body, blue)
    band = mand(rect(SZ, (0, 220, N, 360)), body)
    paint(cv, band, hx("FFF8EA"))
    paint(cv, mand(rect(SZ, (0, 214, N, 224)), body), COFFEE)
    paint(cv, mand(rect(SZ, (0, 356, N, 366)), body), COFFEE)
    d = drop_mask(256, 296, 38)
    part(cv, d, hx("6BAEE8"), outline=8, shade=0.8, rim=0.5)
    part(cv, rrect(SZ, (104, 68, 408, 132), 26), silver, shade=0.8)
    part(cv, rrect(SZ, (104, 414, 408, 476), 26), silver, shade=0.8)
    paint(cv, mand(rrect(SZ, (146, 150, 172, 210), 12), body), WHITE, 0.55)
    return cv


def icon_sua_tuoi():
    cv = canvas(SZ)
    part(cv, rrect(SZ, (178, 28, 334, 100), 16), hx("E6EDF1"), shade=0.82)
    roof = dilate(erode(poly(SZ, [(128, 214), (178, 92), (334, 92), (384, 214)]), 14), 14)
    part(cv, roof, hx("C9D9E6"), shade=0.78)
    body = rrect(SZ, (128, 186, 384, 478), 26)
    part(cv, body, hx("FFFDF6"), shade=0.88)
    band = mand(rect(SZ, (0, 292, N, 400)), body)
    paint(cv, band, MINT)
    paint(cv, mand(rect(SZ, (0, 286, N, 298)), body), COFFEE)
    paint(cv, mand(rect(SZ, (0, 394, N, 406)), body), COFFEE)
    d = drop_mask(256, 352, 30)
    part(cv, d, WHITE, outline=8, shade=0.9, rim=0.0)
    return cv


def rot_rrect(box, r, angle):
    m = rrect(SZ, box, r)
    cx, cy = (box[0] + box[2]) / 2.0, (box[1] + box[3]) / 2.0
    return m.rotate(angle, resample=Image.BICUBIC, center=(cx, cy))


def sparkle(cv, cx, cy, R, alpha=1.0):
    pts = []
    for i in range(8):
        a = math.radians(-90 + 45 * i)
        rad = R if i % 2 == 0 else R * 0.28
        pts.append((cx + rad * math.cos(a), cy + rad * math.sin(a)))
    paint(cv, poly(SZ, pts), WHITE, alpha)


def icon_da():
    cv = canvas(SZ)
    back = rot_rrect((250, 56, 440, 246), 44, 16)
    part(cv, back, hx("DDF3FB"), facet=(24, hx("F0FAFE")))
    front = rot_rrect((52, 160, 360, 468), 62, -9)
    part(cv, front, hx("A9DCF2"), facet=(44, hx("CBEBF8")))
    sparkle(cv, 128, 236, 40, 0.95)
    sparkle(cv, 160, 300, 18, 0.9)
    return cv


def icon_tra():
    cv = canvas(SZ)
    green = hx("3FAE72")
    handle = msub(ellipse(SZ, (350, 220, 478, 372)), ellipse(SZ, (384, 254, 442, 338)))
    part(cv, handle, green, shade=0.75)
    spout = dilate(erode(poly(SZ, [(120, 300), (44, 190), (84, 166), (178, 258)]), 8), 12)
    part(cv, spout, green, shade=0.75)
    body = ellipse(SZ, (90, 168, 400, 462))
    part(cv, body, green, facet=(30, hx("58C487")))
    lens = mand(ellipse(SZ, (176, 270, 296, 400)), ellipse(SZ, (214, 250, 334, 380)))
    lens = lens.rotate(24, resample=Image.BICUBIC, center=(256, 330))
    paint(cv, lens, hx("FFF8EA"))
    part(cv, ellipse(SZ, (160, 124, 332, 210)), hx("2E8A58"), shade=0.8, rim=0.5)
    part(cv, ellipse(SZ, (222, 76, 270, 124)), hx("FFF8EA"), shade=0.82)
    spec(cv, body, (124, 200, 170, 250), 0.85)
    return cv


def icon_check():
    cv = canvas(SZ)
    m = ellipse(SZ, (34, 34, 478, 478))
    part(cv, m, GREEN, facet=(34, hx("43BC70")))
    chk = line(SZ, [(140, 268), (226, 350), (376, 176)], 62)
    paint(cv, shift(chk, 0, 10), GREEN_DK)
    paint(cv, chk, WHITE)
    spec(cv, m, (92, 78, 190, 126), 0.55)
    return cv


def icon_cross():
    cv = canvas(SZ)
    m = ellipse(SZ, (34, 34, 478, 478))
    part(cv, m, RED, facet=(34, hx("EE5044")))
    x = madd(line(SZ, [(168, 168), (344, 344)], 58), line(SZ, [(344, 168), (168, 344)], 58))
    paint(cv, shift(x, 0, 10), RED_DK)
    paint(cv, x, WHITE)
    spec(cv, m, (92, 78, 190, 126), 0.55)
    return cv


def icon_heart():
    cv = canvas(SZ)
    h = madd(ellipse(SZ, (66, 100, 266, 300)), ellipse(SZ, (246, 100, 446, 300)),
             poly(SZ, [(78, 232), (434, 232), (256, 446)]))
    h = erode(dilate(h, 20), 20)
    h = dilate(erode(h, 6), 6)
    part(cv, h, hx("EF4B5C"), facet=(34, hx("FF7482")))
    spec(cv, h, (116, 140, 188, 190), 0.95)
    spec(cv, h, (100, 200, 122, 224), 0.9)
    return cv


# --------------------------------------------------------------------------- icons v2 (docs/hud_visual_polish_2026-10-18.md §1)


def icon_menu():
    """3 thanh bo tròn kem (đặt trên nút coffee: chỉ icon, nền là nút)."""
    cv = canvas(SZ)
    for i in range(3):
        y = 132 + i * 124
        part(cv, rrect(SZ, (92, y, 420, y + 76), 38), CREAM, outline=10, shade=0.86, rim=0.6)
    return cv


def icon_shop():
    """Mái hiên sọc đỏ-kem + quầy gỗ."""
    cv = canvas(SZ)
    wood = hx("C98A4B")
    part(cv, rrect(SZ, (86, 250, 426, 470), 30), wood, facet=(26, hx("D99C5E")))
    part(cv, rrect(SZ, (150, 300, 362, 420), 18), hx("FFF8EA"), outline=10, shade=0.88, rim=0.3)
    part(cv, rrect(SZ, (60, 404, 452, 470), 22), hx("A86E37"), shade=0.8, rim=0.35)
    awn = poly(SZ, [(70, 110), (442, 110), (470, 250), (42, 250)])
    awn = dilate(erode(awn, 14), 14)
    paint(cv, dilate(awn, 12), COFFEE)
    stripes = Image.new("RGBA", SZ, CREAM + (255,))
    for k in range(7):
        x0 = 42 + k * 61
        if k % 2 == 0:
            stripes.paste(RED + (255,), (0, 0), poly(SZ, [(x0 + 26, 110), (x0 + 74, 110), (x0 + 61, 250), (x0, 250)]))
    sh = blur(msub(awn, shift(awn, 0, -18)), 3)
    stripes.paste((0, 0, 0, 255), (0, 0), scale_mask(mand(sh, awn), 0.18))
    cv.paste(stripes, (0, 0), awn)
    for k in range(7):
        cx = 72 + k * 61
        sc = ellipse(SZ, (cx - 32, 222, cx + 32, 282))
        part(cv, sc, RED if k % 2 == 0 else CREAM, outline=10, shade=0.85, rim=0.3)
    part(cv, rrect(SZ, (60, 70, 452, 122), 24), RED_DK, outline=10, shade=0.8, rim=0.45)
    return cv


def icon_book():
    """Sổ công thức bìa đỏ, mép giấy kem, dải đánh dấu vàng."""
    cv = canvas(SZ)
    part(cv, rrect(SZ, (118, 70, 430, 452), 34), hx("FFF8EA"), shade=0.86, rim=0.3)
    for k in range(3):
        paint(cv, rect(SZ, (414, 120 + k * 100, 426, 128 + k * 100)), CREAM_DK)
    cover = rrect(SZ, (80, 46, 398, 446), 38)
    part(cv, cover, RED, facet=(30, hx("E8483C")))
    part(cv, rrect(SZ, (80, 46, 140, 446), 26), RED_DK, outline=0, shade=0.85, rim=0.25)
    part(cv, rrect(SZ, (178, 150, 344, 254), 22), CREAM, outline=10, shade=0.88, rim=0.3)
    paint(cv, line(SZ, [(206, 186), (316, 186)], 16), COFFEE_MD)
    paint(cv, line(SZ, [(206, 218), (286, 218)], 16), COFFEE_MD)
    rib = poly(SZ, [(300, 420), (350, 420), (350, 496), (325, 470), (300, 496)])
    part(cv, rib, GOLD, outline=8, shade=0.85, rim=0.3)
    spec(cv, cover, (164, 72, 300, 110), 0.5)
    return cv


def icon_mission():
    """Bảng kẹp giấy có dấu tích xanh."""
    cv = canvas(SZ)
    board = rrect(SZ, (88, 66, 424, 482), 40)
    part(cv, board, hx("C98A4B"), facet=(24, hx("D99C5E")))
    part(cv, rrect(SZ, (128, 120, 384, 446), 20), hx("FFFDF6"), outline=8, shade=0.9, rim=0.2)
    for k in range(3):
        y = 196 + k * 78
        paint(cv, line(SZ, [(236, y), (350, y)], 18), CREAM_DK)
        box = rrect(SZ, (160, y - 26, 212, y + 26), 12)
        paint(cv, box, GREEN if k < 2 else CREAM_DK)
        if k < 2:
            paint(cv, line(SZ, [(172, y), (184, y + 14), (204, y - 14)], 12), WHITE)
    part(cv, rrect(SZ, (176, 34, 336, 112), 26), hx("CBD3D9"), shade=0.78)
    paint(cv, ellipse(SZ, (236, 52, 276, 92)), COFFEE_MD)
    return cv


def icon_staff():
    """Tạp dề xanh có túi kem + dây cổ."""
    cv = canvas(SZ)
    strap = msub(ellipse(SZ, (170, 20, 342, 190)), ellipse(SZ, (204, 52, 308, 160)))
    strap = mand(strap, rect(SZ, (0, 0, N, 140)))
    part(cv, strap, hx("2E8A58"), outline=10, shade=0.8, rim=0.3)
    body = poly(SZ, [(170, 120), (342, 120), (342, 220), (420, 250), (400, 476), (112, 476), (92, 250), (170, 220)])
    body = dilate(erode(body, 24), 24)
    part(cv, body, hx("3FAE72"), facet=(30, hx("58C487")))
    part(cv, rrect(SZ, (184, 300, 328, 410), 22), CREAM, outline=10, shade=0.86, rim=0.3)
    paint(cv, line(SZ, [(256, 310), (256, 400)], 10), CREAM_DK)
    for side in (-1, 1):
        tie = line(SZ, [(256 + side * 150, 250), (256 + side * 210, 300)], 30)
        part(cv, tie, hx("2E8A58"), outline=8, shade=0.8, rim=0.2)
    return cv


def icon_home():
    """Ngôi nhà nhỏ: mái đỏ, tường kem, cửa nâu (cho pill mục tiêu)."""
    cv = canvas(SZ)
    part(cv, rrect(SZ, (318, 72, 378, 200), 14), hx("A86E37"), shade=0.8, rim=0.3)
    walls = rrect(SZ, (104, 214, 408, 470), 26)
    part(cv, walls, hx("FFF8EA"), shade=0.86, rim=0.3)
    part(cv, rrect(SZ, (210, 312, 302, 470), 22), hx("A86E37"), outline=10, shade=0.8, rim=0.35)
    paint(cv, ellipse(SZ, (272, 382, 290, 400)), GOLD)
    part(cv, rrect(SZ, (130, 268, 190, 328), 10), hx("A9DCF2"), outline=8, shade=0.88, rim=0.6)
    roof = dilate(erode(poly(SZ, [(256, 50), (466, 250), (46, 250)]), 16), 22)
    part(cv, roof, RED, facet=(28, hx("E8483C")))
    spec(cv, roof, (196, 120, 260, 150), 0.6)
    return cv


def icon_broom():
    """Chổi rơm: cán gỗ chéo + đầu chổi vàng có dây buộc."""
    cv = canvas(SZ)
    stick = line(SZ, [(402, 52), (250, 270)], 42)
    part(cv, stick, hx("C98A4B"), outline=10, shade=0.8, rim=0.4)
    head = poly(SZ, [(222, 236), (300, 290), (250, 486), (70, 400)])
    head = dilate(erode(head, 22), 26)
    part(cv, head, hx("F2C66B"), facet=(26, hx("F8D98E")))
    for k in range(4):
        a = 0.2 + 0.2 * k
        x0, y0 = 236 + (280 - 236) * a, 262
        paint(cv, line(SZ, [(x0 - 40, y0 + 40 + k * 4), (110 + k * 46, 420 + k * 14)], 8), hx("C9973E"))
    band = line(SZ, [(206, 262), (300, 322)], 34)
    part(cv, band, RED, outline=8, shade=0.8, rim=0.3)
    return cv


def icon_close():
    """X kem dày (đặt trên nút coffee)."""
    cv = canvas(SZ)
    x = madd(line(SZ, [(140, 140), (372, 372)], 86), line(SZ, [(372, 140), (140, 372)], 86))
    part(cv, x, CREAM, outline=12, shade=0.86, rim=0.55)
    return cv


# --------------------------------------------------------------------------- nguyên liệu Tier 2 (thay đĩa + nhãn 2 chữ của StepIconFallback)


def icon_duong():
    """Hũ đường sứ xanh + 3 viên đường."""
    cv = canvas(SZ)
    blue = hx("4F9BD9")
    part(cv, rrect(SZ, (200, 96, 312, 150), 26), hx("FFF8EA"), shade=0.85, rim=0.3)
    for box, ang in (((150, 40, 250, 140), -12), ((262, 30, 362, 130), 10), ((206, 0, 306, 100), 4)):
        cube = rot_rrect(box, 20, ang)
        part(cv, cube, WHITE, outline=10, shade=0.84, rim=0.0, facet=(14, hx("F4F1EA")))
    bowl = madd(ellipse(SZ, (66, 150, 446, 250)),
                mand(ellipse(SZ, (66, 40, 446, 470)), rect(SZ, (0, 200, N, N))))
    part(cv, bowl, hx("FFFDF6"), facet=(30, WHITE))
    band = mand(rect(SZ, (0, 300, N, 350)), bowl)
    paint(cv, band, blue)
    paint(cv, mand(rect(SZ, (0, 300, N, 310)), bowl), mul(blue, 1.15))
    part(cv, rrect(SZ, (150, 430, 362, 480), 22), hx("FFF8EA"), shade=0.82, rim=0.3)
    paint(cv, ellipse(SZ, (96, 170, 416, 232)), hx("E9E4DA"))
    for cx in (176, 256, 336):
        part(cv, rrect(SZ, (cx - 34, 148, cx + 34, 214), 12), WHITE, outline=8, shade=0.86, rim=0.0)
    spec(cv, bowl, (110, 260, 150, 330), 0.8)
    return cv


def icon_muoi():
    """Lọ muối trắng nắp bạc có lỗ (cà phê muối Huế)."""
    cv = canvas(SZ)
    body = dilate(erode(poly(SZ, [(170, 170), (342, 170), (382, 470), (130, 470)]), 30), 30)
    part(cv, body, hx("EAF2F7"), facet=(26, WHITE))
    paint(cv, mand(rect(SZ, (0, 380, N, 470)), erode(body, 16)), hx("FFFFFF"))
    paint(cv, mand(rect(SZ, (0, 372, N, 384)), erode(body, 16)), hx("D3DEE6"))
    for x, y in ((190, 412), (236, 432), (282, 404), (318, 438), (214, 448)):
        paint(cv, ellipse(SZ, (x - 9, y - 9, x + 9, y + 9)), hx("C8D5DE"))
    lid = dilate(erode(poly(SZ, [(170, 70), (342, 70), (364, 186), (148, 186)]), 22), 22)
    part(cv, lid, hx("BFCAD1"), shade=0.75, rim=0.6)
    for x, y in ((214, 112), (256, 104), (298, 112), (236, 142), (276, 142)):
        paint(cv, ellipse(SZ, (x - 11, y - 11, x + 11, y + 11)), COFFEE_MD)
    paint(cv, mand(rect(SZ, (0, 186, N, 204)), body), hx("9FB0BB"))
    spec(cv, body, (164, 220, 196, 340), 0.8)
    return cv


def icon_kem_muoi():
    """Ly cà phê với lớp kem muối bông xốp trên miệng."""
    cv = canvas(SZ)
    body = dilate(erode(poly(SZ, [(118, 220), (394, 220), (360, 470), (152, 470)]), 30), 30)
    part(cv, body, hx("D6EEF6"), shade=0.88, rim=0.6)
    liquid = mand(erode(body, 12), rect(SZ, (0, 300, N, N)))
    paint(cv, liquid, COFFEE_MD)
    paint(cv, mand(liquid, rect(SZ, (0, 300, N, 326))), lerp(COFFEE_MD, hx("E9D9B5"), 0.45))
    spec(cv, body, (150, 320, 176, 440), 0.7)
    foam = madd(ellipse(SZ, (90, 168, 230, 290)), ellipse(SZ, (180, 120, 340, 270)), ellipse(SZ, (282, 160, 422, 290)),
                rrect(SZ, (96, 220, 416, 300), 40))
    foam = smooth(foam, 0, 12)
    part(cv, foam, hx("FFF6E2"), facet=(26, hx("FFFDF6")))
    for x, y in ((206, 196), (300, 214), (252, 160)):
        paint(cv, ellipse(SZ, (x - 9, y - 9, x + 9, y + 9)), hx("F2E0BC"))
    spec(cv, foam, (196, 140, 250, 174), 0.9)
    return cv


def icon_cot_dua():
    """Nửa trái dừa: vỏ nâu, cùi trắng, nước dừa."""
    cv = canvas(SZ)
    shell = mand(ellipse(SZ, (40, 120, 472, 500)), rect(SZ, (0, 260, N, N)))
    shell = madd(shell, ellipse(SZ, (40, 220, 472, 300)))
    part(cv, shell, hx("8A5A33"), facet=(28, hx("A06A3D")))
    for k in range(5):
        x = 110 + k * 72
        paint(cv, mand(line(SZ, [(x, 330), (x + 18, 450)], 10), erode(shell, 20)), hx("6E4527"))
    flesh = ellipse(SZ, (72, 222, 440, 300))
    part(cv, flesh, hx("FFFDF6"), outline=8, shade=0.9, rim=0.0)
    milk = ellipse(SZ, (112, 236, 400, 288))
    paint(cv, milk, hx("F1ECE0"))
    paint(cv, ellipse(SZ, (150, 244, 260, 266)), WHITE, 0.9)
    d = drop_mask(256, 130, 34)
    part(cv, d, WHITE, outline=8, shade=0.88, rim=0.0)
    return cv


def icon_trung():
    """Trứng nguyên quả vàng kem + lòng đỏ ốp bên cạnh (cà phê trứng)."""
    cv = canvas(SZ)
    white = madd(ellipse(SZ, (196, 250, 476, 470)), ellipse(SZ, (250, 214, 430, 360)))
    white = smooth(white, 0, 20)
    part(cv, white, hx("FFFDF6"), shade=0.88, rim=0.3)
    yolk = ellipse(SZ, (270, 276, 400, 400))
    part(cv, yolk, hx("FFB61E"), outline=8, facet=(16, hx("FFCB4F")))
    spec(cv, yolk, (296, 296, 330, 320), 0.9)
    egg = ellipse(SZ, (52, 60, 300, 400))
    egg = madd(egg, ellipse(SZ, (62, 150, 290, 420)))
    part(cv, egg, hx("F7E3B5"), facet=(30, hx("FBEDCB")))
    spec(cv, egg, (100, 120, 150, 200), 0.85)
    return cv


def icon_chanh():
    """Lát chanh xanh: vỏ xanh đậm, múi xanh nhạt."""
    cv = canvas(SZ)
    c = 256
    rind = ellipse(SZ, (44, 44, 468, 468))
    part(cv, rind, hx("4FAE32"), facet=(18, hx("6BC447")))
    paint(cv, ellipse(SZ, (84, 84, 428, 428)), hx("F4F7D2"))
    pulp = ellipse(SZ, (104, 104, 408, 408))
    paint(cv, pulp, hx("B6DB4A"))
    for k in range(8):
        a = math.radians(22.5 + 45 * k)
        paint(cv, line(SZ, [(c, c), (c + 150 * math.cos(a), c + 150 * math.sin(a))], 14), hx("F4F7D2"))
    paint(cv, ellipse(SZ, (c - 22, c - 22, c + 22, c + 22)), hx("F4F7D2"))
    for k in range(8):
        a = math.radians(45 * k)
        x, y = c + 96 * math.cos(a), c + 96 * math.sin(a)
        paint(cv, ellipse(SZ, (x - 14, y - 24, x + 14, y + 24)).rotate(-45 * k, center=(x, y)), hx("CDEB6E"), 0.9)
    spec(cv, rind, (120, 100, 210, 140), 0.7)
    return cv


def icon_lac():
    """Bình lắc cocktail bằng đồng (khác hẳn phin inox): nắp tròn, cổ, thân thuôn, vệt lắc hai bên."""
    cv = canvas(SZ)
    steel = hx("E0A458")
    body = dilate(erode(poly(SZ, [(150, 232), (362, 232), (338, 478), (174, 478)]), 22), 22)
    part(cv, body, steel, facet=(26, hx("F0BF78")))
    paint(cv, mand(rect(SZ, (0, 300, N, 330)), erode(body, 14)), hx("B97A35"))
    part(cv, rrect(SZ, (136, 196, 376, 252), 24), hx("C98A4B"), shade=0.78)
    neck = dilate(erode(poly(SZ, [(196, 90), (316, 90), (346, 204), (166, 204)]), 18), 18)
    part(cv, neck, steel, shade=0.76)
    part(cv, ellipse(SZ, (212, 26, 300, 110)), hx("B97A35"), shade=0.78)
    spec(cv, body, (182, 262, 206, 430), 0.75)
    spec(cv, neck, (206, 110, 226, 180), 0.6)
    for side in (-1, 1):
        x = 256 + side * 196
        part(cv, line(SZ, [(x - side * 6, 150), (x + side * 20, 120)], 16), WHITE, outline=8, rim=0.0)
        part(cv, line(SZ, [(x - side * 2, 360), (x + side * 26, 382)], 16), WHITE, outline=8, rim=0.0)
    return cv


def icon_danh():
    """Phới đánh trứng: cán gỗ + lồng dây thép."""
    cv = canvas(SZ)
    wires = new_mask(SZ)
    for w in (230, 150, 70):
        loop = msub(ellipse(SZ, (256 - w / 2.0, 30, 256 + w / 2.0, 330)), ellipse(SZ, (256 - w / 2.0 + 22, 52, 256 + w / 2.0 - 22, 316)))
        wires = madd(wires, loop)
    wires = mand(wires, rect(SZ, (0, 0, N, 320)))
    part(cv, wires, hx("D7DFE3"), outline=10, shade=0.75, rim=0.6)
    part(cv, rrect(SZ, (220, 280, 292, 330), 16), hx("BFCAD1"), shade=0.75)
    handle = rrect(SZ, (212, 318, 300, 490), 40)
    part(cv, handle, hx("C98A4B"), facet=(18, hx("D99C5E")))
    paint(cv, ellipse(SZ, (240, 452, 272, 476)), COFFEE_MD)
    spec(cv, handle, (226, 340, 244, 420), 0.6)
    return cv


def icon_cacao():
    """Quả ca cao nâu đỏ có rãnh dọc + muỗng bột ca cao."""
    cv = canvas(SZ)
    pod = ellipse(SZ, (120, 40, 330, 440)).rotate(-28, resample=Image.BICUBIC, center=(225, 240))
    part(cv, pod, hx("9A4A26"), facet=(24, hx("B5602F")))
    for k in (-1, 0, 1):
        g = line(SZ, [(225 + k * 50 - 40, 70), (225 + k * 54 + 40, 410)], 12).rotate(-28, resample=Image.BICUBIC, center=(225, 240))
        paint(cv, mand(g, erode(pod, 16)), hx("7A3518"))
    part(cv, line(SZ, [(150, 70), (120, 30)], 22), hx("6E8A3A"), outline=8, rim=0.2)
    spoon = ellipse(SZ, (250, 300, 450, 460))
    part(cv, spoon, hx("C98A4B"), shade=0.8)
    powder = madd(ellipse(SZ, (272, 300, 428, 400)), ellipse(SZ, (300, 268, 400, 360)))
    part(cv, powder, hx("6B3A22"), outline=8, shade=0.85, rim=0.3)
    part(cv, line(SZ, [(430, 420), (476, 470)], 30), hx("C98A4B"), outline=8, rim=0.3)
    spec(cv, pod, (170, 100, 200, 170), 0.6)
    return cv


def glaze(cv, mask, color, alpha):
    """Phủ màu bán trong suốt (alpha_composite) - dùng cho thủy tinh."""
    layer = Image.new("RGBA", cv.size, color + (0,))
    layer.putalpha(scale_mask(mask, alpha))
    cv.alpha_composite(layer)


GAUGE_W, GAUGE_H = 96, 160


def gauge_inner(size):
    """Lòng ly-thước (khớp giữa ui_gauge_glass và ui_gauge_mask)."""
    return dilate(erode(poly(size, [(18 * SS, 20 * SS), (78 * SS, 20 * SS), (72 * SS, 146 * SS), (24 * SS, 146 * SS)]), 6 * SS), 6 * SS)


def make_gauge_glass():
    size = (GAUGE_W * SS, GAUGE_H * SS)
    cv = Image.new("RGBA", size, (255, 255, 255, 0))
    inner = gauge_inner(size)
    sil = dilate(inner, 6 * SS)
    sh = blur(shift(sil, 0, 5 * SS), 10)
    glaze(cv, sh, SHADOW, 0.25)
    paint(cv, sil, COFFEE)
    cv.paste((255, 255, 255, 0), (0, 0), inner)
    glaze(cv, inner, hx("E6F4FA"), 0.22)
    # đáy ly dày + vành miệng
    base = mand(inner, rect(size, (0, 132 * SS, size[0], size[1])))
    glaze(cv, base, hx("D6EEF6"), 0.55)
    rim = mand(inner, rect(size, (0, 20 * SS, size[0], 27 * SS)))
    glaze(cv, rim, WHITE, 0.55)
    hl = mand(inner, rrect(size, (25 * SS, 32 * SS, 33 * SS, 124 * SS), 4 * SS))
    glaze(cv, blur(hl, 3), WHITE, 0.75)
    hl2 = mand(inner, rrect(size, (38 * SS, 34 * SS, 42 * SS, 70 * SS), 2 * SS))
    glaze(cv, blur(hl2, 2), WHITE, 0.5)
    return down(cv, (GAUGE_W, GAUGE_H))


def make_gauge_mask():
    size = (GAUGE_W * SS, GAUGE_H * SS)
    cv = Image.new("RGBA", size, (255, 255, 255, 0))
    cv.paste((255, 255, 255, 255), (0, 0), gauge_inner(size))
    return down(cv, (GAUGE_W, GAUGE_H))


def make_badge():
    W = 64
    size = (W * SS, W * SS)
    cv = canvas(size)
    d = ellipse(size, (2 * SS, 2 * SS, 62 * SS, 62 * SS))
    paint(cv, d, CREAM)
    inner = erode(d, 5 * SS)
    paint(cv, inner, RED)
    lo = blur(msub(inner, shift(inner, 0, -6 * SS)), 3)
    paint(cv, mand(lo, inner), RED_DK)
    hl = blur(msub(erode(inner, 4 * SS), shift(erode(inner, 4 * SS), 0, 7 * SS)), 2)
    paint(cv, mand(hl, inner), WHITE, 0.45)
    return down(cv, (W, W))


def make_tag():
    W, H = 48, 24
    cv = clay_shape(W, H, (0, 0, 48, 24), 12, 2, CREAM, CREAM_DK, 0, 0, hl=0.0)
    return down(cv, (W, H))


def make_arrow():
    W, H = 48, 36
    size = (W * SS, H * SS)
    cv = canvas(size)
    tri = poly(size, [(6 * SS, 4 * SS), (42 * SS, 4 * SS), (24 * SS, 32 * SS)])
    tri = dilate(erode(tri, 4 * SS), 3 * SS)
    paint(cv, tri, COFFEE)
    inner = erode(tri, 3 * SS)
    paint(cv, inner, STAR)
    hl = blur(msub(inner, shift(inner, 0, 5 * SS)), 2)
    paint(cv, mand(hl, inner), WHITE, 0.6)
    return down(cv, (W, H))


def make_sparkle():
    W = 48
    size = (W * SS, W * SS)
    cv = Image.new("RGBA", size, (255, 255, 255, 0))
    c = W * SS / 2.0
    pts = []
    for i in range(8):
        a = math.radians(-90 + 45 * i)
        rad = 23 * SS if i % 2 == 0 else 5 * SS
        pts.append((c + rad * math.cos(a), c + rad * math.sin(a)))
    m = poly(size, pts)
    glaze(cv, blur(dilate(m, 3 * SS), 6), WHITE, 0.5)
    glaze(cv, m, WHITE, 1.0)
    return down(cv, (W, W))


# --------------------------------------------------------------------------- build


def nine_slice(img, border, size):
    l, b, r, t = border
    W, H = img.size
    tw, th = size
    out = Image.new("RGBA", size, (0, 0, 0, 0))
    xs = [(0, l, 0, l), (l, W - r, l, tw - r), (W - r, W, tw - r, tw)]
    ys = [(0, t, 0, t), (t, H - b, t, th - b), (H - b, H, th - b, th)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            if sx1 <= sx0 or sy1 <= sy0 or dx1 <= dx0 or dy1 <= dy0:
                continue
            piece = img.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.BILINEAR)
            out.alpha_composite(piece, (dx0, dy0))
    return out


def tint(img, color):
    r, g, b, a = img.split()
    rr = r.point(lambda v: v * color[0] // 255)
    gg = g.point(lambda v: v * color[1] // 255)
    bb = b.point(lambda v: v * color[2] // 255)
    return Image.merge("RGBA", (rr, gg, bb, a))


def main():
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    sprites = {}   # name -> (image, border)

    sprites["ui_panel"] = (make_panel(), (40, 52, 40, 40))
    variants = {
        "red": (RED, RED_DK), "green": (GREEN, GREEN_DK), "gold": (GOLD, GOLD_DK),
        "cream": (CREAM, hx("D4C39D")), "coffee": (COFFEE_MD, hx("3A2116")),
    }
    for name, (face, lip) in variants.items():
        sprites["ui_btn_%s" % name] = (make_button(face, lip, False), (40, 52, 40, 40))
        sprites["ui_btn_%s_p" % name] = (make_button(mul(face, 0.93), lip, True), (40, 52, 40, 40))
    sprites["ui_chip"] = (make_chip(), (38, 44, 38, 38))
    sprites["ui_card"] = (make_card(), (32, 42, 32, 32))
    sprites["ui_bar_track"] = (make_bar_track(), (16, 16, 16, 16))
    sprites["ui_bar_fill"] = (make_bar_fill(), (15, 15, 15, 15))
    sprites["ui_ring_bg"] = (make_ring(False), (0, 0, 0, 0))
    sprites["ui_ring_fill"] = (make_ring(True), (0, 0, 0, 0))
    sprites["ui_disc"] = (make_disc(), (0, 0, 0, 0))
    sprites["ui_bubble"] = (make_bubble(), (44, 52, 44, 44))
    sprites["ui_bubble_tail"] = (make_bubble_tail(), (0, 0, 0, 0))
    sprites["ui_dim"] = (make_dim(), (0, 0, 0, 0))

    icons = {
        "coin": icon_coin, "star": icon_star, "clock": icon_clock, "cup": icon_cup, "phin": icon_phin,
        "sua_dac": icon_sua_dac, "sua_tuoi": icon_sua_tuoi, "da": icon_da, "tra": icon_tra,
        "check": icon_check, "cross": icon_cross, "heart": icon_heart,
    }
    for name, fn in icons.items():
        sprites["icon_%s" % name] = (down(fn(), (128, 128)), (0, 0, 0, 0))

    # v2 (hud_visual_polish): icon HUD + ly-thước + huy hiệu số + nhãn nhỏ
    icons2 = {
        "menu": icon_menu, "shop": icon_shop, "book": icon_book, "mission": icon_mission,
        "staff": icon_staff, "home": icon_home, "broom": icon_broom, "close": icon_close,
    }
    for name, fn in icons2.items():
        sprites["icon_%s" % name] = (down(fn(), (128, 128)), (0, 0, 0, 0))
    # nguyên liệu Tier 2 (thay đĩa + nhãn 2 chữ dự phòng)
    icons3 = {
        "duong": icon_duong, "muoi": icon_muoi, "kem_muoi": icon_kem_muoi, "cot_dua": icon_cot_dua, "trung": icon_trung,
        "chanh": icon_chanh, "lac": icon_lac, "danh": icon_danh, "cacao": icon_cacao,
    }
    for name, fn in icons3.items():
        sprites["icon_%s" % name] = (down(fn(), (128, 128)), (0, 0, 0, 0))
    sprites["ui_gauge_glass"] = (make_gauge_glass(), (0, 0, 0, 0))
    sprites["ui_gauge_mask"] = (make_gauge_mask(), (0, 0, 0, 0))
    sprites["ui_badge"] = (make_badge(), (0, 0, 0, 0))
    sprites["ui_tag"] = (make_tag(), (12, 12, 12, 12))
    sprites["ui_arrow"] = (make_arrow(), (0, 0, 0, 0))
    sprites["ui_sparkle"] = (make_sparkle(), (0, 0, 0, 0))

    entries = []
    for name, (img, border) in sprites.items():
        fn = name + ".png"
        img.save(os.path.join(OUT, fn))
        entries.append({"file": fn, "border": list(border), "pixelsPerUnit": 100})
    with open(os.path.join(OUT, "ui_sprites.json"), "w", encoding="utf-8") as f:
        json.dump({"pixelsPerUnit": 100, "sprites": entries}, f, indent=2)
    print("wrote %d sprites to %s" % (len(entries), OUT))

    # ---- contact sheet
    bg = (139, 122, 104, 255)
    sheet = Image.new("RGBA", (1500, 1600), bg)
    fdir = os.path.join(ROOT, "game", "Assets", "_Project", "Art", "Fonts")
    try:
        font = ImageFont.truetype(os.path.join(fdir, "Nunito-Bold.ttf"), 14)
        bigfont = ImageFont.truetype(os.path.join(fdir, "Baloo2-ExtraBold.ttf"), 30)
    except Exception:
        font = bigfont = ImageFont.load_default()
    d = ImageDraw.Draw(sheet)
    x, y = 20, 20
    for name in icons:
        im = sprites["icon_%s" % name][0]
        sheet.alpha_composite(im, (x, y))
        d.text((x + 4, y + 130), name, fill=(255, 255, 255), font=font)
        x += 140
        if x > 1400:
            x, y = 20, y + 160
    x, y = 20, 360
    for name in icons:
        im = sprites["icon_%s" % name][0]
        sheet.alpha_composite(im.resize((64, 64), Image.LANCZOS), (x, y))
        sheet.alpha_composite(im.resize((40, 40), Image.LANCZOS), (x, y + 70))
        x += 80
    x, y = 20, 520
    for name in ["ui_panel", "ui_btn_red", "ui_btn_red_p", "ui_btn_green", "ui_btn_gold", "ui_btn_cream", "ui_btn_coffee", "ui_btn_coffee_p"]:
        sheet.alpha_composite(sprites[name][0], (x, y))
        x += 140
    sheet.alpha_composite(sprites["ui_chip"][0], (x, y))
    x += 110
    sheet.alpha_composite(sprites["ui_card"][0], (x, y))
    x += 110
    sheet.alpha_composite(sprites["ui_ring_bg"][0], (x, y))
    y2 = 680
    pan, pb = sprites["ui_panel"]
    sheet.alpha_composite(nine_slice(pan, pb, (420, 260)), (20, y2))
    d.text((60, y2 + 30), "Quan Ca Phe  Panel", fill=COFFEE, font=bigfont)
    btn, bb = sprites["ui_btn_red"]
    sheet.alpha_composite(nine_slice(btn, bb, (300, 110)), (470, y2))
    sheet.alpha_composite(nine_slice(sprites["ui_btn_red_p"][0], bb, (300, 110)), (470, y2 + 130))
    sheet.alpha_composite(nine_slice(sprites["ui_btn_green"][0], bb, (220, 110)), (790, y2))
    sheet.alpha_composite(nine_slice(sprites["ui_btn_gold"][0], bb, (220, 110)), (790, y2 + 130))
    chip, cb = sprites["ui_chip"]
    sheet.alpha_composite(nine_slice(chip, cb, (230, 88)), (1040, y2))
    sheet.alpha_composite(sprites["icon_coin"][0].resize((96, 96), Image.LANCZOS), (1020, y2 - 12))
    d.text((1150, y2 + 14), "1.791 Xu", fill=COFFEE, font=bigfont)
    sheet.alpha_composite(nine_slice(chip, cb, (300, 88)), (1040, y2 + 110))
    sheet.alpha_composite(sprites["icon_star"][0].resize((96, 96), Image.LANCZOS), (1020, y2 + 98))
    tr, trb = sprites["ui_bar_track"]
    fl, flb = sprites["ui_bar_fill"]
    sheet.alpha_composite(nine_slice(tr, trb, (160, 30)), (1140, y2 + 136))
    sheet.alpha_composite(tint(nine_slice(fl, flb, (80, 24)), GOLD), (1143, y2 + 139))
    card, kb = sprites["ui_card"]
    sheet.alpha_composite(nine_slice(card, kb, (330, 112)), (1100, y2 + 220))
    sheet.alpha_composite(sprites["icon_cup"][0].resize((72, 72), Image.LANCZOS), (1110, y2 + 232))
    d.text((1190, y2 + 240), "Ca phe sua", fill=COFFEE, font=bigfont)
    rb = sprites["ui_ring_bg"][0].resize((64, 64), Image.LANCZOS)
    sheet.alpha_composite(rb, (1345, y2 + 238))
    bub, bbb = sprites["ui_bubble"]
    sheet.alpha_composite(nine_slice(bub, bbb, (150, 110)), (480, y2 + 270))
    sheet.alpha_composite(sprites["ui_bubble_tail"][0], (527, y2 + 270 + 110 - 14))
    sheet.alpha_composite(sprites["icon_phin"][0].resize((72, 72), Image.LANCZOS), (500, y2 + 280))
    ring_bg = sprites["ui_ring_bg"][0]
    ring_fill = sprites["ui_ring_fill"][0]
    rx = 700
    for frac, col in ((1.0, GREEN), (0.6, GOLD), (0.25, hx("D62828"))):
        sheet.alpha_composite(ring_bg, (rx, y2 + 250))
        flc = tint(ring_fill, col)
        mask = Image.new("L", (128, 128), 0)
        ImageDraw.Draw(mask).pieslice((-10, -10, 138, 138), -90, -90 + 360 * frac, fill=255)
        flc.putalpha(ImageChops.multiply(flc.split()[3], mask))
        sheet.alpha_composite(flc, (rx, y2 + 250))
        rx += 140
    # v2: icon HUD mới (trên nút coffee / vàng như trong game) + ly-thước có chất lỏng + huy hiệu + nhãn
    x, y = 20, 1120
    btn_coffee, bcb = sprites["ui_btn_coffee"]
    for name in icons2:
        im = sprites["icon_%s" % name][0]
        if name in ("menu", "close"):
            sheet.alpha_composite(nine_slice(btn_coffee, bcb, (128, 128)), (x, y))
            sheet.alpha_composite(im.resize((80, 80), Image.LANCZOS), (x + 24, y + 18))
        else:
            sheet.alpha_composite(im, (x, y))
        d.text((x + 4, y + 130), name, fill=(255, 255, 255), font=font)
        x += 140
    glass = sprites["ui_gauge_glass"][0]
    gmask = sprites["ui_gauge_mask"][0]
    liquid = tint(gmask, COFFEE_MD)
    cut = Image.new("L", gmask.size, 0)
    ImageDraw.Draw(cut).rectangle((0, 70, gmask.size[0], gmask.size[1]), fill=255)
    liquid.putalpha(ImageChops.multiply(liquid.split()[3], cut))
    sheet.alpha_composite(liquid, (x, y - 20))
    band = Image.new("RGBA", gmask.size, GOLD + (0,))
    bm = Image.new("L", gmask.size, 0)
    ImageDraw.Draw(bm).rectangle((0, 42, gmask.size[0], 66), fill=90)
    band.putalpha(ImageChops.multiply(gmask.split()[3], bm))
    sheet.alpha_composite(band, (x, y - 20))
    sheet.alpha_composite(glass, (x, y - 20))
    x += 110
    sheet.alpha_composite(sprites["ui_badge"][0], (x, y))
    d.text((x + 24, y + 12), "3", fill=CREAM, font=bigfont)
    sheet.alpha_composite(nine_slice(sprites["ui_tag"][0], sprites["ui_tag"][1], (70, 30)), (x, y + 80))
    d.text((x + 16, y + 84), "Rot", fill=COFFEE, font=font)
    sheet.alpha_composite(sprites["ui_arrow"][0], (x + 80, y))
    sheet.alpha_composite(sprites["ui_sparkle"][0], (x + 80, y + 50))
    # nguyên liệu Tier 2: cỡ 128, rồi cả 14 nguyên liệu ở 64 và 40 (cỡ nút / đĩa chuỗi bước) để so độ rõ
    x, y = 20, 1320
    for name in icons3:
        sheet.alpha_composite(sprites["icon_%s" % name][0], (x, y))
        d.text((x + 4, y + 128), name, fill=(255, 255, 255), font=font)
        x += 140
    x, y = 20, 1470
    for name in list(icons3) + ["phin", "sua_dac", "sua_tuoi", "da", "tra"]:
        im = sprites["icon_%s" % name][0]
        sheet.alpha_composite(im.resize((64, 64), Image.LANCZOS), (x, y))
        sheet.alpha_composite(im.resize((40, 40), Image.LANCZOS), (x + 12, y + 72))
        x += 100
    sheet.save(SHEET)
    print("contact sheet:", SHEET)


if __name__ == "__main__":
    main()
