<div class="item-tooltip" bind:this={container} style={item ? '' : 'display: none;'}
	style:border-left="3px solid {accentColor}">
	{#if item}
		<!-- Title section -->
		<div class="tt-title-section">
			<div class="tt-category-row">
				<div class="tt-category-diamond" style:background={accentColor}
					style:box-shadow="0 0 6px {accentColor}aa"></div>
				<span class="tt-category-label" style:color={accentColor}>{categoryName}</span>
				{#if item.equipped}
					<div class="tt-equipped-badge">
						<div class="tt-equipped-dot"></div>
						<span>Equipped</span>
					</div>
				{/if}
			</div>
			<div class="tt-item-name">{item.name}</div>
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
						{#each attributeMap as attr}
							<div class="tt-stat-name">{attr.name}</div>
							<div class="tt-stat-value"
								class:positive={attr.value > 0}
								class:negative={attr.value < 0}
							>{attr.value > 0 ? '+' : ''}{attr.value}</div>
						{/each}
					</div>
				</div>
			{/if}

			<!-- Applied mods -->
			{#if appliedMods?.length}
				<div class="tt-section">
					<div class="tt-section-header">
						<span>Applied mods</span>
						<div class="tt-section-line"></div>
					</div>
					<div class="tt-mods-list">
						{#each appliedMods as mod}
							<div class="tt-mod-tile" style:border-left-color={modTypeAccent(mod.itemModTypeId)}>
								<div class="tt-mod-header">
									<span class="tt-mod-name">{mod.name}</span>
									<span class="tt-mod-type" style:color={modTypeAccent(mod.itemModTypeId)}>{modTypeLabel(mod.itemModTypeId)}</span>
								</div>
								<div class="tt-mod-desc">{mod.description}</div>
							</div>
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
import { EItemCategory, EItemModType } from '$lib/api';
import type { Item } from '$lib/battle';

export const getBaseNode = () => container;

type Props = {
	item: Item | undefined;
};

const { item }: Props = $props();

let container: HTMLDivElement;

const attributeMap = $derived(item?.totalAttributes?.getAttributeMap());
const appliedMods = $derived(item?.appliedMods);

const CATEGORY_ACCENT: Record<number, string> = {
	[EItemCategory.Helm]: '#a1c2f7',
	[EItemCategory.Chest]: '#a1c2f7',
	[EItemCategory.Leg]: '#a1c2f7',
	[EItemCategory.Boot]: '#a1c2f7',
	[EItemCategory.Weapon]: '#e08778',
	[EItemCategory.Accessory]: '#e8c878',
};

const accentColor = $derived(CATEGORY_ACCENT[item?.itemCategoryId ?? 0] ?? '#a1c2f7');
const categoryName = $derived(EItemCategory[item?.itemCategoryId ?? 0] ?? 'Item');

const modTypeAccent = (modType: number) => ({
	[EItemModType.Component]: 'rgba(240, 240, 240, 0.55)',
	[EItemModType.Prefix]: '#9bc7d9',
	[EItemModType.Suffix]: '#c0a8e6',
}[modType] ?? 'rgba(240, 240, 240, 0.55)');

const modTypeLabel = (modType: number) => ({
	[EItemModType.Component]: 'Component',
	[EItemModType.Prefix]: 'Prefix',
	[EItemModType.Suffix]: 'Suffix',
}[modType] ?? '');
</script>

<style lang="scss">
.item-tooltip {
	width: 280px;
	border-radius: 3px;
	box-shadow: -4px 0 16px rgba(0, 0, 0, 0.15);
}

.tt-title-section {
	padding: 14px 16px 12px;
	border-bottom: 1px solid rgba(240, 240, 240, 0.08);
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
	font-family: 'Geist Mono', monospace;
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
	background: rgba(189, 224, 180, 0.1);
	border: 1px solid rgba(189, 224, 180, 0.45);
	border-radius: 2px;

	span {
		font-family: 'Geist Mono', monospace;
		font-size: 9px;
		color: #bde0b4;
		letter-spacing: 1.2px;
		text-transform: uppercase;
	}
}

.tt-equipped-dot {
	width: 5px;
	height: 5px;
	border-radius: 50%;
	background: #bde0b4;
	box-shadow: 0 0 4px rgba(189, 224, 180, 0.9);
}

.tt-item-name {
	font-size: 18px;
	font-weight: 400;
	color: #f0f0f0;
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
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.4);
	margin-bottom: 7px;
	display: flex;
	align-items: center;
	gap: 8px;
}

.tt-section-line {
	flex: 1;
	height: 1px;
	background: rgba(240, 240, 240, 0.06);
}

.tt-stats-grid {
	display: grid;
	grid-template-columns: 1fr auto;
	row-gap: 4px;
	column-gap: 12px;

	.tt-stat-name {
		font-size: 12px;
		color: rgba(240, 240, 240, 0.78);
	}

	.tt-stat-value {
		font-family: 'Geist Mono', monospace;
		font-size: 11.5px;
		letter-spacing: 0.3px;
		text-align: right;
		color: rgba(240, 240, 240, 0.7);

		&.positive { color: #bde0b4; }
		&.negative { color: #f0a094; }
	}
}

.tt-mods-list {
	display: flex;
	flex-direction: column;
	gap: 6px;
}

.tt-mod-tile {
	padding: 6px 10px;
	background: rgba(255, 255, 255, 0.03);
	border-left: 2px solid;
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
	color: #f0f0f0;
}

.tt-mod-type {
	font-family: 'Geist Mono', monospace;
	font-size: 9px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
}

.tt-mod-desc {
	font-size: 11.5px;
	color: rgba(240, 240, 240, 0.65);
	line-height: 1.5;
}

.tt-description {
	font-size: 11.5px;
	font-style: italic;
	color: rgba(240, 240, 240, 0.6);
	line-height: 1.55;
}
</style>
