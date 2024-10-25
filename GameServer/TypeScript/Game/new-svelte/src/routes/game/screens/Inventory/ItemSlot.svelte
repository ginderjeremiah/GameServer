<div
	class={slotClass}
	{style}
	role="presentation"
	ondrop={handleDrop}
	onclick={onClick}
	ondragover={onDragOver}
	ondragleave={onDragLeave}
>
	<div class="img-clipper">
		{#if item}
			<img
				class="item-img"
				{src}
				{draggable}
				{alt}
				ondragstart={handleDragStart}
				ondragend={onDragEnd}
				onmousemove={onMouseMove}
				onmouseenter={onMouseEnter}
				onmouseleave={onMouseLeave}
			/>
		{/if}
	</div>
</div>

<script lang="ts">
import type { Action } from '$lib/common';
import { inventory, swapSlots, type InventorySlot } from '$stores';

type Props = {
	hideBottomBorder?: boolean;
	hideRightBorder?: boolean;
	undraggable?: boolean;
	slot: InventorySlot;
	onDragStart?: Action<DragEvent>;
	onDrop: Action<DragEvent>;
	onClick?: Action<MouseEvent>;
	onMouseMove?: Action<MouseEvent>;
	onMouseEnter?: Action<MouseEvent>;
	onMouseLeave?: Action<MouseEvent>;
};

const {
	hideBottomBorder = false,
	hideRightBorder = false,
	undraggable = false,
	slot,
	onDragStart,
	onDrop,
	onClick,
	onMouseEnter,
	onMouseLeave,
	onMouseMove
}: Props = $props();

let beingDragged = $state(false);
let draggedOver = $state(false);

const item = $derived(slot.item);
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

const slotClass = $derived.by(() => {
	let classes = ['item-slot'];
	if (beingDragged) {
		classes.push('darken');
	}
	if (draggedOver) {
		classes.push('highlight');
	}
	return classes.join(' ');
});

const handleDragStart = (ev: DragEvent) => {
	beingDragged = true;
	inventory.draggedSlot = slot;
	onDragStart?.(ev);
};

const handleDrop = (ev: DragEvent) => {
	ev.preventDefault();
	draggedOver = false;
	if (inventory.draggedSlot) {
		swapSlots(inventory.draggedSlot, slot);
	}
	onDrop?.(ev);
};

const onDragEnd = (ev: DragEvent) => {
	if (inventory.draggedSlot === slot) {
		inventory.draggedSlot = undefined;
	}
	beingDragged = false;
};

const onDragOver = (ev: DragEvent) => {
	if (inventory.draggedSlot && slot.canHold(inventory.draggedSlot.item)) {
		ev.preventDefault();
		draggedOver = !beingDragged;
	}
};

const onDragLeave = (ev: DragEvent) => {
	draggedOver = false;
};

const getDraggedSlot = (ev: DragEvent) => {
	const equipped = ev.dataTransfer?.getData('text/equippedSlot');
	if (equipped !== undefined && ev.dataTransfer?.getData('text/invItem') === 'true') {
		const slotNumber = parseInt(ev.dataTransfer.getData('text/plain'));
		return (equipped === 'true' ? inventory.equippedSlots : inventory.slots)[slotNumber];
	}
};
</script>

<style lang="scss">
.item-slot {
	background-color: var(--slot-background-color);
	box-sizing: border-box;
	border: var(--default-border);
	width: var(--slot-width);
	height: var(--slot-width);
	position: relative;

	.img-clipper {
		overflow: hidden;

		.item-img {
			width: calc(var(--slot-width) - 2px);
		}
	}
}

.darken {
	filter: brightness(50%);
}

.highlight {
	filter: brightness(80%);

	&::before {
		content: '';
		position: absolute;
		top: 0;
		left: 0;
		right: 0;
		bottom: 0;
		pointer-events: none;
		box-shadow:
			inset 0 0 0 1px transparent,
			inset 0 0 0 1px var(--slot-highlight-color);
	}
}
</style>
