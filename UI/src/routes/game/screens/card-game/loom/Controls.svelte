<div class="controls">
	<button
		class="btn reflex"
		class:on={view.game.slow}
		title="Hold to slow time (Agility reserve)"
		onmousedown={() => view.setReflex(true)}
	>
		⏳ REFLEX
	</button>
	<div
		class="reflexmeter"
		title="Agility reserve"
		role="progressbar"
		aria-label="Agility reserve"
		aria-valuenow={Math.round(view.game.reflex)}
		aria-valuemin={0}
		aria-valuemax={100}
	>
		<i style:width="{view.game.reflex}%"></i>
	</div>

	<button class="btn reset" onclick={() => view.reset()}>↺ RESET</button>
</div>

<script lang="ts">
import type { CardGameView } from '../card-game-view.svelte';

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
	height: 7px;
	border-radius: 30px;
	background: color-mix(in srgb, var(--white) 6%, transparent);
	overflow: hidden;

	> i {
		display: block;
		height: 100%;
		background: var(--accent);
		box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 50%, transparent);
		transition: width 0.1s linear;
	}
}
</style>
