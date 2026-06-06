<div class="ring" style:width="{size}px" style:height="{size}px">
	<svg width={size} height={size} style="transform: rotate(-90deg)">
		<circle
			cx={size / 2}
			cy={size / 2}
			r={radius}
			fill="none"
			stroke="color-mix(in srgb, var(--white) 9%, transparent)"
			stroke-width={stroke}
		/>
		<circle
			cx={size / 2}
			cy={size / 2}
			r={radius}
			fill="none"
			stroke={col}
			stroke-width={stroke}
			stroke-dasharray={circumference}
			stroke-dashoffset={circumference * (1 - Math.max(0, Math.min(100, pct)) / 100)}
			stroke-linecap="round"
			style:filter="drop-shadow(0 0 3px {tintColor(col, 0.6)})"
			style:transition="stroke-dashoffset 360ms ease"
		/>
	</svg>
	<div class="ring-center">
		{@render children?.()}
	</div>
</div>

<script lang="ts">
import type { Snippet } from 'svelte';
import { tintColor } from '$lib/common';

interface Props {
	pct: number;
	accent: string;
	size?: number;
	stroke?: number;
	done?: boolean;
	children?: Snippet;
}

const { pct, accent, size = 30, stroke = 3, done = false, children }: Props = $props();

const radius = $derived((size - stroke) / 2);
const circumference = $derived(2 * Math.PI * radius);
const col = $derived(done ? 'var(--success)' : accent);
</script>

<style lang="scss">
.ring {
	position: relative;
	flex-shrink: 0;
}

.ring-center {
	position: absolute;
	inset: 0;
	display: flex;
	align-items: center;
	justify-content: center;
}
</style>
