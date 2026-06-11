<!-- The effect lines of the skill tooltip: one row per authored effect, e.g. "+15 Strength · self · 5s",
     with the magnitude tinted by the buff/debuff direction. Purely presentational — the parent builds
     the descriptions (resolving attribute names from reference data) so this stays render-only. -->
<div class="effect-lines">
	{#each lines as line, index (index)}
		<div class="effect-line">
			<span class="mag" style:color={effectDirectionColor(line.direction)}>{line.magnitude}</span>
			<span class="attr">{line.attributeName}</span>
			<span class="meta">{line.targetLabel} · {line.duration}</span>
		</div>
	{/each}
</div>

<script lang="ts">
import { effectDirectionColor, type EffectDescription } from '$lib/common';

type Props = {
	lines: EffectDescription[];
};

const { lines }: Props = $props();
</script>

<style lang="scss">
.effect-lines {
	display: flex;
	flex-direction: column;
	gap: 5px;
}

.effect-line {
	display: flex;
	align-items: baseline;
	gap: 7px;
	font-size: 11.5px;
}

.mag {
	font-family: var(--mono);
	font-size: 11px;
	font-weight: 600;
}

.attr {
	color: var(--text-secondary);
}

.meta {
	margin-left: auto;
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.4px;
	color: var(--text-muted);
	white-space: nowrap;
}
</style>
