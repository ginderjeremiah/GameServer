"""Render a reference specimen of the Aetheric conlang to specimen.png.

This documents the committed font visually (alphabet, numerals, ligatures, and
sample proficiency "words of power") without anyone needing to install the font.
Draws from the same glyph geometry as generate_font.py, supersampled for clean
edges. Requires Pillow:

    pip install pillow
    python3 UI/scripts/conlang/render_specimen.py
"""
import os
from PIL import Image, ImageDraw, ImageFont
import generate_font as gf

BG = (12, 14, 20)
INK = (150, 214, 240)
GOLD = (224, 196, 132)
LABEL = (120, 132, 148)
GUIDE = (40, 46, 58)
HEAD = (220, 228, 240)
SS = 3                                            # supersample factor
A = gf.alphabet()

def lab(size):
    return ImageFont.load_default(size=size * SS)

def advance(ch):
    return gf.WIDE.get(ch, gf.ADV)

class Canvas:
    def __init__(self, draw):
        self.d = draw
    def text(self, xy, t, size, fill):
        self.d.text((xy[0] * SS, xy[1] * SS), t, font=lab(size), fill=fill)
    def poly(self, pts, fill):
        self.d.polygon([(x * SS, y * SS) for x, y in pts], fill=fill)
    def line(self, pts, fill, w=1):
        self.d.line([(x * SS, y * SS) for x, y in pts], fill=fill, width=w * SS)
    def tlen(self, t, size):
        return self.d.textlength(t, font=lab(size)) / SS

def draw_glyph(c, contours, ox, baseline, color, scale):
    for ct in contours:
        if len(ct) >= 3:
            c.poly([(ox + x * scale, baseline - y * scale) for x, y in ct], color)

def shape(text):
    """Greedy digraph -> ligature shaper. Yields (contours|None, advance)."""
    out, i, t = [], 0, text.lower()
    while i < len(t):
        two = t[i:i + 2]
        if two in gf.LIGS:
            out.append((gf.LIGS[two], advance(two[0]))); i += 2
        elif t[i] == ' ':
            out.append((None, gf.ADV * 0.5)); i += 1
        elif t[i] in A:
            out.append((A[t[i]], advance(t[i]))); i += 1
        else:
            i += 1
    return out

def main():
    cols, cw, ch = 7, 168, 280
    rows_a = (26 + cols - 1) // cols
    rows_d = (10 + cols - 1) // cols
    words = ['inferno', 'tempest', 'aether', 'frostbite', 'verdant grove']
    scale = 0.30
    w = 40 + cols * cw + 20
    h = 90 + rows_a * ch + 60 + rows_d * ch + 80 + len(words) * 250 + 40
    img = Image.new('RGB', (w * SS, h * SS), BG)
    c = Canvas(ImageDraw.Draw(img))
    c.text((40, 26), 'AETHERIC — conlang words of power', 22, HEAD)

    def cell(contours, label, gx, gy, color):
        base = gy + 200
        c.line([(gx, base), (gx + cw - 16, base)], GUIDE, 1)
        c.line([(gx, base + gf.DDEPTH * scale), (gx + cw - 16, base + gf.DDEPTH * scale)], GUIDE, 1)
        draw_glyph(c, contours, gx + (cw - 16 - advance(label) * scale) / 2, base, color, scale)
        c.text((gx + 6, base + 36), label, 14, LABEL)

    y0 = 80
    for i, chl in enumerate('abcdefghijklmnopqrstuvwxyz'):
        cell(A[chl], chl, 40 + (i % cols) * cw, y0 + (i // cols) * ch, INK)

    yd = y0 + rows_a * ch + 30
    c.text((40, yd), 'NUMERALS', 18, HEAD)
    for i, dl in enumerate('0123456789'):
        cell(gf.DIGITS[dl], dl, 40 + (i % cols) * cw, yd + 30 + (i // cols) * ch, GOLD)

    yw = yd + 30 + rows_d * ch + 40
    c.text((40, yw), 'WORDS OF POWER', 18, HEAD)
    yw += 24
    for wd in words:
        base = yw + int(gf.CAP * scale)
        c.line([(40, base), (w - 40, base)], GUIDE, 1)
        x = 60
        for contours, adv in shape(wd):
            if contours is not None:
                draw_glyph(c, contours, x, base, GOLD, scale)
            x += int(adv * scale)
        c.text((x + 20, base - 24), wd, 15, LABEL)
        yw += 250

    img = img.resize((w, h), Image.LANCZOS)
    out_path = os.path.join(os.path.dirname(__file__), 'specimen.png')
    img.save(out_path)
    print('wrote', out_path)

if __name__ == '__main__':
    main()
