<!--
	Searchable, group-by-type attribute picker (#1327, spike #1320 Area E). Replaces the flat native
	`<select>` of every attribute — which has grown past what a single dropdown can handle — across all
	the workbench authoring contexts (skill multipliers/effects, item & mod bonuses, enemy & class
	distributions, the class passive, progression milestones).

	The trigger mimics the native `.sel` look; opening it raises a non-blocking `Popover` (so the panel
	escapes any table-cell clipping and gets focus-trap / Escape / backdrop dismissal for free) with a
	search box and the options grouped by the reference display taxonomy — the damage-type
	amplification/resistance family split per damage type, mirroring the attribute-breakdown screen.
-->
<div class="attr-pick">
	<div class="attr-trigger-wrap" style:width="100%">
		<button
			type="button"
			class="attr-trigger"
			class:dirty
			class:invalid
			aria-label={ariaLabel}
			aria-haspopup="dialog"
			onclick={() => openPanel()}
		>
			<span class="attr-trigger-text">{currentText}</span>
		</button>
		<!-- Self-styled caret (the workbench `.sel-caret` rule is scoped to `.workbench`, but this picker
		     also renders in the progression editor's `.prog` root). -->
		<svg
			class="attr-caret"
			width="10"
			height="10"
			viewBox="0 0 10 10"
			fill="none"
			stroke="currentColor"
			stroke-width="1.4"
			aria-hidden="true"
		>
			<path d="M2 3.5L5 6.5L8 3.5" stroke-linecap="round" stroke-linejoin="round" />
		</svg>
		{#if dirty}<DirtyDot />{/if}
	</div>

	<Popover {open} onClose={close} label={ariaLabel}>
		<div class="attr-panel">
			<div class="attr-search">
				<WorkbenchIcon kind="search" sw={1.4} />
				<input
					class="attr-search-inp"
					placeholder="Search attributes…"
					aria-label={`Search ${ariaLabel}`}
					bind:value={query}
					onkeydown={onSearchKey}
				/>
			</div>
			<div class="attr-list">
				{#each filtered as group (group.key)}
					<div class="attr-group">
						{#if group.damageTypeKey !== undefined}
							<div class="attr-group-head dmg" style:color={damageTypeKeyColor(group.damageTypeKey)}>
								<DamageTypeGlyph
									glyph={damageTypeKeyGlyph(group.damageTypeKey)}
									color={damageTypeKeyColor(group.damageTypeKey)}
									size={11}
								/>
								{group.label}
							</div>
						{:else if group.label}
							<div class="attr-group-head">{group.label}</div>
						{/if}
						{#each group.options as opt (opt.value)}
							{@const taken = isDisabled(opt.value)}
							<button
								type="button"
								class="attr-opt"
								class:selected={opt.value === value}
								disabled={taken}
								title={taken ? 'Already assigned in another row' : undefined}
								onclick={() => pick(opt.value)}
							>
								<span class="attr-opt-text">{opt.text}</span>
								{#if opt.value === value}<WorkbenchIcon kind="check" size={12} sw={1.8} />{/if}
							</button>
						{/each}
					</div>
				{/each}
				{#if filtered.length === 0}
					<div class="attr-empty">No matching attributes.</div>
				{/if}
			</div>
		</div>
	</Popover>
</div>

<script lang="ts">
import { Popover } from '$components';
import DamageTypeGlyph from '$components/DamageTypeGlyph.svelte';
import { damageTypeKeyColor, damageTypeKeyGlyph } from '$lib/common';
import { staticData } from '$stores';
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import DirtyDot from './DirtyDot.svelte';
import { filterAttributeGroups, groupAttributeOptions } from './attribute-groups';
import type { SelectOption } from '../entities/types';

interface Props {
	value: number;
	/** Candidate options (already incl. any "None" sentinel / current value), grouped for display. */
	options: SelectOption[];
	onChange: (value: number) => void;
	/** Accessible name for the trigger and search box (e.g. the field/column label). */
	ariaLabel: string;
	/** Values to disable (a unique table column passes its sibling-row selections). */
	disabledValues?: Set<number>;
	dirty?: boolean;
	invalid?: boolean;
}

const { value, options, onChange, ariaLabel, disabledValues, dirty = false, invalid = false }: Props = $props();

let open = $state(false);
let query = $state('');

const currentText = $derived(options.find((o) => o.value === value)?.text ?? '');
const groups = $derived(groupAttributeOptions(options, staticData.attributes));
const filtered = $derived(filterAttributeGroups(groups, query));

// A value is selectable unless a sibling row already took it (its own current value never disables).
const isDisabled = (v: number) => !!disabledValues?.has(v) && v !== value;
// First selectable option in the filtered view — the target for Enter-to-pick.
const firstMatch = $derived(filtered.flatMap((g) => g.options).find((o) => !isDisabled(o.value)));

const openPanel = () => {
	query = '';
	open = true;
};
const close = () => {
	open = false;
};
const pick = (v: number) => {
	if (isDisabled(v)) {
		return;
	}
	onChange(v);
	close();
};
const onSearchKey = (e: KeyboardEvent) => {
	if (e.key === 'Enter' && firstMatch) {
		e.preventDefault();
		pick(firstMatch.value);
	}
};
</script>

<style lang="scss">
// The root is intentionally NOT positioned: the Popover layer overlays its nearest positioned
// ancestor, so keeping the root static lets the panel escape the (small, relatively-positioned)
// trigger box and center over the detail panel.
.attr-pick {
	width: 100%;
}

.attr-trigger-wrap {
	position: relative;
}

// Mirrors the workbench `.sel` look so the picker reads as a native select in fields and table rows.
.attr-trigger {
	width: 100%;
	display: flex;
	align-items: center;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid color-mix(in srgb, var(--white) 10%, transparent);
	border-radius: 3px;
	color: var(--text-primary);
	font-family: var(--sans);
	font-size: 13.5px;
	padding: 8px 28px 8px 11px;
	cursor: pointer;
	text-align: left;
	outline: none;
	transition:
		border-color 0.14s,
		box-shadow 0.14s,
		background 0.14s;

	&:hover {
		border-color: color-mix(in srgb, var(--white) 20%, transparent);
	}
	&:focus-visible {
		border-color: var(--accent);
		box-shadow:
			0 0 0 1px color-mix(in srgb, var(--accent) 40%, transparent),
			0 0 8px color-mix(in srgb, var(--accent) 35%, transparent);
		background: color-mix(in srgb, var(--accent) 4%, transparent);
	}
	&.dirty {
		border-color: color-mix(in srgb, var(--change-modified) 55%, transparent);
		background: color-mix(in srgb, var(--change-modified) 6%, transparent);
	}
	&.invalid {
		border-left: 2px solid var(--warning);
	}
}

.attr-trigger-text {
	flex: 1;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.attr-caret {
	position: absolute;
	right: 10px;
	top: 50%;
	transform: translateY(-50%);
	pointer-events: none;
	color: var(--text-tertiary);
}

// ── popover panel ──
.attr-panel {
	display: flex;
	flex-direction: column;
	width: min(440px, calc(100vw - 48px));
	max-height: min(560px, calc(100vh - 96px));
	background: var(--surface);
	border: 1px solid var(--border-light);
	border-radius: 6px;
	box-shadow: 0 18px 48px color-mix(in srgb, var(--black) 55%, transparent);
	overflow: hidden;
}

.attr-search {
	position: relative;
	padding: 12px 12px 10px;
	border-bottom: 1px solid var(--border-subtle);

	:global(svg) {
		position: absolute;
		left: 22px;
		top: 50%;
		transform: translateY(-50%);
		color: var(--text-muted);
		pointer-events: none;
	}
}

.attr-search-inp {
	width: 100%;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid color-mix(in srgb, var(--white) 10%, transparent);
	border-radius: 3px;
	color: var(--text-primary);
	font-family: var(--sans);
	font-size: 13px;
	padding: 8px 11px 8px 31px;
	outline: none;

	&:focus {
		border-color: var(--accent);
		box-shadow: 0 0 0 1px color-mix(in srgb, var(--accent) 40%, transparent);
	}
}

.attr-list {
	overflow-y: auto;
	padding: 8px;
}

.attr-group {
	margin-bottom: 8px;
}

.attr-group-head {
	display: flex;
	align-items: center;
	gap: 5px;
	margin: 6px 6px 4px;
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.attr-opt {
	width: 100%;
	display: flex;
	align-items: center;
	gap: 8px;
	background: none;
	border: none;
	border-radius: 4px;
	color: var(--text-secondary);
	font-family: var(--sans);
	font-size: 13.5px;
	text-align: left;
	padding: 7px 9px;
	cursor: pointer;

	.attr-opt-text {
		flex: 1;
		overflow: hidden;
		text-overflow: ellipsis;
		white-space: nowrap;
	}

	&:hover:not(:disabled) {
		background: color-mix(in srgb, var(--white) 6%, transparent);
		color: var(--text-primary);
	}
	&.selected {
		color: var(--accent-light);
		background: color-mix(in srgb, var(--accent) 12%, transparent);
	}
	&:disabled {
		color: var(--text-muted);
		cursor: not-allowed;
		text-decoration: line-through;
	}
}

.attr-empty {
	font-family: var(--mono);
	font-size: 11.5px;
	color: var(--text-muted);
	padding: 16px 8px;
}
</style>
