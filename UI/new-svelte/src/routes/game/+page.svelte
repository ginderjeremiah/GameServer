<div class="game-container">
	<NavMenu {navMenuItems} />
	<div class="screen-container">
		<CurrentScreen />
	</div>
	<div class="log-container round-border">
		{#each logs() as log (log.id)}
			<div class="log-message">
				<span>{log.message}</span>
			</div>
		{/each}
	</div>
</div>

<script lang="ts">
import { NavMenu, type INavMenuItem } from '$components';
import { screenMap } from './screens';
import { startGame } from '$lib/engine';
import { browser } from '$app/environment';
import { logs } from '$stores';
import { normalizeText, routeTo } from '$lib/common';

if (browser) {
	startGame();
}

let CurrentScreen = $state(screenMap.Fight);

const navMenuItems: INavMenuItem[] = Object.entries(screenMap).map(([text, screen]) => ({
	text: normalizeText(text),
	onClick: () => (CurrentScreen = screen)
}));

navMenuItems.push({
	text: 'Admin',
	onClick: () => routeTo('/admin')
});
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
		margin: 1% 5% 2.5% 5%;
		height: 60%;
		overflow: auto;
		user-select: text;

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
