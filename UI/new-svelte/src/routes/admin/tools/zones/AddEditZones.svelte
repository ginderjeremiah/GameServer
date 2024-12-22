{#if initialized}
	<TableEditor
		bind:this={editor}
		{data}
		{hiddenColumns}
		{sampleItem}
		primaryKey="id"
		title="Add/Edit Zones"
	/>
	{@render children()}
{/if}
<Loading loading={!initialized} />

<script lang="ts">
import { TableEditor, Loading } from '$components';
import { ApiRequest, type IChange, type IZone } from '$lib/api';
import { staticData } from '$stores';
import { onMount, type Snippet } from 'svelte';

const { children }: { children: Snippet } = $props();

export const saveChanges = async () => {
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

const hiddenColumns: (keyof IZone)[] = ['zoneDrops'];
const sampleItem = {
	id: -1,
	name: '',
	description: '',
	order: 0,
	levelMin: 0,
	levelMax: 0,
	zoneDrops: []
} satisfies IZone;

onMount(async () => {
	data = await ApiRequest.get('Zones', { refreshCache: true });
	initialized = true;
});
</script>
