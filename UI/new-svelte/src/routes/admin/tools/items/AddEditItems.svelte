{#if initialized}
	<TableEditor
		bind:this={editor}
		{data}
		{hiddenColumns}
		{selectOptions}
		{sampleItem}
		primaryKey="id"
		title="Add/Edit Items"
		onSave={saveChanges}
	/>
{/if}
<Loading loading={!initialized} delay={50} />

<script lang="ts">
import { TableEditor, Loading } from '$components';
import { ApiRequest, EItemCategory, ERarity, type IChange, type IItem, type IItemCategory } from '$lib/api';
import { staticData } from '$stores';
import { enumPairs } from '$lib/common';
import { onMount } from 'svelte';

const saveChanges = async () => {
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

const hiddenColumns: (keyof IItem)[] = ['attributes', 'modSlots'];
const selectOptions = { itemCategoryId: getItemCategories, rarityId: getRarities };
const sampleItem = {
	id: -1,
	name: '',
	iconPath: '',
	itemCategoryId: EItemCategory.Helm,
	rarityId: ERarity.Common,
	description: '',
	attributes: [],
	modSlots: []
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

function getRarities() {
	return {
		options: enumPairs(ERarity),
		disableBlanks: true
	};
}
</script>
