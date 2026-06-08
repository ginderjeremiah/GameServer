<div class="handzone">
	<div class="pile">deck<b>{view.game.deck.length}</b></div>

	<div class="hand">
		{#each view.game.hand as hc, i (hc.id)}
			<CardFace card={CARDS[hc.key]} index={i} slot={i + 1} {view} />
		{/each}
		{#each ghosts as g (g)}
			<div class="ghost"></div>
		{/each}
	</div>

	<div class="pile">used<b>{view.game.discard.length}</b></div>
</div>

<script lang="ts">
import { CARDS } from '$lib/card-game';
import type { CardGameView } from '../card-game-view.svelte';
import CardFace from './CardFace.svelte';

interface Props {
	view: CardGameView;
}
const { view }: Props = $props();

// Ghost placeholders fill the hand out to the (Dexterity-scaled) cap so the row
// shows how much capacity remains.
const ghosts = $derived(Array.from({ length: Math.max(0, view.game.handCap - view.game.hand.length) }, (_, i) => i));
</script>

<style lang="scss">
.handzone {
	display: flex;
	align-items: flex-end;
	justify-content: center;
	gap: 14px;
	margin-top: 16px;
}

.pile {
	width: 58px;
	height: 84px;
	border: 1px solid var(--border-subtle);
	border-radius: 9px;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 3px;
	color: var(--text-muted);
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.1em;
	text-transform: uppercase;
	flex-shrink: 0;
	background: color-mix(in srgb, var(--white) 1.5%, transparent);

	b {
		font-size: 19px;
		color: var(--text-secondary);
		font-weight: 500;
	}
}

.hand {
	display: flex;
	gap: 11px;
	justify-content: center;
	flex-wrap: wrap;
	min-height: 140px;
	align-items: flex-end;
}

.ghost {
	width: 100px;
	height: 138px;
	border: 1px dashed var(--border-subtle);
	border-radius: 10px;
	opacity: 0.5;
	background: color-mix(in srgb, var(--white) 1.2%, transparent);
}
</style>
