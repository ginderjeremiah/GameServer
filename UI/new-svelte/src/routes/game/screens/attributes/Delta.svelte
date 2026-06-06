{#if from === to}
	<span class="single">{formatNum(to)}</span>
{:else}
	<span class="delta">
		<span class="from">{formatNum(from)}</span>
		<svg width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="currentColor" stroke-width="1.4" class="arrow">
			<path d="M2 5h6M5.5 2.5L8 5l-2.5 2.5" stroke-linecap="round" stroke-linejoin="round" />
		</svg>
		<span class="to" class:up={to > from}>{formatNum(to)}</span>
	</span>
{/if}

<script lang="ts">
import { formatNum } from '$lib/common';

interface Props {
	from: number;
	to: number;
}

const { from, to }: Props = $props();
</script>

<style lang="scss">
.single {
	font-family: var(--mono);
	color: color-mix(in srgb, var(--text-primary) 45%, transparent);
}

.delta {
	display: inline-flex;
	align-items: center;
	gap: 5px;
	font-family: var(--mono);
}

.from {
	color: var(--text-muted);
}

.arrow {
	color: var(--text-muted);
	flex-shrink: 0;
}

.to {
	color: var(--enemy-accent);
	font-weight: 500;

	&.up {
		color: var(--accent);
	}
}
</style>
