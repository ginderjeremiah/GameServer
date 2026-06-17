#!/usr/bin/env python3
"""Remove a solid chroma background from a generated icon.

Designed for the lime-green (or magenta) backdrops we generate icons on. Keying
is by HUE in HSV: every pixel whose hue matches the background's, with enough
saturation and brightness, is cleared. Because the chroma colour is chosen to
appear *nowhere* in the subject, a global hue key is both simplest and most
complete -- it clears the solid backdrop, the anti-aliased fringe along the
outline (darker shades of the same hue), AND background trapped inside concave
shapes (e.g. the gap between a bow and its string), while leaving the subject's
dark outline (low saturation/value) and any differently-hued interior (a gold
pommel, a cyan gem, blue-grey steel) untouched.

The background hue is auto-detected as the dominant hue among the bright,
saturated pixels (robust even when the model paints the chroma as a rounded
panel with dark corners). Pass --bg-hue to force it.

For the rare subject that genuinely contains the chroma hue (a green slime on a
green screen), generate it on a contrasting backdrop instead (e.g. magenta) so
the global key stays unambiguous, or use --connected to only clear background
reachable from the image border.

Usage:
  python strip-bg.py INPUT OUTPUT
  python strip-bg.py INPUT OUTPUT --bg-hue 90 --hue-tol 32 --min-sat 0.18 --min-val 0.12
  python strip-bg.py INPUT OUTPUT --connected            # edge-flood mode
"""
import argparse
import colorsys
from collections import deque

from PIL import Image


def hsv(p):
    return colorsys.rgb_to_hsv(p[0] / 255, p[1] / 255, p[2] / 255)


def hue_dist(a, b):
    d = abs(a - b)
    return min(d, 1 - d)


def detect_bg_hue(img):
    """Dominant hue among bright, saturated pixels -> the chroma backdrop."""
    px = img.load()
    w, h = img.size
    bins = [0] * 360
    step = max(1, (w * h) // 200_000)  # subsample large images for speed
    i = 0
    for y in range(h):
        for x in range(w):
            i += 1
            if i % step:
                continue
            hh, ss, vv = hsv(px[x, y])
            if ss > 0.6 and vv > 0.5:
                bins[int(hh * 359)] += 1
    if not any(bins):
        # fallback: most-saturated corner
        return max((hsv(px[c]) for c in [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]),
                   key=lambda t: t[1])[0]
    return bins.index(max(bins)) / 359.0


def trim_corner_blobs(px, w, h):
    """Clear any opaque blob still touching an image corner.

    The model sometimes paints the chroma as a rounded panel, leaving dark
    wedges in the true image corners that the hue key can't catch. After the key
    has cleared the (lime/magenta) panel to transparency, those wedges are
    isolated opaque islands in the corners, walled off from a centered subject by
    transparent pixels -- so a flood over alpha>0 pixels from each corner removes
    exactly the wedges and stops at the transparent moat. Do NOT use on subjects
    that fill the frame to the corners.
    """
    from collections import deque
    visited = bytearray(w * h)
    dq = deque()
    cleared = 0
    for cx, cy in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        i = cy * w + cx
        if not visited[i] and px[cx, cy][3] > 0:
            visited[i] = 1
            dq.append((cx, cy))
    while dq:
        x, y = dq.popleft()
        p = px[x, y]
        if p[3] > 0:
            px[x, y] = (p[0], p[1], p[2], 0)
            cleared += 1
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h:
                j = ny * w + nx
                if not visited[j] and px[nx, ny][3] > 0:
                    visited[j] = 1
                    dq.append((nx, ny))
    return cleared


def fill_enclosed_holes(px, w, h):
    """Re-opaque transparent pixels NOT connected to the image border.

    The hue key can punch a tiny hole where a subject pixel happened to match the
    backdrop hue (e.g. a yellow-green flame highlight close to lime). RGB is
    preserved when alpha is zeroed, so restoring alpha recovers the original
    colour. Do NOT use on subjects with legitimate interior transparency (the gap
    inside a bow, a ring, etc.).
    """
    from collections import deque
    visited = bytearray(w * h)
    dq = deque()

    def seed(x, y):
        i = y * w + x
        if not visited[i] and px[x, y][3] == 0:
            visited[i] = 1
            dq.append((x, y))

    for x in range(w):
        seed(x, 0)
        seed(x, h - 1)
    for y in range(h):
        seed(0, y)
        seed(w - 1, y)
    while dq:
        x, y = dq.popleft()
        for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h:
                j = ny * w + nx
                if not visited[j] and px[nx, ny][3] == 0:
                    visited[j] = 1
                    dq.append((nx, ny))
    restored = 0
    for y in range(h):
        for x in range(w):
            if px[x, y][3] == 0 and not visited[y * w + x]:
                p = px[x, y]
                px[x, y] = (p[0], p[1], p[2], 255)
                restored += 1
    return restored


def remove_bg(inp, outp, bg_hue=None, hue_tol_deg=32.0, min_sat=0.18, min_val=0.12,
              connected=False, trim_corners=False, fill_holes=False):
    img = Image.open(inp).convert("RGBA")
    px = img.load()
    w, h = img.size
    bh = bg_hue if bg_hue is not None else detect_bg_hue(img)
    tol = hue_tol_deg / 360.0

    def is_bg(p):
        hh, ss, vv = hsv(p)
        return ss >= min_sat and vv >= min_val and hue_dist(hh, bh) <= tol

    cleared = 0
    if connected:
        visited = bytearray(w * h)
        dq = deque()

        def visit(x, y):
            nonlocal cleared
            idx = y * w + x
            if visited[idx]:
                return
            visited[idx] = 1
            p = px[x, y]
            if is_bg(p):
                px[x, y] = (p[0], p[1], p[2], 0)
                cleared += 1
                dq.append((x, y))

        for x in range(w):
            visit(x, 0)
            visit(x, h - 1)
        for y in range(h):
            visit(0, y)
            visit(w - 1, y)
        while dq:
            x, y = dq.popleft()
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < w and 0 <= ny < h:
                    visit(nx, ny)
    else:
        for y in range(h):
            for x in range(w):
                p = px[x, y]
                if is_bg(p):
                    px[x, y] = (p[0], p[1], p[2], 0)
                    cleared += 1

    if fill_holes:
        fill_enclosed_holes(px, w, h)
    if trim_corners:
        cleared += trim_corner_blobs(px, w, h)

    img.save(outp)
    print(f"Saved {outp}  (bg hue {bh * 360:.0f} deg, {cleared}/{w * h} px cleared, {100 * cleared // (w * h)}%)")


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description="Hue-key chroma background removal.")
    ap.add_argument("input")
    ap.add_argument("output")
    ap.add_argument("--bg-hue", type=float, default=None, help="Background hue in degrees (auto-detected if omitted).")
    ap.add_argument("--hue-tol", type=float, default=32.0, help="Hue match tolerance in degrees.")
    ap.add_argument("--min-sat", type=float, default=0.18, help="Min saturation to count as background.")
    ap.add_argument("--min-val", type=float, default=0.12, help="Min value to count as background.")
    ap.add_argument("--connected", action="store_true", help="Only clear background connected to the image border.")
    ap.add_argument("--trim-corners", action="store_true", help="Clear leftover opaque blobs touching the image corners (rounded-panel wedges). Centered subjects only.")
    ap.add_argument("--fill-holes", action="store_true", help="Re-opaque enclosed transparent pixels (refill stray hue-key holes). NOT for subjects with legitimate interior transparency.")
    args = ap.parse_args()
    bg_hue = args.bg_hue / 360.0 if args.bg_hue is not None else None
    remove_bg(args.input, args.output, bg_hue, args.hue_tol, args.min_sat, args.min_val, args.connected, args.trim_corners, args.fill_holes)
