<div class="totals">
	<div class="section-rule">
		<span class="diamond"></span>
		<span class="mono-label accent">Equipped totals</span>
		<div class="line"></div>
	</div>

	<StatList attrs={view.equippedTotals} emptyText="No gear equipped yet." />

	<div class="slots-filled">
		<span class="mono-label">Slots filled</span>
		<span class="filled-value">{view.slotsFilled} / {EQUIP_SLOTS.length}</span>
	</div>
</div>

<script lang="ts">
import StatList from './StatList.svelte';
import { EQUIP_SLOTS, type InventoryView } from './inventory-view.svelte';

const { view }: { view: InventoryView } = $props();
</script>

<style lang="scss">
.totals {
	height: 100%;
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
	box-shadow: 0 0 6px rgba(161, 194, 247, 0.7);
	flex-shrink: 0;
}

.mono-label {
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.4);

	&.accent {
		color: rgba(240, 240, 240, 0.4);
	}
}

.line {
	flex: 1;
	height: 1px;
	background: rgba(255, 255, 255, 0.08);
}

.slots-filled {
	margin-top: 12px;
	padding-top: 10px;
	border-top: 1px solid rgba(255, 255, 255, 0.08);
	display: flex;
	align-items: center;
	justify-content: space-between;
}

.filled-value {
	font-family: 'Geist Mono', monospace;
	font-size: 12px;
	color: rgba(240, 240, 240, 0.78);
}
</style>
