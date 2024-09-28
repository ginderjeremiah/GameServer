<div class="equipped-slots-container">
	<div class="equipped-helm-container">
		<ItemContainer />
	</div>
</div>

<script lang="ts">
import ItemContainer from './ItemSlot.svelte';
</script>

<style lang="scss">
.equipped-slots-container {
	margin: 0 7.5% 1em calc(7.5% + 3vw);
	display: block;
	width: 35%;
	border: var(--default-border);
	border-width: 3px;
	border-radius: 1vw;
	background-color: var(--container-background-color);
	position: relative;
	height: calc((74vw - 6px) * 3 / 8);
}

.equipped-helm-container {
	position: absolute;
	left: calc(50% - var(--width) / 2);
	top: 2.2%;
}
</style>
