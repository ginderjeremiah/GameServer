<!-- role="meter" gives the strength a non-visual, text equivalent so the level isn't conveyed by
	colour alone (WCAG 1.4.1). aria-valuetext announces the human-readable label, e.g. "Strong". -->
<div
	class="strength-meter"
	data-testid="strength-meter"
	role="meter"
	aria-label="Password strength"
	aria-valuemin={0}
	aria-valuemax={4}
	aria-valuenow={score}
	aria-valuetext={label}
>
	{#each [0, 1, 2, 3] as i (i)}
		<div class="strength-segment" style:background={i < score ? fillColor : EMPTY_COLOR}></div>
	{/each}
</div>

<script lang="ts">
const EMPTY_COLOR = 'color-mix(in srgb, var(--text-primary) 22%, transparent)';

interface Props {
	/** Number of filled segments, 0–4 (see {@link passwordStrength}). */
	score: number;
	/** Human-readable strength label announced to screen readers (see {@link passwordStrength}). */
	label: string;
}

let { score, label }: Props = $props();

const fillColor = $derived(score >= 3 ? 'var(--success)' : score >= 2 ? 'var(--warning)' : 'var(--error)');
</script>

<style lang="scss">
.strength-meter {
	display: flex;
	gap: 4px;
	justify-content: space-between;
	margin-top: 10px;
}

.strength-segment {
	flex: 1;
	height: 2px;
	transition: background 160ms;
}
</style>
