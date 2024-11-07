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
	--slot-width: calc(6.5vw);

	.equipped-panel {
		margin-top: 2vh;
		display: flex;
		justify-content: space-between;
	}
}
</style>
