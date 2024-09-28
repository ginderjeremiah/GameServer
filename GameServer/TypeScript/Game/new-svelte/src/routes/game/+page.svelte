<div class="game-container">
	<NavMenu on:change-screen="{handleChangeScreen}" />
	<div class="screen-container">
		<svelte:component this="{currentScreen}" />
	</div>
	<div class="log-container round-border">
		{#each $logs as log (log.id)}
			<div class="log-message">
				<span>{log.message}</span>
			</div>
		{/each}
	</div>
</div>

<script lang="ts">
import NavMenu from './NavMenu.svelte';
import type { GameScreen } from './NavMenu.svelte';
import { Fight, Inventory, Attributes, Stats, Help, Options, CardGame, Quit } from './screens';
import { startGame } from '$lib/engine';
import { browser } from '$app/environment';
import { logs } from '$stores/logs';

if (browser) {
	startGame();
}

let currentScreen = Fight;

const handleChangeScreen = (event: CustomEvent<GameScreen>) => {
	switch (event.detail) {
		default:
		case 'Fight':
			currentScreen = Fight;
			break;
		case 'Inventory':
			currentScreen = Inventory;
			break;
		case 'Attributes':
			currentScreen = Attributes;
			break;
		case 'Stats':
			currentScreen = Stats;
			break;
		case 'Help':
			currentScreen = Help;
			break;
		case 'Options':
			currentScreen = Options;
			break;
		case 'CardGame':
			currentScreen = CardGame;
			break;
		case 'Quit':
			currentScreen = Quit;
	}
};
</script>

<style lang="scss">
.game-container {
	width: 100%;
	height: 100%;
	display: flex;
	flex-direction: column;

	.screen-container {
		height: 100%;
		width: 100%;
	}

	.log-container {
		background-color: var(--container-background-color);
		box-sizing: border-box;
		margin: 1% 5% 5% 5%;
		height: 60%;
		overflow: auto;

		.log-message {
			border: var(--default-border);
			border-top: none;
			box-sizing: border-box;
			padding: 0.1rem;
			font-size: 0.75rem;
		}
	}
}
</style>
