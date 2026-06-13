#!/usr/bin/env python3
"""Generate and edit images with Google's Nano Banana (Gemini 2.5 Flash Image).

Uses only the Python standard library so the skill works without any
`pip install`. Reads the API key from an environment variable (default
GEMINI_API_KEY, falling back to GOOGLE_API_KEY).

Text-to-image:
    nano_banana.py --prompt "a red dragon coiled on gold" --output out.png

Edit / refine an existing image (pass it as input, describe the change):
    nano_banana.py --prompt "make the dragon blue, keep everything else" \
        --input out.png --output out-v2.png

Compose / blend multiple references:
    nano_banana.py --prompt "place this character in this scene" \
        --input character.png scene.png --output composed.png
"""

import argparse
import base64
import json
import mimetypes
import os
import sys
import urllib.error
import urllib.request

DEFAULT_MODEL = "gemini-2.5-flash-image"
API_BASE = "https://generativelanguage.googleapis.com/v1beta/models"
# Formats Nano Banana accepts as inline input.
SUPPORTED_INPUT_MIME = {
    "image/png",
    "image/jpeg",
    "image/webp",
    "image/heic",
    "image/heif",
}
EXT_BY_MIME = {
    "image/png": ".png",
    "image/jpeg": ".jpg",
    "image/webp": ".webp",
    "image/heic": ".heic",
    "image/heif": ".heif",
}


def die(message):
    """Print an error to stderr and exit non-zero."""
    print(f"error: {message}", file=sys.stderr)
    sys.exit(1)


def guess_mime(path):
    """Best-effort image MIME type for an input file."""
    mime, _ = mimetypes.guess_type(path)
    if mime is None:
        # Default to PNG; the API rejects anything it cannot decode.
        return "image/png"
    return mime


def build_parts(prompt, input_paths):
    """Assemble the request `parts`: the text prompt plus any input images."""
    parts = [{"text": prompt}]
    for path in input_paths:
        if not os.path.isfile(path):
            die(f"input image not found: {path}")
        mime = guess_mime(path)
        if mime not in SUPPORTED_INPUT_MIME:
            die(f"unsupported input image type {mime} for {path} "
                f"(allowed: {', '.join(sorted(SUPPORTED_INPUT_MIME))})")
        with open(path, "rb") as handle:
            encoded = base64.b64encode(handle.read()).decode("ascii")
        parts.append({"inlineData": {"mimeType": mime, "data": encoded}})
    return parts


def build_body(prompt, input_paths, aspect_ratio):
    """Build the full generateContent request body."""
    body = {"contents": [{"parts": build_parts(prompt, input_paths)}]}
    if aspect_ratio:
        body["generationConfig"] = {"imageConfig": {"aspectRatio": aspect_ratio}}
    return body


def call_api(model, api_key, body):
    """POST to generateContent and return the decoded JSON response."""
    url = f"{API_BASE}/{model}:generateContent"
    request = urllib.request.Request(
        url,
        data=json.dumps(body).encode("utf-8"),
        headers={
            "Content-Type": "application/json",
            "x-goog-api-key": api_key,
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=180) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        detail = error.read().decode("utf-8", "replace")
        die(f"API request failed ({error.code} {error.reason}): {detail}")
    except urllib.error.URLError as error:
        die(f"could not reach the Gemini API: {error.reason}. "
            f"Check network access to generativelanguage.googleapis.com.")


def extract(response):
    """Pull image bytes and any text out of the response."""
    feedback = response.get("promptFeedback", {})
    if feedback.get("blockReason"):
        die(f"prompt blocked by safety filters: {feedback['blockReason']}")

    candidates = response.get("candidates", [])
    if not candidates:
        die(f"no candidates returned: {json.dumps(response)[:500]}")

    candidate = candidates[0]
    images, texts = [], []
    for part in candidate.get("content", {}).get("parts", []):
        inline = part.get("inlineData") or part.get("inline_data")
        if inline and inline.get("data"):
            mime = inline.get("mimeType") or inline.get("mime_type") or "image/png"
            images.append((mime, base64.b64decode(inline["data"])))
        elif part.get("text"):
            texts.append(part["text"])

    if not images:
        reason = candidate.get("finishReason", "unknown")
        note = " ".join(texts).strip()
        die(f"no image in response (finishReason={reason})."
            + (f" Model said: {note}" if note else ""))
    return images, " ".join(texts).strip()


def output_path_for(base_output, index, mime):
    """Path for the Nth returned image (rare: usually one image)."""
    if index == 0:
        return base_output
    root, ext = os.path.splitext(base_output)
    if not ext:
        ext = EXT_BY_MIME.get(mime, ".png")
    return f"{root}-{index + 1}{ext}"


def main():
    parser = argparse.ArgumentParser(
        description="Generate or edit images with Nano Banana (Gemini 2.5 Flash Image).")
    parser.add_argument("--prompt", required=True, help="Text prompt / edit instruction.")
    parser.add_argument("--output", required=True, help="Where to write the generated image.")
    parser.add_argument("--input", nargs="*", default=[],
                        help="Input image(s) to edit, refine, or compose from.")
    parser.add_argument("--model", default=DEFAULT_MODEL, help=f"Model id (default: {DEFAULT_MODEL}).")
    parser.add_argument("--aspect-ratio", default=None,
                        help="Optional aspect ratio, e.g. 1:1, 16:9, 9:16, 4:3, 3:4.")
    parser.add_argument("--api-key-env", default=None,
                        help="Env var holding the API key (default: GEMINI_API_KEY then GOOGLE_API_KEY).")
    parser.add_argument("--dry-run", action="store_true",
                        help="Print the request body (image data truncated) without calling the API.")
    args = parser.parse_args()

    body = build_body(args.prompt, args.input, args.aspect_ratio)

    if args.dry_run:
        preview = json.loads(json.dumps(body))
        for part in preview["contents"][0]["parts"]:
            inline = part.get("inlineData")
            if inline:
                inline["data"] = f"<{len(inline['data'])} base64 chars omitted>"
        print(json.dumps({"model": args.model, "body": preview}, indent=2))
        return

    if args.api_key_env:
        api_key = os.environ.get(args.api_key_env)
        key_source = args.api_key_env
    else:
        api_key = os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY")
        key_source = "GEMINI_API_KEY / GOOGLE_API_KEY"
    if not api_key:
        die(f"no API key found in {key_source}. "
            f"Get a key at https://aistudio.google.com/apikey and export it, e.g. "
            f"export GEMINI_API_KEY=...")

    response = call_api(args.model, api_key, body)
    images, text = extract(response)

    out_dir = os.path.dirname(os.path.abspath(args.output))
    os.makedirs(out_dir, exist_ok=True)

    saved = []
    for index, (mime, data) in enumerate(images):
        path = output_path_for(args.output, index, mime)
        with open(path, "wb") as handle:
            handle.write(data)
        saved.append(path)

    for path in saved:
        print(f"Saved image: {path}")
    if text:
        print(f"Model note: {text}")


if __name__ == "__main__":
    main()
