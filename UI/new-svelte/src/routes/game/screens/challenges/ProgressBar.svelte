<div class="bar-track" style:height="{height}px" style:border-radius="{height / 2}px">
	<div
		class="bar-fill"
		style:width="{clamped}%"
		style:background="linear-gradient(90deg, {tintColor(col, 0.85)}, {tintColor(col, 0.45)})"
		style:box-shadow="0 0 8px {tintColor(col, 0.5)}"
	></div>
	{#if !done && clamped > 3 && clamped < 99}
		<div
			class="bar-cursor"
			style:left="calc({clamped}% - 1px)"
			style:background={col}
			style:box-shadow="0 0 6px {col}"
		></div>
	{/if}
</div>

<script lang="ts">
import { tintColor } from '$lib/common';

interface Props {
	percent: number;
	accent: string;
	done?: boolean;
	height?: number;
}

const { percent, accent, done = false, height = 5 }: Props = $props();

const clamped = $derived(Math.max(0, Math.min(100, percent)));
const col = $derived(done ? 'var(--success)' : accent);
</script>

<style lang="scss">
.bar-track {
	position: relative;
	overflow: hidden;
	background: color-mix(in srgb, var(--white) 7%, transparent);
}

.bar-fill {
	position: absolute;
	inset: 0;
	transition: width 320ms ease;
}

.bar-cursor {
	position: absolute;
	top: -1px;
	bottom: -1px;
	width: 2px;
}
</style>
