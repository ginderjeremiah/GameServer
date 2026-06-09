<div
	class="sandbox"
	title="Demo-only: these stand in for the player's real attributes until the minigame is wired to character data."
>
	<span class="lab">sandbox</span>

	<span class="stat s-agi">
		<span class="nm">AGI</span>
		<input
			type="range"
			min="0"
			max="300"
			value={game.agi}
			oninput={(e) => view.setStat('agi', +e.currentTarget.value)}
		/>
		<b>{game.agi}</b>
	</span>
	<span class="res">draw <b>{game.drawIntervalSec.toFixed(2)}</b>s</span>

	<span class="stat s-dex">
		<span class="nm">DEX</span>
		<input
			type="range"
			min="1"
			max="500"
			value={game.dex}
			oninput={(e) => view.setStat('dex', +e.currentTarget.value)}
		/>
		<b>{game.dex}</b>
	</span>
	<span class="res">hand <b>{game.handCap}</b></span>

	<span class="stat s-luck">
		<span class="nm">LCK</span>
		<input
			type="range"
			min="0"
			max="60"
			value={game.luck}
			oninput={(e) => view.setStat('luck', +e.currentTarget.value)}
		/>
		<b>{game.luck}</b>
	</span>
	<span class="res">✦ /<b>{game.critGap}</b>t</span>
</div>

<script lang="ts">
import type { CardGameView } from '../card-game-view.svelte';

interface Props {
	view: CardGameView;
}
const { view }: Props = $props();
const game = $derived(view.game);
</script>

<style lang="scss">
.sandbox {
	display: flex;
	align-items: center;
	gap: 18px;
	flex-wrap: wrap;
	margin-bottom: 12px;
	padding: 8px 12px;
	border: 1px solid var(--border-subtle);
	border-radius: 10px;
	background: color-mix(in srgb, var(--white) 2%, transparent);
}

.lab {
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.18em;
	text-transform: uppercase;
	color: var(--text-muted);
}

.stat {
	display: flex;
	align-items: center;
	gap: 8px;
	font-family: var(--mono);
	font-size: 12px;
	color: var(--text-secondary);

	.nm {
		font-weight: 700;
		letter-spacing: 0.05em;
	}
}

.stat.s-agi .nm {
	color: var(--attr-agility);
}
.stat.s-dex .nm {
	color: var(--attr-dexterity);
}
.stat.s-luck .nm {
	color: var(--attr-luck);
}

.res {
	font-family: var(--sans);
	font-size: 12px;
	color: var(--text-tertiary);

	b {
		color: var(--text-primary);
		font-family: var(--mono);
	}
}

input[type='range'] {
	width: 84px;
	height: 3px;
	-webkit-appearance: none;
	appearance: none;
	background: color-mix(in srgb, var(--white) 14%, transparent);
	border-radius: 3px;
	outline: none;

	&::-webkit-slider-thumb {
		-webkit-appearance: none;
		width: 13px;
		height: 13px;
		border-radius: 50%;
		background: var(--accent);
		cursor: pointer;
		box-shadow: 0 0 7px color-mix(in srgb, var(--accent) 60%, transparent);
	}
	&::-moz-range-thumb {
		width: 13px;
		height: 13px;
		border: 0;
		border-radius: 50%;
		background: var(--accent);
		cursor: pointer;
	}
}

.stat.s-agi input[type='range']::-webkit-slider-thumb {
	background: var(--attr-agility);
	box-shadow: 0 0 7px color-mix(in srgb, var(--attr-agility) 60%, transparent);
}
.stat.s-dex input[type='range']::-webkit-slider-thumb {
	background: var(--attr-dexterity);
	box-shadow: 0 0 7px color-mix(in srgb, var(--attr-dexterity) 60%, transparent);
}
.stat.s-luck input[type='range']::-webkit-slider-thumb {
	background: var(--attr-luck);
	box-shadow: 0 0 7px color-mix(in srgb, var(--attr-luck) 60%, transparent);
}
.stat.s-agi input[type='range']::-moz-range-thumb {
	background: var(--attr-agility);
}
.stat.s-dex input[type='range']::-moz-range-thumb {
	background: var(--attr-dexterity);
}
.stat.s-luck input[type='range']::-moz-range-thumb {
	background: var(--attr-luck);
}
</style>
