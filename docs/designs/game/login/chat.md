# Login Form Modernization

_Started 2026-05-25 15:25 UTC_

---

## User

To start, I'd like to modernize the logic/account creation screen. It is currently missing form field validation, but please include it in your prototypes.

## Assistant

_[tool: local_ls]_

_[tool: local_ls]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: local_read]_

_[tool: update_todos]_

I have enough context. Looking at the existing Svelte game (RPG-style — battles, enemies, skills, items), the login screen is a simple username/password card with no validation. Let me build a few modernized directions side-by-side on a canvas so you can compare. All three will be fully interactive with real-time field validation.

**System I'm committing to:**
- Keep the existing color tokens (dark gradient bg, off-white containers, `--btn-background` light blue, red errors, 5px radius, subtle shadows, hover-glow)
- Type pairing: Geist (UI) + Geist Mono (validation hints/labels) — modern but neutral; the existing font is Arial so this is a gentle step up
- Validation rules I'm picking (assume reasonable defaults — easy to tweak):
  - Username: 3–20 chars, letters/numbers/underscore/hyphen, no leading number
  - Password: ≥8 chars, mix of letter+number (strength meter: weak/fair/good/strong)
  - Create account: confirm password must match
- Three directions on a design canvas: **Refined Card** (closest to current), **Game Panel** (split with character-art slot + class pick on signup), **Minimal Stack** (typographic, no card)

_[tool: copy_starter_component]_

_[tool: grep]_

_[tool: write_file]_

Now I'll build the shared validation + hook, then the three variants, then the canvas.

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: done]_

_[tool: fork_verifier_agent]_

Three modernized directions live on the canvas, all fully interactive with field validation:

- **A · Refined Card** — closest to the existing screen. Tabs for Sign in/Create, inline validation (✓/✕), password strength meter on signup, show/hide password, caps-lock hint, submit disabled until valid.
- **B · Game Panel** — split layout with a placeholder hero portrait + flavor text on the left, form on the right. Adds starting-class pick on signup. Dark game-launcher feel.
- **C · Minimal Stack** — no card; underlined fields directly on the dark gradient, one consolidated status line, editorial type.

Shared validation rules across all three:
- Username: 3–20 chars, letters/numbers/`_`/`-`, can't start with a number
- Password: ≥8 chars + letter + number (login mode just requires a value, matching your existing backend behavior)
- Signup: confirm-password must match
- Errors gate on blur or first submit (no nag while typing)

To try the error states: sign in with password `wrong`, or sign up as `admin`.

Next steps when you've picked a direction:
1. Which variant (or mix) do you want to take forward?
2. Validation rules to adjust — min/max length, special-char requirement, reserved names?
3. Any flows missing — forgot password, remember-me, guest play?
