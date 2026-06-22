<!-- Shared skill-card stats sub-block: the dmg / cd / dps rows + the damage-multiplier chips.
     Consumed by both the editable EquippedBand and the read-only InnateBand so the two stay in
     sync (effective dmg/dps are defense-aware reads off the live view). -->
<div class="es">
	<div class="stat"><span class="v accent">{fmt(view.effective(skill.id))}</span><span class="k">dmg</span></div>
	<div class="stat"><span class="v">{metrics.cooldown.toFixed(1)}s</span><span class="k">cd</span></div>
	<div class="stat"><span class="v accent">{fmt(view.effectiveDps(skill.id))}</span><span class="k">dps</span></div>
</div>
<div class="chips">
	{#each skill.damageMultipliers as mult (mult.attributeId)}
		<AttributeChip attributeId={mult.attributeId} />
	{/each}
</div>

<script lang="ts">
import { formatNum } from '$lib/common';
import AttributeChip from '$components/AttributeChip.svelte';
import type { SkillsView, SkillMetrics } from './skills-view.svelte';

type Props = {
	view: SkillsView;
	metrics: SkillMetrics;
};

const { view, metrics }: Props = $props();

const skill = $derived(metrics.skill);
const fmt = (n: number) => formatNum(Math.round(n));
</script>

<style lang="scss">
.es {
	display: flex;
	gap: 13px;
	margin-top: auto;
}

.stat {
	display: flex;
	flex-direction: column;
	gap: 2px;

	.v {
		font-size: 15px;
		font-weight: 500;
		line-height: 1;

		&.accent {
			color: var(--accent);
		}
	}

	.k {
		font-family: var(--mono);
		font-size: 8px;
		letter-spacing: 1.2px;
		text-transform: uppercase;
		color: var(--text-muted);
	}
}

.chips {
	display: flex;
	flex-wrap: wrap;
	gap: 5px;
	// Sits above a host card's full-bleed overlay (EquippedBand) so each chip keeps its own hover
	// tooltip; the z-index is inert in bands without an overlay (InnateBand).
	position: relative;
	z-index: 2;
}
</style>
