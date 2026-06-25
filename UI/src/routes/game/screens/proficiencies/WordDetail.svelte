<!-- The word-detail inspector pane (#1218): the right column detailing the selected tier — a compact
     header (name + school + state pill), the tier's word of power illuminated by its decipher stage (the
     hover target driving the shared decipher tooltip), an XP progress bar, the per-level breakdown ladder
     (each level's attribute payout + milestone reward skills), the seed skill, and the "Trained by" chips.
     All formatting/derivation lives in the pure `word-detail` module; this only resolves reference-data
     names from the live stores and renders. -->
<aside class="inspector" data-testid="word-detail">
	<div class="eyebrow">word detail</div>

	<div class="head">
		<div class="head-text">
			<div class="name">{tier.name}</div>
			<div class="school">{path.name}</div>
		</div>
		<span class="pill pill-{pill.key}" data-testid="state-pill">{pill.label}</span>
	</div>

	<!-- The word being deciphered: the glyph illuminates dim → accent → gold as it levels, over a reveal
	     line, and the whole block surfaces the shared decipher tooltip on hover/focus. -->
	<button
		type="button"
		class="word-block"
		use:wordHover={{ controller, tier }}
		use:describedByTooltip={controller.describedById}
	>
		<WordOfPower
			class="glyph stage-{tier.decipher}"
			text={tier.word}
			label={tier.name}
			size={30}
			glow={tier.decipher !== 'undeciphered'}
		/>
		<div class="reveal stage-{tier.decipher}">{revealText}</div>
	</button>

	<div class="xp" style:--bar-fill={tier.state === 'maxed' ? 'var(--gold)' : 'var(--accent)'}>
		<div class="xp-row">
			<span class="xp-level">LV {tier.level} / {tier.maxLevel}</span>
			<span class="xp-text">{xpText}</span>
		</div>
		<Bar
			value={tier.xpForNext > 0 ? tier.xp : 1}
			max={tier.xpForNext > 0 ? tier.xpForNext : 1}
			presentational
			testId="xp-bar"
		/>
	</div>

	<div class="section-head">Per-level breakdown<span class="rule"></span></div>
	<div class="ladder" data-testid="ladder">
		{#each ladder as row (row.level)}
			<div class="rung" class:milestone={row.isMilestone} class:reached={row.reached} data-testid="rung-{row.level}">
				<span class="dot" class:milestone={row.isMilestone}></span>
				<span class="num">{row.level}</span>
				<div class="rung-body">
					<div class="bonus">{row.bonus || '—'}</div>
					{#if row.skillName}
						<div class="grant">grants {row.skillName}</div>
					{/if}
				</div>
				{#if row.isMilestone}
					<span class="ms-tag">★ milestone</span>
				{/if}
			</div>
		{/each}
	</div>

	<div class="section-head">Seed skill<span class="rule"></span></div>
	<div class="seed" data-testid="seed-skill">{seedName ?? '—'}</div>

	<div class="section-head">Trained by<span class="rule"></span></div>
	<div class="chips" data-testid="trained-by">
		{#if chips.length > 0}
			{#each chips as chip (chip)}
				<span class="chip">{chip}</span>
			{/each}
		{:else}
			<span class="chip chip-empty">— none yet —</span>
		{/if}
	</div>
</aside>

<script lang="ts">
import { Bar, WordOfPower } from '$components';
import { describedByTooltip } from '$components/tooltip/describedby-tooltip';
import { staticData } from '$stores';
import type { PathView, TierView } from './proficiencies-lexicon';
import { buildLadder, statePill, trainedBy, xpProgressText } from './word-detail';
import { wordHover, type WordTooltipController } from './word-hover';

interface Props {
	/** The selected tier to detail. */
	tier: TierView;
	/** The tier's owning path, for the school name and the "Trained by" contributions. */
	path: PathView;
	/** Drives the shared decipher tooltip on hover/focus (the same controller the spine cards use). */
	controller: WordTooltipController;
}

const { tier, path, controller }: Props = $props();

// Reference-data lookups resolved from the live stores (skills are id-indexed — mirroring ItemTooltip's
// granted-skill resolution); the pure module formats from these.
const resolveSkill = (id: number): string | undefined => staticData.skills?.[id]?.name;

const pill = $derived(statePill(tier.state));
const xpText = $derived(xpProgressText(tier));
const ladder = $derived(buildLadder(tier, staticData.attributes, resolveSkill));
const chips = $derived(trainedBy(path.contributions, resolveSkill));
const seedName = $derived(tier.seedSkillId !== undefined ? resolveSkill(tier.seedSkillId) : undefined);

const revealText = $derived.by(() => {
	if (tier.decipher === 'translated') {
		return tier.translation;
	}
	if (tier.decipher === 'pronunciation') {
		return `“${tier.pronunciation}”`;
	}
	return '⟨ undeciphered ⟩';
});
</script>

<style lang="scss">
.inspector {
	flex: none;
	width: 312px;
	overflow: auto;
	background: var(--panel);
	border-left: 1px solid var(--border-subtle);
	padding: 18px 18px 24px;
	color: var(--text-primary);
	font-family: var(--sans);
}

.eyebrow {
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--eyebrow);
}

.head {
	margin-top: 10px;
	display: flex;
	align-items: flex-start;
	justify-content: space-between;
	gap: 10px;
}

.head-text {
	min-width: 0;
}

.name {
	font-size: 21px;
	font-weight: 600;
	line-height: 1.1;
	color: var(--text-primary);
}

.school {
	margin-top: 4px;
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.6px;
	color: var(--text-tertiary);
}

// State pill: themed by the tier's state (gold mastered, accent training, neutral learning).
.pill {
	flex: none;
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.4px;
	padding: 4px 9px;
	border-radius: var(--border-radius);
	border: 1px solid;
}

.pill-mastered {
	color: var(--gold);
	background: color-mix(in srgb, var(--gold) 12%, transparent);
	border-color: color-mix(in srgb, var(--gold) 40%, transparent);
}

.pill-training {
	color: var(--accent);
	background: color-mix(in srgb, var(--accent) 12%, transparent);
	border-color: color-mix(in srgb, var(--accent) 40%, transparent);
}

.pill-learning {
	color: var(--text-secondary);
	background: color-mix(in srgb, var(--white) 5%, transparent);
	border-color: var(--border-medium);
}

// The decipher word block — a full-width, focusable hover target (surfacing the shared decipher tooltip
// on hover/focus, mirroring the spine cards) with no button chrome; `help` cursor signals it is purely
// informational, not an action.
.word-block {
	margin-top: 14px;
	display: block;
	width: 100%;
	padding: 0;
	border: none;
	background: transparent;
	color: inherit;
	text-align: left;
	cursor: help;
}

// The glyph is the child WordOfPower element, reached through `:global()` anchored under `.word-block`
// so the rule stays scoped here. Its colour drives both the glyph and (via currentColor) the glow.
.word-block :global(.glyph) {
	display: block;
	line-height: 1.05;
}

.word-block :global(.glyph.stage-undeciphered) {
	color: var(--text-tertiary);
}

.word-block :global(.glyph.stage-pronunciation) {
	color: var(--accent-light);
}

.word-block :global(.glyph.stage-translated) {
	color: var(--gold);
}

.reveal {
	margin-top: 6px;
	font-family: var(--mono);
	font-size: 12px;
	letter-spacing: 0.4px;

	&.stage-undeciphered {
		color: var(--text-muted);
		font-style: italic;
	}

	&.stage-pronunciation {
		color: var(--accent-light);
	}

	&.stage-translated {
		color: var(--gold);
	}
}

.xp {
	margin-top: 16px;
}

.xp-row {
	display: flex;
	align-items: baseline;
	justify-content: space-between;
	margin-bottom: 7px;
}

.xp-level {
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.5px;
	color: var(--text-secondary);
}

.xp-text {
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.3px;
	color: var(--text-tertiary);
}

// Section dividers: a mono label with a hairline rule filling the rest of the row.
.section-head {
	margin-top: 22px;
	display: flex;
	align-items: center;
	gap: 8px;
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--eyebrow);
}

.rule {
	flex: 1;
	height: 1px;
	background: var(--border-subtle);
}

.ladder {
	margin-top: 10px;
	display: flex;
	flex-direction: column;
	gap: 3px;
}

.rung {
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 4px 9px;
	border-radius: var(--border-radius);

	// An un-reached level is dimmed until the player levels into it.
	&:not(.reached) {
		opacity: 0.55;
	}

	// A milestone level is boxed in gold so the reward rungs stand out.
	&.milestone {
		padding: 8px 9px;
		background: color-mix(in srgb, var(--gold) 3%, transparent);
		border: 1px solid color-mix(in srgb, var(--gold) 14%, transparent);

		&.reached {
			background: color-mix(in srgb, var(--gold) 7%, transparent);
			border-color: color-mix(in srgb, var(--gold) 28%, transparent);
		}
	}
}

// Level marker: a small dot, or a diamond on milestone rungs; filled gold once reached.
.dot {
	flex: none;
	width: 7px;
	height: 7px;
	border-radius: 50%;
	background: color-mix(in srgb, var(--white) 16%, transparent);

	&.milestone {
		width: 11px;
		height: 11px;
		border-radius: 0;
		transform: rotate(45deg);
		border: 1.5px solid color-mix(in srgb, var(--white) 28%, transparent);
		background: transparent;
	}
}

.rung.reached .dot {
	background: var(--gold);

	&.milestone {
		border-color: var(--gold);
		box-shadow: 0 0 6px color-mix(in srgb, var(--gold) 60%, transparent);
	}
}

.num {
	flex: none;
	width: 20px;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-tertiary);
}

.rung.reached .num {
	color: var(--gold);
}

.rung-body {
	flex: 1;
	min-width: 0;
}

.bonus {
	font-size: 13.5px;
	line-height: 1.25;
	color: var(--text-secondary);
}

.rung.reached .bonus {
	color: var(--text-primary);
}

.milestone .bonus {
	font-weight: 600;
}

.grant {
	margin-top: 2px;
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 0.2px;
	color: color-mix(in srgb, var(--gold) 80%, var(--text-secondary));
}

.ms-tag {
	flex: none;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
	color: var(--text-tertiary);
}

.rung.reached .ms-tag {
	color: var(--gold);
}

.seed {
	margin-top: 8px;
	font-size: 15px;
	color: var(--text-primary);
}

.chips {
	margin-top: 9px;
	display: flex;
	flex-wrap: wrap;
	gap: 7px;
}

.chip {
	font-size: 12.5px;
	font-weight: 500;
	padding: 4px 11px;
	border: 1px solid var(--border-light);
	border-radius: var(--border-radius);
	background: color-mix(in srgb, var(--white) 4%, transparent);
	color: var(--text-primary);
	white-space: nowrap;
}

.chip-empty {
	color: var(--text-muted);
	font-weight: 400;
}
</style>
