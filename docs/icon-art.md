# Icon Art & Image Generation

How the game's item/skill icons are created, the locked visual style, the
generation pipeline (with its gotchas), and a catalog of every icon that has
been generated so far (with the prompt used). Read this before generating new
icons so the set stays consistent and you can see how existing art was made —
to match it, or to deliberately make something distinct.

## Where icons live

- Files: `UI/static/img/<Display Name>.png` — e.g. `Beginner Sword.png`.
- Format: **1024×1024 PNG, RGBA (transparent background)**.
- Referenced by `iconPath` on items/skills (see the API contracts). The filename
  is the contract, so **regenerate in place under the exact same filename** —
  do not rename.
- Rendered on a **dark surface** (the UI page is `#0f1014`, skill slots are a
  near-dark tile). Always preview new icons composited on that dark background,
  not on white — green fringe and low-contrast fills only show up there.

## House style (locked 2026-06-13)

Chosen by exploring four directions on a single sword (bold "sticker", soft cel,
pastel, minimal) and picking **soft cel + cool UI-matched palette**:

- **Soft, clean cartoon cel-shading.** Gentle shading, softly rounded forms.
- **Thin, subtle dark outline** — *not* a thick "sticker" outline.
- **Cool, interface-matched palette:** cool blue-grey for steel/iron; muted soft
  gold-bronze for brass/bronze; muted, desaturated warm browns for leather/wood.
  Subject-defined colours still win where they're part of the identity (the
  Staff's cyan crystal, fire = warm reds/oranges, etc.).
- **Flat fills, simple shading.** No gradients, no photo texture, no noise.
- **No baked-in glow or rim light.** The UI applies its own `hover-glow` in CSS;
  a painted glow also breaks background stripping (see gotchas).
- **Reads at small sizes.** Icons render as small as a 46px skill slot today
  (slot size may grow). Keep silhouettes clear and low-detail.

## Pipeline

Two steps: generate on a solid chroma background, then key it out to transparency.
Gemini cannot emit a real alpha channel (see gotchas), so we cannot skip the strip.

Tooling:
- `/image` skill → `.claude/skills/image/scripts/generate.py` (Gemini wrapper).
- `strip-bg.py` — chroma background remover. Lives in the local **`image-editing`**
  project (a sibling repo, an additional working directory), alongside `input/`,
  `output/`, and the older `edit-image.py`.

### 1. Generate (on a lime-green backdrop)

```powershell
# The key is only set at the Windows User env scope, not in shell sessions:
$env:GEMINI_API_KEY = [System.Environment]::GetEnvironmentVariable('GEMINI_API_KEY','User')

python <repo>\.claude\skills\image\scripts\generate.py `
  --prompt "<SUBJECT>. <STYLE BLOCK> <BACKGROUND BLOCK>" `
  --name <kebab-slug> --dir <staging-dir> --model nb2 --aspect 1:1
```

- **Model choice (workflow).** Iterate the *design* cheaply on **`--model flash`**
  (`gemini-2.5-flash-image`, fast/cheap, and the only tier that returns a lossless
  **PNG**); once the composition is locked, do the **final render on `--model nb2`**
  (`gemini-3.1-flash-image`, "Nano Banana 2") — a generation newer than 2.5 flash
  with markedly better prompt adherence (~2× the cost, still ~5–7¢/icon). For an
  *already-locked* design (e.g. regenerating a catalog icon) skip straight to `nb2`.
  `--model pro` (`gemini-3-pro-image`, "Nano Banana Pro", GA) is the top-quality
  tier and supports `--size` 1K/2K/4K — reach for it only for maximum fidelity or
  large output.
- **`nb2`/`pro` return JPEG, not PNG — and that's fine here.** The strip is a *hue*
  key, not an edge cleanup: because the lime backdrop's hue sits far from real
  subject colours, the JPEG's edge ringing keys out cleanly anyway. Validated on
  red, gold-adjacent steel, the bow's interior transparency, the staff's cyan
  crystal, and the periwinkle skill hands — no green fringe survived. (Only a
  subject coloured *near* lime would suffer; those use the magenta backdrop.)
- `--aspect 1:1` (icons are square). Output lands in `<staging-dir>/<slug>-vN.<ext>`
  (`.jpg` for `nb2`/`pro`, `.png` for `flash`), auto-incrementing the version.
  Refine with `--edit` (edits the latest version).

### 2. Strip the background to transparency

```powershell
python strip-bg.py <staging-dir>\<slug>-vN.png <staging-dir>\<slug>-cut.png --bg-hue 85
```

`strip-bg.py` is a **hue key**: it clears every pixel whose hue matches the
backdrop (with enough saturation/value), which removes the solid backdrop, the
anti-aliased fringe along the outline, **and** background trapped inside concave
shapes — while protecting differently-hued interiors (gold, cyan, steel) and the
dark outline (low saturation/value). Pass `--bg-hue 85` for the lime backdrop
(don't rely on auto-detect — see gotchas). For a green subject, switch the
backdrop to **magenta** and pass that hue instead (`--bg-hue 300`).

Two opt-in cleanups for generation artifacts (see gotchas): `--trim-corners`
clears leftover opaque wedges when the model paints a rounded panel (centered
subjects only); `--fill-holes` re-opaques enclosed transparent holes punched
where a subject colour happened to match the backdrop hue (never use it on a
subject with legitimate interior transparency, e.g. the bow).

### 3. Verify, then ship

Composite the `-cut.png` onto `#0f1014` and check at large and ~46–96px sizes for
green fringe and small-size legibility. When approved, copy into `UI/static/img/`
under the exact display-name filename.

## Reusable prompt blocks

Compose every prompt as `"<SUBJECT>. <STYLE> <BACKGROUND>"`.

**STYLE** (verbatim, locked):
> Art style: a soft, clean cartoon game icon. Smooth, gentle cel-shading with
> softly rounded forms and a thin, subtle dark outline, no thick heavy outline.
> Cool, interface-matched palette: steel in cool blue-grey, brass/bronze in muted
> soft gold-bronze, leather and wood in muted desaturated warm browns. Clean flat
> fills with only simple soft cel-shading - no gradients, no texture, no noise,
> and NO glow or rim light. Crisp, low-detail, readable at small sizes.

**BACKGROUND** (verbatim — the edge-to-edge wording matters, see gotchas):
> Background: the lime-green background MUST completely fill the entire square
> frame, bleeding off all four edges and into all four corners. Do NOT draw any
> rounded panel, card, sticker border, frame, rounded rectangle, or vignette. The
> background is one flat solid rectangle of pure lime green, hex #7CFC00, behind
> the subject, edge to edge, with no shadow. Square 1:1 composition.

(For green subjects, replace lime `#7CFC00` with magenta `#FF00FF` throughout.)

## Gotchas (learned the hard way)

1. **No real transparency from Gemini.** Asking for a "transparent background"
   makes the model *paint a fake checkerboard* and return a **JPEG** (no alpha).
   This is why we generate on a solid chroma colour and key it out.
2. **Only 2.5 `flash` returns PNG; `nb2` and `pro` return JPEG.** That's fine — the
   strip is a hue key, so the JPEG edge ringing in the lime→subject transition keys
   out cleanly (the backdrop hue is far from any subject colour). Don't avoid
   `nb2`/`pro` over the format; do keep the lime/magenta backdrop that makes the hue
   separation work.
3. **API key is User-scoped only.** `GEMINI_API_KEY` is set at the Windows *User*
   environment scope and is **not** inherited by Bash/PowerShell tool sessions.
   Pull it per-invocation: `[System.Environment]::GetEnvironmentVariable('GEMINI_API_KEY','User')`.
4. **Lime green and gold/yellow are adjacent hues.** The old global RGB-threshold
   stripper (`edit-image.py`) matched gold pixels and ate parts of the crossguard
   and pommel. The hue key (`strip-bg.py`) separates them cleanly (gold ≈ 45°,
   lime ≈ 85°).
5. **Enclosed background pockets.** An edge-flood fill protects interior colours
   but leaves background trapped inside concave shapes (the gap between a bow and
   its string). A **global** hue key clears those too — safe because the backdrop
   colour appears nowhere in the subject. (`--connected` flag falls back to
   edge-flood for the rare same-hue subject.)
6. **Don't auto-detect the backdrop hue.** Detecting the "dominant saturated hue"
   fails when the subject has a large saturated area — brown wood/leather
   out-voted the lime on the bow and shirt. Always **force `--bg-hue 85`** (we
   always use the same backdrop).
7. **The model sometimes paints a rounded panel** with dark corners instead of
   filling the frame, leaving un-strippable background in the corners. The
   BACKGROUND block above explicitly forbids panels/borders and requires fill to
   all four corners — keep that wording. It still recurs (the Fire Bolt paneled
   every attempt); when it slips through, `strip-bg.py --trim-corners` removes the
   surviving corner wedges.
8. **Green subjects can't be lime-keyed.** Use a magenta backdrop instead.
9. **Never bake glow/rim into the art.** A requested "soft outer glow" was painted
   as a rounded box that survived stripping. Glow belongs in the UI's CSS.
10. **The hue key can punch holes in same-hue subject pixels.** The Fire Bolt's
    pale flame core leans yellow-green; one cluster fell into the lime range and
    was keyed out, leaving a transparent dot (a dark fleck on the dark UI).
    `strip-bg.py --fill-holes` restores enclosed transparent pixels (RGB is
    preserved when alpha is zeroed, so the colour comes back). Opt-in — it would
    wrongly fill legitimate interior gaps like the bow's.

## Icon catalog

All entries use the shared STYLE + BACKGROUND blocks above; only the subject
differs. "Version" notes how many iterations it took / any edit step.

### Weapons

| File | Subject prompt (the part before STYLE) | Notes |
|---|---|---|
| `Beginner Sword.png` | a basic starter sword. The sword has a straight steel blade with a subtle central fuller, a simple crossguard, a short wrapped grip, and a small round pommel. Composed at a 45-degree diagonal, blade pointing toward the upper right, centered and filling most of the frame with a small margin. | Style anchor. Cool blue-grey blade, muted gold guard/pommel, brown grip. |
| `Beginner Bow.png` | a simple wooden recurve bow held upright as a vertical curved arc, leather-wrapped grip in the middle, taut bowstring, with a single straight arrow nocked horizontally across it. The steel arrowhead leads outward to the left and the feathered fletching sits at the bowstring; the arrow is not aimed diagonally at the ground. | Arrow-direction wording is baked into the prompt now — NB2 placed the **steel head leading outward** first try (the 2.5-flash version had needed a manual edit). Interior bow/string gap keys out via the global hue key (no `--fill-holes`). |
| `Beginner Daggers.png` | two matching daggers crossed in a large bold symmetric X that fills most of the frame and reaches near the corners. Short slightly-curved steel blades, simple crossguards, brown wrapped grips. | NB2 painted a faint panel, so the corner wedges needed `--trim-corners` (safe here — the blade tips stop short of the true corners). |
| `Beginner Staff.png` | a wooden magic staff held on a diagonal, topped with a single large faceted **cyan-blue crystal** gem at the upper end. Plain straight wooden shaft, the blue crystal the focal point. | Cyan crystal is the identity colour (overrides the cool-neutral default). |
| `Giant Stick.png` | a crude heavy wooden club made from a thick gnarled tree branch, with several sharp thorny wooden spikes and rough bark. Held on a diagonal. | Muted brown wood. |
| `Iron Axe.png` | a one-handed battle axe with a single broad steel head and a straight wooden handle with a small grip wrap near the base. Diagonal, head toward the upper area. | Cool blue-grey head, muted brown handle. |

### Armor

| File | Subject prompt (the part before STYLE) | Notes |
|---|---|---|
| `Bronze Helm.png` | a medieval barbute-style metal helmet with a tall rounded dome and a T-shaped face opening, with a subtle riveted edge. Front three-quarter view. The metal is a muted, soft, slightly-darker bronze (a warm muted bronze, NOT bright shiny gold or polished brass), kept perfectly smooth and clean: no scratches, grime, rust, distressing, patina, or texture. | NB2's default bronze ran too bright/gold, so the prompt forces a muted darker bronze. Keep it explicitly **clean** — an `--edit` asking for "weathered/aged" bronze made it add grime/blotches, which breaks the flat-fill style. Re-rolled fresh, not edited. |
| `Leather Helm.png` | a simple leather cap helmet in muted brown leather, with small cool blue-grey metal studs and a riveted reinforced brow band. Front three-quarter view. | |
| `Leather Boots.png` | a pair of muted brown leather boots, mid-calf, soft fold-over cuff, small strap and buckle near the top, standing side by side at a slight angle. | |
| `Leather Pants.png` | muted brown leather trousers, front view, belt at the waist, a couple of small stitched pocket patches on the thighs. Laid out flat and symmetric. | NB2 painted a faint panel; corner wedges removed with `--trim-corners`. |
| `Leather Shirt.png` | a short-sleeved muted brown leather tunic with a small collar, fastened with **two simple horizontal leather buckle straps** across the chest (no zippers). Front, laid flat and symmetric. | Regenerated once — the first version's vertical straps read like zippers. |

### Skills

Skills use the same rendering style but **drop the metal/leather palette sentence**
from STYLE (the subjects aren't gear) and use subject-appropriate colours. Hands
use a deliberately **non-natural periwinkle-blue skin tone** (≈ `#a8c0f0`, which
ties to the UI accent) so the icons don't imply a character's race. Skills render
smallest, so keep them bold; they were generated with the stronger anti-panel
BACKGROUND wording.

| File | Subject prompt (the part before STYLE) | Notes |
|---|---|---|
| `Fire Bolt.png` | a fiery projectile spell shaped like a comet: a small rounded leading flame head toward the upper right, with a long, thin, tapering trailing streak of flame and a few sparks stretching toward the lower left. Bright yellow/orange core, deeper red-orange edges. | On 2.5-flash this paneled every attempt and lost a yellow-green core pixel (needed `--trim-corners --fill-holes`); the NB2 regen filled the frame and keyed clean with a plain `--bg-hue 85`. |
| `Punch.png` | a properly formed fist in a straight forward punch, three-quarter back view driving toward the upper right; the flat front of the knuckles leads and the thumb is wrapped across the outside of the fingers (NOT a thumb-leading hammerfist). Periwinkle-blue skin. A few motion lines trail behind toward the lower left. | First attempt put the thumb as the striking surface and added an impact sparkle — both fixed on iteration. |
| `Slap.png` | an open bare hand, fingers together and flat, mid-swing sideways slap, palm facing forward and sweeping toward the right. Periwinkle-blue skin. Three or four short curved action lines trail behind on the left. | |

## Status

All 14 current icons (11 gear + 3 skills above) were **regenerated on `nb2`
(`gemini-3.1-flash-image`) on 2026-06-14** in this style, replacing the earlier
2.5-flash versions — better adherence and fewer strip hacks (NB2 fixed the Fire Bolt
paneling, the bow's arrow direction, and the Punch thumb on the first try; only
`daggers` and `leather-pants` still needed `--trim-corners`). **Drops** (`Rat Tail`,
`Slime Ball`) are slated for removal — do not regenerate them. Skill slots are
expected to grow to a **64×64 minimum (possibly 96×96)**, so favour legibility at
those sizes for new skill art.
