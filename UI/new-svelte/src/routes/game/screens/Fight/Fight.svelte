<div class="fight-screen">
	<div class="zone-nav-wrapper">
		<ZoneNav />
	</div>
	<div class="battlers-container">
		<BattlerCard battler={battleState.player} />
		<BattlerCard battler={battleState.enemy} />
	</div>
</div>

<script lang="ts">
import ZoneNav from './ZoneNav.svelte';
import BattlerCard from './BattlerCard.svelte';
import { BattleStage, battleState } from '$lib/engine';
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
