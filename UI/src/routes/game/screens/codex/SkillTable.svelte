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
		{#each view.skillsTab.skillRows as row (row.id)}
			<button
				type="button"
				class="row"
				class:selected={row.id === view.skillsTab.selectedSkillId}
				data-testid="codex-skill-{row.id}"
				onclick={() => view.skillsTab.selectSkill(row.id)}
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
@use '$styles/codex-table' as table;

.table {
	@include table.frame;
}

.head {
	@include table.head-row;
}

.c-name {
	@include table.col-name;
}

.c-num {
	@include table.col-num;
}

.narrow {
	@include table.col-narrow;
}

.rows {
	@include table.rows-list;
}

.row {
	@include table.row(var(--attr-intellect));
}

.name-cell {
	@include table.name-cell;
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
	@include table.row-name;
}

.val {
	font-family: var(--mono);
	font-size: 10.5px;
	color: var(--text-secondary);
}

.muted {
	@include table.muted-num;
}
</style>
