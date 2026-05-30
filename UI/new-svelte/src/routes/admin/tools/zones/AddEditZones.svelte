{#if initialized}
	<TableEditor
		bind:this={editor}
		{data}
		{sampleItem}
		primaryKey="id"
		title="Add/Edit Zones"
		onSave={saveChanges}
	/>
{/if}
<Loading loading={!initialized} />

<script lang="ts">
import { TableEditor, Loading } from '$components';
import { ApiRequest, type IChange, type IZone } from '$lib/api';
import { staticData } from '$stores';
import { onMount } from 'svelte';

const saveChanges = async () => {
	const changes = editor?.getChanges();
	if (changes?.length) {
		await new ApiRequest('AdminTools/AddEditZones').post(changes);
		data = await ApiRequest.get('Zones', { refreshCache: true });
		staticData.zones = data;
	}
};

let data = $state<IZone[]>([]);
let initialized = $state(false);
let editor = $state<{ getChanges: () => IChange<IZone>[] }>();

const sampleItem = {
	id: -1,
	name: '',
	description: '',
	order: 0,
	levelMin: 0,
	levelMax: 0,
} satisfies IZone;

onMount(async () => {
	data = await ApiRequest.get('Zones', { refreshCache: true });
	initialized = true;
});
</script>
