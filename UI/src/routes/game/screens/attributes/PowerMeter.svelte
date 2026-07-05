<!-- Combat-power readout for the Attributes screen (spike #1526 Decision 7): the server-computed combat
     rating, sitting beside BudgetMeter/LevelMeter as the third progression readout. Gives the inert-stat
     signal (#1528) a numeric companion — allocating a point into an enabler-less amplifier visibly
     doesn't move this number. Server-only value: never recomputed locally, so it updates only once the
     server responds to a save (no live preview of the pending draft). -->
<div class="power-meter">
	<div class="row">
		<span class="label">Power</span>
		<span class="value">{formatNum(Math.round(playerManager.playerRating))}</span>
	</div>
</div>

<script lang="ts">
import { formatNum } from '$lib/common';
import { playerManager } from '$lib/engine';
</script>

<style lang="scss">
.power-meter {
	display: flex;
	flex-direction: column;
	gap: 7px;
	min-width: 90px;
}

.row {
	display: flex;
	align-items: baseline;
	justify-content: space-between;
	gap: 12px;
	white-space: nowrap;
}

.label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-tertiary);
}

.value {
	font-family: var(--sans);
	font-size: 18px;
	font-weight: 600;
	letter-spacing: -0.3px;
	white-space: nowrap;
	color: var(--text-primary);
}
</style>
