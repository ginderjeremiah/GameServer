<div class="equipped-slots-container">
	<div class="equipped-helm-container">
		<ItemContainer />
	</div>
	<div class="equipped-chest-container">
		<ItemContainer />
	</div>
	<div class="equipped-leg-container">
		<ItemContainer />
	</div>
	<div class="equipped-boot-container">
		<ItemContainer />
	</div>
	<div class="equipped-weapon-container">
		<ItemContainer />
	</div>
	<div class="equipped-accessory-container">
		<ItemContainer />
	</div>
</div>

<script lang="ts">
import ItemContainer from './ItemSlot.svelte';
</script>

<style lang="scss">
.equipped-slots-container {
	margin: 0 0 1em 5%;
	display: block;
	width: 25%;
	border: var(--default-border);
	border-width: 3px;
	border-radius: 1vw;
	background-color: var(--container-background-color);
	position: relative;
	height: calc((74vw - 6px) * 3 / 8);

	.equipped-helm-container {
		position: absolute;
		left: calc(50% - var(--slot-width) / 2);
		top: 2.2%;
	}

	.equipped-chest-container {
		position: absolute;
		left: calc(50% - var(--slot-width) / 2);
		top: calc(var(--slot-width) + 4.4%);
	}

	.equipped-leg-container {
		position: absolute;
		left: calc(50% - var(--slot-width) / 2);
		top: calc(var(--slot-width) * 2 + 6.6%);
	}

	.equipped-boot-container {
		position: absolute;
		left: calc(50% - var(--slot-width) / 2);
		top: calc(var(--slot-width) * 3 + 8.8%);
	}

	.equipped-weapon-container {
		position: absolute;
		left: calc(46% - var(--slot-width) * 1.5);
		top: calc(var(--slot-width) * 1.5 + 5.5%);
	}

	.equipped-accessory-container {
		position: absolute;
		left: calc(54% + var(--slot-width) / 2);
		top: calc(var(--slot-width) * 1.5 + 5.5%);
	}
}
</style>
