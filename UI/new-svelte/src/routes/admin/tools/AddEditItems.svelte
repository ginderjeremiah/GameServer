{#if initialized}
	<TableEditor
		bind:this={editor}
		{data}
		{hiddenColumns}
		{selectOptions}
		{sampleItem}
		primaryKey="id"
	/>
{:else}
	<Loading />
{/if}

<script lang="ts">
import { TableEditor, Loading } from '$components';
import { ApiRequest, EItemCategory, type IChange, type IItem, type IItemCategory } from '$lib/api';
import { staticData } from '$stores';
import { onMount } from 'svelte';

export const saveChanges = async () => {
	const changes = editor?.getChanges();
	if (changes?.length) {
		await new ApiRequest('AdminTools/AddEditItems').post(changes);
		staticData.items = await ApiRequest.get('Items', { refreshCache: true });
	}
};

let itemCategories = $state<IItemCategory[]>([]);
let initialized = $state(false);
let editor = $state<{ getChanges: () => IChange<IItem>[] }>();

const hiddenColumns: (keyof IItem)[] = ['attributes'];
const selectOptions = { itemCategoryId: getItemCategories };
const sampleItem = {
	id: -1,
	name: '',
	iconPath: '',
	itemCategoryId: EItemCategory.Helm,
	description: '',
	attributes: []
} satisfies IItem;

const data = $derived(staticData.items);

onMount(async () => {
	itemCategories = await ApiRequest.get('ItemCategories');
	initialized = true;
});

function getItemCategories() {
	return {
		options: itemCategories,
		disableBlanks: true
	};
}
</script>
