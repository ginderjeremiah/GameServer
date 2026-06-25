"""Generate the 'Aetheric' conlang display font — the proficiency "words of power".

Aetheric is a flowing, incantatory script used purely as decoration (it is NOT
readable copy — see UI/scripts/conlang/README.md and docs/frontend.md). Every
glyph is defined parametrically as a set of polylines (incl. sampled quadratic
curves and rings) that are stroked into thick filled contours, so the whole
alphabet is regenerated from these definitions rather than hand-drawn in a font
editor. Re-run this script to rebuild `UI/static/fonts/Aetheric.woff2`:

    pip install fonttools brotli        # brotli is required for woff2 output
    python3 UI/scripts/conlang/generate_font.py

Design rules the forms follow (so edits stay consistent):
  * Abstract, NOT the Latin letter at each key — words must not read as English.
  * Everything sits on the baseline; descenders are a deliberate few (g j p y).
  * Bases vary — single stem + bows, twin-stem bridges, stem + twigs, leaning
    stems, and bowls — so words gain rhythm instead of a picket fence of stems.
"""
import math
import os
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

UPM = 1000
CAP = 700
DESC = -200
WIDTH = 46          # default stroke thickness (font units)

# ---- primitives (unit coords, y-up, baseline 0) ----
def ln(a, b):
    return [a, b]

def qb(p0, c, p1, n=20):
    """Sample a quadratic Bezier into a polyline."""
    out = []
    for i in range(n + 1):
        t = i / n
        out.append(((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * c[0] + t * t * p1[0],
                    (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * c[1] + t * t * p1[1]))
    return out

def ring(cx, cy, rx, ry=None, n=40):
    ry = ry or rx
    return [(cx + rx * math.cos(2 * math.pi * i / n),
             cy + ry * math.sin(2 * math.pi * i / n)) for i in range(n + 1)]

def seg_quad(a, b, w):
    """A single straight stroke segment as a 4-point filled quad."""
    (ax, ay), (bx, by) = a, b
    dx, dy = bx - ax, by - ay
    length = math.hypot(dx, dy) or 1.0
    ux, uy = dx / length, dy / length
    nx, ny = -uy, ux
    h = w / 2.0
    ax, ay = ax - ux * h, ay - uy * h
    bx, by = bx + ux * h, by + uy * h
    return [(ax + nx * h, ay + ny * h), (bx + nx * h, by + ny * h),
            (bx - nx * h, by - ny * h), (ax - nx * h, ay - ny * h)]

def thick(points, w=WIDTH):
    """Stroke a polyline into a chain of overlapping filled quads."""
    return [seg_quad(points[i], points[i + 1], w) for i in range(len(points) - 1)]

def dot(cx, cy, r, n=16):
    return [(cx + r * math.cos(2 * math.pi * i / n), cy + r * math.sin(2 * math.pi * i / n)) for i in range(n)]

def glyph(strokes, decos=(), w=WIDTH):
    contours = []
    for s in strokes:
        contours += thick(s, w)
    return contours + list(decos)

# ---- shared construction helpers ----
SX = 180                                          # default vertical stem x
DDEPTH = -150                                     # modest, deliberate descender depth

def stem(x, y0=0, y1=660):
    return ln((x, y0), (x, y1))                   # a stem that sits on the baseline

def bow(y0, y1, d, x=SX):
    """A curved bow off the stem at x, bulging by signed depth d (right +)."""
    return qb((x, y0), (x + d, (y0 + y1) / 2), (x, y1))

def twig(x, y, dx, dy):
    return ln((x, y), (x + dx, y + dy))           # a short straight branch

def bridge(x0, x1, y, sag):
    return qb((x0, y), ((x0 + x1) / 2, y + sag), (x1, y))   # an arc joining two stems

# ---- the alphabet (abstract, baseline-anchored, varied bases) ----
def alphabet():
    a = {}
    # single stem + bow(s) — the thorn family
    a['a'] = glyph([stem(SX), bow(560, 320, 300)], [dot(SX, 40, 26)])
    a['b'] = glyph([stem(SX), bow(560, 320, -300)])
    a['c'] = glyph([stem(SX), bow(560, 360, 260), bow(320, 90, -260)])
    a['e'] = glyph([stem(SX), bow(620, 380, 300), bow(380, 140, 300)])
    a['t'] = glyph([stem(SX), bow(440, 160, 300)])
    a['r'] = glyph([stem(SX), bow(560, 360, -260), bow(320, 90, 260)])       # reverse-c alternation
    a['s'] = glyph([stem(SX), qb((SX, 640), (540, 360), (SX, 80))])          # one big sweeping bow
    a['x'] = glyph([stem(SX, 0, 640), qb((SX, 640), (20, 340), (SX, 40)),
                    qb((SX, 640), (340, 340), (SX, 40))])                    # almond bisected by stem
    # twin stems + bridge
    a['h'] = glyph([stem(120), stem(320), bridge(120, 320, 560, 140)])
    a['m'] = glyph([stem(120), stem(320), bridge(120, 320, 360, 140)], [dot(220, 580, 28)])
    a['n'] = glyph([stem(SX, 0, 380), ring(SX, 520, 150, 150)])             # lollipop: loop atop a stem
    a['w'] = glyph([stem(110), stem(330), twig(110, 560, 220, -420), twig(110, 140, 220, 420)])  # twin + X
    # stem + twigs
    a['k'] = glyph([stem(120), twig(120, 520, 250, 140), twig(120, 300, 250, -140)])  # diverging twigs
    a['f'] = glyph([stem(SX, 0, 700), twig(SX, 420, 170, 70), twig(SX, 300, 170, 70)])
    # leaning stems landing on the baseline
    a['d'] = glyph([ln((140, 0), (310, 660)), ring(385, 470, 120, 120)])
    a['v'] = glyph([ln((120, 0), (300, 660)), qb((300, 560), (480, 400), (300, 240))])
    a['y'] = glyph([ln((110, 0), (300, 660)), qb((250, 520), (60, 380), (250, 240))])  # leaning stem + left bow
    a['z'] = glyph([stem(SX, 0, 520), qb((SX, 520), (430, 650), (430, 420))], [dot(SX, 40, 26)])  # stem + top crook
    # bowls / cradles that sit on the baseline
    a['o'] = glyph([qb((SX, 0), (20, 330), (SX, 660)), qb((SX, 660), (340, 330), (SX, 0)),
                    ln((55, 330), (305, 330))])                            # almond + crossbar
    a['u'] = glyph([qb((120, 640), (250, 40), (380, 640)), ln((250, 360), (250, 40))])
    a['q'] = glyph([stem(SX, 300, 660), ring(300, 160, 150, 150)])
    # short forms
    a['i'] = glyph([stem(SX, 0, 560), twig(SX, 540, 130, -55), twig(SX, 430, 130, 55)])  # short stem + chevron
    a['l'] = glyph([stem(230, 0, 700), twig(230, 540, -150, 0)])
    # the deliberate descenders
    a['g'] = glyph([stem(SX, DDEPTH, 660), bow(560, 320, 300)])
    a['j'] = glyph([qb((SX, 600), (SX, DDEPTH), (70, DDEPTH - 30)), bow(580, 400, 210)])  # hooked cane + bow
    a['p'] = glyph([stem(SX, DDEPTH, 660), bow(600, 360, 300), bow(360, 60, 300)])
    return a

# in-script numerals — a shorter "register" (stems to ~520) so numbers read as
# numbers, distinct from the taller letters, while sharing the flowing DNA.
DGT = 180
def s5(y0=0, y1=520):
    return ln((DGT, y0), (DGT, y1))
DIGITS = {
    '0': glyph([qb((DGT, 40), (10, 280), (DGT, 480)), qb((DGT, 480), (350, 280), (DGT, 40))]),
    '1': glyph([s5(), twig(DGT, 520, -120, -50)]),
    '2': glyph([s5(), bow(480, 200, 240, DGT)]),
    '3': glyph([s5(), bow(500, 300, 200, DGT), bow(300, 80, 200, DGT)]),
    '4': glyph([s5(), twig(DGT, 300, -150, 90), twig(DGT, 300, 150, 90)]),
    '5': glyph([s5(), bow(480, 200, -240, DGT)]),
    '6': glyph([s5(180, 520), ring(265, 150, 125, 125)]),
    '7': glyph([s5(), twig(DGT, 500, 190, 0)]),
    '8': glyph([s5(), bow(480, 280, 180, DGT), bow(280, 60, -180, DGT)]),
    '9': glyph([s5(380, 520), ring(265, 360, 125, 125), twig(DGT, 380, 0, -380)]),
}

# digraph ligatures — grounded fused forms (a base carrying both features)
LIGS = {
    'th': glyph([stem(SX, 0, 700), bow(640, 380, 300), bow(380, 120, -300)]),
    'sh': glyph([stem(SX), qb((SX, 640), (540, 360), (SX, 80))], [dot(SX, 40, 26)]),
    'ch': glyph([stem(SX), bow(560, 360, 260), bow(320, 90, -260), qb((SX, 90), (360, 40), (440, 120))]),
    'ng': glyph([stem(120), stem(320), bridge(120, 320, 560, 140)], [dot(220, 580, 26)]),
    'ph': glyph([stem(SX, DDEPTH, 700), bow(600, 360, 300), twig(SX, 640, 150, 40)]),
    'kh': glyph([stem(120), twig(120, 520, 250, 140), twig(120, 300, 250, -140), twig(120, 560, -120, 40)]),
}

PUNCT = {
    'period': [dot(150, 50, 30)],
    'comma': [dot(150, 70, 30), [(120, 50), (180, 50), (110, -150)]],
    'exclam': glyph([ln((235, 200), (235, 640))], [dot(235, 70, 30)]),
    'question': glyph([qb((110, 560), (300, 760), (400, 520)), qb((400, 520), (300, 380), (255, 300)),
                       ln((255, 300), (255, 230))], [dot(255, 70, 30)]),
    'hyphen': glyph([qb((110, 360), (290, 410), (440, 360))]),
    'colon': [dot(235, 200, 30), dot(235, 470, 30)],
    'apostrophe': glyph([qb((230, 700), (290, 640), (235, 540))]),
}
CMAP_PUNCT = {ord('.'): 'period', ord(','): 'comma', ord('!'): 'exclam',
              ord('?'): 'question', ord('-'): 'hyphen', ord(':'): 'colon', ord("'"): 'apostrophe'}

LIGATURE_PAIRS = (('t', 'h'), ('s', 'h'), ('c', 'h'), ('n', 'g'), ('p', 'h'), ('k', 'h'))

# Spacing. Advance widths are derived from each glyph's actual ink bounds plus a
# uniform side bearing on both sides — so every glyph carries the same left/right
# gap and words space evenly. (A hand-tuned advance table previously left large
# gaps wherever a glyph's ink sat far from its advance box, e.g. b's left-bulging
# bow read as a near-space before the following letter.)
SIDE_BEARING = 75
SPACE_ADV = 280

def _ink_bounds_x(contours):
    xs = [p[0] for c in contours if len(c) >= 3 for p in c]
    return (min(xs), max(xs)) if xs else None

def normalized(contours, sb=SIDE_BEARING):
    """Shift contours so their ink starts at the side bearing; return (contours, advance)."""
    bounds = _ink_bounds_x(contours)
    if bounds is None:
        return contours, SPACE_ADV
    minx, maxx = bounds
    dx = sb - minx
    shifted = [[(x + dx, y) for (x, y) in c] for c in contours]
    return shifted, round((maxx - minx) + 2 * sb)

def _pen(contours):
    pen = TTGlyphPen(None)
    for c in contours:
        if len(c) < 3:
            continue
        pen.moveTo((round(c[0][0]), round(c[0][1])))
        for pt in c[1:]:
            pen.lineTo((round(pt[0]), round(pt[1])))
        pen.closePath()
    return pen.glyph()

def _font():
    a = alphabet()
    glyphs, metrics, cmap, order = {}, {}, {}, ['.notdef']

    def add(name, contours, codepoints=()):
        shifted, advance = normalized(contours)
        glyphs[name] = _pen(shifted)
        metrics[name] = (advance, SIDE_BEARING)
        order.append(name)
        for cp in codepoints:
            cmap[cp] = name

    notdef_shifted, notdef_adv = normalized([ring(300, 360, 180, 300)])
    glyphs['.notdef'] = _pen(notdef_shifted)
    metrics['.notdef'] = (notdef_adv, SIDE_BEARING)
    glyphs['space'] = TTGlyphPen(None).glyph()
    metrics['space'] = (SPACE_ADV, 0)
    order.append('space')
    cmap[0x20] = 'space'

    for ch, contours in a.items():
        add('cl_' + ch, contours, (ord(ch), ord(ch.upper())))
    for ch, contours in DIGITS.items():
        add('cl_d' + ch, contours, (ord(ch),))
    for name, contours in PUNCT.items():
        add(name, contours, [cp for cp, n in CMAP_PUNCT.items() if n == name])
    for lg, contours in LIGS.items():
        add('cl_' + lg, contours, ())

    fb = FontBuilder(UPM, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=CAP + 60, descent=DESC)
    fb.setupNameTable({'familyName': 'Aetheric', 'styleName': 'Regular',
                       'psName': 'Aetheric-Regular', 'version': '1.0'})
    fb.setupOS2(sTypoAscender=CAP + 60, sTypoDescender=DESC,
                usWinAscent=CAP + 60, usWinDescent=-DESC)
    fb.setupPost(keepGlyphNames=True)
    fea = "feature liga {\n" + "".join(
        f"    sub cl_{x} cl_{y} by cl_{x}{y};\n" for x, y in LIGATURE_PAIRS
    ) + "} liga;\n"
    fb.addOpenTypeFeatures(fea)
    return fb

def build():
    out_dir = os.path.normpath(os.path.join(os.path.dirname(__file__), '..', '..', 'static', 'fonts'))
    os.makedirs(out_dir, exist_ok=True)
    fb = _font()
    fb.font.flavor = 'woff2'
    out_path = os.path.join(out_dir, 'Aetheric.woff2')
    fb.font.save(out_path)
    print('wrote', out_path)

if __name__ == '__main__':
    build()
