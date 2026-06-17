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
  Generates, edits, and combines via your Antigravity `agy` subscription by default; a
  GEMINI_API_KEY (Google AI Studio key) is only needed for non-square output and the API fallback.
---

# Image generation & refinement (Nano Banana)

This skill turns text into images and refines them through conversation. The whole
point is the loop: generate something, look at it together with the user, and adjust
until it's right. Two bundled backends do the work — see [Backends](#backends).

> **Working in this repo (GameServer)?** Game item/skill icons in `UI/static/img`
> have a *locked* art style and a generate-on-chroma → background-strip pipeline.
> Read [`docs/icon-art.md`](../../../docs/icon-art.md) **first** — it has the style
> recipe, the exact reusable prompt blocks, a per-icon prompt catalog (so you stay
> consistent and don't duplicate existing icons), and the background-removal
> tooling and gotchas. (Project-specific — ignore this note if the skill was copied
> to another project.)

## Backends

Two scripts back this skill; pick per step:

- **`scripts/generate-agy.py` — the default for generating, editing, and combining.**
  Routes through the Antigravity `agy` CLI on your **subscription's image quota (no API
  spend)**. The agy agent runs on **`Claude Sonnet 4.6 (Thinking)`** by default and
  automatically **falls back to `Gemini 3.5 Flash (Low)`** when Sonnet usage is exhausted
  (override with `--model` / `--fallback-model`). Supports `--edit` (refine the latest
  `<name>-v*`) and `--input` (combine reference images, up to 3 total — the tool's cap),
  `--strip` to chroma-key the background in the same step, and `--detached` so it runs
  **without an interactive terminal** (it spawns agy in a hidden console — details in
  [`docs/icon-art.md`](../../../docs/icon-art.md)). Output is always a square 1024×1024
  JPEG; no `--aspect` or `--size`. Its image quota resets ~every 5h.
- **`scripts/generate.py` — for non-square output and as the API fallback.** Use it when
  you need a non-square `--aspect` or a `pro` `--size`, or when agy is unavailable (both
  models out of usage, or quota not yet reset). It also does `--edit` / `--input` if you
  prefer the Gemini image models. Billed to `GEMINI_API_KEY` (see
  [Prerequisites](#prerequisites)).

## Prerequisites

The default `generate-agy.py` backend needs no API key (it uses your `agy`
subscription). The key below is only for `generate.py` — i.e. non-square output,
`pro` sizes, and the API fallback.

`generate.py` needs a Google AI Studio API key in `GEMINI_API_KEY` (or
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

**`generate-agy.py` — agy *agent* model.** Here `--model` picks the LLM that drives
agy's built-in image tool (the image model itself is a fixed NB2-class model, not
selectable). It defaults to `Claude Sonnet 4.6 (Thinking)` and auto-falls back to
`Gemini 3.5 Flash (Low)` when Sonnet usage is out; override with `--model` /
`--fallback-model` (run `agy models` for the current list). Output is always a square
1024×1024 JPEG.

**`generate.py` — Gemini *image* model.** Here `--model` selects the image model
(aliases resolve to the current model ids):

- **`flash`** (default) -> `gemini-2.5-flash-image` (the original Nano Banana).
  Fast and cheap, and the only tier that returns a lossless **PNG** -- great for
  everyday generation and rapid iteration.
- **`nb2`** (a.k.a. `flash2`, `nano-banana-2`) -> `gemini-3.1-flash-image`
  (Nano Banana 2). A generation newer than 2.5 flash with markedly better prompt
  adherence; ~2× flash's cost but still cheap. Returns **JPEG**.
- **`pro`** (a.k.a. `nano-banana-pro`) -> `gemini-3-pro-image` (Nano Banana Pro,
  GA). Best quality and legible in-image text; supports `--size` (1K/2K/4K).
  Slower and pricier. Returns **JPEG**.

A full model id can also be passed through directly. Default to `flash` for
iteration; switch to `nb2` (or `pro`) for a higher-quality final render.

**Output format: only `flash` returns PNG; `nb2` and `pro` return JPEG.** This
matters when you need clean edges — e.g. before chroma-keying to transparency. But
JPEG is *not* disqualifying: a hue key (see below and `docs/icon-art.md`) tolerates
the JPEG ringing as long as the backdrop hue is far from the subject's colours.
See [Transparent backgrounds](#transparent-backgrounds).

## Workflow

### 1. Generate

Pick a short, stable kebab-case `--name` slug from the subject (e.g.
`neon-city-skyline`). Reuse the same slug for every refinement of that image so
versions stay grouped. Generate via the default `generate-agy.py` backend, run from
the user's working directory — add `--detached` so it works from a non-interactive
shell, and `--strip` to chroma-key the background to transparency in the same step:

```bash
python <skill-dir>/scripts/generate-agy.py \
  --prompt "A glowing neon city skyline at night, synthwave palette, reflective wet streets" \
  --name neon-city-skyline --detached
```

Images land in `./generated-images/<name>-v<N>.jpg` (square 1024×1024). The script
prints the saved path.

**Need a non-square aspect, a specific size, or a photographic look?** Use
`generate.py` instead — it takes `--aspect` / `--size` and the Gemini image models
(billed to `GEMINI_API_KEY`):

```bash
python <skill-dir>/scripts/generate.py \
  --prompt "A cozy bookshop interior at golden hour, warm lamplight, plants on every shelf, photorealistic, 35mm" \
  --name cozy-bookshop \
  --aspect 3:2
```

### 2. Show the result

Immediately **Read the saved image file** so it renders for the user — never
just report a path and assume it's good. Describe what you see in a sentence,
then ask what they'd like to change (or confirm it's done).

### 3. Refine

Refinement is editing the latest version with a new instruction. Pass `--edit`
to automatically use the most recent `<name>-v*` file as the base image (stay on the
default agy backend so it's still on your subscription):

```bash
python <skill-dir>/scripts/generate-agy.py \
  --prompt "Change it to nighttime, add string lights in the window, keep everything else the same" \
  --name neon-city-skyline --edit --detached
```

This writes `neon-city-skyline-v2.jpg` (v1 is preserved). Read the new version, show
it, and repeat. Keep editing the same series rather than regenerating from scratch.
Use `generate.py --edit` instead only when you need a non-square result or agy is out
of usage; the `--edit` input is just the latest `<name>-v*` on disk, so either backend
can pick up a series the other started.

### 4. Composite reference images (optional)

To merge or place real images into a scene, pass them with repeatable `--input`
flags instead of `--edit` (agy combines up to **3** images total — its tool's cap):

```bash
python <skill-dir>/scripts/generate-agy.py \
  --prompt "Place this product on a marble kitchen counter with soft morning light" \
  --name product-shot --input ./product.png --detached
```

`--edit` and `--input` can combine (the edited base counts toward the 3-image limit).
On `generate.py` the same flags work without the 3-image cap.

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
2. Prefer **`--model flash`** for a lossless PNG, but `nb2`/`pro` JPEG output also
   keys cleanly via a hue key (the backdrop hue is far from the subject's colours).
3. **Key the colour out** to transparency. In this repo, `scripts/strip-bg.py`
   (bundled with this skill, alongside `generate.py`) does this — a hue key (so it
   doesn't eat same-ish-hued subject colours) with
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
