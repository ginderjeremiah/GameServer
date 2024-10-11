<div class="item-slot" {style} role="presentation" ondragend={onDragEnd} onclick={onClick}>
	{#if item}
		<img class="item-img" {src} {draggable} {alt} ondragstart={onDragStart} />
	{/if}
</div>

<script lang="ts">
import type { IItem } from '$lib/api';
import type { Action } from '$lib/common';

type Props = {
	hideBottomBorder?: boolean;
	hideRightBorder?: boolean;
	undraggable?: boolean;
	item: IItem | undefined;
	onDragStart?: Action<MouseEvent>;
	onDragEnd?: Action<MouseEvent>;
	onClick?: Action<MouseEvent>;
};

const {
	hideBottomBorder = false,
	hideRightBorder = false,
	undraggable = false,
	item,
	onDragStart,
	onDragEnd,
	onClick
}: Props = $props();

const src = $derived(item?.iconPath);
const alt = $derived(item?.name);
const draggable = $derived(!undraggable);

const style = $derived.by(() => {
	if (hideBottomBorder && hideRightBorder) {
		return 'border-right: none; border-bottom: none;';
	} else if (hideRightBorder) {
		return 'border-right: none;';
	} else if (hideBottomBorder) {
		return 'border-bottom: none;';
	}
});
</script>

<style lang="scss">
.item-slot {
	background-color: var(--slot-background-color);
	box-sizing: border-box;
	border: var(--default-border);
	width: var(--slot-width);
	height: var(--slot-width);

	.item-img {
		width: calc(var(--slot-width) - 2px);
		width: calc(var(--slot-width) - 2px);
	}
}
</style>
