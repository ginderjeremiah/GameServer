<div class="settings-rail" data-testid="settings-rail">
	<div class="rail-header">Categories</div>
	{#each SETTINGS_CATS as cat (cat.key)}
		{@const isActive = active === cat.key}
		<button
			class="rail-item"
			class:active={isActive}
			class:disabled={!cat.built}
			disabled={!cat.built}
			data-testid="settings-cat-{cat.key}"
			onclick={() => cat.built && onPick(cat.key)}
		>
			{#if isActive}
				<span class="active-bar"></span>
			{/if}
			<SettingsGlyph glyph={cat.glyph} color={isActive ? 'var(--accent)' : 'currentColor'} />
			<span class="rail-label">{cat.label}</span>
			{#if !cat.built}
				<span class="soon-badge">soon</span>
			{/if}
		</button>
	{/each}
</div>

<script lang="ts">
import SettingsGlyph from './SettingsGlyph.svelte';
import { SETTINGS_CATS } from './options-view.svelte';

interface Props {
	active: string;
	onPick: (key: string) => void;
}

const { active, onPick }: Props = $props();
</script>

<style lang="scss">
.settings-rail {
	width: 210px;
	flex-shrink: 0;
	border-right: 1px solid rgba(255, 255, 255, 0.07);
	padding: 22px 0;
	display: flex;
	flex-direction: column;
}

.rail-header {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: var(--text-muted);
	padding: 0 22px 12px;
}

.rail-item {
	position: relative;
	display: flex;
	align-items: center;
	gap: 12px;
	width: 100%;
	padding: 10px 22px;
	border: none;
	text-align: left;
	background: transparent;
	color: rgba(240, 240, 240, 0.72);
	font-family: inherit;
	font-size: 13.5px;
	cursor: pointer;
	transition:
		background 130ms,
		color 130ms;

	&:hover:not(.disabled) {
		background: rgba(255, 255, 255, 0.025);
	}

	&.active {
		background: color-mix(in srgb, var(--accent) 7%, transparent);
		color: var(--text-primary);
	}

	&.disabled {
		color: rgba(240, 240, 240, 0.32);
		cursor: default;
	}
}

.active-bar {
	position: absolute;
	left: 0;
	top: 6px;
	bottom: 6px;
	width: 2px;
	background: var(--accent);
	box-shadow: 0 0 10px color-mix(in srgb, var(--accent) 67%, transparent);
}

.rail-label {
	flex: 1;
}

.soon-badge {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.3);
	border: 1px solid rgba(240, 240, 240, 0.13);
	border-radius: 2px;
	padding: 1px 4px;
}
</style>
