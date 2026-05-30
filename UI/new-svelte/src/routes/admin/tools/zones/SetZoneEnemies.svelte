<div class="tool-gate">
	<GateSelect label="Zone" options={zones} bind:value={zoneId} />
</div>
<Loading {loading} />
{#if zoneId > -1 && !loading}
	<TableEditor bind:this={editor} {data} {sampleItem} {selectOptions} title="Set Zone Enemies" onSave={saveChanges} />
{/if}

<script lang="ts">
import { ApiRequest, type IZoneEnemy } from '$lib/api';
import { Loading, TableEditor, type SelectOptions } from '$components';
import GateSelect from '../../GateSelect.svelte';
import { staticData } from '$stores';
import { onMount } from 'svelte';

const saveChanges = async () => {
	const zoneEnemies = editor?.getRowData();
	if (zoneEnemies?.length) {
		await ApiRequest.post('AdminTools/SetZoneEnemies', { zoneId, zoneEnemies });
		await getZoneEnemies();
	}
};

const selectOptions = {
	enemyId: () => ({ options: enemies, disableBlanks: true }) as SelectOptions
};
const sampleItem: IZoneEnemy = {
	enemyId: 0,
	weight: 5
};

let data = $state<IZoneEnemy[]>([]);
let editor = $state<{ getRowData: () => IZoneEnemy[] }>();
let zoneId = $state(-1);
let loading = $state(true);

const zones = $derived(staticData.zones);
const enemies = $derived(staticData.enemies);

onMount(async () => {
	try {
		const zonesPromise = ApiRequest.get('Zones', { refreshCache: true });
		const enemiesPromise = ApiRequest.get('Enemies', { refreshCache: true });
		staticData.zones = await zonesPromise;
		staticData.enemies = await enemiesPromise;
	} finally {
		loading = false;
	}
});

$effect(() => {
	if (zoneId > -1) {
		getZoneEnemies();
	}
});

const getZoneEnemies = async () => {
	loading = true;
	try {
		data = await ApiRequest.get('Zones/ZoneEnemies', { zoneId });
	} catch {
		data = [];
	} finally {
		loading = false;
	}
};
</script>

<style lang="scss">
.tool-gate {
	margin-bottom: 18px;
}
</style>
