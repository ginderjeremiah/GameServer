<!-- Zone dossier spawn table: every non-retired enemy that spawns in the zone, with its share of the
     zone's total spawn weight. Each row is a cross-link into that enemy's dossier. -->
<SpawnRowList
	heading={`Spawn table · ${view.zonesTab.zoneSpawnCount}`}
	rows={view.zonesTab.zoneSpawns.map((spawn) => ({
		id: spawn.enemyId,
		name: spawn.enemyName,
		share: spawn.share,
		weightLabel: spawn.weightLabel
	}))}
	emptyMessage="No spawns — encounter-only zone"
	testidPrefix="codex-zone-spawn"
	onSelect={(id) => view.openEnemy(id)}
/>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import SpawnRowList from './SpawnRowList.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>
