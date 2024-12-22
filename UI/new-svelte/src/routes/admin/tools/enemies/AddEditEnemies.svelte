{#if initialized}
	<TableEditor
		bind:this={editor}
		{data}
		{hiddenColumns}
		{sampleItem}
		primaryKey="id"
		title="Add/Edit Enemies"
	/>
	{@render children()}
{/if}
<Loading loading={!initialized} />

<script lang="ts">
import { TableEditor, Loading } from '$components';
import { ApiRequest, type IEnemy, type IChange } from '$lib/api';
import { staticData } from '$stores';
import { onMount, type Snippet } from 'svelte';

const { children }: { children: Snippet } = $props();

export const saveChanges = async () => {
	const changes = editor?.getChanges();
	if (changes?.length) {
		await new ApiRequest('AdminTools/AddEditEnemies').post(changes);
		data = await ApiRequest.get('Enemies', { refreshCache: true });
		staticData.enemies = data;
	}
};

let data = $state<IEnemy[]>([]);
let initialized = $state(false);
let editor = $state<{ getChanges: () => IChange<IEnemy>[] }>();

const hiddenColumns: (keyof IEnemy)[] = ['attributeDistribution', 'drops', 'skillPool'];
const sampleItem = {
	id: -1,
	name: '',
	attributeDistribution: [],
	drops: [],
	skillPool: []
} satisfies IEnemy;

onMount(async () => {
	data = await ApiRequest.get('Enemies', { refreshCache: true });
	initialized = true;
});
</script>
