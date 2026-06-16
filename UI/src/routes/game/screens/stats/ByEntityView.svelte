<!-- "By entity" view: entity-kind tabs over a picker + dossier. Mirrors the
     "by statistic" view's tabs → content rhythm so the two read as one system. -->
<div class="by-entity">
	<UnderlineTabs tabs={kindTabs} active={view.entKind} onChange={(k) => view.switchKind(k as StatEntityKind)} />
	<div class="panes">
		<EntityPicker {view} />
		<EntityDossier {view} />
	</div>
</div>

<script lang="ts">
import UnderlineTabs, { type Tab } from './UnderlineTabs.svelte';
import EntityPicker from './EntityPicker.svelte';
import EntityDossier from './EntityDossier.svelte';
import { ENTITY_KINDS, type StatEntityKind, type StatisticsView } from './statistics-view.svelte';
import { statKindColor, statKindPlural } from './statistics-display';

interface Props {
	view: StatisticsView;
}

let { view }: Props = $props();

// `$derived` (not a plain const) so the per-kind counts track live `staticData`
// changes, mirroring `ByStatisticView`'s tabs.
const kindTabs = $derived<Tab[]>(
	ENTITY_KINDS.map((k) => ({
		key: k,
		label: statKindPlural(k),
		color: statKindColor(k),
		count: view.data.entityList(k).length,
		glyphKind: k
	}))
);
</script>

<style lang="scss">
.by-entity {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
}

.panes {
	flex: 1;
	min-height: 0;
	display: grid;
	grid-template-columns: 286px 1fr;
	gap: 22px;
	margin-top: 16px;
}

@media (max-width: 760px) {
	.panes {
		grid-template-columns: 240px 1fr;
		gap: 16px;
	}
}
</style>
