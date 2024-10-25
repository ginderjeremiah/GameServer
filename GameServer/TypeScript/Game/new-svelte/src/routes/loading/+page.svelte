<h1 class="loading-title">Loading</h1>
<div class="center-content">
	<div class="loading-container round-border">
		<div>
			<LoadingSection title="Zones" loading={zonesLoading} />
			<LoadingSection title="Enemies" loading={enemiesLoading} />
			<LoadingSection title="Items" loading={itemsLoading} />
			<LoadingSection title="Skills" loading={skillsLoading} />
			<LoadingSection title="Item Mods" loading={itemModsLoading} />
			<LoadingSection title="Attributes" loading={attributesLoading} />
		</div>
		<Button loading={anyLoading} text="Start Game" onClick={tryStartGame} />
	</div>
</div>

<script lang="ts">
import { ApiRequest } from '$lib/api/api-request';
import { routeTo } from '$lib/common';
import { initializeInventoryItems, staticData } from '$stores';
import Button from '$components/Button.svelte';
import LoadingSection from './LoadingSection.svelte';
import { onMount } from 'svelte';

const zonesLoading = $derived(!staticData.zones);
const enemiesLoading = $derived(!staticData.enemies);
const itemsLoading = $derived(!staticData.items);
const skillsLoading = $derived(!staticData.skills);
const itemModsLoading = $derived(!staticData.itemMods);
const attributesLoading = $derived(!staticData.attributes);

const anyLoading = $derived(
	zonesLoading ||
		enemiesLoading ||
		itemsLoading ||
		skillsLoading ||
		itemModsLoading ||
		attributesLoading
);

const tryStartGame = () => {
	if (!anyLoading) {
		initializeInventoryItems();
		routeTo('/game');
	}
};

const fetchZones = async () => {
	staticData.zones ??= await ApiRequest.get('/api/Zones');
};

const fetchEnemies = async () => {
	staticData.enemies ??= await ApiRequest.get('/api/Enemies');
};

const fetchItems = async () => {
	staticData.items ??= await ApiRequest.get('/api/Items');
};

const fetchSkills = async () => {
	staticData.skills ??= await ApiRequest.get('/api/Skills');
};

const fetchItemMods = async () => {
	staticData.itemMods ??= await ApiRequest.get('/api/ItemMods');
};

const fetchAttributes = async () => {
	staticData.attributes ??= await ApiRequest.get('/api/Attributes');
};

onMount(() => {
	tryStartGame();
	fetchZones();
	fetchEnemies();
	fetchItems();
	fetchSkills();
	fetchItemMods();
	fetchAttributes();
});
</script>

<style lang="scss">
.loading-title {
	text-align: center;
	margin-bottom: 2.5 rem;
	color: var(--title-color);
	text-shadow: var(--default-shadow);
}

.loading-container {
	background-color: var(--container-background-color);
	border: var(--default-border);
	padding: 2rem;
	width: 10rem;
	height: fit-content;
	box-shadow: var(--default-shadow);

	div {
		margin-bottom: 0.5rem;
	}
}
</style>
