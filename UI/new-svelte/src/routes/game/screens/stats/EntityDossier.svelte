<!-- The selected entity's dossier: a header identifying it, then every statistic
     that references it (with its rank), each clickable to jump to its category. -->
{#if entity}
	<div class="dossier" data-testid="entity-dossier">
		<div class="dossier-head">
			<div class="glyph-box" style:--kind-color={kindColor}>
				<StatGlyph kind={view.entKind} size={24} />
			</div>
			<div>
				<div class="title-row">
					<h2 class="name">{entity.name}</h2>
					<KindBadge kind={view.entKind} />
					{#if view.entKind === 'enemy' && entity.boss}
						<span class="boss">Boss</span>
					{/if}
				</div>
				<span class="sub">{subline}</span>
			</div>
		</div>

		<div class="tiles">
			{#each view.entityStats as info (info.stat.id)}
				<DossierTile
					{info}
					data={view.data}
					kind={view.entKind}
					selId={entity.id}
					onPickStat={(cat) => view.goStat(cat)}
				/>
			{/each}
		</div>
	</div>
{/if}

<script lang="ts">
import StatGlyph from './StatGlyph.svelte';
import KindBadge from './KindBadge.svelte';
import DossierTile from './DossierTile.svelte';
import type { StatisticsView } from './statistics-view.svelte';
import { statKindColor } from './statistics-display';

interface Props {
	view: StatisticsView;
}

let { view }: Props = $props();

const entity = $derived(view.selectedEntity);
const kindColor = $derived(statKindColor(view.entKind));
const count = $derived(view.entityStats.length);
const kindNoun = $derived(
	view.entKind === 'zone' && entity?.zoneNum != null
		? `Zone ${entity.zoneNum}`
		: view.entKind === 'skill'
			? 'Skill'
			: 'Enemy'
);
const subline = $derived(`${kindNoun} · appears in ${count} statistic${count === 1 ? '' : 's'}`);
</script>

<style lang="scss">
.dossier {
	overflow: auto;
	padding-right: 6px;
}

.dossier-head {
	display: flex;
	align-items: center;
	gap: 14px;
	margin-bottom: 4px;
}

.glyph-box {
	width: 46px;
	height: 46px;
	border-radius: 5px;
	background: color-mix(in srgb, var(--kind-color) 8%, transparent);
	border: 1px solid color-mix(in srgb, var(--kind-color) 40%, transparent);
	display: flex;
	align-items: center;
	justify-content: center;
	flex-shrink: 0;
}

.title-row {
	display: flex;
	align-items: center;
	gap: 10px;
	margin-bottom: 3px;
}

.name {
	margin: 0;
	font-size: 24px;
	font-weight: 500;
	letter-spacing: -0.3px;
}

.boss {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	color: var(--category-accessory);
	border: 1px solid color-mix(in srgb, var(--category-accessory) 40%, transparent);
	border-radius: 2px;
	padding: 2px 6px;
}

.sub {
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.6px;
	color: var(--text-tertiary);
}

.tiles {
	display: grid;
	grid-template-columns: repeat(2, 1fr);
	gap: 12px;
	margin-top: 18px;
}

@media (max-width: 760px) {
	.tiles {
		grid-template-columns: 1fr;
	}
}
</style>
