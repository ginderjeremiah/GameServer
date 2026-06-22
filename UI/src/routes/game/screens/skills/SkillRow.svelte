<button
	type="button"
	class="row"
	class:sel={view.selectedId === metrics.skill.id}
	onclick={() => view.select(metrics.skill.id)}
>
	<SkillIcon skill={metrics.skill} size={30} />
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
};

const { metrics, view }: Props = $props();

const fmt = (n: number) => formatNum(Math.round(n));
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

	&:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
	}

	&.sel {
		background: color-mix(in srgb, var(--accent) 12%, transparent);
		border-color: color-mix(in srgb, var(--accent) 40%, transparent);
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
