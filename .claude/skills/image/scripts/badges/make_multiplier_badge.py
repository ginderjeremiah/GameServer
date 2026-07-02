#!/usr/bin/env python3
"""One-off generator for badges/multiplier.png (the CriticalChanceMultiplier badge, issue #1465).

Hand-composited with PIL rather than run through the AI generation pipeline (generate.py /
generate-agy.py) because this session has neither GEMINI_API_KEY nor the agy CLI available.
Mirrors amp.png/resist.png's own construction: this project's docs (docs/icon-art.md) note the
crit/dodge family's original `%` badge was likewise "not generated" but programmatically
composited, so a procedurally-drawn glyph is the established alternative to full AI generation
for a small corner badge. Matches amp/resist's measured cream fill (~#FAF3D9 top -> ~#F1DCB1
base), thin dark outline (~#4F4A2A), and vertical top-bright/base-deep shading.
"""
import math

from PIL import Image, ImageDraw

SIZE = 1024
OUT = "multiplier.png"

FILL_TOP = (253, 251, 239)
FILL_BASE = (241, 220, 177)
OUTLINE = (60, 56, 32)


def rounded_x_mask(size, half_len, half_thick, margin):
    """A bold '×' silhouette: two crossed capsules (thick diagonal rounded bars)."""
    mask = Image.new("L", (size, size), 0)
    draw = ImageDraw.Draw(mask)
    cx = cy = size / 2
    d = (size / 2 - margin) * math.sqrt(2) / 2 * (half_len / (size / 2))
    for sign in (1, -1):
        dx, dy = d, d * sign
        draw.line([(cx - dx, cy - dy), (cx + dx, cy + dy)], fill=255, width=round(half_thick * 2))
        for ex, ey in ((cx - dx, cy - dy), (cx + dx, cy + dy)):
            draw.ellipse([ex - half_thick, ey - half_thick, ex + half_thick, ey + half_thick], fill=255)
    return mask


def main():
    outline_mask = rounded_x_mask(SIZE, half_len=330, half_thick=150, margin=60)
    fill_mask = rounded_x_mask(SIZE, half_len=330, half_thick=128, margin=60)

    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    outline_layer = Image.new("RGBA", (SIZE, SIZE), OUTLINE + (255,))
    canvas = Image.composite(outline_layer, canvas, outline_mask)

    gradient = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    gpix = gradient.load()
    for y in range(SIZE):
        t = y / (SIZE - 1)
        r = round(FILL_TOP[0] + (FILL_BASE[0] - FILL_TOP[0]) * t)
        g = round(FILL_TOP[1] + (FILL_BASE[1] - FILL_TOP[1]) * t)
        b = round(FILL_TOP[2] + (FILL_BASE[2] - FILL_TOP[2]) * t)
        for x in range(SIZE):
            gpix[x, y] = (r, g, b, 255)

    fill_layer = Image.composite(gradient, Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0)), fill_mask)
    canvas.alpha_composite(fill_layer)

    canvas.save(OUT)
    print(f"wrote {OUT} {canvas.size}")


if __name__ == "__main__":
    main()
