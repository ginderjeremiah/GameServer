<div class="zone-select-container">
	<Select bind:value={zoneId} options={zones} label="Select Zone" />
</div>
<Loading {loading} />
{#if zoneId > -1 && !loading}
	<TableEditor bind:this={editor} {data} {sampleItem} {selectOptions} title="Set Zone Enemies" />
	{@render children()}
{/if}

<script lang="ts">
import { ApiRequest, type IZoneEnemy } from '$lib/api';
import { Select, Loading, TableEditor, type SelectOptions } from '$components';
import { staticData } from '$stores';
import { onMount, type Snippet } from 'svelte';

const { children }: { children: Snippet } = $props();

export const saveChanges = async () => {
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
.zone-select-container {
	width: 6em;
	margin-bottom: 1em;
}
</style>
