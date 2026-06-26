<div class="card-game-frame" data-testid="card-game-screen">
	<div class="cg-header">
		<span class="diamond"></span>
		<div>
			<div class="cg-title">The Initiative Loom <span class="tag">continuous</span></div>
			<div class="cg-subtitle">Combat · Card-Boss Duel — a deliberate active break from the idle grind</div>
		</div>
	</div>

	<div class="cg-body">
		<div class="stage">
			<Combatants game={view.game} />
			<SandboxStrip {view} />
			<Board {view} />
			<Controls {view} />
			<Hand {view} />
			<p class="hint">
				<b>drag</b> = aim up the lane · <b>click</b> or <b>1–7</b> = cast to tail · hold <b>space</b> = slow time · ✦
				strike = ×2 · block the boss's
				<b>CHANNEL</b>
			</p>
		</div>
		<Legend />
	</div>
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { CardGameView } from './card-game-view.svelte';
import { runLoomLoop, bindLoomInput } from './loom-input';
import Combatants from './loom/Combatants.svelte';
import SandboxStrip from './loom/SandboxStrip.svelte';
import Board from './loom/Board.svelte';
import Controls from './loom/Controls.svelte';
import Hand from './loom/Hand.svelte';
import Legend from './loom/Legend.svelte';

const view = new CardGameView();

onMount(() => {
	const stopLoop = runLoomLoop(view);
	const unbindInput = bindLoomInput(view);
	return () => {
		stopLoop();
		unbindInput();
	};
});
</script>

<style lang="scss">
.card-game-frame {
	height: 100%;
	display: flex;
	flex-direction: column;
	color: var(--text-primary);
	font-family: var(--sans);
	overflow: hidden;
}

.cg-header {
	padding: 20px 28px 18px;
	display: flex;
	align-items: center;
	gap: 12px;
	border-bottom: 1px solid color-mix(in srgb, var(--white) 7%, transparent);
	flex-shrink: 0;
}

.diamond {
	width: 11px;
	height: 11px;
	transform: rotate(45deg);
	background: var(--accent);
	box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 60%, transparent);
	flex-shrink: 0;
}

.cg-title {
	font-size: 21px;
	font-weight: 500;
	letter-spacing: -0.3px;
	display: flex;
	align-items: center;
	gap: 9px;
}

.tag {
	font-family: var(--mono);
	font-size: 10px;
	font-weight: 500;
	letter-spacing: 0.1em;
	text-transform: uppercase;
	color: var(--text-on-accent);
	background: var(--accent);
	padding: 3px 9px 2px;
	border-radius: 5px;
}

.cg-subtitle {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.cg-body {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding: 24px 28px 40px;
}

.stage {
	position: relative;
	max-width: 1100px;
	margin: 0 auto;
	border: 1px solid var(--border-medium);
	border-radius: 16px;
	padding: 16px 16px 18px;
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--white) 4%, var(--surface)),
		color-mix(in srgb, var(--surface) 85%, var(--black))
	);
	box-shadow:
		0 0 0 1px color-mix(in srgb, var(--black) 40%, transparent),
		0 30px 70px -30px color-mix(in srgb, var(--black) 80%, transparent),
		inset 0 1px 0 color-mix(in srgb, var(--white) 4%, transparent);
}

.hint {
	text-align: center;
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.02em;
	color: var(--text-muted);
	margin-top: 13px;

	b {
		color: var(--text-tertiary);
		font-weight: 500;
	}
}
</style>
