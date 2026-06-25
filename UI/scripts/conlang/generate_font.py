"""Generate the 'Aetheric' conlang display font — the proficiency "words of power".

Aetheric is a flowing, incantatory script used purely as decoration (it is NOT
readable copy — see UI/scripts/conlang/README.md and docs/frontend.md). Every
glyph is defined parametrically as a set of centerline strokes (lines, quadratic
curves, rings); those centerlines are stroked into smooth filled outlines with a
real pen (round caps + round joins, via skia-pathops) and emitted as TrueType
quadratic curves. So the whole alphabet is regenerated from these definitions
rather than hand-drawn in a font editor. Re-run to rebuild the woff2:

    pip install -r requirements.txt        # fonttools, brotli, skia-pathops
    python3 UI/scripts/conlang/generate_font.py

Design rules the forms follow (so edits stay consistent):
  * Abstract, NOT the Latin letter at each key — words must not read as English.
  * Everything sits on the baseline; descenders are a deliberate few (g j p y).
  * Bases vary — single stem + bows, twin-stem bridges, stem + twigs, leaning
    stems, and bowls — so words gain rhythm instead of a picket fence of stems.
"""
import math
import os
import pathops
from pathops import Path, LineCap, LineJoin
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

UPM = 1000
CAP = 700
DESC = -200
WIDTH = 46          # stroke thickness (font units)
CONIC_TOL = 0.1     # tolerance when converting round-join/cap conics to quads

# ---- centerline primitives ------------------------------------------------
# A "subpath" is a list of path commands: ('M', pt) ('L', pt) ('Q', ctrl, pt)
# ('Z',). Open subpaths (a stem, a bow) are stroked with round caps; closed
# subpaths (a ring) are stroked into a tube. Solid decorations (dots) are filled
# rings, not stroked.
def line_sub(a, b):
    return [('M', a), ('L', b)]

def quad_sub(p0, c, p1):
    return [('M', p0), ('Q', c, p1)]

def ellipse_sub(cx, cy, rx, ry=None, k=16):
    """A closed ellipse as k quadratic arcs."""
    ry = ry if ry is not None else rx
    cr = 1.0 / math.cos(math.pi / k)            # control radius for a quad arc
    sub = [('M', (cx + rx, cy))]
    for i in range(k):
        a1 = 2 * math.pi * (i + 1) / k
        am = 2 * math.pi * (i + 0.5) / k
        sub.append(('Q', (cx + rx * cr * math.cos(am), cy + ry * cr * math.sin(am)),
                         (cx + rx * math.cos(a1), cy + ry * math.sin(a1))))
    sub.append(('Z',))
    return sub

# Aliases used directly in the glyph definitions.
def ln(a, b):
    return line_sub(a, b)

def qb(p0, c, p1):
    return quad_sub(p0, c, p1)

def ring(cx, cy, rx, ry=None):
    return ellipse_sub(cx, cy, rx, ry)

def dot(cx, cy, r):
    return ellipse_sub(cx, cy, r, r)            # a solid disc (goes in decos)

# ---- shared construction helpers ------------------------------------------
SX = 180                                          # default vertical stem x
DDEPTH = -150                                     # modest, deliberate descender depth

def stem(x, y0=0, y1=660):
    return line_sub((x, y0), (x, y1))             # a stem that sits on the baseline

def bow(y0, y1, d, x=SX):
    """A curved bow off the stem at x, bulging by signed depth d (right +)."""
    return quad_sub((x, y0), (x + d, (y0 + y1) / 2), (x, y1))

def twig(x, y, dx, dy):
    return line_sub((x, y), (x + dx, y + dy))     # a short straight branch

def bridge(x0, x1, y, sag):
    return quad_sub((x0, y), ((x0 + x1) / 2, y + sag), (x1, y))   # arc joining two stems

def glyph(strokes, decos=()):
    """A glyph is (centerline strokes, solid decorations)."""
    return (list(strokes), list(decos))

# ---- the alphabet (abstract, baseline-anchored, varied bases) -------------
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
    a['n'] = glyph([stem(SX, 0, 372), ring(SX, 520, 150)])                  # lollipop: loop atop a stem
    a['w'] = glyph([stem(110), stem(330), twig(110, 560, 220, -420), twig(110, 140, 220, 420)])  # twin + X
    # stem + twigs
    a['k'] = glyph([stem(120), twig(120, 520, 250, 140), twig(120, 300, 250, -140)])  # diverging twigs
    a['f'] = glyph([stem(SX, 0, 700), twig(SX, 420, 170, 70), twig(SX, 300, 170, 70)])
    # leaning stems landing on the baseline
    a['d'] = glyph([ln((140, 0), (310, 660)), ring(385, 470, 120)])
    a['v'] = glyph([ln((120, 0), (300, 660)), qb((300, 560), (480, 400), (300, 240))])
    a['y'] = glyph([ln((110, 0), (300, 660)), qb((250, 520), (60, 380), (250, 240))])  # leaning stem + left bow
    a['z'] = glyph([stem(SX, 0, 520), qb((SX, 520), (430, 650), (430, 420))], [dot(SX, 40, 26)])  # stem + top crook
    # bowls / cradles that sit on the baseline
    a['o'] = glyph([qb((SX, 0), (20, 330), (SX, 660)), qb((SX, 660), (340, 330), (SX, 0)),
                    ln((55, 330), (305, 330))])                            # almond + crossbar
    a['u'] = glyph([qb((120, 640), (250, 40), (380, 640)), ln((250, 335), (250, 0))])
    a['q'] = glyph([stem(SX, 235, 660), ring(300, 160, 150)])
    # short forms
    a['i'] = glyph([stem(SX, 0, 560), twig(SX, 540, 130, -55), twig(SX, 430, 130, 55)])  # short stem + chevron
    a['l'] = glyph([stem(230, 0, 700), twig(230, 540, -150, 0)])
    # the deliberate descenders
    a['g'] = glyph([stem(SX, -8, 660), bow(560, 320, 300), ring(SX, -90, 80)])  # bowl + looped tail
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
    '6': glyph([s5(180, 520), ring(265, 150, 125)]),
    '7': glyph([s5(), twig(DGT, 500, 190, 0)]),
    '8': glyph([s5(), bow(480, 280, 180, DGT), bow(280, 60, -180, DGT)]),
    '9': glyph([s5(380, 520), ring(265, 360, 125), twig(DGT, 380, 0, -380)]),
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
    'period': glyph([], [dot(150, 50, 34)]),
    'comma': glyph([qb((175, 70), (150, -40), (95, -150))], [dot(170, 110, 34)]),
    'exclam': glyph([ln((235, 200), (235, 640))], [dot(235, 70, 32)]),
    'question': glyph([qb((110, 560), (300, 760), (400, 520)), qb((400, 520), (300, 380), (255, 300)),
                       ln((255, 300), (255, 230))], [dot(255, 70, 32)]),
    'hyphen': glyph([qb((110, 360), (290, 410), (440, 360))]),
    'colon': glyph([], [dot(235, 200, 32), dot(235, 470, 32)]),
    'apostrophe': glyph([qb((230, 700), (290, 640), (235, 540))]),
}
CMAP_PUNCT = {ord('.'): 'period', ord(','): 'comma', ord('!'): 'exclam',
              ord('?'): 'question', ord('-'): 'hyphen', ord(':'): 'colon', ord("'"): 'apostrophe'}

LIGATURE_PAIRS = (('t', 'h'), ('s', 'h'), ('c', 'h'), ('n', 'g'), ('p', 'h'), ('k', 'h'))

# Spacing. Advance widths are derived from each glyph's actual ink bounds plus a
# uniform side bearing on both sides, so every glyph carries the same left/right
# gap and words space evenly.
SIDE_BEARING = 75
SPACE_ADV = 280

# ---- stroking pipeline ----------------------------------------------------
def _emit(pen, subpaths):
    for sub in subpaths:
        for cmd in sub:
            kind = cmd[0]
            if kind == 'M':
                pen.moveTo(cmd[1])
            elif kind == 'L':
                pen.lineTo(cmd[1])
            elif kind == 'Q':
                pen.qCurveTo(cmd[1], cmd[2])
            elif kind == 'Z':
                pen.closePath()
        if sub[-1][0] != 'Z':
            pen.endPath()

def outline(gd):
    """Stroke the centerlines (round caps + joins), union with the solid decos,
    and return a pathops.Path of quadratic outlines — the final filled glyph."""
    strokes, solids = gd
    stroked = Path()
    if strokes:
        _emit(stroked.getPen(), strokes)
        stroked.stroke(WIDTH, LineCap.ROUND_CAP, LineJoin.ROUND_JOIN, 4.0)
        stroked.convertConicsToQuads(CONIC_TOL)
    solid = Path()
    if solids:
        _emit(solid.getPen(), solids)
    return pathops.op(stroked, solid, pathops.PathOp.UNION)

def _flatten(path, n=12):
    """Sample a quadratic outline path into point contours (for bounds + specimen)."""
    contours, cur = [], []
    for verb, pts in path:
        v = int(verb)
        if v == 0:
            if cur:
                contours.append(cur)
            cur = [tuple(pts[0])]
        elif v == 1:
            cur.append(tuple(pts[0]))
        elif v == 2:
            (cx, cy), (ex, ey) = pts[0], pts[1]
            px, py = cur[-1]
            for i in range(1, n + 1):
                t = i / n
                mt = 1 - t
                cur.append((mt * mt * px + 2 * mt * t * cx + t * t * ex,
                            mt * mt * py + 2 * mt * t * cy + t * t * ey))
        elif v == 5:
            if cur:
                contours.append(cur)
                cur = []
    if cur:
        contours.append(cur)
    return contours

def _bounds_x(contours):
    xs = [x for c in contours for x, y in c]
    return (min(xs), max(xs)) if xs else (0.0, 0.0)

def glyph_metrics(gd):
    """Return (outline path, x-shift to a uniform left bearing, advance width)."""
    path = outline(gd)
    minx, maxx = _bounds_x(_flatten(path))
    dx = SIDE_BEARING - minx
    return path, dx, round((maxx - minx) + 2 * SIDE_BEARING)

def shaped(gd, n=12):
    """(shifted point contours, advance) — for the specimen renderer."""
    path, dx, adv = glyph_metrics(gd)
    return [[(x + dx, y) for x, y in c] for c in _flatten(path, n)], adv

def _curve_glyph(path, dx):
    pen = TTGlyphPen(None)
    for verb, pts in path:
        v = int(verb)
        if v == 0:
            pen.moveTo((round(pts[0][0] + dx), round(pts[0][1])))
        elif v == 1:
            pen.lineTo((round(pts[0][0] + dx), round(pts[0][1])))
        elif v == 2:
            pen.qCurveTo((round(pts[0][0] + dx), round(pts[0][1])),
                         (round(pts[1][0] + dx), round(pts[1][1])))
        elif v == 5:
            pen.closePath()
    return pen.glyph()

def _font():
    glyphs, hmtx, cmap, order = {}, {}, {}, []

    def add(name, gd, codepoints=()):
        path, dx, adv = glyph_metrics(gd)
        glyphs[name] = _curve_glyph(path, dx)
        hmtx[name] = (adv, SIDE_BEARING)
        order.append(name)
        for cp in codepoints:
            cmap[cp] = name

    add('.notdef', glyph([ring(300, 360, 180, 300)]))
    glyphs['space'] = TTGlyphPen(None).glyph()
    hmtx['space'] = (SPACE_ADV, 0)
    order.append('space')
    cmap[0x20] = 'space'

    for ch, gd in alphabet().items():
        add('cl_' + ch, gd, (ord(ch), ord(ch.upper())))
    for ch, gd in DIGITS.items():
        add('cl_d' + ch, gd, (ord(ch),))
    for name, gd in PUNCT.items():
        add(name, gd, [cp for cp, n in CMAP_PUNCT.items() if n == name])
    for lg, gd in LIGS.items():
        add('cl_' + lg, gd, ())

    fb = FontBuilder(UPM, isTTF=True)
    fb.setupGlyphOrder(order)
    fb.setupCharacterMap(cmap)
    fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics(hmtx)
    fb.setupHorizontalHeader(ascent=CAP + 60, descent=DESC)
    fb.setupNameTable({'familyName': 'Aetheric', 'styleName': 'Regular',
                       'psName': 'Aetheric-Regular', 'version': '1.1'})
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
