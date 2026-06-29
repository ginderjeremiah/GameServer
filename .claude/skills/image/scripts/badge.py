#!/usr/bin/env python3
"""Composite an amplification / resistance badge onto a stripped attribute base icon.

The damage-type amp/resist family (#1320/#1340) shares ONE generated base symbol
per type; the amplification vs resistance variant is set by a small badge
composited into the lower-right corner (the crit/dodge "%" badge pattern in
docs/icon-art.md). The badge art itself is generated in the same soft-cel house
style and lives under `badges/` next to this script, so every `...Amplification`
icon is its base + the `amp` badge and every `...Resistance` icon is the base +
the `resist` badge - keeping the siblings identical apart from the badge, and
keeping the badge on-style (a real soft-cel pip, not a flat vector overlay).

  amp    -> badges/amp.png    (an upward arrow: "you deal MORE of this damage")
  resist -> badges/resist.png (a shield:        "you take LESS of this damage")

Usage:
  python badge.py <base.png> <out.png> --badge amp|resist
  python badge.py base.png out.png --badge resist --scale 0.42 --margin 0.02
"""
import argparse
import os

from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
BADGE_DIR = os.path.join(HERE, "badges")


def _autocrop(im):
    """Crop a transparent-background RGBA image to its opaque content."""
    bbox = im.split()[-1].getbbox()
    return im.crop(bbox) if bbox else im


def composite(base_path, out_path, badge, scale, margin, badge_dir):
    base = Image.open(base_path).convert("RGBA")
    w, _ = base.size
    art = _autocrop(Image.open(os.path.join(badge_dir, badge + ".png")).convert("RGBA"))

    # Scale the badge so its larger side is `scale` of the base width.
    box = round(scale * w)
    aw, ah = art.size
    f = box / max(aw, ah)
    art = art.resize((max(1, round(aw * f)), max(1, round(ah * f))), Image.LANCZOS)
    bw, bh = art.size

    inset = round(margin * w)
    x, y = base.size[0] - inset - bw, base.size[1] - inset - bh

    # Soft drop shadow so the badge separates from a busy base.
    off = round(0.012 * w)
    shadow = Image.new("RGBA", base.size, (0, 0, 0, 0))
    pip = Image.new("RGBA", art.size, (9, 10, 13, 255))
    pip.putalpha(art.split()[-1].point(lambda a: int(a * 0.5)))
    shadow.alpha_composite(pip, (x + off, y + off))
    shadow = shadow.filter(ImageFilter.GaussianBlur(off))

    out = Image.alpha_composite(base, shadow)
    out.alpha_composite(art, (x, y))
    out.save(out_path)
    print(f"{out_path}  ({badge} badge, scale={scale}, margin={margin})")


def main():
    ap = argparse.ArgumentParser(description="Composite an amp/resist badge onto a base icon.")
    ap.add_argument("base")
    ap.add_argument("out")
    ap.add_argument("--badge", required=True, help="Badge name (amp / resist), resolved under --badge-dir.")
    ap.add_argument("--scale", type=float, default=0.42, help="Badge size as a fraction of icon width.")
    ap.add_argument("--margin", type=float, default=0.02, help="Inset from the lower-right corner, fraction of width.")
    ap.add_argument("--badge-dir", default=BADGE_DIR, help="Directory holding <badge>.png art.")
    args = ap.parse_args()
    composite(args.base, args.out, args.badge, args.scale, args.margin, args.badge_dir)


if __name__ == "__main__":
    main()
