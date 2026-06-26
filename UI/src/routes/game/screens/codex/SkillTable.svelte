<!-- The skill table: a column header over a scrollable list of selectable skill rows. Each row shows
     a rarity-tinted tier mark, the name, the base damage, the cooldown and how many enemies use it. -->
<div class="table">
	<div class="head">
		<span class="c-name">Name</span>
		<span class="c-num">Base dmg</span>
		<span class="c-num">Cooldown</span>
		<span class="c-num narrow">Used by</span>
	</div>

	<div class="rows" data-testid="codex-skill-rows">
		{#each view.skillRows as row (row.id)}
			<button
				type="button"
				class="row"
				class:selected={row.id === view.selectedSkillId}
				data-testid="codex-skill-{row.id}"
				onclick={() => view.selectSkill(row.id)}
			>
				<span class="c-name name-cell">
					<span class="mark" style:--mark={row.rarityColor}></span>
					<span class="name">{row.name}</span>
				</span>
				<span class="c-num val">{row.baseDamageLabel}</span>
				<span class="c-num val">{row.cooldownLabel}</span>
				<span class="c-num narrow muted">{row.usedByCount}</span>
			</button>
		{/each}
	</div>
</div>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.table {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
}

.head {
	display: flex;
	align-items: center;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);
	padding: 0 14px 8px;
	border-bottom: 1px solid var(--border-subtle);
	margin-right: 14px;
}

.c-name {
	flex: 2.4;
	min-width: 0;
}

.c-num {
	flex: 1;
	text-align: right;
}

.narrow {
	flex: 0.9;
}

.rows {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding-right: 6px;
}

.row {
	width: 100%;
	text-align: left;
	display: flex;
	align-items: center;
	padding: 9px 14px;
	border: none;
	border-left: 2px solid transparent;
	border-bottom: 1px solid color-mix(in srgb, var(--white) 5%, transparent);
	background: transparent;
	cursor: pointer;
	font-family: var(--sans);

	&:hover {
		background: color-mix(in srgb, var(--white) 3%, transparent);
	}

	&.selected {
		border-left-color: var(--attr-intellect);
		background: color-mix(in srgb, var(--attr-intellect) 10%, transparent);

		.name {
			color: var(--white);
			font-weight: 600;
		}
	}
}

.name-cell {
	display: flex;
	align-items: center;
	gap: 11px;
	min-width: 0;
}

.mark {
	width: 8px;
	height: 8px;
	flex: none;
	transform: rotate(45deg);
	// Tier mark tinted by the skill's rarity (themeable var), mirroring how items signal rarity.
	background: var(--mark, var(--attr-intellect));
	box-shadow: 0 0 6px color-mix(in srgb, var(--mark, var(--attr-intellect)) 45%, transparent);
}

.name {
	font-size: 13.5px;
	color: var(--text-primary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.val {
	font-family: var(--mono);
	font-size: 10.5px;
	color: var(--text-secondary);
}

.muted {
	font-family: var(--mono);
	font-size: 10.5px;
	color: var(--text-tertiary);
}
</style>
