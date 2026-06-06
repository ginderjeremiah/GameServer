<div class="log-row" class:on data-testid="log-row-{type.id}">
	<!-- dirty marker -->
	<span class="dirty-bar" class:dirty></span>

	<!-- glyph chip -->
	<div class="chip" style:--chip-color={type.color}>
		<LogGlyph glyph={type.glyph} color={on ? type.color : 'rgba(240, 240, 240, 0.4)'} size={16} />
	</div>

	<!-- label + description -->
	<div class="body">
		<div class="name-row">
			<span class="name">{type.name}</span>
			{#if dirty}
				<span class="edited-tag">edited</span>
			{/if}
		</div>
		<div class="desc">{type.desc}</div>
	</div>

	<Toggle {on} onToggle={(next) => view.setOne(type.id, next)} label={type.name} testId="toggle-{type.id}" />
</div>

<script lang="ts">
import { LogGlyph } from '$components';
import Toggle from './Toggle.svelte';
import type { LogTypeDef, OptionsView } from './options-view.svelte';

interface Props {
	type: LogTypeDef;
	view: OptionsView;
}

const { type, view }: Props = $props();

const on = $derived(view.isOn(type.id));
const dirty = $derived(view.isDirtyId(type.id));
</script>

<style lang="scss">
.log-row {
	display: flex;
	align-items: center;
	gap: 16px;
	padding: 14px 18px;
	border-bottom: 1px solid rgba(255, 255, 255, 0.05);
	transition: background 130ms;
	position: relative;

	&:hover {
		background: rgba(255, 255, 255, 0.025);
	}

	&:last-child {
		border-bottom: none;
	}
}

.dirty-bar {
	position: absolute;
	left: 0;
	top: 8px;
	bottom: 8px;
	width: 2px;
	background: transparent;
	transition: background 140ms;

	&.dirty {
		background: var(--warning);
		box-shadow: 0 0 8px color-mix(in srgb, var(--warning) 80%, transparent);
	}
}

.chip {
	width: 34px;
	height: 34px;
	flex-shrink: 0;
	border-radius: 3px;
	display: flex;
	align-items: center;
	justify-content: center;
	background: rgba(255, 255, 255, 0.03);
	border: 1px solid rgba(255, 255, 255, 0.1);
	transition: all 160ms;

	.on & {
		background: color-mix(in srgb, var(--chip-color) 10%, transparent);
		border-color: color-mix(in srgb, var(--chip-color) 33%, transparent);
	}
}

.body {
	flex: 1;
	min-width: 0;
}

.name-row {
	display: flex;
	align-items: center;
	gap: 9px;
	margin-bottom: 2px;
}

.name {
	font-size: 14.5px;
	letter-spacing: -0.1px;
	color: rgba(240, 240, 240, 0.62);
	transition: color 140ms;

	.on & {
		color: var(--text-primary);
	}
}

.edited-tag {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	color: var(--warning);
	border: 1px solid color-mix(in srgb, var(--warning) 40%, transparent);
	border-radius: 2px;
	padding: 0 4px;
}

.desc {
	font-size: 12.5px;
	color: rgba(240, 240, 240, 0.45);
}
</style>
