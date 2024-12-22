{#if initialized}
	<TableEditor
		bind:this={editor}
		{data}
		{hiddenColumns}
		{selectOptions}
		{sampleItem}
		primaryKey="id"
		title="Add/Edit Items"
	/>
	{@render children()}
{/if}
<Loading loading={!initialized} />

<script lang="ts">
import { TableEditor, Loading } from '$components';
import { ApiRequest, EItemCategory, type IChange, type IItem, type IItemCategory } from '$lib/api';
import { staticData } from '$stores';
import { onMount, type Snippet } from 'svelte';

const { children }: { children: Snippet } = $props();

export const saveChanges = async () => {
	const changes = editor?.getChanges();
	if (changes?.length) {
		await new ApiRequest('AdminTools/AddEditItems').post(changes);
		data = await ApiRequest.get('Items', { refreshCache: true });
		staticData.items = data;
	}
};

let itemCategories = $state<IItemCategory[]>([]);
let data = $state<IItem[]>([]);
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

onMount(async () => {
	[itemCategories, data] = await Promise.all([
		ApiRequest.get('ItemCategories'),
		ApiRequest.get('Items', { refreshCache: true })
	]);
	initialized = true;
});

function getItemCategories() {
	return {
		options: itemCategories,
		disableBlanks: true
	};
}
</script>
