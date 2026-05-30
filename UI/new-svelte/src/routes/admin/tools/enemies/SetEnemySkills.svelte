<div class="tool-gate">
	<GateSelect label="Enemy" options={enemies} bind:value={enemyId} />
</div>
<Loading {loading} />
{#if enemyId > -1 && !loading}
	<TableEditor bind:this={editor} {data} {sampleItem} {selectOptions} title="Set Enemy Skills" onSave={saveChanges} />
{/if}

<script lang="ts">
import { ApiRequest } from '$lib/api';
import { Loading, TableEditor, type SelectOptions } from '$components';
import GateSelect from '../../GateSelect.svelte';
import { staticData } from '$stores';
import { onMount } from 'svelte';

const saveChanges = async () => {
	const enemySkills = editor?.getRowData();
	if (enemySkills?.length) {
		const skillIds = enemySkills.map((c) => c.skillId);
		await ApiRequest.post('AdminTools/SetEnemySkills', { enemyId, skillIds });
		staticData.enemies = await ApiRequest.get('Enemies', { refreshCache: true });
	}
};

const selectOptions = {
	skillId: () => ({ options: skills, disableBlanks: true }) as SelectOptions
};
const sampleItem = { skillId: 0 };

let editor = $state<{ getRowData: () => { skillId: number }[] }>();
let enemyId = $state(-1);
let loading = $state(true);

const enemies = $derived(staticData.enemies);
const skills = $derived(staticData.skills);
const data = $derived(enemies[enemyId]?.skillPool.map((skillId) => ({ skillId })));

onMount(async () => {
	try {
		[staticData.enemies, staticData.skills] = await Promise.all([
			await ApiRequest.get('Enemies', { refreshCache: true }),
			await ApiRequest.get('Skills', { refreshCache: true })
		]);
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
