<div role="tooltip" class="tooltip-container" {style} bind:this={container}></div>

<script lang="ts">
import type { TooltipData } from '$stores';

let { component, position, visible }: TooltipData = $props();

let container: HTMLDivElement;

$effect(() => {
	const comp = component();
	if (comp) {
		// eslint-disable-next-line svelte/no-dom-manipulating
		container.replaceChildren();
		// eslint-disable-next-line svelte/no-dom-manipulating
		container.appendChild(comp.getBaseNode());
	}
});

const style = $derived.by(() => {
	const mouseX = position?.x ?? 0;
	const mouseY = position?.y ?? 0;

	if (!container || !visible) {
		return;
	}

	let vertical = '';
	let horizontal = '';

	if (container.offsetWidth + mouseX + 15 < window.innerWidth) {
		horizontal = `left: ${mouseX + 15}px`;
	} else {
		horizontal = `right: ${window.innerWidth - mouseX + 15}px`;
	}

	if (container.offsetHeight + mouseY + 15 < window.innerHeight) {
		vertical = `top: ${mouseY + 15}px`;
	} else {
		vertical = `bottom: ${window.innerHeight - mouseY + 15}px`;
	}

	return `${vertical}; ${horizontal}; display: block;`;
});
</script>

<style lang="scss">
.tooltip-container {
	position: absolute;
	display: none;
	background: rgba(20, 21, 27, 0.96);
	border: 1px solid rgba(255, 255, 255, 0.14);
	border-radius: 3px;
	box-shadow:
		0 12px 28px rgba(0, 0, 0, 0.55),
		0 0 0 1px rgba(0, 0, 0, 0.4);
	backdrop-filter: blur(6px);
	z-index: 15;
	color: #f0f0f0;
	overflow: hidden;
}
</style>
