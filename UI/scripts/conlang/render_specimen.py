"""Render a reference specimen of the Aetheric conlang to specimen.png.

Documents the committed font visually (alphabet, numerals, ligatures, and sample
proficiency "words of power") without anyone needing to install the font. Draws
the same stroked-Bezier outlines the font ships, filled with the even-odd rule so
loop counters (o, n, d, q, 6, 9) render hollow. Requires Pillow:

    pip install -r requirements.txt
    python3 UI/scripts/conlang/render_specimen.py
"""
import os
import sys
sys.path.insert(0, os.path.dirname(__file__))
import generate_font as gf
from PIL import Image, ImageDraw, ImageChops, ImageFont

BG = (12, 14, 20)
INK = (150, 214, 240)
GOLD = (224, 196, 132)
LABEL = (120, 132, 148)
GUIDE = (40, 46, 58)
HEAD = (220, 228, 240)
SS = 3                                            # supersample factor
A = gf.alphabet()

def font(size):
    return ImageFont.load_default(size=size * SS)

def glyph_tile(cs, scale, color):
    """Even-odd filled RGBA tile for shifted contours; returns (tile, minx, maxy)."""
    xs = [x for c in cs for x, y in c]
    ys = [y for c in cs for x, y in c]
    minx, maxx, miny, maxy = min(xs), max(xs), min(ys), max(ys)
    pad = 2
    w = int((maxx - minx) * scale) + pad * 2
    h = int((maxy - miny) * scale) + pad * 2
    acc = Image.new('1', (w * SS, h * SS), 0)
    for c in cs:
        if len(c) < 3:
            continue
        pts = [(((x - minx) * scale + pad) * SS, ((maxy - y) * scale + pad) * SS) for x, y in c]
        tmp = Image.new('1', (w * SS, h * SS), 0)
        ImageDraw.Draw(tmp).polygon(pts, fill=1)
        acc = ImageChops.logical_xor(acc, tmp)
    tile = Image.new('RGBA', (w * SS, h * SS), (0, 0, 0, 0))
    solid = Image.new('RGBA', (w * SS, h * SS), color + (255,))
    return Image.composite(solid, tile, acc.convert('L')), minx, maxy

def paste_glyph(img, gd, x0, base, scale, color):
    """Paste a glyph so glyph-x 0 lands at x0 and the baseline at `base` (px)."""
    cs, adv = gf.shaped(gd)
    tile, minx, maxy = glyph_tile(cs, scale, color)
    px = round((x0 + minx * scale - 2) * SS)
    py = round((base - maxy * scale - 2) * SS)
    img.paste(tile, (px, py), tile)
    return adv

def shape(text):
    """Greedy digraph -> ligature shaper. Yields (glyph_data|None, advance)."""
    out, i, t = [], 0, text.lower()
    while i < len(t):
        two = t[i:i + 2]
        if two in gf.LIGS:
            out.append((gf.LIGS[two], gf.shaped(gf.LIGS[two])[1])); i += 2
        elif t[i] == ' ':
            out.append((None, gf.SPACE_ADV)); i += 1
        elif t[i] in A:
            out.append((A[t[i]], gf.shaped(A[t[i]])[1])); i += 1
        else:
            i += 1
    return out

def main():
    cols, cw, ch = 7, 168, 280
    rows_a = (26 + cols - 1) // cols
    rows_d = (10 + cols - 1) // cols
    words = ['inferno', 'frostbite', 'orbit', 'abyss', 'tempest', 'verdant grove']
    scale = 0.30
    w = 40 + cols * cw + 20
    h = 90 + rows_a * ch + 60 + rows_d * ch + 80 + len(words) * 250 + 40
    img = Image.new('RGB', (w * SS, h * SS), BG)
    dd = ImageDraw.Draw(img)
    dd.text((40 * SS, 26 * SS), 'AETHERIC — stroked-Bezier outlines', font=font(22), fill=HEAD)

    def hline(y, x0, x1, fill):
        dd.line([(x0 * SS, y * SS), (x1 * SS, y * SS)], fill=fill, width=SS)

    def cell(gd, label, gx, gy, color):
        base = gy + 200
        _, adv = gf.shaped(gd)
        hline(base, gx, gx + cw - 16, GUIDE)
        hline(base - gf.DDEPTH * scale, gx, gx + cw - 16, GUIDE)
        paste_glyph(img, gd, gx + (cw - 16 - adv * scale) / 2, base, scale, color)
        dd.text(((gx + 6) * SS, (base + 36) * SS), label, font=font(14), fill=LABEL)

    y0 = 80
    for i, chl in enumerate('abcdefghijklmnopqrstuvwxyz'):
        cell(A[chl], chl, 40 + (i % cols) * cw, y0 + (i // cols) * ch, INK)

    yd = y0 + rows_a * ch + 30
    dd.text((40 * SS, yd * SS), 'NUMERALS', font=font(18), fill=HEAD)
    for i, dl in enumerate('0123456789'):
        cell(gf.DIGITS[dl], dl, 40 + (i % cols) * cw, yd + 30 + (i // cols) * ch, GOLD)

    yw = yd + 30 + rows_d * ch + 40
    dd.text((40 * SS, yw * SS), 'WORDS OF POWER', font=font(18), fill=HEAD)
    yw += 24
    for wd in words:
        base = yw + int(gf.CAP * scale)
        hline(base, 40, w - 40, GUIDE)
        x = 60
        for gd, adv in shape(wd):
            if gd is not None:
                paste_glyph(img, gd, x, base, scale, GOLD)
            x += adv * scale
        dd.text(((x + 20) * SS, (base - 24) * SS), wd, font=font(15), fill=LABEL)
        yw += 250

    img = img.resize((w, h), Image.LANCZOS)
    out_path = os.path.join(os.path.dirname(__file__), 'specimen.png')
    img.save(out_path)
    print('wrote', out_path)

if __name__ == '__main__':
    main()
