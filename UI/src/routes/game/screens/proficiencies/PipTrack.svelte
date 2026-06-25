<!-- The level/milestone track for a tier: one pip per level up to the cap, filled (gold) up to the
     current level. A milestone level (one that grants a reward) draws as a diamond; every other level a
     thin bar. Purely presentational — the milestone set and counts come from the view-model. -->
<div class="pips" role="img" aria-label="Level {level} of {maxLevel}">
	{#each pips as pip (pip.level)}
		<span class="pip" class:milestone={pip.milestone} class:filled={pip.filled}></span>
	{/each}
</div>

<script lang="ts">
interface Props {
	/** The tier's current level — pips at or below it render filled. */
	level: number;
	/** The level cap, i.e. the number of pips drawn. */
	maxLevel: number;
	/** Levels drawn as milestone diamonds (the reward levels). */
	milestoneLevels: number[];
}

const { level, maxLevel, milestoneLevels }: Props = $props();

// One pip per level. Proficiencies use a low level cap (~10, per spike #982), so the row stays short;
// `.pips` wraps rather than overflowing should a tier ever be authored with an unusually high cap.
const pips = $derived.by(() => {
	const milestones = new Set(milestoneLevels);
	return Array.from({ length: maxLevel }, (_, i) => {
		const lvl = i + 1;
		return { level: lvl, milestone: milestones.has(lvl), filled: lvl <= level };
	});
});
</script>

<style lang="scss">
.pips {
	display: flex;
	flex-wrap: wrap;
	justify-content: flex-end;
	align-items: center;
	gap: 7px;
	min-height: 16px;
}

.pip {
	display: inline-block;
	box-sizing: border-box;
	flex: none;

	// A regular (non-milestone) level: a thin upright bar.
	width: 4px;
	height: 13px;
	border-radius: 1.5px;
	background: color-mix(in srgb, var(--white) 14%, transparent);

	&.filled {
		background: var(--gold);
	}

	// A milestone level: a hollow diamond that fills gold (with a glow) once reached.
	&.milestone {
		width: 11px;
		height: 11px;
		border-radius: 0;
		transform: rotate(45deg);
		border: 1.5px solid color-mix(in srgb, var(--white) 28%, transparent);
		background: transparent;

		&.filled {
			border-color: var(--gold);
			background: var(--gold);
			box-shadow: 0 0 7px color-mix(in srgb, var(--gold) 60%, transparent);
		}
	}
}
</style>
