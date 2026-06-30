<div class="band-head">
	{#if swapping}
		<span class="label warn">Replacing with {swapName} — click a slot to swap in</span>
		<div class="spacer"></div>
		<button type="button" class="band-chip action" onclick={() => view.cancelSwap()}>Cancel</button>
	{:else}
		<span class="label">Equipped loadout — priority left → right · drag or use the arrows to reorder</span>
		<div class="spacer"></div>
		<span class="band-chip">{view.equipped.length}/{view.cap}</span>
	{/if}
</div>

<div class="band" style:--cap={view.cap}>
	{#each slots as id, index (id ?? `empty-${index}`)}
		{@const metrics = id != null ? view.metric(id) : undefined}
		{#if id != null && metrics}
			{@const dormant = view.dormant(metrics.skill)}
			<!-- Accessible card: a presentational container whose primary action is a full-bleed
			     OverlayButton (inspect the skill, or resolve a pending swap into this slot), with the
			     remove button as a higher-z-index sibling and the drag handle on the overlay — native
			     keyboard activation, no nested interactive elements. -->
			<!-- svelte-ignore a11y_no_static_element_interactions -->
			<div
				class="eqcard"
				class:swap-target={swapping}
				class:dragging={view.dragIndex === index}
				class:dragover={dragOverIndex === index}
				class:dormant
				ondragover={(e) => onDragOver(e, index)}
				ondrop={(e) => onDrop(e, index)}
			>
				<OverlayButton
					label={swapping
						? `Replace slot ${index + 1} with ${swapName}`
						: `${metrics.skill.name}, loadout slot ${index + 1}`}
					draggable={!swapping}
					onActivate={() => (swapping ? view.swapInto(index) : view.select(id))}
					onDragStart={(e) => onDragStart(e, index)}
					{onDragEnd}
				/>
				<span class="num">{index + 1}</span>
				{#if swapping}
					<span class="rep">↪ replace</span>
				{:else}
					<button
						type="button"
						class="rm"
						title="Remove from loadout"
						aria-label="Remove {metrics.skill.name} from loadout"
						onclick={() => view.toggle(id)}>✕</button
					>
					<!-- Keyboard/touch reorder path: HTML5 drag-and-drop is mouse-only, so these real
					     buttons give a focus- and touch-reachable way to change battle priority. -->
					<div class="reorder">
						<button
							type="button"
							class="mv"
							title="Move earlier in priority"
							aria-label="Move {metrics.skill.name} earlier in priority"
							disabled={index === 0}
							onclick={() => view.reorder(index, index - 1)}>‹</button
						>
						<button
							type="button"
							class="mv"
							title="Move later in priority"
							aria-label="Move {metrics.skill.name} later in priority"
							disabled={index >= view.equipped.length - 1}
							onclick={() => view.reorder(index, index + 1)}>›</button
						>
					</div>
				{/if}
				<span class="en">{metrics.skill.name}</span>
				{#if dormant}
					<!-- Off-weapon: the skill stays saved but doesn't field until a matching weapon is held (#1342). -->
					<DormantNote skill={metrics.skill} />
				{:else}
					<SkillCardStats {view} {metrics} />
				{/if}
			</div>
		{:else}
			<button type="button" class="eqcard empty" class:dimmed={swapping} onclick={() => focusAvailable()}>
				<span class="plus">+</span>
				<span class="label">add skill</span>
			</button>
		{/if}
	{/each}
</div>

<script lang="ts">
import OverlayButton from '$components/OverlayButton.svelte';
import SkillCardStats from './SkillCardStats.svelte';
import DormantNote from './DormantNote.svelte';
import type { SkillsView } from './skills-view.svelte';

type Props = {
	view: SkillsView;
};

const { view }: Props = $props();

let dragOverIndex = $state<number | null>(null);

const swapping = $derived(view.pendingSwap != null);
const swapName = $derived(view.pendingSwap != null ? (view.metric(view.pendingSwap)?.skill.name ?? '') : '');

/** Fixed-length slot list (capacity), nulls for empty slots. */
const slots = $derived(Array.from({ length: view.cap }, (_, i) => view.equipped[i] ?? null));

/** Selecting an empty slot surfaces the first available skill to equip. */
const focusAvailable = () => {
	const first = view.availableRail[0];
	if (first) {
		view.select(first.skill.id);
	}
};

const onDragStart = (e: DragEvent, index: number) => {
	view.dragIndex = index;
	if (e.dataTransfer) {
		e.dataTransfer.effectAllowed = 'move';
	}
};

const onDragOver = (e: DragEvent, index: number) => {
	if (view.dragIndex != null) {
		e.preventDefault();
		dragOverIndex = index;
	}
};

const onDrop = (e: DragEvent, index: number) => {
	if (view.dragIndex == null) {
		return;
	}
	e.preventDefault();
	view.reorder(view.dragIndex, index);
	view.dragIndex = null;
	dragOverIndex = null;
};

const onDragEnd = () => {
	view.dragIndex = null;
	dragOverIndex = null;
};
</script>

<style lang="scss">
.band-head {
	display: flex;
	align-items: center;
	gap: 12px;
	margin: 16px 0 9px;

	.spacer {
		flex: 1;
	}
}

.label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);

	&.warn {
		color: var(--enemy-accent);
	}
}

.band-chip {
	padding: 2px 9px;
	border: 1px solid var(--border-light);
	border-radius: 20px;
	background: transparent;
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	color: var(--text-tertiary);

	&.action {
		color: var(--text-primary);
		border-color: var(--border-medium);
		cursor: pointer;
	}
}

.band {
	display: grid;
	grid-template-columns: repeat(var(--cap), 1fr);
	gap: 11px;
}

.eqcard {
	position: relative;
	display: flex;
	flex-direction: column;
	gap: 7px;
	min-height: 104px;
	padding: 10px 11px;
	border: 1px solid var(--border-light);
	border-radius: 4px;
	background: color-mix(in srgb, var(--white) 3.5%, transparent);
	text-align: left;
	color: var(--text-primary);
	// Filled cards are drag sources; the full-bleed overlay inherits this grab cursor.
	cursor: grab;

	&.dragging {
		opacity: 0.4;
	}

	&.dragover {
		border-color: var(--accent);
		box-shadow: 0 0 0 1px var(--accent);
	}

	// Off-weapon (dormant): the saved skill isn't fielded until a matching weapon is held. Dimmed and
	// border-muted so it reads as inactive, while staying fully interactive (removable / reorderable).
	&.dormant {
		opacity: 0.55;
		border-style: dashed;
		border-color: var(--border-medium);
	}

	&.swap-target {
		cursor: pointer;
		border-color: color-mix(in srgb, var(--enemy-accent) 55%, transparent);
		background: color-mix(in srgb, var(--enemy-accent) 10%, transparent);

		&:hover {
			box-shadow: 0 0 0 1px var(--enemy-accent);
		}
	}

	&.empty {
		align-items: center;
		justify-content: center;
		gap: 4px;
		border-style: dashed;
		background: transparent;
		color: var(--text-muted);
		cursor: pointer;

		&.dimmed {
			opacity: 0.3;
		}

		.plus {
			font-size: 24px;
			font-weight: 300;
		}
	}

	.num {
		position: absolute;
		top: 7px;
		left: 9px;
		font-family: var(--mono);
		font-size: 9px;
		color: var(--text-muted);
	}

	.rep {
		position: absolute;
		top: 7px;
		right: 9px;
		font-family: var(--mono);
		font-size: 8.5px;
		color: var(--enemy-accent);
	}

	.rm {
		position: absolute;
		top: 5px;
		right: 6px;
		// Above the full-bleed overlay button so it stays clickable.
		z-index: 2;
		width: 16px;
		height: 16px;
		border: none;
		border-radius: 2px;
		background: color-mix(in srgb, var(--black) 45%, transparent);
		color: var(--text-tertiary);
		font-size: 11px;
		line-height: 16px;
		text-align: center;
		cursor: pointer;

		&:hover {
			color: var(--enemy-accent);
		}
	}

	.reorder {
		position: absolute;
		top: 5px;
		left: 50%;
		transform: translateX(-50%);
		display: flex;
		gap: 3px;
		// Above the full-bleed overlay button so the arrows stay clickable.
		z-index: 2;
		// Revealed on hover or keyboard focus so the card stays clean by default; tapping the card
		// (focusing its overlay) reveals them on touch, where there is no hover.
		opacity: 0;
		transition: opacity 120ms;
	}

	&:hover .reorder,
	&:focus-within .reorder {
		opacity: 1;
	}

	.mv {
		width: 16px;
		height: 16px;
		border: none;
		border-radius: 2px;
		background: color-mix(in srgb, var(--black) 45%, transparent);
		color: var(--text-tertiary);
		font-size: 13px;
		line-height: 16px;
		text-align: center;
		cursor: pointer;

		&:hover:not(:disabled) {
			color: var(--accent);
		}

		&:disabled {
			opacity: 0.3;
			cursor: default;
		}
	}

	.en {
		margin-top: 9px;
		color: inherit;
		font-size: 13.5px;
		font-weight: 500;
		line-height: 1.1;
	}
}
</style>
