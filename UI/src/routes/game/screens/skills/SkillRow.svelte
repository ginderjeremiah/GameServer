<button
	type="button"
	class="row"
	class:sel={view.selectedId === metrics.skill.id}
	class:lock={!metrics.unlocked}
	onclick={() => view.select(metrics.skill.id)}
	onmouseenter={gated ? (e) => onGateEnter?.(metrics, e) : undefined}
	onmousemove={gated ? onGateMove : undefined}
	onmouseleave={gated ? onGateLeave : undefined}
>
	<SkillIcon skill={metrics.skill} locked={!metrics.unlocked} size={30} />
	<span class="body">
		<span class="rowname">{metrics.skill.name}</span>
		<span class="rowmeta"
			>{fmt(view.effective(metrics.skill.id))} dmg · {metrics.cooldown.toFixed(1)}s · {fmt(
				view.effectiveDps(metrics.skill.id)
			)} dps</span
		>
	</span>
	{#if view.isEquipped(metrics.skill.id)}
		<span class="slotbadge">{view.slotOf(metrics.skill.id)}</span>
	{/if}
</button>

<script lang="ts">
import { formatNum } from '$lib/common';
import SkillIcon from './SkillIcon.svelte';
import type { SkillMetrics, SkillsView } from './skills-view.svelte';

type Props = {
	metrics: SkillMetrics;
	view: SkillsView;
	/** Hover callbacks that surface a gated skill's gating challenge. Fired only when the row is
	 *  gated (locked + rewarded by a challenge); unlocked rows never invoke them. */
	onGateEnter?: (metrics: SkillMetrics, ev: MouseEvent) => void;
	onGateMove?: (ev: MouseEvent) => void;
	onGateLeave?: () => void;
};

const { metrics, view, onGateEnter, onGateMove, onGateLeave }: Props = $props();

const fmt = (n: number) => formatNum(Math.round(n));

// A locked skill that is some challenge's reward is gated behind that challenge — hovering it
// surfaces the gate. Unlocked skills (and locked ones with no challenge source) show no tooltip.
const gated = $derived(!metrics.unlocked && metrics.source != null);
</script>

<style lang="scss">
.row {
	display: flex;
	align-items: center;
	gap: 9px;
	width: 100%;
	padding: 7px;
	border: 1px solid transparent;
	border-radius: 3px;
	background: transparent;
	cursor: pointer;
	text-align: left;
	color: var(--text-primary);

	&:hover {
		background: color-mix(in srgb, var(--white) 4%, transparent);
	}

	&.sel {
		background: color-mix(in srgb, var(--accent) 12%, transparent);
		border-color: color-mix(in srgb, var(--accent) 40%, transparent);
	}

	&.lock {
		opacity: 0.5;
	}
}

.body {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
}

.rowname {
	font-size: 13.5px;
	line-height: 1.1;
}

.rowmeta {
	margin-top: 2px;
	font-family: var(--mono);
	font-size: 9px;
	color: var(--text-muted);
}

.slotbadge {
	display: flex;
	align-items: center;
	justify-content: center;
	flex-shrink: 0;
	width: 17px;
	height: 17px;
	border-radius: 3px;
	font-family: var(--mono);
	font-size: 9px;
	color: var(--text-on-accent);
	background: var(--accent);
}
</style>
