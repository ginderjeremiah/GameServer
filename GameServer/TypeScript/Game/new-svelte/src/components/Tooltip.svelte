<div
	role="tooltip"
	class="tooltip-container round-border"
	style="{getPositionStyle($tooltip)}"
	bind:this="{container}"
></div>

<script lang="ts">
import type { ReadableEx } from '$lib/common';
import type { TooltipData } from '$stores/tooltip';
import { onMount } from 'svelte';

export let tooltip: ReadableEx<TooltipData>;

let container: HTMLDivElement;

const component = $tooltip.component;

$: if ($component) {
	container.appendChild($component.getBaseNode());
}

const getPositionStyle = (tooltip: TooltipData) => {
	if (!container || !tooltip.visible) {
		return;
	}

	const mouseX = tooltip.position?.x ?? 0;
	const mouseY = tooltip.position?.y ?? 0;

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
};
</script>

<style lang="scss">
.tooltip-container {
	position: absolute;
	border: var(--default-border);
	display: none;
	background-color: var(--default-title-color);
	padding: 0.1rem;
	z-index: 15;
}
</style>
