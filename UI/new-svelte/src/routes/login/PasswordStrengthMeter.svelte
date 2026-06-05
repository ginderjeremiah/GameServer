<div class="strength-meter" data-testid="strength-meter">
	{#each [0, 1, 2, 3] as i (i)}
		<div class="strength-segment" style:background={i < score ? fillColor : EMPTY_COLOR}></div>
	{/each}
</div>

<script lang="ts">
const EMPTY_COLOR = 'color-mix(in srgb, var(--text-primary) 22%, transparent)';

/** Number of filled segments, 0–4 (see {@link passwordStrength}). */
let { score }: { score: number } = $props();

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
