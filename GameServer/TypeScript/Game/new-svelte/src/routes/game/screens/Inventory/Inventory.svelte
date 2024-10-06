<div class="inventory-screen">
	<div class="equipped-panel">
		<EquippedItems />
		<InventoryItems />
	</div>
</div>

<script lang="ts">
import EquippedItems from './EquippedItems.svelte';
import InventoryItems from './InventoryItems.svelte';
</script>

<style lang="scss">
.inventory-screen {
	--slot-width: calc((74vw - 6px) / 12);

	.equipped-panel {
		margin-top: 2vh;
		display: flex;
	}
}
</style>
