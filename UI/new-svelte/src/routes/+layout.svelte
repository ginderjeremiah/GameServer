<div class="page-base">
	{@render children()}
	<TooltipBase />
	<ToastContainer />
</div>

<script lang="ts">
import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { page } from '$app/state';
import { TooltipBase, ToastContainer } from '$components';
import { onSocketError } from '$lib/api';
import { playerManager } from '$lib/engine';
import { toastError } from '$stores';
import '$styles/common.scss';

let { children } = $props();

// Surface otherwise-silent WebSocket failures to the player. Registered during
// layout init (the root layout lives for the whole app session), so the hook's
// onDestroy cleanup is valid and a single subscription covers every screen.
onSocketError((message) => toastError(message));

$effect(() => {
	if (!playerManager.name && page.url.pathname !== '/') {
		goto(resolve('/'));
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
	font-family: Geist, Arial, Helvetica, sans-serif;

	--border-radius: 3px;
	--default-glow-color: #{colors.$accent};
	--btn-background: #{colors.$accent};
	--spinner-color: #{colors.$accent};
	--title-color: #{colors.$off-white};
	--default-border: 1px solid #{colors.$black};
	--error-color: #{colors.$error};
	--health-missing-color: #{colors.$hp-red-bg};
	--health-remaining-color: #{colors.$hp-green};
	--health-disappearing-color: #{colors.$hp-red-disappearing};
	--slot-background-color: #{colors.$white};
	--surface: #{colors.$surface};
	--surface-alpha: #{colors.$surface-alpha};
	--page: #0f1014;
	--panel: #16171e;
	--panel-2: #1b1c24;
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
	--border-subtle: #{colors.$border-subtle};
	--border-light: #{colors.$border-light};
	--border-medium: #{colors.$border-medium};
	--success: #{colors.$success};
	--warning: #{colors.$warning};
	--enemy-accent: #{colors.$enemy-accent};

	// Item-mod-type accents. Consumed via the helpers in $lib/common/item-display.
	--mod-component: #{colors.$mod-component};
	--mod-prefix: #{colors.$mod-prefix};
	--mod-suffix: #{colors.$mod-suffix};

	// Item-category accents. Consumed via the helpers in $lib/common/item-display.
	--category-armor: #{colors.$category-armor};
	--category-weapon: #{colors.$category-weapon};
	--category-accessory: #{colors.$category-accessory};

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
}
</style>
