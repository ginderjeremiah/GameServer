<!-- Spawns sub-tab: the zones this enemy spawns in (with its share of each zone's spawn table), or a
     single "Encounter" row for a boss. Each row cross-links into that zone's dossier. -->
<SpawnRowList
	heading={view.enemiesTab.spawnHeading}
	rows={view.enemiesTab.spawns.map((spawn) => ({
		id: spawn.zoneId,
		name: spawn.zoneName,
		share: spawn.share,
		weightLabel: spawn.weightLabel
	}))}
	testidPrefix="codex-enemy-spawn"
	onSelect={(id) => view.openZone(id)}
/>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import SpawnRowList from './SpawnRowList.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>
