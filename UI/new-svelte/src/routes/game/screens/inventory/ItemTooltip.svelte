<div
	class="item-tooltip"
	bind:this={container}
	style={item ? '' : 'display: none;'}
	style:border-left="3px solid {rarityAccent}"
>
	{#if item}
		<!-- Title section -->
		<div class="tt-title-section">
			<div class="tt-category-row">
				<div
					class="tt-category-diamond"
					style:background={categoryColor}
					style:box-shadow="0 0 6px {tintColor(categoryColor, 0.67)}"
				></div>
				<span class="tt-category-label" style:color={categoryColor}>{categoryName}</span>
				{#if item.equipped}
					<div class="tt-equipped-badge">
						<div class="tt-equipped-dot"></div>
						<span>Equipped</span>
					</div>
				{/if}
			</div>
			<div class="tt-item-name">{displayName}</div>
		</div>

		<div class="tt-body">
			<!-- Stats -->
			{#if attributeMap?.length}
				<div class="tt-section">
					<div class="tt-section-header">
						<span>Stats</span>
						<div class="tt-section-line"></div>
					</div>
					<div class="tt-stats-grid">
						{#each attributeMap as attr (attr.name)}
							<div class="tt-stat-name">{attr.name}</div>
							<div class="tt-stat-value" class:positive={attr.value > 0} class:negative={attr.value < 0}>
								{attr.value > 0 ? '+' : ''}{attr.value}
							</div>
						{/each}
					</div>
				</div>
			{/if}

			<!-- Mods — every slot, filled or empty -->
			{#if modSlots.length}
				<div class="tt-section">
					<div class="tt-section-header">
						<span>Mods · {filledCount}/{modSlots.length}</span>
						<div class="tt-section-line"></div>
					</div>
					<div class="tt-mods-list">
						{#each modSlots as slot (slot.slotId)}
							{#if slot.mod}
								<div class="tt-mod-tile" style:border-left-color={rarityColor(slot.mod.rarityId)}>
									<div class="tt-mod-header">
										<span class="tt-mod-name" style:color={rarityColor(slot.mod.rarityId)}>{slot.mod.name}</span>
										<span class="tt-mod-type" style:color={modTypeColor(slot.type)}>{modTypeLabel(slot.type)}</span>
									</div>
									<div class="tt-mod-desc">{slot.mod.description}</div>
								</div>
							{:else}
								<div class="tt-mod-empty" style:border-left-color={modTypeColor(slot.type)}>
									<span class="tt-mod-empty-label">Empty slot</span>
									<span class="tt-mod-type" style:color={modTypeColor(slot.type)}>{modTypeLabel(slot.type)}</span>
								</div>
							{/if}
						{/each}
					</div>
				</div>
			{/if}

			<!-- Description -->
			{#if item.description}
				<div class="tt-section last">
					<div class="tt-section-header">
						<span>Description</span>
						<div class="tt-section-line"></div>
					</div>
					<div class="tt-description">{item.description}</div>
				</div>
			{/if}
		</div>
	{/if}
</div>

<script lang="ts">
import type { Item } from '$lib/battle';
import {
	composeItemName,
	itemCategoryColor,
	itemCategoryName,
	modTypeColor,
	modTypeLabel,
	rarityColor,
	tintColor
} from '$lib/common';

export const getBaseNode = () => container;

type Props = {
	item: Item | undefined;
};

const { item }: Props = $props();

let container: HTMLDivElement;

const attributeMap = $derived(item?.totalAttributes?.getAttributeMap());

// Every mod slot (filled or empty), so the tooltip surfaces open slots too.
const modSlots = $derived(
	(item?.modSlots ?? []).map((slot) => ({
		slotId: slot.id,
		type: slot.itemModSlotTypeId,
		mod: item?.appliedMods.find((m) => m.itemModSlotId === slot.id) ?? null
	}))
);
const filledCount = $derived(modSlots.filter((s) => s.mod).length);

// The tooltip's main accent (left border) reflects the item's rarity, while the
// category row (diamond + label) stays category-coloured — mirroring how
// ModTooltip accents its border by rarity and its diamond/label by mod type.
const rarityAccent = $derived(item ? rarityColor(item.rarityId) : 'var(--rarity-common)');
const categoryColor = $derived(item ? itemCategoryColor(item.itemCategoryId) : 'var(--category-armor)');
const categoryName = $derived(item ? itemCategoryName(item.itemCategoryId) : 'Item');
// Item name reflects its applied mods: prefix mod names prepend, suffix names append.
const displayName = $derived(item ? composeItemName(item.name, item.appliedMods) : '');
</script>

<style lang="scss">
.item-tooltip {
	width: 280px;
	border-radius: 3px;
	box-shadow: -4px 0 16px color-mix(in srgb, var(--black) 15%, transparent);
}

.tt-title-section {
	padding: 14px 16px 12px;
	border-bottom: 1px solid color-mix(in srgb, var(--text-primary) 8%, transparent);
}

.tt-category-row {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 4px;
}

.tt-category-diamond {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
}

.tt-category-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
}

.tt-equipped-badge {
	margin-left: auto;
	display: flex;
	align-items: center;
	gap: 6px;
	padding: 2px 8px;
	background: color-mix(in srgb, var(--success) 10%, transparent);
	border: 1px solid color-mix(in srgb, var(--success) 45%, transparent);
	border-radius: 2px;

	span {
		font-family: var(--mono);
		font-size: 9px;
		color: var(--success);
		letter-spacing: 1.2px;
		text-transform: uppercase;
	}
}

.tt-equipped-dot {
	width: 5px;
	height: 5px;
	border-radius: 50%;
	background: var(--success);
	box-shadow: 0 0 4px color-mix(in srgb, var(--success) 90%, transparent);
}

.tt-item-name {
	font-size: 18px;
	font-weight: 400;
	color: var(--text-primary);
	letter-spacing: -0.2px;
	line-height: 1.15;
}

.tt-body {
	padding: 12px 16px 14px;
}

.tt-section {
	margin-bottom: 12px;

	&.last {
		margin-bottom: 0;
	}
}

.tt-section-header {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin-bottom: 7px;
	display: flex;
	align-items: center;
	gap: 8px;
}

.tt-section-line {
	flex: 1;
	height: 1px;
	background: color-mix(in srgb, var(--text-primary) 6%, transparent);
}

.tt-stats-grid {
	display: grid;
	grid-template-columns: 1fr auto;
	row-gap: 4px;
	column-gap: 12px;

	.tt-stat-name {
		font-size: 12px;
		color: var(--text-secondary);
	}

	.tt-stat-value {
		font-family: var(--mono);
		font-size: 11.5px;
		letter-spacing: 0.3px;
		text-align: right;
		color: color-mix(in srgb, var(--text-primary) 70%, transparent);

		&.positive {
			color: var(--success);
		}
		&.negative {
			color: var(--error);
		}
	}
}

.tt-mods-list {
	display: flex;
	flex-direction: column;
	gap: 6px;
}

.tt-mod-tile {
	padding: 6px 10px;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border-left: 2px solid;
}

.tt-mod-empty {
	padding: 6px 10px;
	border: 1px dashed var(--border-light);
	border-left: 2px solid;
	display: flex;
	align-items: center;
	gap: 8px;
}

.tt-mod-empty-label {
	font-size: 11.5px;
	font-style: italic;
	color: color-mix(in srgb, var(--text-primary) 50%, transparent);
}

.tt-mod-header {
	display: flex;
	align-items: baseline;
	gap: 8px;
	margin-bottom: 2px;
}

.tt-mod-name {
	font-size: 12px;
	font-weight: 500;
	color: var(--text-primary);
}

.tt-mod-type {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
}

.tt-mod-desc {
	font-size: 11.5px;
	color: color-mix(in srgb, var(--text-primary) 65%, transparent);
	line-height: 1.5;
}

.tt-description {
	font-size: 11.5px;
	font-style: italic;
	color: color-mix(in srgb, var(--text-primary) 60%, transparent);
	line-height: 1.55;
}
</style>
