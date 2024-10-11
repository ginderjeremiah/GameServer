<div class="inventory-items-container">
	<div class="inventory-items-inner">
		<div class="inventory-slots-subcontainer">
			{#each test as item, index}
				<ItemSlot
					{item}
					hideBottomBorder={index < 18}
					hideRightBorder={index % 9 !== 8}
					onDragStart={() => console.log(item, index)}
					onDragEnd={() => console.log(item, index)}
					onClick={() => console.log(item, index)}
				/>
			{/each}
		</div>
		<div class="inventory-bottom">
			<ItemSlot
				item={trashItem}
				undraggable
				onDragEnd={() => console.log('trash drag end')}
				onClick={() => console.log('trash click')}
			/>
		</div>
	</div>
	<!-- ItemTooltip -->
</div>

<script lang="ts">
import { staticData } from '$stores';
import ItemSlot from './ItemSlot.svelte';
const trashItem = staticData.items[0];
</script>

<style lang="scss">
.inventory-items-container {
	margin: 0 5% 1rem 0;
	width: 60%;
	padding: 1rem;
	display: block;
	border: var(--default-border);
	border-width: 3px;
	border-radius: 1vw;
	background-color: var(--container-background-color);
	position: relative;

	.inventory-items-inner {
		margin: 0 auto;
		height: 100%;
		width: fit-content;
		display: flex;
		flex-direction: column;
		justify-content: space-between;

		.inventory-slots-subcontainer {
			width: calc(9 * var(--slot-width));
			display: flex;
			flex-wrap: wrap;
		}

		.inventory-bottom {
			display: flex;
			justify-content: flex-end;
		}
	}
}
</style>
