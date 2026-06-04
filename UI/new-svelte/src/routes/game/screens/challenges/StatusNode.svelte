<div class="status-node" style:width="{size}px" style:height="{size}px">
	<div
		class="node-diamond"
		style:border="1px solid {state === 'locked' ? 'var(--border-light)' : tintColor(col, 0.7)}"
		style:background={state === 'done'
			? tintColor(col, 0.16)
			: state === 'active'
				? tintColor(col, 0.1)
				: 'transparent'}
		style:box-shadow={state === 'locked' ? 'none' : `0 0 8px ${tintColor(col, 0.45)}`}
	></div>
	{#if state === 'done'}
		<svg
			width={size * 0.5}
			height={size * 0.5}
			viewBox="0 0 16 16"
			fill="none"
			stroke={col}
			stroke-width="2"
			stroke-linecap="round"
			stroke-linejoin="round"
			class="node-check"
		>
			<path d="M3 8.5l3.5 3.5L13 4.5" />
		</svg>
	{:else}
		<div
			class="node-dot"
			style:background={state === 'active' ? col : 'transparent'}
			style:border={state === 'locked' ? '1px solid var(--text-muted)' : 'none'}
		></div>
	{/if}
</div>

<script lang="ts">
import { tintColor } from '$lib/common';
import type { ChallengeState } from './challenges-view.svelte';

interface Props {
	state: ChallengeState;
	accent: string;
	size?: number;
}

const { state, accent, size = 22 }: Props = $props();

const col = $derived(state === 'done' ? 'var(--success)' : state === 'active' ? accent : 'var(--text-muted)');
</script>

<style lang="scss">
.status-node {
	flex-shrink: 0;
	position: relative;
	display: flex;
	align-items: center;
	justify-content: center;
}

.node-diamond {
	position: absolute;
	inset: 0;
	transform: rotate(45deg);
	border-radius: 2px;
}

.node-check {
	position: relative;
}

.node-dot {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
	position: relative;
}
</style>
