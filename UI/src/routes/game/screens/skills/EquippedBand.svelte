<div class="band-head">
	{#if swapping}
		<span class="label warn">Replacing with {swapName} — click a slot to swap in</span>
		<div class="spacer"></div>
		<button type="button" class="band-chip action" onclick={() => view.cancelSwap()}>Cancel</button>
	{:else}
		<span class="label">Equipped loadout — priority left → right · drag to reorder</span>
		<div class="spacer"></div>
		<span class="band-chip">{view.equipped.length}/{view.cap}</span>
	{/if}
</div>

<div class="band" style:--cap={view.cap}>
	{#each slots as id, index (id ?? `empty-${index}`)}
		{@const metrics = id != null ? view.metric(id) : undefined}
		{#if id != null && metrics}
			<div
				class="eqcard"
				class:swap-target={swapping}
				class:dragging={view.dragIndex === index}
				class:dragover={dragOverIndex === index}
				draggable={!swapping}
				role="button"
				tabindex="0"
				aria-label={swapping
					? `Replace slot ${index + 1} with ${swapName}`
					: `${metrics.skill.name}, loadout slot ${index + 1}`}
				onclick={() => swapping && view.swapInto(index)}
				onkeydown={(e) => onCardKeydown(e, index, id)}
				ondragstart={(e) => onDragStart(e, index)}
				ondragover={(e) => onDragOver(e, index)}
				ondrop={(e) => onDrop(e, index)}
				ondragend={onDragEnd}
			>
				<span class="num">{index + 1}</span>
				{#if swapping}
					<span class="rep">↪ replace</span>
				{:else}
					<button
						type="button"
						class="rm"
						title="Remove from loadout"
						aria-label="Remove {metrics.skill.name} from loadout"
						onclick={(e) => remove(e, id)}>✕</button
					>
				{/if}
				<button type="button" class="en" onclick={(e) => pick(e, id)}>{metrics.skill.name}</button>
				<div class="es">
					<div class="stat"><span class="v accent">{fmt(view.effective(id))}</span><span class="k">dmg</span></div>
					<div class="stat"><span class="v">{metrics.cooldown.toFixed(1)}s</span><span class="k">cd</span></div>
					<div class="stat"><span class="v accent">{fmt(view.effectiveDps(id))}</span><span class="k">dps</span></div>
				</div>
				<div class="chips">
					{#each metrics.skill.damageMultipliers as mult (mult.attributeId)}
						<span class="achip" style:--ac={attributeColor(mult.attributeId)}>
							<AttributeIcon id={mult.attributeId} size={12} />
							{attributeCode(mult.attributeId, staticData.attributes)}
						</span>
					{/each}
				</div>
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
import { attributeCode, attributeColor, formatNum } from '$lib/common';
import { staticData } from '$stores';
import AttributeIcon from '$components/AttributeIcon.svelte';
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

const fmt = (n: number) => formatNum(Math.round(n));

const remove = (e: MouseEvent, id: number) => {
	e.stopPropagation();
	view.toggle(id);
};

const pick = (e: MouseEvent, id: number) => {
	e.stopPropagation();
	view.select(id);
};

/** Selecting an empty slot surfaces the first available skill to equip. */
const focusAvailable = () => {
	const first = view.availableRail[0];
	if (first) {
		view.select(first.skill.id);
	}
};

/** Enter/Space resolves a pending swap into this slot, or inspects the skill otherwise. */
const onCardKeydown = (e: KeyboardEvent, index: number, id: number) => {
	if (e.key !== 'Enter' && e.key !== ' ') {
		return;
	}
	e.preventDefault();
	if (swapping) {
		view.swapInto(index);
	} else {
		view.select(id);
	}
};

const onDragStart = (e: DragEvent, index: number) => {
	if (swapping) {
		e.preventDefault();
		return;
	}
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

	&[draggable='true'] {
		cursor: grab;
	}

	&.dragging {
		opacity: 0.4;
	}

	&.dragover {
		border-color: var(--accent);
		box-shadow: 0 0 0 1px var(--accent);
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

	.en {
		margin-top: 9px;
		padding: 0;
		border: none;
		background: transparent;
		color: inherit;
		font-size: 13.5px;
		font-weight: 500;
		line-height: 1.1;
		text-align: left;
		cursor: pointer;
	}

	.es {
		display: flex;
		gap: 13px;
		margin-top: auto;
	}

	.chips {
		display: flex;
		gap: 5px;
	}
}

.stat {
	display: flex;
	flex-direction: column;
	gap: 2px;

	.v {
		font-size: 15px;
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

.achip {
	display: inline-flex;
	align-items: center;
	gap: 4px;
	padding: 2px 8px;
	border: 1px solid color-mix(in srgb, var(--ac) 38%, transparent);
	border-radius: 3px;
	background: color-mix(in srgb, var(--ac) 12%, transparent);
	color: var(--ac);
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.5px;
	white-space: nowrap;
}
</style>
