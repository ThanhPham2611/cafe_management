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
    sheet = Image.new("RGBA", (1500, 1120), bg)
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
    sheet.save(SHEET)
    print("contact sheet:", SHEET)


if __name__ == "__main__":
    main()
