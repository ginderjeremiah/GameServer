<!--
	A lesson's ordered tutorial steps. Array position is the step order — there is no stable
	row id/ordinal on the wire — so reordering is a plain splice-and-reinsert and the whole array
	is sent wholesale by the `steps` child saver whenever it differs from baseline.
-->
{#if lesson.steps.length === 0}
	<EmptySection
		icon="bars"
		title="This lesson has no steps yet."
		sub="Add a step to start the tutorial."
		addLabel="Add step"
		onAdd={addStep}
	/>
{:else}
	<div class="steps-list">
		<!-- Keyed by index intentionally: there is no stable step id, and array position IS the order. -->
		{#each lesson.steps as step, i (i)}
			<div class="step-row">
				<div class="step-index">{i + 1}</div>
				<div class="step-fields">
					<textarea
						class="txtarea"
						class:invalid={!step.text.trim()}
						aria-label={`Step ${i + 1} text`}
						placeholder="Step text shown to the player…"
						value={step.text}
						oninput={(e) => setText(i, e.currentTarget.value)}
					></textarea>
					<input
						class="inp"
						aria-label={`Step ${i + 1} anchor key`}
						placeholder="anchor key (optional)"
						value={step.anchorKey ?? ''}
						oninput={(e) => setAnchorKey(i, e.currentTarget.value)}
					/>
				</div>
				<div class="step-actions">
					<button
						type="button"
						class="icon-btn"
						title="Move up"
						aria-label={`Move step ${i + 1} up`}
						disabled={i === 0}
						onclick={() => moveStep(i, i - 1)}
					>
						<span class="rot180"><WorkbenchIcon kind="chevD" size={12} /></span>
					</button>
					<button
						type="button"
						class="icon-btn"
						title="Move down"
						aria-label={`Move step ${i + 1} down`}
						disabled={i === lesson.steps.length - 1}
						onclick={() => moveStep(i, i + 1)}
					>
						<WorkbenchIcon kind="chevD" size={12} />
					</button>
					<button
						type="button"
						class="row-x"
						title="Remove"
						aria-label={`Remove step ${i + 1}`}
						onclick={() => removeStep(i)}
					>
						<WorkbenchIcon kind="x" size={11} />
					</button>
				</div>
			</div>
		{/each}
	</div>
	<div class="table-actions">
		<button type="button" class="btn sm" onclick={addStep}>
			<WorkbenchIcon kind="plus" size={12} />Add step
		</button>
	</div>
{/if}

<script lang="ts">
import type { ILesson, ILessonStep } from '$lib/api';
import type { EntityStore } from '../../entity-store.svelte';
import type { Identified } from '../../entities/types';
import WorkbenchIcon from '../../WorkbenchIcon.svelte';
import EmptySection from '../EmptySection.svelte';

interface Props {
	record: Identified;
	baseline: Identified | undefined;
	store: EntityStore<Identified>;
}

const { record, store }: Props = $props();

const lesson = $derived(record as unknown as ILesson);

const mutateSteps = (mutate: (steps: ILessonStep[]) => void) =>
	store.patch(lesson.id, (d) => {
		mutate((d as unknown as ILesson).steps);
	});

const addStep = () => mutateSteps((steps) => steps.push({ text: '', anchorKey: undefined }));
const removeStep = (i: number) => mutateSteps((steps) => steps.splice(i, 1));
const setText = (i: number, text: string) => mutateSteps((steps) => (steps[i].text = text));
const setAnchorKey = (i: number, anchorKey: string) =>
	mutateSteps((steps) => (steps[i].anchorKey = anchorKey.trim() === '' ? undefined : anchorKey));

const moveStep = (from: number, to: number) =>
	mutateSteps((steps) => {
		if (to < 0 || to >= steps.length) {
			return;
		}
		const [moved] = steps.splice(from, 1);
		steps.splice(to, 0, moved);
	});
</script>

<style lang="scss">
.steps-list {
	display: flex;
	flex-direction: column;
	gap: 10px;
}
.step-row {
	display: flex;
	gap: 10px;
	align-items: flex-start;
}
.step-index {
	flex-shrink: 0;
	width: 20px;
	padding-top: 8px;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-muted);
	text-align: right;
}
.step-fields {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	gap: 6px;
}
.step-fields .txtarea {
	min-height: 52px;
}
.step-actions {
	flex-shrink: 0;
	display: flex;
	gap: 4px;
	padding-top: 4px;
}
.icon-btn {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	width: 22px;
	height: 22px;
	border: 1px solid var(--border);
	border-radius: 4px;
	background: transparent;
	color: var(--text-secondary);
	cursor: pointer;

	&:disabled {
		opacity: 0.35;
		cursor: default;
	}

	&:not(:disabled):hover {
		color: var(--text-primary);
		border-color: var(--text-muted);
	}
}
.rot180 {
	display: inline-flex;
	transform: rotate(180deg);
}
.table-actions {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
	margin-top: 12px;
}
</style>
