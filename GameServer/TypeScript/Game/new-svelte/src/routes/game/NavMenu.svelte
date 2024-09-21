<div class="nav-menu">
	<button class="hover-glow" on:click="{() => changeScreen('Fight')}">Fight</button>
	<button class="hover-glow" on:click="{() => changeScreen('Inventory')}">Inventory</button>
	<button class="hover-glow" on:click="{() => changeScreen('Attributes')}">Attributes</button>
	<button class="hover-glow" on:click="{() => changeScreen('Stats')}">Stats</button>
	<button class="hover-glow" on:click="{() => changeScreen('Help')}">Help</button>
	<button class="hover-glow" on:click="{() => changeScreen('Options')}">Options</button>
	<button class="hover-glow" on:click="{() => changeScreen('CardGame')}">Card Game</button>
	<button class="hover-glow" on:click="{() => changeScreen('Quit')}">Quit</button>
</div>

<script lang="ts" context="module">
export type GameScreen =
	| 'Fight'
	| 'Inventory'
	| 'Attributes'
	| 'Stats'
	| 'Help'
	| 'Options'
	| 'CardGame'
	| 'Quit';
</script>

<script lang="ts">
import { createEventDispatcher } from 'svelte';

const emit = createEventDispatcher<{
	'change-screen': GameScreen;
}>();

const changeScreen = (screen: GameScreen) => {
	emit('change-screen', screen);
};
</script>

<style lang="scss">
.nav-menu {
	height: fit-content;
	width: 100%;
	background-color: var(--nav-bar-color);
	display: flex;

	button {
		font-size: 1.5rem;
		background-color: transparent;
		outline: none;
		border: none;
		color: var(--default-title-color);
		text-shadow: var(--default-shadow);
		margin: 0.1rem 0.5rem;
	}
}
</style>
