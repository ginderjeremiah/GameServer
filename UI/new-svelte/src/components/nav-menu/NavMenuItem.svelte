{#snippet childItems()}
	{#if children?.length}
		<div class="children-container">
			{#each children as child}
				<NavMenuItem {...child} />
			{/each}
		</div>
	{/if}
{/snippet}

{#if !!onClick}
	<button class="nav-menu-item" onclick={handleButtonClick}>
		{displayText}
		{@render childItems()}
	</button>
{:else}
	<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
	<div class="nav-menu-item" tabindex="0" onclick={handleDivClick} role="presentation">
		{displayText}
		{@render childItems()}
	</div>
{/if}

<script lang="ts">
import type { INavMenuItem } from './types';
import NavMenuItem from './NavMenuItem.svelte';
import type { MouseEventHandler } from 'svelte/elements';
import { normalizeText } from '$lib/common';

const props: INavMenuItem = $props();
const { text, children, onClick } = props;

const displayText = $derived(normalizeText(text));

const handleButtonClick: MouseEventHandler<HTMLButtonElement> = (ev) => {
	blurEventTarget(ev);
	onClick?.(ev, props);
};

const handleDivClick = (ev: MouseEvent) => {
	blurEventTarget(ev);
	ev.stopPropagation();
};

const blurEventTarget = (ev: MouseEvent) => {
	if (ev.target) {
		const target = ev.target as HTMLElement;
		target.blur();
	}
};
</script>

<style lang="scss">
button.nav-menu-item,
div.nav-menu-item {
	font-size: 1.5em;
	background-color: inherit;
	color: var(--title-color);
	text-shadow: var(--default-shadow);
	padding: 0.1rem 0.75rem;
	position: relative;
	text-wrap: nowrap;
	width: 100%;
	text-align: left;

	&:hover {
		background-color: var(--nav-bar-hover-color);
	}

	&:not(:hover, :focus, :active, :focus-visible, :focus-within) {
		.children-container {
			pointer-events: none;
			top: 90%;
			opacity: 0%;
		}
	}
}

div.nav-menu-item {
	cursor: default;
}

button.nav-menu-item {
	border: none;
	cursor: pointer;
	display: block;

	&:active {
		&:not(:has(*:active)) {
			background-color: var(--nav-bar-active-color);
		}
	}
}

.children-container {
	font-size: 0.75rem;
	position: absolute;
	padding: 0;
	top: 100%;
	left: 0;
	background-color: var(--nav-bar-color);
	min-width: 100%;
	opacity: 100%;
	z-index: 11;
	transition:
		opacity 0.2s linear,
		top 0.2s linear;
}
</style>
