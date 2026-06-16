<!-- Challenge progress bar: the shared Bar primitive accented by challenge type (or success once
     done), with a position cursor overlaid at the fill edge while in progress. -->
<Bar
	value={percent}
	--bar-height={barHeight}
	--bar-radius={barRadius}
	--bar-track-bg="color-mix(in srgb, var(--white) 7%, transparent)"
	--bar-fill={fill}
	--bar-fill-shadow={fillShadow}
	--bar-transition="width 320ms ease"
>
	{#if !done && clamped > 3 && clamped < 99}
		<div
			class="bar-cursor"
			style:left="calc({clamped}% - 1px)"
			style:background={col}
			style:box-shadow="0 0 6px {col}"
		></div>
	{/if}
</Bar>

<script lang="ts">
import { tintColor } from '$lib/common';
import { Bar } from '$components';

interface Props {
	percent: number;
	accent: string;
	done?: boolean;
	height?: number;
}

const { percent, accent, done = false, height = 5 }: Props = $props();

const clamped = $derived(Math.max(0, Math.min(100, percent)));
const col = $derived(done ? 'var(--success)' : accent);
const fill = $derived(`linear-gradient(90deg, ${tintColor(col, 0.85)}, ${tintColor(col, 0.45)})`);
const fillShadow = $derived(`0 0 8px ${tintColor(col, 0.5)}`);
const barHeight = $derived(`${height}px`);
const barRadius = $derived(`${height / 2}px`);
</script>

<style lang="scss">
.bar-cursor {
	position: absolute;
	top: -1px;
	bottom: -1px;
	width: 2px;
}
</style>
