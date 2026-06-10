<div class="skills-frame" data-testid="skills-screen">
	<div class="head">
		<span class="diamond"></span>
		<div>
			<div class="eyebrow">Equip the skills you fight with</div>
			<h1 class="title">Skills</h1>
		</div>
		<div class="spacer"></div>
		<div class="head-totals">
			<div class="stat"><span class="v">{view.equipped.length}/{view.cap}</span><span class="k">equipped</span></div>
			<div class="stat">
				<span class="v accent">{fmt(view.combinedEffectiveDps)}</span><span class="k">eff. dps</span>
			</div>
			<div class="stat"><span class="v">{fmt(view.combinedEffectiveBurst)}</span><span class="k">eff. burst</span></div>
		</div>
	</div>

	<div class="versus">
		<span class="vs-eyebrow">Compare vs</span>
		<span class="vs-note">Drag to preview damage against an enemy's flat Defense</span>
		<div class="vs-slider">
			<span class="vs-eyebrow">Defense</span>
			<input
				type="range"
				min="0"
				max={view.maxDamage}
				value={view.defense}
				aria-label="Compare-vs enemy defense"
				oninput={(e) => view.setDefense(+e.currentTarget.value)}
			/>
			<span class="vs-defval">DEF <b>{Math.round(view.defense)}</b></span>
		</div>
	</div>

	<div class="cols">
		<SkillRail {view} />
		<SkillInspector {view} />
	</div>

	<EquippedBand {view} />

	<SortFilterModal {view} />
</div>

<script lang="ts">
import { formatNum } from '$lib/common';
import { SkillsView } from './skills-view.svelte';
import SkillRail from './SkillRail.svelte';
import SkillInspector from './SkillInspector.svelte';
import EquippedBand from './EquippedBand.svelte';
import SortFilterModal from './SortFilterModal.svelte';

const view = new SkillsView();

const fmt = (n: number) => formatNum(Math.round(n));
</script>

<style lang="scss">
.skills-frame {
	position: relative;
	height: 100%;
	padding: 22px 26px 26px;
	display: flex;
	flex-direction: column;
	overflow: hidden;
}

.head {
	display: flex;
	align-items: center;
	gap: 13px;

	.spacer {
		flex: 1;
	}
}

.diamond {
	width: 11px;
	height: 11px;
	flex-shrink: 0;
	transform: rotate(45deg);
	background: var(--accent);
	box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 60%, transparent);
}

.eyebrow {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.title {
	margin: 0;
	font-size: 21px;
	font-weight: 500;
	letter-spacing: -0.3px;
	line-height: 1;
}

.head-totals {
	display: flex;
	gap: 24px;
	align-items: center;
	padding: 8px 16px;
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	background: color-mix(in srgb, var(--white) 4%, transparent);
}

.stat {
	display: flex;
	flex-direction: column;
	gap: 2px;

	.v {
		font-size: 17px;
		font-weight: 500;
		line-height: 1;

		&.accent {
			color: var(--accent);
		}
	}

	.k {
		font-family: var(--mono);
		font-size: 8px;
		letter-spacing: 1.2px;
		text-transform: uppercase;
		color: var(--text-muted);
	}
}

.versus {
	display: flex;
	align-items: center;
	gap: 16px;
	margin-top: 14px;
	padding: 10px 14px;
	border: 1px solid color-mix(in srgb, var(--enemy-accent) 26%, transparent);
	border-radius: 4px;
	background: color-mix(in srgb, var(--enemy-accent) 6%, transparent);
}

.vs-eyebrow {
	flex-shrink: 0;
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--enemy-accent) 80%, var(--text-primary));
}

.vs-note {
	font-size: 12px;
	color: var(--text-tertiary);
}

.vs-slider {
	display: flex;
	align-items: center;
	gap: 10px;
	margin-left: auto;
	flex-shrink: 0;

	input[type='range'] {
		appearance: none;
		width: 180px;
		height: 4px;
		border-radius: 3px;
		background: color-mix(in srgb, var(--white) 12%, transparent);
		outline: none;

		&::-webkit-slider-thumb {
			appearance: none;
			width: 14px;
			height: 14px;
			border-radius: 50%;
			background: var(--enemy-accent);
			cursor: pointer;
			box-shadow: 0 0 8px color-mix(in srgb, var(--enemy-accent) 50%, transparent);
		}

		&::-moz-range-thumb {
			width: 14px;
			height: 14px;
			border: none;
			border-radius: 50%;
			background: var(--enemy-accent);
			cursor: pointer;
		}
	}
}

.vs-defval {
	min-width: 54px;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-tertiary);

	b {
		color: var(--enemy-accent);
		font-size: 14px;
		font-weight: 600;
	}
}

.cols {
	display: grid;
	grid-template-columns: 288px 1fr;
	gap: 16px;
	margin-top: 14px;
	flex: 1;
	min-height: 0;
}

@media (max-width: 900px) {
	.cols {
		grid-template-columns: 1fr;
	}
}
</style>
