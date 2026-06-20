<!-- Character progression meter for the Attributes screen: the player's level plus a visible
     current/total XP readout over the shared gold XpBar. Sits beside the points BudgetMeter and
     mirrors its label/value layout, so the two read as one progression dashboard (XP → level →
     stat points to spend). Reads the player manager directly, like the fight-screen card. -->
<div class="level-meter">
	<div class="row">
		<span class="label">Level {playerManager.level}</span>
		<span class="value">
			{formatNum(playerManager.exp)}<span class="xp-total"
				>&nbsp;/&nbsp;{formatNum(playerManager.nextLevelThreshold)} XP</span
			>
		</span>
	</div>
	<XpBar
		level={playerManager.level}
		exp={playerManager.exp}
		nextLevelThreshold={playerManager.nextLevelThreshold}
		ariaLabel="Experience progress"
		testId="attributes-xp-bar"
	/>
</div>

<script lang="ts">
import { formatNum } from '$lib/common';
import { playerManager } from '$lib/engine';
import { XpBar } from '$components';
</script>

<style lang="scss">
.level-meter {
	display: flex;
	flex-direction: column;
	gap: 7px;
	min-width: 210px;
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
	color: var(--gold);
	text-shadow: 0 0 12px color-mix(in srgb, var(--gold) 40%, transparent);
}

.xp-total {
	font-size: 11px;
	font-weight: 400;
	color: var(--text-muted);
}
</style>
