<div class="tool-gate">
	<GateSelect label="Skill" options={skills} bind:value={skillId} />
</div>
<Loading {loading} delay={50} />
{#if skillId > -1 && !loading}
	<TableEditor
		bind:this={editor}
		{data}
		{sampleItem}
		{selectOptions}
		title="Set Skill Multipliers"
		onSave={saveChanges}
	/>
{/if}

<script lang="ts">
import { ApiRequest, EAttribute, type IAttributeMultiplier, type IChange } from '$lib/api';
import { Loading, TableEditor, type SelectOptions } from '$components';
import GateSelect from '../../GateSelect.svelte';
import { staticData } from '$stores';
import { onMount } from 'svelte';
import { enumPairs } from '$lib/common';

const saveChanges = async () => {
	const multiplierChanges = editor?.getChanges();
	if (multiplierChanges?.length) {
		const changes = multiplierChanges.map((c) => ({
			changeType: c.changeType,
			item: {
				attributeId: c.item.attributeId,
				amount: c.item.multiplier
			}
		}));
		await ApiRequest.post('AdminTools/SetSkillMultipliers', { id: skillId, changes });
		staticData.skills = await ApiRequest.get('Skills', { refreshCache: true });
	}
};

const selectOptions = {
	attributeId: () => ({ options: enumPairs(EAttribute), disableBlanks: true }) as SelectOptions
};
const sampleItem: IAttributeMultiplier = {
	attributeId: EAttribute.Strength,
	multiplier: 1
};

let editor = $state<{ getChanges: () => IChange<IAttributeMultiplier>[] }>();
let skillId = $state(-1);
let loading = $state(true);

const skills = $derived(staticData.skills);
const data = $derived(skills[skillId]?.damageMultipliers);

onMount(async () => {
	try {
		staticData.skills = await ApiRequest.get('Skills', { refreshCache: true });
	} finally {
		loading = false;
	}
});
</script>

<style lang="scss">
.tool-gate {
	margin-bottom: 18px;
}
</style>
