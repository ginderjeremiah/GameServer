<div class="page-base" data-hydrated={hydrated ? 'true' : undefined}>
	{#if booting}
		<BootSplash />
	{:else}
		{@render children()}
	{/if}
	<TooltipBase />
	<ToastContainer />
	<ModalHost />
</div>

<script lang="ts">
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { page } from '$app/state';
import { onMount } from 'svelte';
import { TooltipBase, ToastContainer, ModalHost } from '$components';
import { getTokens, onSocketError } from '$lib/api';
import { playerManager } from '$lib/engine';
import { resumeSession } from '$lib/engine/session';
import { bootRedirect, shouldReturnToLogin } from '$lib/engine/boot-redirect';
import { toastError } from '$stores';
import BootSplash from './BootSplash.svelte';
import '$styles/common.scss';

let { children } = $props();

// Hydration signal for end-to-end tests. The form markup is server-rendered, so it is present and
// clickable before the client bundle has hydrated and wired up event handlers; a click landing in
// that window is silently dropped. This onMount runs after every child component's onMount (Svelte
// mounts children first), so `data-hydrated="true"` on the root marks the whole page as interactive.
let hydrated = $state(false);
onMount(() => {
	hydrated = true;
});

// Surface otherwise-silent WebSocket failures to the player. Registered during
// layout init (the root layout lives for the whole app session), so we opt into
// the hook's onDestroy cleanup; a single subscription covers every screen.
onSocketError((message) => toastError(message), true);

// Boot gate. On a fresh page load (refresh or deep link) the in-memory game state is gone, so we
// attempt to restore the session and decide where to land — keeping a fully-restored session on the
// route it loaded on (so refreshing /admin stays on /admin), the loading screen when something must
// download, or the login screen otherwise — showing a lightweight splash while we work it out rather
// than flashing the loading manifest or the login form. `booting` starts false so it matches the
// server render (no hydration mismatch) and is flipped on only on the client, where the tokens live.
let booting = $state(false);
// True once the boot decision has resolved; until then the redirect guard below stays inert so it
// can't bounce us off a protected route mid-restore.
let booted = $state(false);

onMount(async () => {
	try {
		if (!getTokens()) {
			// No stored session: a refresh on a protected route still needs to land on login.
			if (page.url.pathname !== '/') {
				await goto(resolve('/'));
			}
			return;
		}

		booting = true;
		const destination = await resumeSession();
		const redirect = bootRedirect(destination, page.url.pathname);
		if (redirect === 'game') {
			await goto(resolve('/game'));
		} else if (redirect === 'loading') {
			await goto(resolve('/loading'));
		} else if (redirect === 'login') {
			await goto(resolve('/'));
		}
	} finally {
		// Always clear the splash and arm the guard, even if resume/navigation throws, so the boot
		// gate can never strand the player on the splash.
		booting = false;
		booted = true;
	}
});

// Safety net once booted: if the in-memory player state is lost (e.g. an auth failure tore it down)
// while on a protected route, return to login. Inert during boot and on the pre-player auth routes
// (login + character-select), where arriving without a loaded player is expected — the player isn't
// loaded until a character is selected. `redirecting` latches the in-flight navigation so a reactive
// re-run (a tracked dep changing before `pathname` settles) can't fire a redundant second goto; it
// clears once the navigation completes.
let redirecting = false;
$effect(() => {
	if (booted && !redirecting && shouldReturnToLogin(!!playerManager.name, page.url.pathname)) {
		redirecting = true;
		goto(resolve('/')).finally(() => {
			redirecting = false;
		});
	}
});
</script>

<style lang="scss">
@use '../styles/colors.scss';

.page-base {
	height: 100vh;
	user-select: none;
	background: colors.$dark-light-gray-grad;
	color: colors.$text-primary;
	font-family: var(--sans);

	--border-radius: 3px;
	--default-glow-color: #{colors.$accent};
	--btn-background: #{colors.$accent};
	--spinner-color: #{colors.$accent};
	--title-color: #{colors.$off-white};
	--default-border: 1px solid #{colors.$black};
	--error-color: #{colors.$error};
	--health-missing-color: #{colors.$hp-red-bg};
	--health-remaining-color: #{colors.$hp-green};
	--health-remaining-dark: #{colors.$hp-green-dark};
	--block-color: #{colors.$hp-green};
	--health-disappearing-color: #{colors.$hp-red-disappearing};
	--slot-background-color: #{colors.$white};
	--surface: #{colors.$surface};
	--surface-alpha: #{colors.$surface-alpha};
	// Tooltip panel background. The surface tint percentage is the configurable
	// opacity knob — lower it for a more see-through tooltip — and flows through
	// theme overrides. Paired with the container's backdrop blur for legibility.
	--tooltip-bg: color-mix(in srgb, var(--surface) 92%, transparent);
	--page: #{colors.$page};
	--panel: #{colors.$panel};
	--panel-2: #{colors.$panel-2};
	--mono: 'Geist Mono', ui-monospace, monospace;
	--sans: Geist, system-ui, sans-serif;
	--enemy: #{colors.$enemy-accent};
	--error: #{colors.$error};
	--accent: #{colors.$accent};
	--accent-light: #{colors.$accent-light};
	--text-primary: #{colors.$text-primary};
	--text-secondary: #{colors.$text-secondary};
	--text-tertiary: #{colors.$text-tertiary};
	--text-muted: #{colors.$text-muted};
	--text-on-accent: #{colors.$text-on-accent};
	// Base neutrals — the opaque white/black that the translucent line/fill blends
	// (color-mix(in srgb, var(--white|--black) N%, transparent)) flow from.
	--white: #{colors.$white};
	--black: #{colors.$black};
	// Scrollbar theming. The OS-default scrollbar renders as a bright, off-theme
	// bar on the dark surfaces (most noticeable on the Statistics and Options
	// screens). These neutral translucent-white tokens drive the app-wide themed
	// scrollbar (the global default in common.scss) and are reused by the combat
	// log panel, so a theme can restyle every scrollbar by overriding them alone.
	--scrollbar-thumb: color-mix(in srgb, var(--white) 16%, transparent);
	--scrollbar-thumb-hover: color-mix(in srgb, var(--white) 28%, transparent);
	--border-subtle: #{colors.$border-subtle};
	--border-light: #{colors.$border-light};
	--border-medium: #{colors.$border-medium};
	--success: #{colors.$success};
	--warning: #{colors.$warning};
	--enemy-accent: #{colors.$enemy-accent};
	// Gold reward/timing accent — card-game crit ticks and the draw-timer fill.
	--gold: #{colors.$gold};
	// Boss accent — the heightened gold treatment of the Challenge Boss fight. A
	// distinct functional token (see $boss-accent) so a theme can restyle boss
	// fights independently of the card-game timing gold.
	--boss-accent: #{colors.$boss-accent};

	// Editor change-state accents. The admin table editor and Workbench signal a
	// pending add / edit (unsaved) / remove with these hues. They currently match
	// --accent / --warning / --enemy-accent but are distinct functional concerns
	// (a row marked for removal is not an "enemy"; an unsaved edit is not a
	// validation "warning"), so they are declared separately and a theme can
	// restyle change-state affordances on their own.
	--change-added: #{colors.$accent};
	--change-modified: #{colors.$warning};
	--change-removed: #{colors.$enemy-accent};

	// Dead-letter ops severity accents. The admin Ops console classifies a dead-lettered
	// event by how worthwhile a replay is — poison (re-fails), poison-ish (unhandled type),
	// or replayable. They currently match --error / --warning / --accent but are a distinct
	// functional concern from validation/change-state, so they are declared separately and a
	// theme can restyle ops-severity affordances on their own.
	--dead-letter-poison: #{colors.$error};
	--dead-letter-warn: #{colors.$warning};
	--dead-letter-ok: #{colors.$accent};

	// Combat-log palette — semantic colours for log messages, shared by the log
	// panel, the options live preview, and the per-type rows (via `logColors` in
	// log-kind.ts). Most reuse a base token; only the softer enemy hue is its own.
	--log-player: var(--accent-light);
	--log-enemy: #{colors.$log-enemy};
	--log-loot: var(--success);
	--log-reward: var(--warning);
	--log-system: color-mix(in srgb, var(--text-primary) 70%, transparent);
	--log-effect: var(--effect-accent);
	// Mono section-label / eyebrow accent (a muted accent-light), used across the
	// log + options screens.
	--eyebrow: color-mix(in srgb, var(--accent-light) 70%, transparent);

	// Skill-effect accents — the buff (beneficial) vs debuff (detrimental) hue of a
	// timed skill effect, and the neutral marker on a skill that carries effects.
	// Consumed via the helpers in $lib/common/skill-effect-display (the effect chips,
	// tooltip lines, and skill-button badge), so a theme can restyle effect signalling
	// independently of the success/enemy/accent base hues they currently borrow.
	--effect-buff: var(--success);
	--effect-debuff: var(--enemy-accent);
	--effect-accent: var(--accent-light);

	// Item-mod-type accents. Consumed via the helpers in $lib/common/item-display.
	--mod-component: #{colors.$mod-component};
	--mod-prefix: #{colors.$mod-prefix};
	--mod-suffix: #{colors.$mod-suffix};

	// Item-category accents. Consumed via the helpers in $lib/common/item-display.
	--category-armor: #{colors.$category-armor};
	--category-weapon: #{colors.$category-weapon};
	--category-accessory: #{colors.$category-accessory};

	// Tag-category accents (workbench tag UI). The hue is procedural per category,
	// but the oklch lightness/chroma and border/background alphas are themeable here.
	// Consumed via the tagColor helper in $lib/common/tag-color.
	--tag-lightness: 0.85;
	--tag-chroma: 0.06;
	--tag-border-alpha: 0.45;
	--tag-bg-alpha: 0.12;

	// Challenge-type accents (one per EChallengeType). Consumed via the helpers
	// in $lib/common/challenge-type.
	--challenge-enemies-killed: #{colors.$challenge-enemies-killed};
	--challenge-bosses-defeated: #{colors.$challenge-bosses-defeated};
	--challenge-zones-cleared: #{colors.$challenge-zones-cleared};
	--challenge-time-trial: #{colors.$challenge-time-trial};
	--challenge-level-reached: #{colors.$challenge-level-reached};
	--challenge-damage-dealt: #{colors.$challenge-damage-dealt};
	--challenge-battles-won: #{colors.$challenge-battles-won};
	--challenge-skills-used: #{colors.$challenge-skills-used};

	// Rarity palette + per-tier glow intensity (0–1, unitless). Consumed by the
	// inventory and admin rarity UI via the shared helpers in $lib/common/rarity.
	--rarity-common: #{colors.$rarity-common};
	--rarity-uncommon: #{colors.$rarity-uncommon};
	--rarity-rare: #{colors.$rarity-rare};
	--rarity-epic: #{colors.$rarity-epic};
	--rarity-legendary: #{colors.$rarity-legendary};
	--rarity-mythic: #{colors.$rarity-mythic};
	--rarity-common-glow: 0;
	--rarity-uncommon-glow: 0.22;
	--rarity-rare-glow: 0.38;
	--rarity-epic-glow: 0.54;
	--rarity-legendary-glow: 0.72;
	--rarity-mythic-glow: 0.9;

	// Core-attribute accents (one per core EAttribute 0..5). Consumed via the
	// helpers in $lib/common/attribute-display.
	--attr-strength: #{colors.$attr-strength};
	--attr-endurance: #{colors.$attr-endurance};
	--attr-intellect: #{colors.$attr-intellect};
	--attr-agility: #{colors.$attr-agility};
	--attr-dexterity: #{colors.$attr-dexterity};
	--attr-luck: #{colors.$attr-luck};

	// Attribute-modifier-source accents (one per EAttributeModifierSource shown in
	// the breakdown). Consumed via the helpers in the attribute-breakdown screen's
	// source-display.
	--source-base: #{colors.$source-base};
	--source-points: #{colors.$source-points};
	--source-item: #{colors.$source-item};
	--source-mod: #{colors.$source-mod};
	--source-derived: #{colors.$source-derived};
}
</style>
