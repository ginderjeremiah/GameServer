<div class="inventory-frame" data-testid="inventory-screen">
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
			</div>
		</div>
	</div>

	<!-- Detail drawer: right-side slide-over with dimmed backdrop + click-outside close -->
	<div class="drawer-layer" style:pointer-events={view.selected ? 'auto' : 'none'}>
		<button
			class="backdrop"
			type="button"
			tabindex="-1"
			aria-label="Close item drawer"
			class:open={!!view.selected}
			onclick={() => view.select(null)}
		></button>
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
	color: var(--text-primary);
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
	box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 60%, transparent);
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
	color: var(--text-muted);
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
	background: color-mix(in srgb, var(--surface) 50%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	overflow: hidden;
}

.totals-inner {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding: 16px 18px;
}

.drawer-layer {
	position: absolute;
	inset: 0;
	z-index: 60;
}

.backdrop {
	position: absolute;
	inset: 0;
	padding: 0;
	border: none;
	cursor: default;
	background: color-mix(in srgb, var(--black) 50%, transparent);
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
	background: color-mix(in srgb, var(--surface) 99%, transparent);
	border-left: 1px solid var(--border-light);
	box-shadow: -16px 0 48px color-mix(in srgb, var(--black) 50%, transparent);
	transform: translateX(100%);
	transition: transform 220ms cubic-bezier(0.4, 0, 0.2, 1);
	display: flex;
	flex-direction: column;

	&.open {
		transform: translateX(0);
	}
}
</style>
