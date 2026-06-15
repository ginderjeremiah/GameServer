{#if !item.modSlots || item.modSlots.length === 0}
	<div class="no-slots">This item has no mod slots.</div>
{:else}
	<div class="mod-slots">
		{#each item.modSlots as slot (slot.id)}
			{@const applied = item.appliedMods.find((m) => m.itemModSlotId === slot.id)}
			{@const accent = modTypeColor(slot.itemModSlotTypeId)}
			<div class="mod-slot-wrap">
				<!--
					An empty slot is the click target that opens the mod picker, so it's a real
					<button>; a filled slot is non-interactive (it carries its own Remove button),
					so it renders as a plain <div> to avoid nesting a button inside a button.
				-->
				{#if applied}
					<div class="mod-slot filled" style:border-left-color={accent}>
						{@render slotBody(applied, accent, slot)}
					</div>
				{:else}
					<button type="button" class="mod-slot" style:border-left-color={accent} onclick={() => togglePicker(slot.id)}>
						{@render slotBody(applied, accent, slot)}
					</button>
				{/if}

				{#if openSlotId === slot.id && !applied}
					{@const options = view.compatibleMods(slot.itemModSlotTypeId, item)}
					<div class="mod-picker" style:border-left-color={accent}>
						<div class="picker-label" style:color={accent}>Install {modTypeLabel(slot.itemModSlotTypeId)}</div>
						{#if options.length === 0}
							<div class="picker-empty">
								No unlocked {modTypeLabel(slot.itemModSlotTypeId).toLowerCase()} mods available.
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
										<div class="option-name" style:color={rarityColor(mod.rarityId)}>{mod.name}</div>
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

{#snippet slotBody(
	applied: (typeof item.appliedMods)[number] | undefined,
	accent: string,
	slot: (typeof item.modSlots)[number]
)}
	<div class="mod-info">
		<div class="mod-head">
			{#if applied}
				<span class="mod-name" style:color={rarityColor(applied.rarityId)}>{applied.name}</span>
			{:else}
				<span class="mod-empty">Empty slot</span>
			{/if}
			<span class="mod-type" style:color={accent}>{modTypeLabel(slot.itemModSlotTypeId)}</span>
		</div>
		{#if applied}
			<div class="mod-desc">{applied.description}</div>
		{:else}
			<div class="mod-hint">Click to install a {modTypeLabel(slot.itemModSlotTypeId).toLowerCase()}</div>
		{/if}
	</div>

	{#if applied}
		<button
			class="mod-remove"
			title="Remove mod"
			aria-label="Remove {applied.name}"
			onclick={stopPropagation(() => view.removeMod(item.itemId, slot.id))}>×</button
		>
	{:else}
		<span class="mod-add" style:color={accent} aria-hidden="true">+</span>
	{/if}
{/snippet}

<script lang="ts">
import type { Item } from '$lib/battle';
import { modTypeColor, modTypeLabel, rarityColor } from '$lib/common';
import { stopPropagation } from '$lib/common/event-wrappers';
import type { InventoryView } from './inventory-view.svelte';

const { item, view }: { item: Item; view: InventoryView } = $props();

let openSlotId = $state<number | null>(null);

const togglePicker = (slotId: number) => {
	openSlotId = openSlotId === slotId ? null : slotId;
};
</script>

<style lang="scss">
.no-slots {
	font-size: 11.5px;
	font-style: italic;
	color: var(--text-muted);
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
	appearance: none;
	width: 100%;
	display: flex;
	align-items: flex-start;
	gap: 8px;
	padding: 7px 9px;
	background: transparent;
	border: 1px dashed var(--border-light);
	border-left: 2px solid;
	border-radius: 2px;
	cursor: pointer;
	min-height: 38px;
	font: inherit;
	color: inherit;
	text-align: left;

	&.filled {
		background: color-mix(in srgb, var(--white) 3%, transparent);
		border-style: solid;
		border-color: var(--border-subtle);
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
	color: var(--text-primary);
}

.mod-empty {
	font-size: 12px;
	color: var(--text-tertiary);
}

.mod-type {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
}

.mod-desc {
	font-size: 11px;
	color: var(--text-tertiary);
	line-height: 1.45;
	margin-top: 2px;
}

.mod-hint {
	font-size: 11px;
	color: var(--text-muted);
	margin-top: 2px;
}

.mod-remove {
	flex-shrink: 0;
	width: 18px;
	height: 18px;
	border: none;
	border-radius: 2px;
	background: color-mix(in srgb, var(--white) 6%, transparent);
	color: var(--text-tertiary);
	cursor: pointer;
	font-size: 11px;
	padding: 0;
}

.mod-add {
	flex-shrink: 0;
	font-size: 14px;
	line-height: 1;
}

.mod-picker {
	margin-top: 4px;
	background: color-mix(in srgb, var(--surface) 98%, transparent);
	border: 1px solid var(--border-light);
	border-left: 2px solid;
	border-radius: 3px;
	padding: 6px;
}

.picker-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	padding: 2px 4px 6px;
}

.picker-empty {
	padding: 8px 6px;
	font-size: 11.5px;
	color: var(--text-muted);
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
		background: color-mix(in srgb, var(--white) 6%, transparent);
	}
}

.option-name {
	font-size: 12px;
	font-weight: 500;
	color: var(--text-primary);
}

.option-desc {
	font-size: 11px;
	color: var(--text-tertiary);
	line-height: 1.45;
	margin-top: 1px;
}
</style>
