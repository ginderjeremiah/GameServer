#!/usr/bin/env python3
"""Generate and refine images with Google's Nano Banana (Gemini image models).

Standard library only -- no pip install required. Reads the API key from
GEMINI_API_KEY (falling back to GOOGLE_API_KEY).

Generate:   python generate.py --prompt "a red fox in snow" --name snow-fox
Refine:     python generate.py --prompt "make it night, add aurora" --name snow-fox --edit
Composite:  python generate.py --prompt "put the logo on the mug" --name mug \
                --input logo.png --input mug.png

Output is written to <dir>/<name>-v<N>.<ext>, auto-incrementing the version so
every refinement is kept (snow-fox-v1.png, snow-fox-v2.png, ...).
"""
import argparse
import base64
import json
import mimetypes
import os
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path

API_BASE = "https://generativelanguage.googleapis.com/v1beta/models"

# Friendly aliases -> real model ids. Any other value is passed through as-is.
MODEL_ALIASES = {
    "pro": "gemini-3-pro-image-preview",            # Nano Banana Pro
    "nano-banana-pro": "gemini-3-pro-image-preview",
    "flash": "gemini-2.5-flash-image",              # Nano Banana
    "nano-banana": "gemini-2.5-flash-image",
}
DEFAULT_MODEL = "flash"


def api_key():
    key = os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY")
    if not key:
        sys.exit(
            "ERROR: No API key. Set GEMINI_API_KEY (or GOOGLE_API_KEY) to a "
            "Google AI Studio key.\nGet one at https://aistudio.google.com/apikey"
        )
    return key


def next_version(out_dir, name):
    """Return (next_version_int, latest_existing_path|None) for <name>-v<N>.<ext> files."""
    pat = re.compile(rf"^{re.escape(name)}-v(\d+)\.(png|jpg|jpeg|webp)$", re.IGNORECASE)
    latest_n, latest_path = 0, None
    if out_dir.exists():
        for f in out_dir.iterdir():
            m = pat.match(f.name)
            if m and int(m.group(1)) > latest_n:
                latest_n, latest_path = int(m.group(1)), f
    return latest_n + 1, latest_path


def load_image_part(path):
    data = Path(path).read_bytes()
    mime = mimetypes.guess_type(path)[0] or "image/png"
    if len(data) > 18 * 1024 * 1024:
        print(
            f"WARNING: {path} is {len(data) // (1024 * 1024)}MB; total inline "
            "request must stay under ~20MB.",
            file=sys.stderr,
        )
    return {"inline_data": {"mime_type": mime, "data": base64.b64encode(data).decode()}}


def build_body(prompt, image_paths, aspect, size):
    parts = [{"text": prompt}] + [load_image_part(p) for p in image_paths]
    gen_config = {"responseModalities": ["TEXT", "IMAGE"]}
    # Only include imageConfig when explicitly requested -- the core generate
    # path then never depends on a field whose name has shifted across API
    # versions, and the API uses its own sensible default size/ratio.
    image_cfg = {}
    if aspect:
        image_cfg["aspectRatio"] = aspect
    if size:
        image_cfg["imageSize"] = size
    if image_cfg:
        gen_config["imageConfig"] = image_cfg
    return {"contents": [{"parts": parts}], "generationConfig": gen_config}


def call_api(model, body, key):
    req = urllib.request.Request(
        f"{API_BASE}/{model}:generateContent",
        data=json.dumps(body).encode(),
        headers={"x-goog-api-key": key, "Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=300) as resp:
            return json.loads(resp.read())
    except urllib.error.HTTPError as e:
        # Surface the raw error body verbatim so the caller can adapt (bad
        # model id, unsupported size, quota, schema drift, etc.).
        sys.exit(f"ERROR: Gemini API returned HTTP {e.code}:\n{e.read().decode(errors='replace')}")
    except urllib.error.URLError as e:
        sys.exit(f"ERROR: network failure calling Gemini API: {e.reason}")


def extract(resp):
    """Return (image_bytes|None, mime, text) from the first candidate."""
    candidates = resp.get("candidates")
    if not candidates:
        block = resp.get("promptFeedback", {}).get("blockReason")
        hint = f" (prompt blocked: {block})" if block else ""
        sys.exit(f"ERROR: API returned no candidates{hint}.\n{json.dumps(resp, indent=2)[:2000]}")
    parts = candidates[0].get("content", {}).get("parts")
    if not parts:
        reason = candidates[0].get("finishReason", "unknown")
        sys.exit(f"ERROR: no content parts (finishReason={reason}).\n{json.dumps(resp, indent=2)[:2000]}")
    img, mime, texts = None, "image/png", []
    for part in parts:
        inline = part.get("inline_data") or part.get("inlineData")
        if inline and inline.get("data"):
            img = base64.b64decode(inline["data"])
            mime = inline.get("mime_type") or inline.get("mimeType") or mime
        elif "text" in part:
            texts.append(part["text"])
    return img, mime, "\n".join(texts).strip()


def ext_for(mime):
    return {"image/png": "png", "image/jpeg": "jpg", "image/webp": "webp"}.get(mime, "png")


def main():
    ap = argparse.ArgumentParser(description="Generate or refine an image with Nano Banana (Gemini).")
    ap.add_argument("--prompt", required=True, help="Generation prompt or, with --edit, the edit instruction.")
    ap.add_argument("--name", required=True, help="Stable kebab-case slug for this image series, e.g. snow-fox.")
    ap.add_argument("--dir", default="generated-images", help="Output directory (default: generated-images).")
    ap.add_argument("--model", default=DEFAULT_MODEL, help="flash | pro | full model id (default: flash).")
    ap.add_argument("--edit", action="store_true", help="Refine the latest existing version of --name.")
    ap.add_argument("--input", action="append", default=[], help="Explicit input image path(s); repeatable.")
    ap.add_argument("--aspect", help="Aspect ratio, e.g. 1:1, 16:9, 9:16, 4:3, 3:2, 21:9.")
    ap.add_argument("--size", help="Image size: 1K, 2K, or 4K (Pro model only).")
    args = ap.parse_args()

    key = api_key()
    model = MODEL_ALIASES.get(args.model, args.model)
    out_dir = Path(args.dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    version, latest = next_version(out_dir, args.name)

    inputs = list(args.input)
    if args.edit and not inputs:
        if not latest:
            sys.exit(f"ERROR: --edit set but no '{args.name}-v*' image exists in {out_dir}.")
        inputs = [str(latest)]

    resp = call_api(model, build_body(args.prompt, inputs, args.aspect, args.size), key)
    img, mime, text = extract(resp)
    if img is None:
        sys.exit(f"ERROR: API returned no image. Model said: {text or '(nothing)'}")

    out_path = out_dir / f"{args.name}-v{version}.{ext_for(mime)}"
    out_path.write_bytes(img)

    print(f"SAVED: {out_path}")
    if inputs:
        print(f"EDITED_FROM: {', '.join(inputs)}")
    print(f"MODEL: {model}")
    if text:
        print(f"MODEL_NOTE: {text}")


if __name__ == "__main__":
    main()
