<div class="topbar">
	<div class="combatant enemy">
		<div class="row">
			<span class="name"><span class="glyph"></span>The Warden</span>
			<span class="hpnum">{game.enemyHP} / {game.enemyMax}</span>
		</div>
		<Bar
			value={game.enemyHP}
			max={game.enemyMax}
			ariaLabel="The Warden health"
			valueText="{game.enemyHP} / {game.enemyMax}"
		/>
	</div>

	<div class="combatant you">
		<div class="row">
			<span class="name"><span class="glyph"></span>You</span>
			<span class="hpnum">{game.playerHP} / {game.playerMax}</span>
		</div>
		<Bar
			value={game.playerHP}
			max={game.playerMax}
			ariaLabel="Your health"
			valueText="{game.playerHP} / {game.playerMax}"
		/>
	</div>

	<div class="drawcluster">
		<span class="mono">deck</span><b>{game.deck.length}</b>
		<div class="drawbar" title="next draw">
			<Bar value={drawPerc} ariaLabel="Next draw progress" />
		</div>
		<span class="mono">used</span><b>{game.discard.length}</b>
	</div>
</div>

<script lang="ts">
import type { LoomGame } from '$lib/card-game';
import { Bar } from '$components';

interface Props {
	game: LoomGame;
}
const { game }: Props = $props();

// Percentage toward the next draw; Bar clamps the overshoot to 100%.
const drawPerc = $derived((game.drawAcc / game.drawIntervalSec) * 100);
</script>

<style lang="scss">
.topbar {
	display: flex;
	align-items: center;
	gap: 20px;
	flex-wrap: wrap;
	margin-bottom: 13px;
}

.combatant {
	display: flex;
	flex-direction: column;
	gap: 5px;
	min-width: 210px;
	flex: 1;

	// Shared HP-bar track geometry; the fill treatment differs per side below.
	--bar-height: 9px;
	--bar-track-shadow: inset 0 1px 2px color-mix(in srgb, var(--black) 40%, transparent);

	.row {
		display: flex;
		justify-content: space-between;
		align-items: baseline;
	}

	.name {
		font-family: var(--sans);
		font-weight: 600;
		font-size: 14px;
		display: flex;
		align-items: center;
		gap: 7px;
	}

	.glyph {
		width: 8px;
		height: 8px;
		transform: rotate(45deg);
	}

	.hpnum {
		font-family: var(--mono);
		font-size: 11px;
		color: var(--text-tertiary);
	}
}

.combatant.enemy {
	--bar-fill: linear-gradient(90deg, color-mix(in srgb, var(--enemy-accent) 70%, var(--black)), var(--enemy-accent));
	--bar-fill-shadow: 0 0 10px color-mix(in srgb, var(--enemy-accent) 50%, transparent);

	.name {
		color: var(--log-enemy);
	}
	.glyph {
		background: var(--enemy-accent);
		box-shadow: 0 0 8px var(--enemy-accent);
	}
}

.combatant.you {
	--bar-fill: linear-gradient(90deg, var(--health-remaining-dark), var(--health-remaining-color));
	--bar-fill-shadow: 0 0 10px color-mix(in srgb, var(--health-remaining-color) 45%, transparent);

	.name {
		color: var(--health-remaining-color);
	}
	.glyph {
		background: var(--health-remaining-color);
		box-shadow: 0 0 8px var(--health-remaining-color);
	}
}

.drawcluster {
	display: flex;
	align-items: center;
	gap: 8px;

	.mono {
		font-family: var(--mono);
		font-size: 10px;
		letter-spacing: 0.06em;
		text-transform: uppercase;
		color: var(--text-muted);
	}

	b {
		color: var(--text-secondary);
		font-family: var(--mono);
		font-size: 12px;
	}
}

.drawbar {
	width: 54px;
	--bar-height: 7px;
	--bar-fill: var(--gold);
	--bar-fill-shadow: 0 0 8px color-mix(in srgb, var(--gold) 50%, transparent);
	// The meter retracks every frame, so it fills instantly rather than easing.
	--bar-transition: none;
}
</style>
