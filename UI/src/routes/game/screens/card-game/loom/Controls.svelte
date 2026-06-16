<div class="controls">
	<button
		class="btn reflex"
		class:on={view.game.slow}
		title="Hold to slow time (Agility reserve)"
		onpointerdown={() => view.setReflex(true)}
	>
		⏳ REFLEX
	</button>
	<div class="reflexmeter" title="Agility reserve">
		<Bar value={view.game.reflex} ariaLabel="Agility reserve" />
	</div>

	<button class="btn reset" onclick={() => view.reset()}>↺ RESET</button>
</div>

<script lang="ts">
import type { CardGameView } from '../card-game-view.svelte';
import { Bar } from '$components';

interface Props {
	view: CardGameView;
}
const { view }: Props = $props();
</script>

<style lang="scss">
.controls {
	display: flex;
	align-items: center;
	gap: 9px;
	flex-wrap: wrap;
	margin-top: 13px;
}

.btn {
	font-family: var(--mono);
	font-size: 11px;
	font-weight: 500;
	letter-spacing: 0.04em;
	border: 1px solid var(--border-medium);
	background: color-mix(in srgb, var(--white) 3%, transparent);
	color: var(--text-secondary);
	padding: 6px 13px;
	border-radius: 8px;
	cursor: pointer;

	&:hover {
		border-color: var(--border-medium);
		color: var(--text-primary);
	}
	&:active {
		transform: translateY(1px);
	}
}

.btn.reflex {
	// Press-and-hold on touch must drive the slow-time gesture, not scroll the page.
	touch-action: none;
}

.btn.reflex.on {
	background: var(--accent);
	color: var(--text-on-accent);
	border-color: var(--accent);
	box-shadow: 0 0 14px color-mix(in srgb, var(--accent) 40%, transparent);
}

.btn.reset {
	margin-left: auto;
}

.reflexmeter {
	width: 80px;
	--bar-height: 7px;
	--bar-fill: var(--accent);
	--bar-fill-shadow: 0 0 8px color-mix(in srgb, var(--accent) 50%, transparent);
	--bar-transition: width 0.1s linear;
}
</style>
