<div class="topbar">
	<div class="combatant enemy">
		<div class="row">
			<span class="name"><span class="glyph"></span>The Warden</span>
			<span class="hpnum">{game.enemyHP} / {game.enemyMax}</span>
		</div>
		<div class="bar enemy"><i style:width="{(game.enemyHP / game.enemyMax) * 100}%"></i></div>
	</div>

	<div class="combatant you">
		<div class="row">
			<span class="name"><span class="glyph"></span>You</span>
			<span class="hpnum">{game.playerHP} / {game.playerMax}</span>
		</div>
		<div class="bar player"><i style:width="{(game.playerHP / game.playerMax) * 100}%"></i></div>
	</div>

	<div class="drawcluster">
		<span class="mono">deck</span><b>{game.deck.length}</b>
		<div class="drawbar" title="next draw">
			<i style:width="{Math.min(100, (game.drawAcc / game.drawIntervalSec) * 100)}%"></i>
		</div>
		<span class="mono">used</span><b>{game.discard.length}</b>
	</div>
</div>

<script lang="ts">
import type { LoomGame } from '$lib/card-game';

interface Props {
	game: LoomGame;
}
const { game }: Props = $props();
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
	.name {
		color: var(--log-enemy);
	}
	.glyph {
		background: var(--enemy-accent);
		box-shadow: 0 0 8px var(--enemy-accent);
	}
}

.combatant.you {
	.name {
		color: var(--health-remaining-color);
	}
	.glyph {
		background: var(--health-remaining-color);
		box-shadow: 0 0 8px var(--health-remaining-color);
	}
}

.bar {
	height: 9px;
	border-radius: 30px;
	background: color-mix(in srgb, var(--white) 6%, transparent);
	overflow: hidden;
	box-shadow: inset 0 1px 2px rgba(0, 0, 0, 0.4);

	> i {
		display: block;
		height: 100%;
		border-radius: 30px;
		transition: width 0.14s linear;
	}
}

.bar.enemy > i {
	background: linear-gradient(90deg, color-mix(in srgb, var(--enemy-accent) 70%, black), var(--enemy-accent));
	box-shadow: 0 0 10px color-mix(in srgb, var(--enemy-accent) 50%, transparent);
}

.bar.player > i {
	background: linear-gradient(90deg, var(--health-remaining-dark), var(--health-remaining-color));
	box-shadow: 0 0 10px color-mix(in srgb, var(--health-remaining-color) 45%, transparent);
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
	height: 7px;
	border-radius: 30px;
	background: color-mix(in srgb, var(--white) 6%, transparent);
	overflow: hidden;

	> i {
		display: block;
		height: 100%;
		background: var(--gold);
		box-shadow: 0 0 8px color-mix(in srgb, var(--gold) 50%, transparent);
	}
}
</style>
