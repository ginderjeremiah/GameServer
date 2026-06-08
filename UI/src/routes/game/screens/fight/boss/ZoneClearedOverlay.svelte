<!-- The "Zone Cleared" moment played over the arena after a dedicated-boss victory.
     Zone-navigation locking is deferred (#190), so this celebrates the clear without
     the mockup's (not-yet-truthful) "next zone unlocked" claim. -->
<div class="zone-cleared" data-testid="zone-cleared-overlay">
	<BossKicker size={11}>Zone Cleared</BossKicker>
	<div class="headline">{bossName} defeated</div>
	<div class="subline">
		<ClearedSeal />
		<span>{zoneName} cleared</span>
	</div>
	{#if autoFight}
		<div class="re-engage">Auto-fight · re-engaging…</div>
	{/if}
</div>

<script lang="ts">
import BossKicker from './BossKicker.svelte';
import ClearedSeal from './ClearedSeal.svelte';

type Props = {
	bossName: string;
	zoneName: string;
	autoFight: boolean;
};

const { bossName, zoneName, autoFight }: Props = $props();
</script>

<style lang="scss">
.zone-cleared {
	position: absolute;
	inset: 0;
	z-index: 20;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 10px;
	pointer-events: none;
	text-align: center;
	background: radial-gradient(
		ellipse at center,
		color-mix(in srgb, var(--surface) 55%, transparent),
		color-mix(in srgb, var(--black) 86%, transparent)
	);
	animation: zone-cleared-in 360ms ease-out;
}

.headline {
	font-size: 40px;
	font-weight: 600;
	color: var(--text-primary);
	letter-spacing: -0.6px;
	line-height: 1.05;
	text-shadow: 0 0 40px color-mix(in srgb, var(--boss-accent) 40%, transparent);
}

.subline {
	display: flex;
	align-items: center;
	gap: 12px;
	margin-top: 6px;
	font-family: var(--mono);
	font-size: 12px;
	letter-spacing: 0.8px;
	color: color-mix(in srgb, var(--text-primary) 78%, transparent);
}

.re-engage {
	margin-top: 4px;
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--text-primary) 50%, transparent);
}

@keyframes zone-cleared-in {
	from {
		opacity: 0;
	}
	to {
		opacity: 1;
	}
}
</style>
