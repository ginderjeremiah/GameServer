{#if initialized}
	<TableEditor
		bind:this={editor}
		{data}
		{hiddenColumns}
		{sampleItem}
		primaryKey="id"
		title="Add/Edit Skills"
	/>
	{@render children()}
{/if}
<Loading loading={!initialized} />

<script lang="ts">
import { TableEditor, Loading } from '$components';
import { ApiRequest, type ISkill, type IChange } from '$lib/api';
import { staticData } from '$stores';
import { onMount, type Snippet } from 'svelte';

const { children }: { children: Snippet } = $props();

export const saveChanges = async () => {
	const changes = editor?.getChanges();
	if (changes?.length) {
		await new ApiRequest('AdminTools/AddEditSkills').post(changes);
		staticData.skills = await ApiRequest.get('Skills', { refreshCache: true });
	}
};

let initialized = $state(false);
let editor = $state<{ getChanges: () => IChange<ISkill>[] }>();

const data = $derived(staticData.skills);

const hiddenColumns: (keyof ISkill)[] = ['damageMultipliers'];
const sampleItem = {
	id: -1,
	name: '',
	description: '',
	baseDamage: 0,
	cooldownMs: 1000,
	damageMultipliers: [],
	iconPath: ''
} satisfies ISkill;

onMount(async () => {
	staticData.skills = await ApiRequest.get('Skills', { refreshCache: true });
	initialized = true;
});
</script>
