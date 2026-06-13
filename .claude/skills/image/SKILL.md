---
name: image
description: >-
  Generate images from a text description and iteratively refine them using
  Google's Nano Banana models (Gemini 3 Pro Image and Gemini 2.5 Flash Image).
  Use this whenever the user wants to create, generate, make, or design an
  image, illustration, picture, artwork, concept art, logo, icon, sticker,
  poster, banner, avatar, wallpaper, or any visual from a description -- or to
  edit, refine, tweak, restyle, recolor, or iterate on an image this skill
  previously produced (e.g. "make it more colorful", "change the background to
  night", "add a hat", "remove the text"). Also use to composite or combine
  reference images into a new scene. Triggered directly as /image <description>.
  Requires a GEMINI_API_KEY (Google AI Studio key) in the environment.
---

# Image generation & refinement (Nano Banana)

This skill turns text into images and refines them through conversation, using
Google's Gemini image models via a bundled standard-library Python script
(`scripts/generate.py`). The whole point is the loop: generate something, look
at it together with the user, and adjust until it's right.

> **Working in this repo (GameServer)?** Game item/skill icons in `UI/static/img`
> have a *locked* art style and a generate-on-chroma → background-strip pipeline.
> Read [`docs/icon-art.md`](../../../docs/icon-art.md) **first** — it has the style
> recipe, the exact reusable prompt blocks, a per-icon prompt catalog (so you stay
> consistent and don't duplicate existing icons), and the background-removal
> tooling and gotchas. (Project-specific — ignore this note if the skill was copied
> to another project.)

## Prerequisites

The script needs a Google AI Studio API key in `GEMINI_API_KEY` (or
`GOOGLE_API_KEY`). If a run fails with a "No API key" error, tell the user to
get a free key at https://aistudio.google.com/apikey and set it:

```powershell
# PowerShell (this session only)
$env:GEMINI_API_KEY = "your-key"
# Persist across sessions:
setx GEMINI_API_KEY "your-key"
```

Don't proceed with calls until a key is set -- every call hits a paid/quota'd
API, so failing fast is kinder than retrying blindly.

On Windows, a key set with `setx` (or via the System env UI) lives at the **User**
scope and is **not** inherited by an already-open shell/tool session — so
`$env:GEMINI_API_KEY` can read empty even though the key is set. Before asking the
user to re-set it, pull it inline for the call:

```powershell
$env:GEMINI_API_KEY = [System.Environment]::GetEnvironmentVariable('GEMINI_API_KEY','User')
```

## Models

The user chose to support both, selectable per call via `--model`:

- **`flash`** (default) -> `gemini-2.5-flash-image` (the original Nano Banana).
  Fast and cheap -- great for everyday generation and rapid iteration.
- **`pro`** -> `gemini-3-pro-image-preview` (Nano Banana Pro). Best quality,
  prompt adherence, and legible in-image text; supports `--size` (1K/2K/4K).
  Slower and pricier -- reach for it via `--model pro` for a final polish.

A full model id can also be passed through directly. Stick with the default
`flash` unless the user wants top quality or large sizes, then use `--model pro`.

Output format differs: **`flash` returns a lossless PNG, `pro` returns a JPEG.**
This matters whenever you need clean, crisp edges — e.g. before chroma-keying a
background out to transparency (JPEG's edge artifacts make keying messy), prefer
`flash`. See [Transparent backgrounds](#transparent-backgrounds).

## Workflow

### 1. Generate

Pick a short, stable kebab-case `--name` slug from the subject (e.g.
`neon-city-skyline`). Reuse the same slug for every refinement of that image so
versions stay grouped. Run from the user's working directory:

```bash
python <skill-dir>/scripts/generate.py \
  --prompt "A cozy bookshop interior at golden hour, warm lamplight, plants on every shelf, photorealistic, 35mm" \
  --name cozy-bookshop \
  --aspect 3:2
```

Images land in `./generated-images/<name>-v<N>.png`. The script prints the saved
path.

### 2. Show the result

Immediately **Read the saved image file** so it renders for the user — never
just report a path and assume it's good. Describe what you see in a sentence,
then ask what they'd like to change (or confirm it's done).

### 3. Refine

Refinement is editing the latest version with a new instruction. Pass `--edit`
to automatically use the most recent `<name>-v*` file as the input image:

```bash
python <skill-dir>/scripts/generate.py \
  --prompt "Change it to nighttime, add string lights in the window, keep everything else the same" \
  --name cozy-bookshop --edit
```

This writes `cozy-bookshop-v2.png` (the original v1 is preserved). Read the new
version, show it, and repeat. `pro` handles multi-turn edits especially well, so
keep editing the same series rather than regenerating from scratch.

### 4. Composite reference images (optional)

To merge or place real images into a scene, pass them with repeatable `--input`
flags instead of `--edit`:

```bash
python <skill-dir>/scripts/generate.py \
  --prompt "Place this product on a marble kitchen counter with soft morning light" \
  --name product-shot --input ./product.png
```

## Prompting tips for Nano Banana

These models reward rich, specific description over keyword soup.

- **Describe a scene, not tags.** Subject, setting, composition, lighting, mood,
  color palette, and art style. For a photographic look, name a lens/camera
  feel ("85mm portrait, shallow depth of field"); for art, name the medium
  ("watercolor", "flat vector", "3D claymation").
- **Editing: be precise about the change and what to keep.** "Make the jacket
  red and keep the pose, face, and background unchanged" beats "make it red".
  Small, single-purpose edits per step are more reliable than many at once.
- **Text in images works well, especially on `pro`.** Quote the exact text to
  render: `a poster that reads "GRAND OPENING" in bold serif`.
- **Iterate.** First result rarely nails it. Adjust one thing, view, repeat —
  that loop is the strength here, so lean on it instead of over-engineering one
  giant prompt.

## Transparent backgrounds

These models **cannot output a real alpha channel.** Ask for a "transparent
background" and the model paints a fake transparency *checkerboard* into the image
(and often returns a JPEG, which can't hold alpha at all). To get a transparent
PNG, **chroma-key** it instead:

1. Generate on a **flat solid background colour the subject doesn't contain** —
   lime green `#7CFC00` works for most subjects; use magenta `#FF00FF` for green
   ones. Explicitly tell the model to **fill the whole frame edge-to-edge** — it
   otherwise tends to frame the subject in a rounded panel, trapping un-keyable
   colour in the corners.
2. Use **`--model flash`** so the output is a lossless PNG with clean edges.
3. **Key the colour out** to transparency. In this repo, `image-editing/strip-bg.py`
   does this — a hue key (so it doesn't eat same-ish-hued subject colours) with
   `--trim-corners` and `--fill-holes` cleanup for the panel/hole artifacts above.
   See [`docs/icon-art.md`](../../../docs/icon-art.md) for the full pipeline.

## Aspect ratios & sizes

- `--aspect`: `1:1`, `2:3`, `3:2`, `3:4`, `4:3`, `4:5`, `5:4`, `9:16`, `16:9`,
  `21:9` (and more). Default if omitted is the model's own (square-ish).
- `--size`: `1K`, `2K`, `4K` — **`pro` only**. Passing it to `flash` will return
  an API error (the script prints it verbatim).

## Asking vs. acting

If the request is descriptive enough, just generate — refinement is the safety
net, so don't stall with questions. If it's truly vague ("make me an image"),
ask one quick question about subject and style, then go.

## Troubleshooting

The script exits non-zero and prints the raw API error on failure. Common cases:

- **No API key** — guide the user through the setup above.
- **HTTP 400 about a field / `--size`** — drop `--size` (or switch to `pro`); the
  raw message names the offending field.
- **HTTP 429 with `limit: 0`** — not a transient rate-limit: this project has no
  image-generation quota. Gemini's image models generally aren't on the free
  tier, so enable billing on the key's Google Cloud project (or use a
  billing-enabled key). Retrying won't help — the limit is literally zero, even
  though the error still includes a `retryDelay`.
- **HTTP 429 with a non-zero limit and a `retryDelay`** — a genuine per-minute or
  per-day rate limit; wait the suggested delay and retry.
- **No candidates / blocked** — the prompt tripped a safety filter; rephrase.
- **Output is a JPEG, or has a painted checkerboard "transparent" background** —
  expected: the models can't emit real alpha. See
  [Transparent backgrounds](#transparent-backgrounds) for the chroma-key approach.

This skill lives in this repo at `.claude/skills/image/`, so `/image` is
available when working in this project. Copy it to `~/.claude/skills/image/` if
you later want it available across all projects.
