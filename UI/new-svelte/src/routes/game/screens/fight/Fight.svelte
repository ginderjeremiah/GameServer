<div class="fight-screen" data-testid="fight-screen">
	<div class="zone-nav-wrapper">
		<ZoneNav />
	</div>
	<div class="combatants">
		<BattlerCard battler={battleEngine.player} side="player" />

		<!-- VS badge -->
		<div class="vs-badge" data-testid="vs-badge">
			<div class="vs-diamond"></div>
			<div class="vs-diamond-inner"></div>
			<span class="vs-text">vs</span>
		</div>

		<BattlerCard battler={battleEngine.enemy} side="enemy" />
	</div>
</div>

<script lang="ts">
import ZoneNav from './ZoneNav.svelte';
import BattlerCard from './BattlerCard.svelte';
import { battleEngine } from '$lib/engine';
</script>

<style lang="scss">
.fight-screen {
	width: 100%;
	height: 100%;
	display: flex;
	flex-direction: column;
	overflow: auto;
}

.zone-nav-wrapper {
	padding: 22px 32px 0;
	display: flex;
	justify-content: center;
}

.combatants {
	flex: 1;
	position: relative;
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 40px;
	padding: 20px 36px 96px;
	min-height: 0;
}

.vs-badge {
	width: 56px;
	height: 56px;
	position: relative;
	display: flex;
	align-items: center;
	justify-content: center;
	flex-shrink: 0;
}

.vs-diamond {
	position: absolute;
	inset: 0;
	border: 1px solid var(--border-medium);
	transform: rotate(45deg);
	background: color-mix(in srgb, var(--surface) 60%, transparent);
}

.vs-diamond-inner {
	position: absolute;
	inset: 14px;
	background: color-mix(in srgb, var(--white) 4%, transparent);
	transform: rotate(45deg);
	box-shadow: inset 0 0 12px var(--border-subtle);
}

.vs-text {
	position: relative;
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--text-primary) 85%, transparent);
}
</style>
