<div id={tooltipElementId(id)} role="tooltip" class="tooltip-container" bind:this={container}>
	<!-- The tooltip content is rendered directly into the container, so it is not present in the markup here. -->
</div>

<script lang="ts">
import { tooltipElementId, type TooltipData } from '$stores';

// A stable `id` + `role="tooltip"` lets a focusable trigger reference this container via
// `aria-describedby` (see `describedByTooltip`) so its explanation is announced on focus.
let { id, component, position, visible }: TooltipData = $props();

let container = $state<HTMLDivElement>();

$effect(() => {
	const node = component()?.getBaseNode();
	// Guard `container`: the effect tracks `component`, which can change before the bind:this
	// binding establishes, so the node could be present while `container` is still unbound.
	if (node && container) {
		// eslint-disable-next-line svelte/no-dom-manipulating
		container.replaceChildren();
		// eslint-disable-next-line svelte/no-dom-manipulating
		container.appendChild(node);
	}
});

// Positioning is an $effect writing inline styles (not a derived style attribute): the edge-flip
// logic needs the panel's rendered size, which measures 0x0 until `display: block` reaches the DOM.
// A derived evaluates before the DOM updates, so the first show — the only positioning a
// keyboard-focus trigger gets — would never flip and could clip offscreen at viewport edges.
$effect(() => {
	const mouseX = position?.x ?? 0;
	const mouseY = position?.y ?? 0;

	if (!container) {
		return;
	}

	if (!visible) {
		// Clear the inline display so the scoped `display: none` takes back over.
		container.style.display = '';
		return;
	}

	// Make the panel renderable first so offsetWidth/offsetHeight measure the real box.
	container.style.display = 'block';

	if (container.offsetWidth + mouseX + 15 < window.innerWidth) {
		container.style.left = `${mouseX + 15}px`;
		container.style.right = '';
	} else {
		container.style.right = `${window.innerWidth - mouseX + 15}px`;
		container.style.left = '';
	}

	if (container.offsetHeight + mouseY + 15 < window.innerHeight) {
		container.style.top = `${mouseY + 15}px`;
		container.style.bottom = '';
	} else {
		container.style.bottom = `${window.innerHeight - mouseY + 15}px`;
		container.style.top = '';
	}
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
