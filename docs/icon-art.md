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
- **Thin, subtle dark outline** — _not_ a thick "sticker" outline.
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
- `strip-bg.py` — chroma background remover. Bundled with the `/image` skill at
  `.claude/skills/image/scripts/strip-bg.py` (alongside `generate.py`), so it travels
  with the repo.
- `badge.py` — composites an amp / resist **badge** onto a stripped base icon (the
  damage-type amplification/resistance family — see the _Damage types_ catalog section).
  The badge art (`badges/amp.png`, `badges/resist.png`) is generated soft-cel art, not a vector;
  `badge.py` autocrops it, scales it to ~42% of the icon width, and drops it lower-right with a soft
  shadow. Keeps the `…Amplification` / `…Resistance` siblings identical bar the badge (the crit `%`-badge
  pattern, generalised).
- `generate-agy.py` — **default** generation backend: routes through the Antigravity
  CLI (`agy`) on your **subscription's image quota** instead of billing the paid API.
  Same prompt; `--strip` chains `strip-bg.py`. `agy` needs a real console, so run it
  from an interactive terminal — or pass `--detached` to let a headless caller (e.g.
  Claude's shell) drive it. See step 1 below; fall back to `generate.py` for large
  batches or non-square output.

### 1. Generate (on a lime-green backdrop)

```powershell
# The key is only set at the Windows User env scope, not in shell sessions:
$env:GEMINI_API_KEY = [System.Environment]::GetEnvironmentVariable('GEMINI_API_KEY','User')

python <repo>\.claude\skills\image\scripts\generate.py `
  --prompt "<SUBJECT>. <STYLE BLOCK> <BACKGROUND BLOCK>" `
  --name <kebab-slug> --dir <staging-dir> --model nb2 --aspect 1:1
```

- **Model choice (workflow).** Iterate the _design_ cheaply on **`--model flash`**
  (`gemini-2.5-flash-image`, fast/cheap, and the only tier that returns a lossless
  **PNG**); once the composition is locked, do the **final render on `--model nb2`**
  (`gemini-3.1-flash-image`, "Nano Banana 2") — a generation newer than 2.5 flash
  with markedly better prompt adherence (~2× the cost, still ~5–7¢/icon). For an
  _already-locked_ design (e.g. regenerating a catalog icon) skip straight to `nb2`.
  `--model pro` (`gemini-3-pro-image`, "Nano Banana Pro", GA) is the top-quality
  tier and supports `--size` 1K/2K/4K — reach for it only for maximum fidelity or
  large output.
- **`nb2`/`pro` return JPEG, not PNG — and that's fine here.** The strip is a _hue_
  key, not an edge cleanup: because the lime backdrop's hue sits far from real
  subject colours, the JPEG's edge ringing keys out cleanly anyway. Validated on
  red, gold-adjacent steel, the bow's interior transparency, the staff's cyan
  crystal, and the periwinkle skill hands — no green fringe survived. (Only a
  subject coloured _near_ lime would suffer; those use the magenta backdrop.)
- `--aspect 1:1` (icons are square). Output lands in `<staging-dir>/<slug>-vN.<ext>`
  (`.jpg` for `nb2`/`pro`, `.png` for `flash`), auto-incrementing the version.
  Refine with `--edit` (edits the latest version).

**Subscription-backed generation (no API spend) — the `/image` skill's default.**
`generate-agy.py` (same `--prompt`/`--name` as `generate.py`) generates on your
Antigravity/Pro subscription's image quota via the `agy` CLI. Add `--strip` to key out
the background in one step:

```powershell
# Interactive terminal — agy runs in the current console:
python <repo>\.claude\skills\image\scripts\generate-agy.py --name <slug> --strip `
  --prompt "<SUBJECT>. <STYLE> <BACKGROUND>"

# Headless caller (e.g. Claude's shell) — add --detached:
python <repo>\.claude\skills\image\scripts\generate-agy.py --name <slug> --strip --detached `
  --prompt "<SUBJECT>. <STYLE> <BACKGROUND>"
```

`agy`'s built-in `generate_image` tool uses the NB2-class image model (matches our
finals) and saves a native 1024×1024 JPEG to its session folder; `--strip` keys that
JPEG straight to a transparent PNG (no format conversion). The agy _agent_ model (the LLM
that calls the tool) defaults to `Claude Sonnet 4.6 (Thinking)` and auto-falls back to
`Gemini 3.5 Flash (Low)` when Sonnet usage is exhausted — override with `--model` /
`--fallback-model`. It also **edits and combines**: `--edit` feeds the latest `<name>-v<N>`
back in to refine an icon (e.g. tweak one detail while keeping the rest), and `--input <path>`
(repeatable) adds reference images to combine — up to 3 base images total (the tool's cap).
Its image quota resets ~every 5h, so fall back to `generate.py` (API) for large batches or
when you need `--aspect` / non-square output (agy is square-only).

**Why `--detached`.** `agy` checks for a real console (TTY) and hangs forever in a
headless/piped shell, so it can't be driven directly from an agent's tool calls.
`--detached` makes the script re-spawn _itself_ attached to its own **hidden** console
(`subprocess.Popen(creationflags=CREATE_NO_WINDOW)`) — that console IS a real TTY, so agy
runs — then it polls a status file the child writes on exit and returns success or
failure. Nothing appears on the desktop and no typing is needed. (agy's tty check is on
the console handle, not window visibility, so a windowless console passes. Passing the
prompt through the subprocess argv list also dodges the `Start-Process` quote-mangling
that splits a multi-word prompt into bad args.)

### 2. Strip the background to transparency

```powershell
python <repo>\.claude\skills\image\scripts\strip-bg.py <staging-dir>\<slug>-vN.png <staging-dir>\<slug>-cut.png --bg-hue 85
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

**Then record the exact final prompt in the [catalog](#icon-catalog) below — this is a
required step, not optional.** Every icon that ships MUST have the verbatim final subject
prompt that produced it listed there (recover it from `agy`'s conversation DBs under
`~/.gemini/antigravity-cli/conversations/*.db` if you lost it — a raw-text scan between
`this exact Prompt:` and `After the tool reports` yields it). The catalog is only useful for
reproducing and matching the set if it stays complete.

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
   makes the model _paint a fake checkerboard_ and return a **JPEG** (no alpha).
   This is why we generate on a solid chroma colour and key it out.
2. **Only 2.5 `flash` returns PNG; `nb2` and `pro` return JPEG.** That's fine — the
   strip is a hue key, so the JPEG edge ringing in the lime→subject transition keys
   out cleanly (the backdrop hue is far from any subject colour). Don't avoid
   `nb2`/`pro` over the format; do keep the lime/magenta backdrop that makes the hue
   separation work.
3. **API key is User-scoped only.** `GEMINI_API_KEY` is set at the Windows _User_
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
11. **`agy`'s image quota runs out mid-batch (~20 icons), and resets in ~5h.** When it
    does, a `--detached` run "succeeds" but its child reports `no generated image found in
    agy's brain folder` — the agent ran but had no image quota left to draw. Don't retry on
    `agy`; **fall back to the paid API** `generate.py` (`--model nb2`, then `strip-bg.py`
    separately — `generate.py` has no `--strip`). It bills `GEMINI_API_KEY` but is unmetered
    by the subscription, so it clears a stuck batch immediately. (A large batch is exactly the
    case `docs` already flags `generate.py` for.)
12. **Drive a multi-icon batch serially, one `--detached` call at a time.** Two concurrent
    `agy` sessions race on the session dir and one dies with `no status file`. A bash loop that
    calls `generate-agy.py --detached` per icon serialises naturally (each call blocks on its
    child), so a whole base set generates unattended without contention.
13. **`--strip-arg` doesn't survive the `--detached` re-spawn.** Passing
    `--strip-arg=--trim-corners` to a detached run failed (`no status file`); generate plain,
    then apply `strip-bg.py --trim-corners` as a separate step on the saved `…-vN.jpg`.
14. **A "death / decay / dark" themed prompt can draw an opaque DARK backdrop panel** — which,
    unlike a lime corner wedge (gotcha 7), the hue key can't remove (dark pixels are low-saturation,
    protected). The wilting-flower DoT drew a full dark panel twice. Fix in the prompt, not the strip:
    spell out that the subject "sits alone on a flat pure lime-green background with absolutely nothing
    behind it — no dark, black, grey, or coloured panel/box/shadow/vignette." Verify by checking the
    stripped PNG's edge pixels are transparent (a panel leaves them opaque).
15. **Multi-colour subjects (e.g. the Elemental orb's four element quadrants) can't use the global
    hue key** — a quadrant whose hue nears the backdrop's gets keyed out. Strip with `--connected`
    (edge-flood: removes only backdrop connected to the frame edge, protecting every interior colour),
    and keep the subject free of enclosed background pockets so none survive.

## Icon catalog

All entries use the shared STYLE + BACKGROUND blocks above; only the subject
differs. "Version" notes how many iterations it took / any edit step.

**Every shipped icon must appear here with the exact, verbatim final subject prompt** (the
part before the STYLE block) that generated it — recording it is a required ship step (see
[Verify, then ship](#3-verify-then-ship)). If an icon was re-rolled, list the prompt of the
version that actually shipped, not an earlier draft.

### Weapons

<!-- prettier-ignore -->
| File | Subject prompt (the part before STYLE) | Notes |
|---|---|---|
| `Beginner Sword.png` | a basic starter sword. The sword has a straight steel blade with a subtle central fuller, a simple crossguard, a short wrapped grip, and a small round pommel. Composed at a 45-degree diagonal, blade pointing toward the upper right, centered and filling most of the frame with a small margin. | Style anchor. Cool blue-grey blade, muted gold guard/pommel, brown grip. |
| `Beginner Bow.png` | a simple wooden recurve bow held upright as a vertical curved arc, leather-wrapped grip in the middle, taut bowstring, with a single straight arrow nocked horizontally across it. The steel arrowhead leads outward to the left and the feathered fletching sits at the bowstring; the arrow is not aimed diagonally at the ground. | Arrow-direction wording is baked into the prompt now — NB2 placed the **steel head leading outward** first try (the 2.5-flash version had needed a manual edit). Interior bow/string gap keys out via the global hue key (no `--fill-holes`). |
| `Beginner Daggers.png` | two matching daggers crossed in a large bold symmetric X that fills most of the frame and reaches near the corners. Short slightly-curved steel blades, simple crossguards, brown wrapped grips. | NB2 painted a faint panel, so the corner wedges needed `--trim-corners` (safe here — the blade tips stop short of the true corners). |
| `Beginner Staff.png` | a wooden magic staff held on a diagonal, topped with a single large faceted **cyan-blue crystal** gem at the upper end. Plain straight wooden shaft, the blue crystal the focal point. | Cyan crystal is the identity colour (overrides the cool-neutral default). |
| `Giant Stick.png` | a crude heavy wooden club made from a thick gnarled tree branch, with several sharp thorny wooden spikes and rough bark. Held on a diagonal. | Muted brown wood. |
| `Iron Axe.png` | a one-handed battle axe with a single broad steel head and a straight wooden handle with a small grip wrap near the base. Diagonal, head toward the upper area. | Cool blue-grey head, muted brown handle. |

### Armor

<!-- prettier-ignore -->
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

<!-- prettier-ignore -->
| File | Subject prompt (the part before STYLE) | Notes |
|---|---|---|
| `Fire Bolt.png` | a fiery projectile spell shaped like a comet: a small rounded leading flame head toward the upper right, with a long, thin, tapering trailing streak of flame and a few sparks stretching toward the lower left. Bright yellow/orange core, deeper red-orange edges. | On 2.5-flash this paneled every attempt and lost a yellow-green core pixel (needed `--trim-corners --fill-holes`); the NB2 regen filled the frame and keyed clean with a plain `--bg-hue 85`. |
| `Punch.png` | a properly formed fist in a straight forward punch, three-quarter back view driving toward the upper right; the flat front of the knuckles leads and the thumb is wrapped across the outside of the fingers (NOT a thumb-leading hammerfist). Periwinkle-blue skin. A few motion lines trail behind toward the lower left. | First attempt put the thumb as the striking surface and added an impact sparkle — both fixed on iteration. |
| `Slap.png` | an open bare hand, fingers together and flat, mid-swing sideways slap, palm facing forward and sweeping toward the right. Periwinkle-blue skin. Three or four short curved action lines trail behind on the left. | |

### Attributes

The 11 currently-visible character attributes (issue #531). Like skills, these **drop
the metal/leather palette sentence** from STYLE (they're abstract symbols, not gear) and
bake a per-subject colour into the subject. They render **very small** — down to ~16px on
combat effect chips and skill code chips — so each is a single bold, iconic silhouette.
The two **green** subjects (Luck, Health Regen) can't be lime-keyed, so they generate on
the **magenta** backdrop (`--bg-hue 300`); the rest use lime. The Strength arm uses the
same **periwinkle-blue skin** as the skill hands. All 11 were rendered on `nb2`. Their
STYLE drops the metal/leather palette sentence (like skills) **and** appends one extra
sentence to force small-size legibility, in place of the gear closing sentence:

> A single bold, iconic, centred symbol with minimal internal detail - it must stay
> clearly readable when shown as small as 16 pixels.

<!-- prettier-ignore -->
| File | Subject prompt (the part before STYLE) | Notes |
|---|---|---|
| `Strength.png` | A single flexed muscular arm bent at the elbow with a clenched fist, a strong bulging bicep — the classic 'strength' flex, side view. Periwinkle blue-violet skin (≈ `#a8c0f0`). | Periwinkle skin like the skill hands. |
| `Endurance.png` | A single stylized mountain peak — a bold triangular mountain with a rounded snow-capped top and simple flat facet shading. Cool blue-grey rock, lighter off-white cap. | Mountain (not a heart/shield) to avoid colliding with Max Health / Toughness. |
| `Intellect.png` | A stylized human brain, three-quarter front angle, simple rounded lobes and a few clean fold lines. Soft periwinkle-blue and lavender (not fleshy pink). | Cool/UI-tied palette, deliberately not pink. |
| `Agility.png` | A single winged boot — a simple low boot with a small feathered wing on the heel/ankle (a Hermes winged sandal). Muted brown boot, cool blue-grey/off-white wing. Fills most of the frame. | |
| `Dexterity.png` | A bullseye target of concentric rings, face-on, with a single slim arrow struck dead centre. Cool blue-grey and off-white rings, small bright centre. | Concentric rings stay legible even at 16px. |
| `Luck.png` | A single four-leaf clover: four rounded heart-shaped leaves and a short stem. Fresh muted green. | **Magenta backdrop** (green subject), `--bg-hue 300`. |
| `Max Health.png` | A single bold heart symbol, smooth and rounded. Warm red. Fills most of the frame. | |
| `Cooldown Recovery.png` | A single classic hourglass (sand timer), face-on: two rounded glass bulbs in a simple frame, sand falling through. Cool blue-grey frame, pale warm sand. Fills most of the frame. | Hourglass beat a stopwatch+arrow, which blurred below ~24px. |
| `Damage Taken Per Second.png` | Two blood droplets — one large dominant teardrop with a small highlight, plus a second much smaller drop beside/below it. Deep red. Fills most of the frame. | Two-drop motif separates it from the single Max Health heart. |
| `Health Regen Per Second.png` | A single heart with a small upward arrow and a tiny sparkle above it. A fresh healing **green** heart (not red). | **Magenta backdrop** (green subject), `--bg-hue 300`. |

### Crit / dodge (issue #801; Block retired #1378)

The player crit/dodge attributes (the #178 spike) share a small **visual language**: each
family has **one clean base symbol**; the _magnitude_ attribute uses the clean symbol
unchanged, and the _chance_ attribute reuses it with a **composited `%` badge** in the
lower-right corner:

- **Crit** — an explosive impact burst (white-hot core, jagged shockwave rays).
- **Dodge** — a cool blue-grey humanoid silhouette mid-sidestep with two faded **phantom
  afterimages** trailing it.

> **Block retired (#1378).** The Block mechanic was removed with the mitigation rework (#1333),
> so its art (`Block Reduction.png`, `Block Chance.png`) and the brown-leather-bracers base symbol
> were deleted. The reflection that replaced Block has its own art — see
> [Toughness & Damage Reflection](#toughness--damage-reflection-1378).

**Base symbols.** The two exact subject prompts (the part before STYLE), generated on the
`agy`/NB2 backend on the lime backdrop, keyed with the default `--bg-hue 85`. Like the other
attributes they use the locked STYLE **minus** the metal/leather palette sentence **plus** the
16px legibility sentence, with the skin colours baked into the subject:

<!-- prettier-ignore -->
| Base | Subject prompt (the part before STYLE) |
|---|---|
| crit | A single bold explosive critical-hit impact burst - a sharp jagged multi-pointed starburst exploding outward with a bright white-hot center and sharp angular shockwave rays of varying length, conveying a powerful high-impact strike. Centred, filling most of the frame. Bright white-yellow core, vivid orange middle, deep red-orange pointed tips. |
| dodge | A single sleek bold humanoid silhouette dodging to the side - leaning and stepping sideways to evade an incoming attack, the body angled and weight shifted to one side so it clearly reads as sidestepping rather than running forward, with two translucent phantom afterimage copies trailing to show the quick evasive motion. The leading silhouette is a solid cool slate blue-grey, the trailing afterimages a faded lighter blue-grey, never green. Centred, filling most of the frame. |

**Files.** Each base ships twice — the clean symbol and a `%`-badged variant:

<!-- prettier-ignore -->
| File | Base | Variant |
|---|---|---|
| `Critical Damage.png` | crit | clean (magnitude) |
| `Critical Chance.png` | crit | + `%` (chance) |
| `Dodge.png` | dodge | clean — standalone, for the combat-float popup |
| `Dodge Chance.png` | dodge | + `%` (chance) |

The `%` badge is **not generated** — it is composited onto the stripped base PNG (a bold
outlined off-white `%`, lower-right, ~46% of the icon width with a thick dark outline so it
reads even over the light dodge figure), so the chance and magnitude siblings stay
pixel-identical apart from the badge, and the badge is trivially restyled.

The clean symbols double as the **combat-float popup** art: `CombatFloaters.svelte`'s
`.floater-icon` renders `Critical Damage.png` / `Dodge.png` for the per-outcome CRIT/DODGE
floaters. `Dodge` needs its own file because dodge has no magnitude attribute to carry the
clean symbol.

### Damage types & amplification / resistance (#1320/#1340)

The eight leaf damage types + the two cross-cutting categories (Elemental, DoT) each get **one
base icon** that serves two roles: the **damage-type icon** itself (drawn by `DamageTypeIcon`
in the breakdown headers / combat floaters, replacing the old inline-SVG `DamageTypeGlyph`
stopgap) **and** the **base** for that type's amplification / resistance attribute icons. Amp
and resist are not regenerated — `badge.py` composites a generated soft-cel **amp** (up-arrow,
"deal more") or **resist** (shield, "take less") badge onto the base, lower-right, so
`<Type> Amplification.png` and `<Type> Resistance.png` are identical apart from the badge.

Subjects bake the type's `--dmg-*` hue and use the attribute STYLE (drop the metal/leather
palette sentence, add the 16px legibility sentence). Backdrop is lime (`--bg-hue 85`) except
the two near-lime subjects — **Wind** (mint) and **Poison** (yellow-green) — which use the
**magenta** backdrop (`--bg-hue 300`). **Elemental** is the exception to the global hue key: its
four element-coloured quadrants would be partly keyed out, so it strips with `--connected`
(edge-flood, removes only the connected exterior backdrop). Keep critical detail out of the
lower-right corner — that is where the amp/resist badge lands.

<!-- prettier-ignore -->
| Base file | Subject prompt (the part before STYLE) | Notes |
|---|---|---|
| `Physical.png` | Two steel swords crossed in a bold symmetric X, both blades pointing up and outward to the top corners, with simple crossguards and short wrapped grips meeting at the bottom centre. Cool blue-grey steel blades, like hex #b6bcc8, with small muted brown grips and a hint of muted gold-bronze on the guards. A clean iconic crossed-swords emblem. Centred, filling most of the frame. | Crossed swords — refined from a too-ambiguous impact burst. API/nb2. |
| `Fire.png` | A cluster of bold flames - two or three orange flame tongues of varying height rising together from a common base, like a small bonfire or fireball, with a brighter yellow-orange core. Warm orange fire, like hex #ef8a5d, deeper red-orange at the outer edges. Centred, filling most of the frame. | Multi-flame cluster, set apart from Burn's single ember-flame. API/nb2. |
| `Water.png` | A single bold curling ocean wave - a powerful breaking wave that curls over at the crest with a little white foam and a few spray droplets, stylized and clean and dynamic. Clear blue water, like hex #6fb2e0, with soft off-white foam. Centred, filling most of the frame. | Wave (more powerful than the earlier droplet). API/nb2. |
| `Earth.png` | A single chunky angular boulder - one heavy rounded rock with a couple of smaller stones at its base and a few simple facet lines, solid and weighty. Muted tan-brown earth color, like hex #c2a368. NOT a mountain or a tall peak, a low chunky rock. Centred, filling most of the frame. | Boulder, not a mountain (avoids the Endurance peak). |
| `Wind.png` | A single bold swirling wind gust - one clean curling spiral of wind with two short tapering trailing motion streaks, conveying a gust of moving air. Soft mint-teal color, like hex #9fd9c0. Centred, filling most of the frame. | **Magenta backdrop** (mint ≈ lime hue). |
| `Bleed.png` | Two blood droplets - one large dominant teardrop centred with a small soft highlight, plus a second smaller droplet beside it to the upper left. Deep vivid red blood, like hex #d76a72. Centred, filling most of the frame. | Blood drops (the old Damage-Taken motif); the claw-slashes were freed for enemy art. Needed `--trim-corners`. API/nb2. |
| `Poison.png` | A single rounded droplet of venom in sickly toxic green, like hex #a9c45f, marked with a small bold white skull-and-crossbones symbol on its face - a clear little skull above two crossed bones. Menacing and deadly poison. Centred, filling most of the frame. | Skull-and-crossbones in a toxic drop. **Magenta backdrop** (green subject). API/nb2. |
| `Burn.png` | A single LARGE bold amber flame rising from a small glowing ember at its base, with one or two small sparks, conveying a smouldering burn. Warm amber-gold colour, like hex #f0b259, clearly more golden-amber than a bright orange fire. Large and bold, filling almost the entire frame. Centred. | Amber/ember vs Fire's orange; regenerated larger. API/nb2. |
| `Elemental.png` | A round elemental medallion divided into four equal pie quadrants, each a different classical element: a warm orange flame in one quadrant, a blue water droplet in another, a tan-brown rock in another, and a mint-green wind swirl in the last, all enclosed by a clean lavender-purple ring around the rim. Conveys all four elements combined into one emblem. Centred, filling most of the frame. | Four-element orb. Stripped with `--connected` (the four hues confuse a global key). API/nb2. |
| `Damage Over Time.png` | A single wilting, drooping flower for a damage-over-time game icon - a limp bent stem and a couple of withered drooping leaves in muted dried olive-brown (a dull desaturated brown-green, NOT a bright vivid green), topped by a drooping withered bloom of faded dusty mauve-purple petals (around hex #b98fbf) turning brown and brittle at the edges, with one shrivelled petal falling away below. Muted, desaturated, naturalistic colours. Centred, filling most of the frame. | Wilting flower (decay over time); naturalistic palette keeps the mauve bloom for the DoT hue. Needed extra-strong anti-panel wording (a "death/decay" prompt drew a dark backdrop panel twice). API/nb2. |

**Amp/resist files** (composited via `badge.py`, no regeneration): `<Type> Amplification.png`
(amp badge) and `<Type> Resistance.png` (resist badge) for each of the ten bases above. The two
**badge** source arts under `.claude/skills/image/scripts/badges/` are generated, not vector:

<!-- prettier-ignore -->
| Badge | Subject prompt (the part before STYLE) |
|---|---|
| `amp` | A single bold upward-pointing arrow, thick and chunky with gently rounded corners, pointing straight up - a simple solid arrow shape (triangular head over a short wide shaft). Warm cream off-white color with soft gentle cel-shading, a touch brighter at the top and a touch deeper at the base, and a clean thin dark outline. Centred, filling most of the frame. |
| `resist` | A single bold heater shield, front view - a simple solid shield with a wide flat top and sides tapering to a point at the bottom, a subtle raised center boss, and gently rounded edges. Warm cream off-white color with soft gentle cel-shading, a touch brighter at the top and a touch deeper toward the point, and a clean thin dark outline. Centred, filling most of the frame. |

### Weapon-type icons (#1340)

The six weapon damage-type leaves (physical-category) each get a base weapon icon, which ships
as the type icon **and** (via `badge.py` amp badge) the `<Weapon> Amplification.png` attribute
icon — weapon types are **amp-only** (a weapon hit mitigates through `Physical Resistance`).
These use the **gear** STYLE (keep the metal/leather palette sentence) + the 16px legibility
sentence; lime backdrop.

<!-- prettier-ignore -->
| Base file | Subject prompt (the part before STYLE) | Notes |
|---|---|---|
| `Sword.png` | A single upright steel sword pointing straight up - a straight double-edged blade with a subtle central fuller, a simple crossguard, and a short wrapped grip with a small pommel. Cool blue-grey steel blade, muted brown grip, small muted gold-bronze guard. Centred, filling most of the frame. | |
| `Axe.png` | A single one-handed battle axe held upright - one broad curved steel axe head on a straight wooden handle with a small grip wrap near the base. Cool blue-grey steel head, muted brown wooden handle. Centred, filling most of the frame. | Needed `strip-bg.py --trim-corners` (painted panel). |
| `Bow.png` | A single wooden recurve bow held upright as a vertical curved arc, with a leather-wrapped grip in the middle and a taut bowstring. Muted brown wood, lighter taut string. No arrow. Centred, filling most of the frame. | API/nb2 (agy quota exhausted mid-batch). |
| `Club.png` | A single crude heavy wooden club held upright - a thick gnarled tree-branch cudgel, wider and knobbly at the top with rough bark, narrowing to a grip at the bottom. Muted brown wood. Centred, filling most of the frame. | |
| `Dagger.png` | A single short steel dagger pointing straight up - one slightly curved short blade, a simple small crossguard, and a wrapped grip. Cool blue-grey steel blade, muted brown grip. Centred, filling most of the frame. | API/nb2. |
| `Unarmed.png` | A single clenched fist, knuckles facing forward, in a bold straight-on punch pose. Periwinkle blue-violet skin, like hex #a8c0f0. Centred, filling most of the frame. | Periwinkle skin (like the skill hands); API/nb2. Uses the attribute STYLE (no gear palette). |

### Toughness & Damage Reflection (#1378)

The two mitigation-rework attributes. Both use the attribute STYLE (steel colours baked into
the subject) + the 16px legibility sentence, lime backdrop. `Toughness` replaced the old
`Defense` shield (now retired); a breastplate keeps it distinct from the `Endurance` mountain,
the `Max Health` heart, and the resist shield-badge.

<!-- prettier-ignore -->
| File | Subject prompt (the part before STYLE) | Notes |
|---|---|---|
| `Toughness.png` | A single sturdy steel breastplate body-armor (a cuirass), front view - a smooth rounded plain chest plate with a raised central ridge down the middle and subtle layered overlapping plates at the shoulders and waist. Keep the chest surface completely plain and smooth with no emblem, crest, engraving, animal, or decoration. Cool blue-grey steel with soft cel-shading. Centred, filling most of the frame. | Re-rolled to drop a lion crest (noisy when small). |
| `Damage Reflection.png` | A single bold reflect-damage symbol: a vivid red arrow flies in from the lower-left, strikes an angled steel deflector plate at the centre, and ricochets sharply back out toward the upper-left, the two arrow segments forming a clear bent V with a bright spark at the bounce point - unmistakably an attack being bounced back the way it came. Cool blue-grey steel plate, vivid red-orange arrow and spark. Bold, simple, low-detail. Centred, filling most of the frame. | v2 — a clearer ricochet than the first "arrows into a wall" draft. |

## Status

All 14 current icons (11 gear + 3 skills above) were **regenerated on `nb2`
(`gemini-3.1-flash-image`) on 2026-06-14** in this style, replacing the earlier
2.5-flash versions — better adherence and fewer strip hacks (NB2 fixed the Fire Bolt
paneling, the bow's arrow direction, and the Punch thumb on the first try; only
`daggers` and `leather-pants` still needed `--trim-corners`). The **11 attribute
icons** (the Attributes section above) were generated the same way on `nb2` on
2026-06-14 (issue #531). The **crit/dodge set** (the standalone `Dodge.png` + the crit/dodge
attribute icons, two base symbols) was added on the `agy`/NB2 backend on **2026-06-17** (issue
#801) — see the _Crit / dodge_ section above; the former block art was retired with the
mechanic (#1378).

The **damage-type / amplification-resistance / weapon family + the #1378 mitigation attrs**
were added **2026-06-29**: 16 base icons (10 damage types + 6 weapons) + the 2 amp/resist badge
sources, composited by `badge.py` into 26 amp/resist/weapon-amp variants, plus `Toughness.png`
and `Damage Reflection.png` — 44 new files. Generated on the `agy`/NB2 backend until its image
quota ran out ~20 icons in, then finished on the paid API (`generate.py --model nb2`) — see
gotchas 11–13. This replaced the inline-SVG `DamageTypeGlyph` with PNG `DamageTypeIcon` art and
retired `Defense.png` (Toughness's old placeholder) and the Block art.

Skill slots are expected to grow to a **64×64 minimum (possibly 96×96)**, so favour legibility
at those sizes for new skill art.
