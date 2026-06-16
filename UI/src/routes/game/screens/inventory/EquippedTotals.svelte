<div class="totals">
	<div class="main-content">
		<div class="section-rule">
			<span class="diamond"></span>
			<span class="mono-label accent">Equipped totals</span>
			<div class="line"></div>
		</div>

		<TooltipStatsGrid entries={view.equippedTotals} emptyText="No gear equipped yet." format />

		<div class="slots-filled">
			<span class="mono-label">Slots filled</span>
			<span class="filled-value">{view.slotsFilled} / {EQUIP_SLOTS.length}</span>
		</div>
	</div>
	<div class="footer">
		<div class="totals-hint">Click an item to inspect &amp; mod it</div>
	</div>
</div>

<script lang="ts">
import TooltipStatsGrid from '$components/tooltip/TooltipStatsGrid.svelte';
import { EQUIP_SLOTS, type InventoryView } from './inventory-view.svelte';

const { view }: { view: InventoryView } = $props();
</script>

<style lang="scss">
.totals {
	height: 100%;
	display: flex;
	flex-direction: column;
	justify-content: space-between;
}

.section-rule {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 10px;
}

.diamond {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
	background: var(--accent);
	box-shadow: 0 0 6px color-mix(in srgb, var(--accent) 70%, transparent);
	flex-shrink: 0;
}

.line {
	flex: 1;
	height: 1px;
	background: var(--border-subtle);
}

.slots-filled {
	margin-top: 12px;
	padding-top: 10px;
	border-top: 1px solid var(--border-subtle);
	display: flex;
	align-items: center;
	justify-content: space-between;
}

.filled-value {
	font-family: var(--mono);
	font-size: 12px;
	color: var(--text-secondary);
}

.totals-hint {
	margin-top: 18px;
	text-align: center;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}
</style>
