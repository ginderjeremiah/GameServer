<div class="group" data-testid="log-group-{group.key}">
	<div class="group-header">
		<button class="header-toggle" onclick={() => (open = !open)} aria-expanded={open}>
			<svg
				class="chevron"
				class:open
				width="11"
				height="11"
				viewBox="0 0 12 12"
				fill="none"
				stroke="rgba(240, 240, 240, 0.55)"
				stroke-width="1.5"
			>
				<path d="M4 2.5L8 6L4 9.5" stroke-linecap="round" stroke-linejoin="round" />
			</svg>
			<span class="group-label">{group.label}</span>
			{#if groupDirty}
				<span class="group-dirty-dot"></span>
			{/if}
			<span class="group-count">{onCount}/{types.length}</span>
		</button>

		<GroupToggle
			on={allOn}
			mixed={!allOn && !allOff}
			onClick={() => view.setMany(keys, !allOn)}
			testId="group-toggle-{group.key}"
		/>
	</div>

	{#if open}
		<div class="group-rows">
			{#each types as type (type.id)}
				<LogTypeRow {type} {view} />
			{/each}
		</div>
	{/if}
</div>

<script lang="ts">
import GroupToggle from './GroupToggle.svelte';
import LogTypeRow from './LogTypeRow.svelte';
import type { LogGroupDef, LogTypeDef, OptionsView } from './options-view.svelte';

interface Props {
	group: LogGroupDef;
	types: LogTypeDef[];
	view: OptionsView;
}

const { group, types, view }: Props = $props();

let open = $state(true);

const keys = $derived(types.map((t) => t.id));
const onCount = $derived(types.filter((t) => view.isOn(t.id)).length);
const allOn = $derived(onCount === types.length);
const allOff = $derived(onCount === 0);
const groupDirty = $derived(types.some((t) => view.isDirtyId(t.id)));
</script>

<style lang="scss">
.group {
	border: 1px solid rgba(255, 255, 255, 0.08);
	border-radius: 4px;
	background: rgba(255, 255, 255, 0.015);
	overflow: hidden;
}

.group-header {
	display: flex;
	align-items: center;
	gap: 12px;
	padding: 11px 16px;
	transition: background 130ms;

	&:hover {
		background: rgba(255, 255, 255, 0.02);
	}
}

.header-toggle {
	display: flex;
	align-items: center;
	gap: 10px;
	flex: 1;
	background: transparent;
	border: none;
	cursor: pointer;
	padding: 0;
	text-align: left;
	color: inherit;
}

.chevron {
	transition: transform 160ms;

	&.open {
		transform: rotate(90deg);
	}
}

.group-label {
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.78);
}

.group-dirty-dot {
	width: 5px;
	height: 5px;
	border-radius: 50%;
	background: var(--warning);
	box-shadow: 0 0 6px color-mix(in srgb, var(--warning) 85%, transparent);
}

.group-count {
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.5px;
	color: rgba(240, 240, 240, 0.4);
}

.group-rows {
	border-top: 1px solid rgba(255, 255, 255, 0.07);
}
</style>
