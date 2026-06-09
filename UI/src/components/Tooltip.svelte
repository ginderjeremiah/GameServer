<div role="tooltip" class="tooltip-container" {style} bind:this={container}>
	<!-- The tooltip content is rendered directly into the container, so it is not present in the markup here. -->
</div>

<script lang="ts">
import type { TooltipData } from '$stores';

let { component, position, visible }: TooltipData = $props();

let container: HTMLDivElement;

$effect(() => {
	const node = component()?.getBaseNode();
	if (node) {
		// eslint-disable-next-line svelte/no-dom-manipulating
		container.replaceChildren();
		// eslint-disable-next-line svelte/no-dom-manipulating
		container.appendChild(node);
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
	// See-through panel driven by the themeable `--tooltip-bg` opacity knob; the
	// backdrop blur keeps content legible over busy backgrounds. The translucency
	// lives entirely in the background (not an element `opacity`) so the text and
	// accents stay crisp.
	background: var(--tooltip-bg);
	border: 1px solid var(--border-light);
	border-radius: var(--border-radius);
	box-shadow:
		0 12px 28px color-mix(in srgb, var(--black) 55%, transparent),
		0 0 0 1px color-mix(in srgb, var(--black) 40%, transparent);
	backdrop-filter: blur(6px);
	z-index: 15;
	color: var(--text-primary);
	overflow: hidden;
}
</style>
