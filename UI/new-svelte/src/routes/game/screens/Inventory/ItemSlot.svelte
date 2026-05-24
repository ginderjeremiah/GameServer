<div
	class={slotClass}
	role="button"
	tabindex="0"
	onclick={handleClick}
	onkeydown={handleKeydown}
>
	<div class="img-clipper">
		{#if item}
			<img
				class="item-img"
				src={item.iconPath}
				alt={item.name}
				draggable="false"
				onmousemove={handleMouseMove}
				onmouseenter={handleMouseEnter}
				onmouseleave={handleMouseLeave}
			/>
		{/if}
	</div>
</div>

<script lang="ts">
import type { Action } from '$lib/common';
import type { Item } from '$lib/battle';

type Props = {
	item?: Item;
	onClick?: Action<[]>;
	onMouseMove?: Action<[MouseEvent]>;
	onMouseEnter?: Action<[MouseEvent]>;
	onMouseLeave?: Action<[MouseEvent]>;
};

const {
	item,
	onClick,
	onMouseEnter,
	onMouseLeave,
	onMouseMove
}: Props = $props();

const slotClass = $derived.by(() => {
	let classes = ['item-slot'];
	if (item?.equipped) {
		classes.push('equipped');
	}
	return classes.join(' ');
});

const handleClick = () => {
	onClick?.();
};

const handleKeydown = (ev: KeyboardEvent) => {
	if (ev.key === 'Enter' || ev.key === ' ') {
		ev.preventDefault();
		onClick?.();
	}
};

const handleMouseMove = (ev: MouseEvent) => {
	onMouseMove?.(ev);
};
const handleMouseEnter = (ev: MouseEvent) => {
	onMouseEnter?.(ev);
};
const handleMouseLeave = (ev: MouseEvent) => {
	onMouseLeave?.(ev);
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
	cursor: pointer;
	transition: filter 0.1s;

	&:hover {
		filter: brightness(120%);
	}

	.img-clipper {
		overflow: hidden;

		.item-img {
			width: calc(var(--slot-width) - 2px);
		}
	}
}

.equipped {
	border-color: gold;
	border-width: 2px;
}
</style>
