<div
	class={slotClass}
	{style}
	role="presentation"
	ondrop={handleDrop}
	onclick={handleClick}
	ondragover={handleDragOver}
	ondragleave={handleDragLeave}
>
	<div class="img-clipper">
		{#if item}
			<img
				class="item-img"
				{src}
				{draggable}
				{alt}
				ondragstart={handleDragStart}
				ondragend={handleDragEnd}
				onmousemove={handleMouseMove}
				onmouseenter={handleMouseEnter}
				onmouseleave={handleMouseLeave}
			/>
		{/if}
	</div>
</div>

<script lang="ts">
import type { Action } from '$lib/common';
import { inventoryManager, type InventorySlot } from '$lib/engine';

type Props = {
	hideBottomBorder?: boolean;
	hideRightBorder?: boolean;
	undraggable?: boolean;
	slot: InventorySlot;
	onDragStart?: Action<[DragEvent, InventorySlot]>;
	onDrop: Action<[DragEvent, InventorySlot]>;
	onClick?: Action<[MouseEvent, InventorySlot]>;
	onMouseMove?: Action<[MouseEvent, InventorySlot]>;
	onMouseEnter?: Action<[MouseEvent, InventorySlot]>;
	onMouseLeave?: Action<[MouseEvent, InventorySlot]>;
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
	inventoryManager.draggedSlot = slot;
	onDragStart?.(ev, slot);
};

const handleDrop = (ev: DragEvent) => {
	ev.preventDefault();
	draggedOver = false;
	if (inventoryManager.draggedSlot) {
		inventoryManager.swapSlots(inventoryManager.draggedSlot, slot);
	}
	onDrop?.(ev, slot);
};

const handleDragEnd = (ev: DragEvent) => {
	if (inventoryManager.draggedSlot === slot) {
		inventoryManager.draggedSlot = undefined;
	}
	beingDragged = false;
};

const handleDragOver = (ev: DragEvent) => {
	if (inventoryManager.draggedSlot && slot.canHold(inventoryManager.draggedSlot.item)) {
		ev.preventDefault();
		draggedOver = !beingDragged;
	}
};

const handleDragLeave = (ev: DragEvent) => {
	draggedOver = false;
};

const handleClick = (ev: MouseEvent) => {
	onClick?.(ev, slot);
};

const handleMouseMove = (ev: MouseEvent) => {
	onMouseMove?.(ev, slot);
};
const handleMouseEnter = (ev: MouseEvent) => {
	onMouseEnter?.(ev, slot);
};
const handleMouseLeave = (ev: MouseEvent) => {
	onMouseLeave?.(ev, slot);
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
