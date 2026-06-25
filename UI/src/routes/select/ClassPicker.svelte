<!-- Class picker for the create-character flow: a radiogroup of the active classes plus a preview of
     the chosen class's kit (attribute fingerprint, signature passive, starter skills, equipment) and
     its decorative word of power. Reads the class/skill/item/attribute reference sets from the static
     store — the select screen loads them ahead of the main loading screen via `loadClassPickerData`. -->
<div class="class-picker" data-testid="class-picker">
	{#if available.length === 0}
		<p class="picker-empty" data-testid="class-picker-empty">No classes are available yet.</p>
	{:else}
		<div class="class-options" role="radiogroup" aria-label="Class">
			{#each available as cls (cls.id)}
				<button
					type="button"
					class="class-option"
					class:selected={cls.id === selectedClassId}
					role="radio"
					aria-checked={cls.id === selectedClassId}
					data-testid="class-option-{cls.id}"
					{disabled}
					onclick={() => onSelect(cls.id)}
				>
					<span class="class-name">{cls.name}</span>
					<WordOfPower text={cls.word} label={cls.name} size={12} />
				</button>
			{/each}
		</div>

		{#if selected}
			<div class="class-preview" data-testid="class-preview">
				<p class="class-desc">{selected.description}</p>

				<div class="kit-section">
					<span class="kit-label">Attributes</span>
					<div class="chips">
						{#each fingerprint as dist (dist.attributeId)}
							<AttributeChip attributeId={dist.attributeId} />
						{/each}
					</div>
				</div>

				<div class="kit-section">
					<span class="kit-label">Signature passive</span>
					<span class="passive" data-testid="class-passive">{passive}</span>
				</div>

				<div class="kit-section">
					<span class="kit-label">Starter skills</span>
					<div class="kit-list">
						{#each skills as skill (skill.id)}
							<span class="kit-item">{skill.name}</span>
						{/each}
					</div>
				</div>

				{#if equipment.length > 0}
					<div class="kit-section">
						<span class="kit-label">Equipment</span>
						<div class="kit-list">
							{#each equipment as item (item.itemId)}
								<span class="kit-item">{item.name}</span>
							{/each}
						</div>
					</div>
				{/if}
			</div>
		{/if}
	{/if}
</div>

<script lang="ts">
import { staticData } from '$stores';
import AttributeChip from '$components/AttributeChip.svelte';
import WordOfPower from '$components/WordOfPower.svelte';
import { passiveSummary, resolveStarterEquipment, resolveStarterSkills } from './class-summary';

interface Props {
	/** The id of the currently chosen class, or null before a class is selected. */
	selectedClassId: number | null;
	/** Notifies the parent of a class choice (also fired to default to the first class once they load). */
	onSelect: (classId: number) => void;
	/** Disables the options while a create request is in flight. */
	disabled?: boolean;
}

const { selectedClassId, onSelect, disabled = false }: Props = $props();

// Retired classes stay resolvable by id but are out of circulation for new characters (the
// reference-data convention), so the picker only offers active ones.
const available = $derived((staticData.classes ?? []).filter((c) => c.retiredAt == null));
const selected = $derived(available.find((c) => c.id === selectedClassId) ?? null);

// The fingerprint reads strongest-first so the class's defining attributes lead.
const fingerprint = $derived(
	selected ? [...selected.attributeDistributions].sort((a, b) => b.baseAmount - a.baseAmount) : []
);
const skills = $derived(selected ? resolveStarterSkills(selected, staticData.skills) : []);
const equipment = $derived(selected ? resolveStarterEquipment(selected, staticData.items) : []);
const passive = $derived(selected ? passiveSummary(selected, staticData.attributes) : '');

// Default to the first available class once the catalogue loads, so the form is submittable without
// an extra click. Self-limiting: once a class is selected the guard stops it re-firing.
$effect(() => {
	if (selectedClassId == null && available.length > 0) {
		onSelect(available[0].id);
	}
});
</script>

<style lang="scss">
.class-picker {
	margin-bottom: 14px;
}

.picker-empty {
	margin: 0 0 8px;
	font-size: 12.5px;
	color: var(--text-tertiary);
	font-family: var(--mono);
	letter-spacing: 0.4px;
}

.class-options {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
}

.class-option {
	flex: 1 1 0;
	min-width: 96px;
	display: flex;
	flex-direction: column;
	align-items: center;
	gap: 4px;
	padding: 10px 12px;
	background: transparent;
	color: var(--text-secondary);
	border: 1px solid var(--border-subtle);
	border-radius: var(--border-radius);
	font-family: inherit;
	cursor: pointer;
	transition: all 160ms;

	&:hover:not(:disabled),
	&:focus-visible {
		color: var(--text-primary);
		border-color: var(--accent);
	}

	&.selected {
		color: var(--text-primary);
		border-color: var(--accent);
		background: color-mix(in srgb, var(--accent) 12%, transparent);
	}

	&:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
	}

	&:disabled {
		cursor: not-allowed;
		opacity: 0.6;
	}

	.class-name {
		font-size: 13px;
		font-weight: 500;
		letter-spacing: 0.4px;
	}
}

.class-preview {
	margin-top: 12px;
	padding: 12px 14px;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: var(--border-radius);
}

.class-desc {
	margin: 0 0 12px;
	font-size: 12.5px;
	line-height: 1.5;
	color: var(--text-secondary);
}

.kit-section {
	display: flex;
	flex-direction: column;
	gap: 5px;

	& + & {
		margin-top: 10px;
	}
}

.kit-label {
	font-size: 10px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-tertiary);
	font-family: var(--mono);
}

.chips,
.kit-list {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
}

.passive {
	font-size: 12.5px;
	color: var(--text-primary);
	font-family: var(--mono);
}

.kit-item {
	padding: 2px 8px;
	font-size: 11.5px;
	color: var(--text-secondary);
	background: color-mix(in srgb, var(--text-primary) 6%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 3px;
}
</style>
