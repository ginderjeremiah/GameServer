<div class="fight-screen">
	<div class="zone-nav-wrapper">
		<ZoneNav />
	</div>
	<div class="battlers-container">
		<BattlerCard battler="{$battlePlayer}" />
		<BattlerCard battler="{$battleEnemy}" />
	</div>
</div>

<script lang="ts">
import ZoneNav from './ZoneNav.svelte';
import BattlerCard from './BattlerCard.svelte';
import { battlePlayer, battleEnemy } from '$lib/engine';
</script>

<style lang="scss">
.zone-nav-wrapper {
	margin: 1.5rem 0;
}

.battlers-container {
	display: flex;
	justify-content: space-around;
}
</style>
