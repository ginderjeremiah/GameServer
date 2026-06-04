<div class="sort-control">
	<span class="sort-label">Sort</span>
	<div class="sort-options">
		{#each SORT_OPTIONS as opt, i (opt.key)}
			<button
				class="sort-option"
				class:active={value === opt.key}
				class:divided={i > 0}
				onclick={() => onChange(opt.key)}
			>
				{opt.label}
			</button>
		{/each}
	</div>
</div>

<script lang="ts">
import { SORT_OPTIONS, type SortKey } from './challenges-view.svelte';

interface Props {
	value: SortKey;
	onChange: (sort: SortKey) => void;
}

const { value, onChange }: Props = $props();
</script>

<style lang="scss">
.sort-control {
	display: flex;
	align-items: center;
	gap: 9px;
}

.sort-label {
	font-family: 'Geist Mono', monospace;
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.sort-options {
	display: flex;
	border: 1px solid var(--border-subtle);
	border-radius: 2px;
	overflow: hidden;
}

.sort-option {
	padding: 4px 11px;
	font-family: Geist, sans-serif;
	font-size: 11px;
	cursor: pointer;
	border: none;
	background: transparent;
	color: var(--text-tertiary);
	transition: all 120ms;

	&.divided {
		border-left: 1px solid var(--border-subtle);
	}

	&.active {
		background: color-mix(in srgb, var(--accent) 16%, transparent);
		color: var(--text-primary);
	}
}
</style>
