<div role="tooltip" class="tooltip-container round-border" {style} bind:this={container}></div>

<script lang="ts">
import type { TooltipData } from '$stores';

let { component, position, visible }: TooltipData = $props();

let container: HTMLDivElement;

$effect(() => {
	const comp = component();
	if (comp) {
		container.replaceChildren();
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
	border: var(--default-border);
	display: none;
	background-color: var(--container-background-color);
	padding: 0.1rem;
	z-index: 15;
	box-shadow: var(--default-shadow);
}
</style>
