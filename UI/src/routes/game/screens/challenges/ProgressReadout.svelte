<div>
	{#if c.prog.atMost}
		<!-- Minimisation goal (e.g. TimeTrial): show best vs target, never a 0→goal fill. -->
		<div class="row" class:with-bar={showBar}>
			<span class="best">
				<span class="best-value" class:done class:muted={!c.prog.hasData}>
					{c.prog.hasData ? formatTime(c.prog.best) : 'no time yet'}
				</span>
				{#if c.prog.hasData}<span class="best-label">best</span>{/if}
			</span>
			<span class="beat">
				<span class="beat-label">BEAT</span>
				<span class="beat-target" style:color={c.typeAccent}>≤ {formatTime(c.prog.target)}</span>
			</span>
		</div>
	{:else}
		<div class="row" class:with-bar={showBar}>
			<span class="count">
				<span class:done>{formatCount(c.prog.value)}</span>
				<span class="muted"> / {formatCount(c.prog.goal)}</span>
				<span class="muted"> {c.unit}</span>
			</span>
			{#if !done}<span class="pct">{Math.round(c.prog.percent)}%</span>{/if}
		</div>
	{/if}
	{#if showBar}
		<ProgressBar percent={c.prog.percent} accent={c.typeAccent} {done} height={barHeight} />
	{/if}
</div>

<script lang="ts">
import { formatTime } from '$lib/common';
import { formatCount } from './challenge-meta';
import ProgressBar from './ProgressBar.svelte';
import type { ChallengeVM } from './challenges-view.svelte';

interface Props {
	c: ChallengeVM;
	showBar?: boolean;
	barHeight?: number;
}

const { c, showBar = true, barHeight = 5 }: Props = $props();

const done = $derived(c.state === 'done');
</script>

<style lang="scss">
.row {
	display: flex;
	align-items: baseline;
	justify-content: space-between;
	gap: 10px;

	&.with-bar {
		margin-bottom: 6px;
	}
}

.best {
	display: inline-flex;
	align-items: baseline;
	gap: 6px;
}

.best-value {
	font-family: var(--mono);
	font-size: 13px;
	color: var(--text-primary);

	&.done {
		color: var(--success);
	}
	&.muted {
		color: var(--text-muted);
	}
}

.best-label {
	font-family: var(--mono);
	font-size: 9.5px;
	color: var(--text-muted);
}

.beat {
	display: inline-flex;
	align-items: baseline;
	gap: 5px;
}

.beat-label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	color: var(--text-muted);
}

.beat-target {
	font-family: var(--mono);
	font-size: 12px;
}

.count {
	font-family: var(--mono);
	font-size: 12px;
	letter-spacing: 0.3px;
	color: var(--text-primary);

	.done {
		color: var(--success);
	}
	.muted {
		color: var(--text-muted);
	}
}

.pct {
	font-family: var(--mono);
	font-size: 10.5px;
	color: var(--text-tertiary);
}
</style>
