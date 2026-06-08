<div class="manifest-window" data-testid="manifest-window" bind:this={manifestEl}>
	<div class="manifest-track" style:transform="translateY({animatedY}px)">
		{#each items as item, i (item.key)}
			<ManifestRow {item} active={i === activeIndex} opacity={rowOpacity(i)} />
		{/each}
	</div>
	{#if phase !== 'done' && phase !== 'checking'}
		<div class="active-indicator"></div>
	{/if}
</div>

<script lang="ts">
import { onMount } from 'svelte';
import ManifestRow from './ManifestRow.svelte';
import type { LoadItem, Phase } from './loading-view.svelte';

type Props = {
	items: LoadItem[];
	activeIndex: number;
	phase: Phase;
};

let { items, activeIndex, phase }: Props = $props();

const ROW_HEIGHT = 42;

// The window keeps the active row centred (slot 2 of the five visible rows);
// the wheel lets the player nudge the manifest within its bounds.
let manifestEl: HTMLElement;
let scrollOffset = $state(0);
let animatedY = $state(3 * ROW_HEIGHT); // matches the initial cursorY (activeIndex = -1)
let rafId = 0;

const cursorY = $derived((2 - activeIndex) * ROW_HEIGHT);
const maxOffset = $derived(2 * ROW_HEIGHT - cursorY);
const minOffset = $derived(items.length > 0 ? (3 - items.length) * ROW_HEIGHT - cursorY : 0);
const targetY = $derived(cursorY + scrollOffset);

$effect(() => {
	const target = targetY;
	cancelAnimationFrame(rafId);
	function step() {
		const diff = target - animatedY;
		if (Math.abs(diff) < 0.5) {
			animatedY = target;
			return;
		}
		animatedY += diff * 0.18;
		rafId = requestAnimationFrame(step);
	}
	rafId = requestAnimationFrame(step);
	return () => cancelAnimationFrame(rafId);
});

const rowOpacity = (i: number) => {
	const visualCenter = 2 - animatedY / ROW_HEIGHT;
	const distance = Math.abs(i - visualCenter);
	if (distance > 2) {
		return 0;
	}
	return 1 - distance * 0.18;
};

const handleWheel = (e: WheelEvent) => {
	e.preventDefault();
	const step = e.deltaY > 0 ? -ROW_HEIGHT : ROW_HEIGHT;
	scrollOffset = Math.max(minOffset, Math.min(maxOffset, scrollOffset + step));
};

onMount(() => {
	manifestEl.addEventListener('wheel', handleWheel, { passive: false });
	return () => manifestEl.removeEventListener('wheel', handleWheel);
});
</script>

<style lang="scss">
.manifest-window {
	margin-top: 18px;
	height: calc(42px * 5);
	position: relative;
	overflow: hidden;
	mask-image: linear-gradient(to bottom, transparent 0%, black 22%, black 78%, transparent 100%);
	-webkit-mask-image: linear-gradient(to bottom, transparent 0%, black 22%, black 78%, transparent 100%);
}

.manifest-track {
	position: absolute;
	left: 0;
	right: 0;
	transition: none;
}

.active-indicator {
	position: absolute;
	left: 0;
	right: 0;
	top: calc(42px * 2);
	height: 42px;
	border: 1px solid color-mix(in srgb, var(--accent) 18%, transparent);
	background: color-mix(in srgb, var(--accent) 5%, transparent);
	border-radius: 3px;
	pointer-events: none;
}
</style>
