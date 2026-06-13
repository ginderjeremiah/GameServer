---
name: image
description: >-
  Generate and iteratively refine images using Google's Nano Banana (the Gemini 2.5 Flash Image
  model). Use whenever the user wants to create, generate, draw, or make an image / picture /
  artwork / illustration / icon / concept art / game asset from a text description — e.g.
  "generate an image of a frost dragon", "make me a logo", "create concept art for a lava zone
  boss", "draw an icon for a health potion". Also use to edit or refine an image: applying a
  follow-up change to a previously generated image ("make it darker", "add a moon", "now in
  pixel-art style"), editing a provided image, or composing/blending multiple reference images
  into one. Drives a bundled script that calls the Gemini image API and saves results locally,
  then loops on refinements while preserving each version.
---

# /image — Generate and refine images with Nano Banana

Generate images from a description and refine them through follow-up edits, using Google's
**Nano Banana** model (`gemini-2.5-flash-image`) via the Gemini API. The skill bundles a
zero-dependency Python script that handles text-to-image, image editing, and multi-image
composition; your job is to write strong prompts, manage the output files, show results, and
loop on refinements.

## Prerequisites (check first, before generating)

1. **API key.** The script reads `GEMINI_API_KEY` (or `GOOGLE_API_KEY`) from the environment.
   If neither is set, stop and tell the user to create one at
   https://aistudio.google.com/apikey and export it (`export GEMINI_API_KEY=...`). Don't try to
   work around a missing key. Quick check: `printenv GEMINI_API_KEY GOOGLE_API_KEY`.
2. **Network.** The script calls `generativelanguage.googleapis.com`. In a sandboxed/remote
   session this requires the environment's network policy to allow it; if requests fail with a
   network error, tell the user the egress is blocked rather than retrying blindly.
3. **Python 3** is required (standard library only — no `pip install` needed).

## The script

`scripts/nano_banana.py` (path is relative to this skill's directory). Always invoke it with
`python3` and an absolute path to the script.

```bash
python3 <skill_dir>/scripts/nano_banana.py \
  --prompt "PROMPT OR EDIT INSTRUCTION" \
  --output generated-images/<name>.png \
  [--input PREVIOUS_OR_REFERENCE.png ...] \
  [--aspect-ratio 1:1] \
  [--model gemini-2.5-flash-image] \
  [--dry-run]
```

- **Generate** (text → image): provide `--prompt` and `--output`, no `--input`.
- **Refine / edit** (image → image): pass the image to change via `--input` and describe the
  change in `--prompt`. Always write to a **new** `--output` so earlier versions are kept.
- **Compose / blend**: pass multiple `--input` images and describe how to combine them.
- `--aspect-ratio` is optional (e.g. `1:1`, `16:9`, `9:16`, `4:3`, `3:4`). Omit it for the
  model's default. Useful: `1:1` icons, `16:9` scene/banner art, `9:16` mobile/portrait.
- `--dry-run` prints the request body without calling the API — handy for sanity-checking.

On success the script prints `Saved image: <path>` (and a `Model note:` line if the model
returned text). On failure it prints a clear `error:` line and exits non-zero — relay the
message instead of retrying identically.

## Workflow

1. **Understand the request.** Identify the subject, style, mood, composition, and any
   constraints. Note whether it's a fresh generation, an edit of an existing/provided image, or
   a composition of several. Pick a sensible aspect ratio for the use case.
2. **Choose output paths.** Default to a `generated-images/` directory at the repo root. Use a
   short descriptive slug and a version suffix so history is preserved, e.g.
   `generated-images/frost-dragon-v1.png`, then `-v2`, `-v3` for refinements. Create the
   directory if needed (the script also creates parent dirs).
3. **Write a strong prompt** (see prompting tips below) and run the script.
4. **Show the result.** If a file-sharing/upload tool is available in this environment, use it
   to surface the saved image to the user; otherwise `Read` the output file so it renders
   inline. Always state the saved path.
5. **Offer refinement and loop.** Ask what they'd like to change. For each refinement, run the
   script again with `--input` set to the **latest** saved version and `--output` set to the
   next version. Keep iterating until they're satisfied. If the user wants to branch from an
   earlier version, use that file as `--input`.

## Prompting tips for Nano Banana

- **Describe a scene, don't list keywords.** Nano Banana responds best to descriptive,
  narrative prompts. Prefer "A weathered iron health potion icon, glowing red liquid in a
  rounded glass vial with a cork, centered on a transparent-feeling dark background, crisp
  game-UI style, soft rim light" over "potion, red, icon, game".
- **Specify what matters:** subject, setting, composition/camera angle, lighting, color
  palette, art style/medium, mood, and level of detail.
- **For text in the image,** put the exact words in quotes and keep them short; describe font
  feel and placement.
- **When editing, be surgical.** Say exactly what to change and what to keep: "Change only the
  dragon's scales to deep blue; keep the pose, gold hoard, lighting, and background identical."
  This preserves consistency across versions (a strength of this model).
- **For consistent characters/assets across a set,** feed a reference image via `--input` and
  ask for the same subject in a new pose/scene/style.
- **Iterate in small steps.** One or two changes per refinement gives more predictable results
  than a long list of simultaneous edits.

## Notes & limits

- Generated images include an invisible **SynthID** watermark; the model may also decline
  prompts that hit safety filters (the script surfaces the block reason).
- Inputs must be `image/png`, `image/jpeg`, `image/webp`, `image/heic`, or `image/heif`, and
  the total request stays under ~20 MB — for large reference images, expect to keep them modest.
- Image generation is a **paid** Gemini API feature; each call consumes quota/credits. Don't
  fire off many speculative generations — confirm the prompt with the user when in doubt.

## Examples

Generate a square game icon:

```bash
python3 <skill_dir>/scripts/nano_banana.py \
  --prompt "A glossy game-UI icon of a health potion: a rounded glass vial with a cork stopper, filled with glowing crimson liquid and tiny bubbles, soft top-down rim lighting, subtle inner glow, clean edges, painterly fantasy style, centered composition on a neutral dark backdrop" \
  --aspect-ratio 1:1 \
  --output generated-images/health-potion-v1.png
```

Refine it (keep everything, change one thing):

```bash
python3 <skill_dir>/scripts/nano_banana.py \
  --prompt "Keep the vial, lighting, and composition exactly the same, but change the liquid from crimson to a glowing emerald green and add a faint green mist above the cork." \
  --input generated-images/health-potion-v1.png \
  --output generated-images/health-potion-v2.png
```
