{#if !item.modSlots || item.modSlots.length === 0}
	<div class="no-slots">This item has no mod slots.</div>
{:else}
	<div class="mod-slots">
		{#each item.modSlots as slot (slot.id)}
			{@const applied = item.appliedMods.find((m) => m.itemModSlotId === slot.id)}
			{@const accent = modAccent(slot.itemModSlotTypeId)}
			<div class="mod-slot-wrap">
				<div
					class="mod-slot"
					class:filled={!!applied}
					style:border-left-color={accent}
					role="button"
					tabindex="0"
					onclick={() => {
						if (!applied) togglePicker(slot.id);
					}}
					onkeydown={(e) => {
						if (!applied && (e.key === 'Enter' || e.key === ' ')) {
							e.preventDefault();
							togglePicker(slot.id);
						}
					}}
				>
					<div class="mod-info">
						<div class="mod-head">
							{#if applied}
								<span class="mod-name">{applied.name}</span>
							{:else}
								<span class="mod-empty">Empty slot</span>
							{/if}
							<span class="mod-type" style:color={accent}>{modLabel(slot.itemModSlotTypeId)}</span>
						</div>
						{#if applied}
							<div class="mod-desc">{applied.description}</div>
						{:else}
							<div class="mod-hint">Click to install a {modLabel(slot.itemModSlotTypeId).toLowerCase()}</div>
						{/if}
					</div>

					{#if applied}
						{#if applied.removable}
							<button
								class="mod-remove"
								title="Remove mod"
								onclick={(e) => {
									e.stopPropagation();
									view.removeMod(item.itemId, slot.id);
								}}>×</button
							>
						{:else}
							<span class="mod-locked" title="Permanent"></span>
						{/if}
					{:else}
						<span class="mod-add" style:color={accent}>+</span>
					{/if}
				</div>

				{#if openSlotId === slot.id && !applied}
					{@const options = view.compatibleMods(slot.itemModSlotTypeId, item)}
					<div class="mod-picker" style:border-left-color={accent}>
						<div class="picker-label" style:color={accent}>Install {modLabel(slot.itemModSlotTypeId)}</div>
						{#if options.length === 0}
							<div class="picker-empty">
								No unlocked {modLabel(slot.itemModSlotTypeId).toLowerCase()} mods available.
							</div>
						{:else}
							<div class="picker-options">
								{#each options as mod (mod.id)}
									<button
										class="picker-option"
										style:border-left-color={accent}
										onclick={() => {
											view.applyMod(item.itemId, slot.id, mod.id);
											openSlotId = null;
										}}
									>
										<div class="option-name">{mod.name}</div>
										<div class="option-desc">{mod.description}</div>
									</button>
								{/each}
							</div>
						{/if}
					</div>
				{/if}
			</div>
		{/each}
	</div>
{/if}

<script lang="ts">
import { EItemModType } from '$lib/api';
import type { Item } from '$lib/battle';
import type { InventoryView } from './inventory-view.svelte';

const { item, view }: { item: Item; view: InventoryView } = $props();

let openSlotId = $state<number | null>(null);

const togglePicker = (slotId: number) => {
	openSlotId = openSlotId === slotId ? null : slotId;
};

const MOD_LABELS: Record<number, string> = {
	[EItemModType.Component]: 'Component',
	[EItemModType.Prefix]: 'Prefix',
	[EItemModType.Suffix]: 'Suffix'
};
const MOD_ACCENTS: Record<number, string> = {
	[EItemModType.Component]: 'rgba(240, 240, 240, 0.55)',
	[EItemModType.Prefix]: '#9bc7d9',
	[EItemModType.Suffix]: '#c0a8e6'
};
const modLabel = (type: number) => MOD_LABELS[type] ?? '';
const modAccent = (type: number) => MOD_ACCENTS[type] ?? 'rgba(240, 240, 240, 0.55)';
</script>

<style lang="scss">
.no-slots {
	font-size: 11.5px;
	font-style: italic;
	color: rgba(240, 240, 240, 0.4);
	padding: 4px 0;
}

.mod-slots {
	display: flex;
	flex-direction: column;
	gap: 6px;
}

.mod-slot-wrap {
	position: relative;
}

.mod-slot {
	display: flex;
	align-items: flex-start;
	gap: 8px;
	padding: 7px 9px;
	background: transparent;
	border: 1px dashed rgba(255, 255, 255, 0.14);
	border-left: 2px solid;
	border-radius: 2px;
	cursor: pointer;
	min-height: 38px;

	&.filled {
		background: rgba(255, 255, 255, 0.03);
		border-style: solid;
		border-color: rgba(255, 255, 255, 0.08);
		cursor: default;
	}
}

.mod-info {
	min-width: 0;
	flex: 1;
}

.mod-head {
	display: flex;
	align-items: baseline;
	gap: 8px;
}

.mod-name {
	font-size: 12px;
	font-weight: 500;
	color: #f0f0f0;
}

.mod-empty {
	font-size: 12px;
	color: rgba(240, 240, 240, 0.55);
}

.mod-type {
	font-family: 'Geist Mono', monospace;
	font-size: 8.5px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
}

.mod-desc {
	font-size: 11px;
	color: rgba(240, 240, 240, 0.55);
	line-height: 1.45;
	margin-top: 2px;
}

.mod-hint {
	font-size: 11px;
	color: rgba(240, 240, 240, 0.4);
	margin-top: 2px;
}

.mod-remove {
	flex-shrink: 0;
	width: 18px;
	height: 18px;
	border: none;
	border-radius: 2px;
	background: rgba(255, 255, 255, 0.06);
	color: rgba(240, 240, 240, 0.55);
	cursor: pointer;
	font-size: 11px;
	padding: 0;
}

.mod-locked {
	flex-shrink: 0;
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
	border: 1px solid rgba(240, 240, 240, 0.4);
	margin-top: 4px;
}

.mod-add {
	flex-shrink: 0;
	font-size: 14px;
	line-height: 1;
}

.mod-picker {
	margin-top: 4px;
	background: rgba(20, 21, 27, 0.98);
	border: 1px solid rgba(255, 255, 255, 0.14);
	border-left: 2px solid;
	border-radius: 3px;
	padding: 6px;
}

.picker-label {
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	padding: 2px 4px 6px;
}

.picker-empty {
	padding: 8px 6px;
	font-size: 11.5px;
	color: rgba(240, 240, 240, 0.4);
	font-style: italic;
}

.picker-options {
	display: flex;
	flex-direction: column;
	gap: 2px;
	max-height: 200px;
	overflow-y: auto;
}

.picker-option {
	text-align: left;
	border: none;
	border-left: 2px solid;
	background: transparent;
	cursor: pointer;
	padding: 6px 8px;
	border-radius: 2px;

	&:hover {
		background: rgba(255, 255, 255, 0.06);
	}
}

.option-name {
	font-size: 12px;
	font-weight: 500;
	color: #f0f0f0;
}

.option-desc {
	font-size: 11px;
	color: rgba(240, 240, 240, 0.55);
	line-height: 1.45;
	margin-top: 1px;
}
</style>
