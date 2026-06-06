<!-- The top-level "By statistic / By entity" segmented control. Filled (not
     underline) so it reads as a higher-level switch than the sub-tabs below it,
     plus a one-line hint describing the active view. -->
<div class="toggle-row">
	<div class="segmented" role="tablist">
		{#each options as opt (opt.key)}
			<button
				type="button"
				class="seg"
				class:on={mode === opt.key}
				role="tab"
				aria-selected={mode === opt.key}
				data-testid="view-{opt.key}"
				onclick={() => onChange(opt.key)}
			>
				{opt.label}
			</button>
		{/each}
	</div>
	<span class="hint">
		{mode === 'stat'
			? 'Pick a statistic to see its per-entity breakdown — click any entity to pivot to it.'
			: 'Pick an entity to see every statistic that references it — click any stat to jump back.'}
	</span>
</div>

<script lang="ts">
import type { ViewMode } from './statistics-view.svelte';

interface Props {
	mode: ViewMode;
	onChange: (mode: ViewMode) => void;
}

let { mode, onChange }: Props = $props();

const options: { key: ViewMode; label: string }[] = [
	{ key: 'stat', label: 'By statistic' },
	{ key: 'entity', label: 'By entity' }
];
</script>

<style lang="scss">
.toggle-row {
	display: flex;
	align-items: center;
	gap: 14px;
	margin-bottom: 15px;
	flex-wrap: wrap;
}

.segmented {
	display: inline-flex;
	gap: 3px;
	padding: 3px;
	background: color-mix(in srgb, var(--white) 4%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 6px;
}

.seg {
	padding: 6px 16px;
	border: none;
	border-radius: 4px;
	cursor: pointer;
	background: transparent;
	color: var(--text-tertiary);
	font-family: var(--sans);
	font-size: 12.5px;
	font-weight: 500;
	transition: all 120ms;

	&.on {
		background: var(--accent);
		color: var(--text-on-accent);
		font-weight: 600;
	}
}

.hint {
	font-family: var(--mono);
	font-size: 9.5px;
	color: var(--text-muted);
}
</style>
