<div class="inventory-frame">
	<div class="inv-header">
		<span class="diamond"></span>
		<div>
			<div class="inv-title">Inventory</div>
			<div class="inv-subtitle">Drag to a slot · click to inspect &amp; mod · ⌘-click to equip</div>
		</div>
	</div>

	<div class="inv-columns">
		<EquippedRail {view} />
		<InventoryGrid {view} />
		<div class="totals-column">
			<div class="totals-inner">
				<EquippedTotals {view} />
				<div class="totals-hint">Click an item to inspect &amp; mod it</div>
			</div>
		</div>
	</div>

	<!-- Detail drawer: right-side slide-over with dimmed backdrop + click-outside close -->
	<div class="drawer-layer" style:pointer-events={view.selected ? 'auto' : 'none'}>
		<div class="backdrop" class:open={!!view.selected} role="presentation" onclick={() => view.select(null)}></div>
		<div class="drawer" class:open={!!view.selected}>
			{#if view.selected}
				<ItemDrawer item={view.selected} {view} />
			{/if}
		</div>
	</div>
</div>

<script lang="ts">
import EquippedRail from './EquippedRail.svelte';
import InventoryGrid from './InventoryGrid.svelte';
import EquippedTotals from './EquippedTotals.svelte';
import ItemDrawer from './ItemDrawer.svelte';
import { InventoryView } from './inventory-view.svelte';

const view = new InventoryView();
</script>

<style lang="scss">
.inventory-frame {
	height: 100%;
	display: flex;
	flex-direction: column;
	position: relative;
	color: #f0f0f0;
	font-family: Geist, Arial, Helvetica, sans-serif;
	overflow: hidden;
}

.inv-header {
	padding: 20px 28px 14px;
	display: flex;
	align-items: center;
	gap: 12px;
}

.diamond {
	width: 11px;
	height: 11px;
	transform: rotate(45deg);
	background: var(--accent);
	box-shadow: 0 0 8px rgba(161, 194, 247, 0.6);
	flex-shrink: 0;
}

.inv-title {
	font-size: 21px;
	font-weight: 500;
	letter-spacing: -0.3px;
}

.inv-subtitle {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.4);
}

.inv-columns {
	flex: 1;
	min-height: 0;
	display: flex;
	gap: 18px;
	padding: 0 28px 24px;
}

.totals-column {
	width: 330px;
	flex-shrink: 0;
	display: flex;
	flex-direction: column;
	background: rgba(20, 21, 27, 0.5);
	border: 1px solid rgba(255, 255, 255, 0.08);
	border-radius: 4px;
	overflow: hidden;
}

.totals-inner {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding: 16px 18px;
}

.totals-hint {
	margin-top: 18px;
	text-align: center;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.4);
}

.drawer-layer {
	position: absolute;
	inset: 0;
	z-index: 60;
}

.backdrop {
	position: absolute;
	inset: 0;
	background: rgba(0, 0, 0, 0.5);
	opacity: 0;
	transition: opacity 200ms;

	&.open {
		opacity: 1;
	}
}

.drawer {
	position: absolute;
	top: 0;
	right: 0;
	bottom: 0;
	width: 380px;
	background: rgba(18, 19, 25, 0.99);
	border-left: 1px solid rgba(255, 255, 255, 0.14);
	box-shadow: -16px 0 48px rgba(0, 0, 0, 0.5);
	transform: translateX(100%);
	transition: transform 220ms cubic-bezier(0.4, 0, 0.2, 1);
	display: flex;
	flex-direction: column;

	&.open {
		transform: translateX(0);
	}
}
</style>
