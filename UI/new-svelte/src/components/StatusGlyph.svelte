<!--
	Minimal status glyph drawn from primitives (check / cross / triangle / circle), one per ToastType.
	Strokes in `currentColor` so the parent controls the hue; decorative, so it is hidden from
	assistive tech (the adjacent status label carries the meaning).
-->
<svg
	class="status-glyph"
	width={size}
	height={size}
	viewBox="0 0 16 16"
	fill="none"
	stroke="currentColor"
	stroke-width="1.6"
	stroke-linecap="round"
	stroke-linejoin="round"
	aria-hidden="true"
>
	{#if type === 'success'}
		<path d="M3.5 8.5l3 3 6-6.5" />
	{:else if type === 'error'}
		<path d="M4 4l8 8M12 4l-8 8" />
	{:else if type === 'warning'}
		<path d="M8 2.5l5.5 10.5h-11z" />
		<path d="M8 6.5v3.2" />
		<path d="M8 11.4v.1" />
	{:else}
		<circle cx="8" cy="8" r="5.5" />
		<path d="M8 7.2v3.6" />
		<path d="M8 5.1v.1" />
	{/if}
</svg>

<script lang="ts">
import type { ToastType } from '$stores';

interface Props {
	type: ToastType;
	size?: number;
}

let { type, size = 14 }: Props = $props();
</script>

<style lang="scss">
.status-glyph {
	display: block;
	flex-shrink: 0;
}
</style>
