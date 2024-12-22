<div class="enemy-select-container">
	<Select bind:value={enemyId} options={enemies} label="Select Enemy" />
</div>
<Loading {loading} />
{#if enemyId > -1 && !loading}
	<TableEditor
		bind:this={editor}
		{data}
		{sampleItem}
		{selectOptions}
		title="Set Attribute Distributions"
	/>
	{@render children()}
{/if}

<script lang="ts">
import { ApiRequest, EAttribute, type IAttributeDistribution } from '$lib/api';
import { Select, Loading, TableEditor, type SelectOptions } from '$components';
import { staticData } from '$stores';
import { onMount, type Snippet } from 'svelte';
import { enumPairs } from '$lib/common';

const { children }: { children: Snippet } = $props();

export const saveChanges = async () => {
	const attributeDistributions = editor?.getRowData();
	if (attributeDistributions?.length) {
		await ApiRequest.post('AdminTools/SetEnemyAttributeDistributions', {
			enemyId,
			attributeDistributions
		});
		staticData.enemies = await ApiRequest.get('Enemies', { refreshCache: true });
	}
};

const selectOptions = {
	attributeId: () => ({ options: enumPairs(EAttribute), disableBlanks: true }) as SelectOptions
};
const sampleItem = {
	attributeId: EAttribute.Strength,
	baseAmount: 0,
	amountPerLevel: 0
};

let editor = $state<{ getRowData: () => IAttributeDistribution[] }>();
let enemyId = $state(-1);
let loading = $state(true);

const enemies = $derived(staticData.enemies);
const data = $derived(enemies[enemyId]?.attributeDistribution);

onMount(async () => {
	try {
		staticData.enemies = await ApiRequest.get('Enemies', { refreshCache: true });
	} finally {
		loading = false;
	}
});
</script>

<style lang="scss">
.enemy-select-container {
	width: 6em;
	margin-bottom: 1em;
}
</style>
