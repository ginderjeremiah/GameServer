{#if path}
	<div class="falloff-banner">
		<div class="fb-cell">
			<div class="fb-label">Falloff base</div>
			<div class="fb-value">{path.falloffBase}</div>
		</div>
		<span class="arrow">→</span>
		<div class="steps">
			{#each falloffSteps(path.falloffBase) as step (step.distance)}
				<span class="step" class:home={step.distance === 0}>d{step.distance} · {step.factor.toFixed(2)}</span>
			{/each}
		</div>
		<span class="fb-note">pull decays per tier above a skill's home</span>
	</div>

	<div class="grid-head">
		<span class="c-skill">Skill</span>
		<span class="c-home">Home tier</span>
		<span class="c-weight">Weight</span>
		<span class="c-rm"></span>
	</div>

	{#each path.contributions as row, index (index)}
		<div class="grid-row">
			<div class="c-skill">
				<ProgSelect
					value={row.skillId}
					options={skillOptionsFor(row.skillId)}
					onChange={(v) => store.updateContribution(pathId, index, { skillId: v })}
				/>
			</div>
			<div class="c-home">
				<ProgSelect
					value={row.homeTier}
					options={homeTierOptions(tiers)}
					onChange={(v) => store.updateContribution(pathId, index, { homeTier: v })}
				/>
			</div>
			<div class="c-weight">
				<ProgNumber value={row.weight} onChange={(v) => store.updateContribution(pathId, index, { weight: v })} />
			</div>
			<button
				type="button"
				class="rm"
				aria-label="Remove contribution"
				onclick={() => store.removeContribution(pathId, index)}
			>
				<WorkbenchIcon kind="x" size={12} />
			</button>
		</div>
	{/each}

	{#if path.contributions.length === 0}
		<div class="empty">No contributing skills yet.</div>
	{/if}

	<button
		type="button"
		class="add"
		data-testid="progression-add-contribution"
		onclick={() => store.addContribution(pathId)}
	>
		+ Add contributing skill
	</button>
{/if}

<script lang="ts">
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import { reference } from '../reference.svelte';
import type { ProgressionStore } from './progression-store.svelte';
import { falloffSteps, homeTierOptions, tiersOfPath } from './progression-helpers';
import ProgSelect from './ProgSelect.svelte';
import ProgNumber from './ProgNumber.svelte';

interface Props {
	store: ProgressionStore;
	pathId: number;
}

const { store, pathId }: Props = $props();

const path = $derived(store.paths.find((p) => p.id === pathId));
const tiers = $derived(tiersOfPath(store.profs, pathId));
const usedSkills = $derived(new Set((path?.contributions ?? []).map((c) => c.skillId)));

// A skill can feed a path once: offer unused skills plus this row's current pick.
const skillOptionsFor = (current: number) =>
	reference.skillOptions(current).filter((o) => o.value === current || !usedSkills.has(o.value));
</script>

<style lang="scss">
.falloff-banner {
	display: flex;
	align-items: center;
	gap: 12px;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: 5px;
	padding: 11px 14px;
	margin-bottom: 16px;
}
.fb-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.5px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin-bottom: 4px;
}
.fb-value {
	font-family: var(--mono);
	font-size: 14px;
	color: var(--text-primary);
}
.arrow {
	color: var(--accent);
}
.steps {
	display: flex;
	gap: 6px;
}
.step {
	font-family: var(--mono);
	font-size: 10px;
	color: var(--text-tertiary);
	border: 1px solid var(--border-light);
	border-radius: 3px;
	padding: 3px 8px;

	&.home {
		color: var(--text-secondary);
	}
}
.fb-note {
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-muted);
	margin-left: auto;
}
.grid-head {
	display: flex;
	gap: 10px;
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	color: var(--text-muted);
	padding: 0 4px 8px;
	border-bottom: 1px solid var(--border-light);
}
.grid-row {
	display: flex;
	gap: 10px;
	align-items: flex-end;
	padding: 9px 4px;
	border-bottom: 1px solid var(--border-subtle);
}
.c-skill {
	flex: 1;
}
.c-home {
	width: 200px;
}
.c-weight {
	width: 96px;
}
.c-rm {
	width: 24px;
}
.rm {
	width: 24px;
	height: 36px;
	background: transparent;
	border: none;
	color: var(--text-muted);
	cursor: pointer;

	&:hover {
		color: var(--change-removed);
	}
}
.empty {
	padding: 16px 4px;
	color: var(--text-muted);
	font-size: 12.5px;
}
.add {
	background: transparent;
	border: none;
	color: var(--accent);
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.5px;
	text-transform: uppercase;
	padding: 12px 4px 0;
	cursor: pointer;
}
</style>
