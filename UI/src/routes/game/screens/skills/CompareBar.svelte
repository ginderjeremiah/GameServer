<div class="versus">
	<span class="vs-eyebrow">Compare vs</span>
	{#if view.comparePresets.length}
		<div class="enemy-pills">
			{#each view.comparePresets as preset (preset.key)}
				<button
					type="button"
					class="epill"
					class:on={view.activePresetKey === preset.key}
					class:boss={preset.isBoss}
					onclick={() => view.selectPreset(preset)}
				>
					<span class="en">{preset.isBoss ? `◆ ${preset.name}` : preset.name}</span>
					<span class="ed">TGH {Math.round(preset.toughness)}</span>
				</button>
			{/each}
		</div>
	{:else}
		<span class="vs-note">Drag to preview damage against an enemy's Toughness</span>
	{/if}
	<div class="vs-slider">
		<span class="vs-eyebrow">Toughness</span>
		<input
			type="range"
			min="0"
			max={view.maxToughness}
			value={view.effectiveToughness}
			aria-label="Compare-vs enemy toughness"
			oninput={(e) => view.setToughness(+e.currentTarget.value)}
		/>
		<span class="vs-defval">TGH <b>{Math.round(view.effectiveToughness)}</b></span>
	</div>
</div>

<script lang="ts">
import type { SkillsView } from './skills-view.svelte';

type Props = {
	view: SkillsView;
};

const { view }: Props = $props();
</script>

<style lang="scss">
.versus {
	display: flex;
	align-items: center;
	gap: 16px;
	margin-top: 14px;
	padding: 10px 14px;
	border: 1px solid color-mix(in srgb, var(--enemy-accent) 26%, transparent);
	border-radius: 4px;
	background: color-mix(in srgb, var(--enemy-accent) 6%, transparent);
}

.vs-eyebrow {
	flex-shrink: 0;
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--enemy-accent) 80%, var(--text-primary));
}

.vs-note {
	font-size: 12px;
	color: var(--text-tertiary);
}

.enemy-pills {
	display: flex;
	gap: 7px;
	flex-wrap: wrap;
}

.epill {
	display: flex;
	flex-direction: column;
	align-items: flex-start;
	gap: 1px;
	padding: 5px 11px;
	border: 1px solid var(--border-light);
	border-radius: 3px;
	background: transparent;
	cursor: pointer;
	font-family: var(--sans);
	color: var(--text-secondary);

	.en {
		font-size: 12px;
	}

	.ed {
		font-family: var(--mono);
		font-size: 8px;
		letter-spacing: 0.5px;
		color: var(--text-muted);
	}

	&.on {
		border-color: var(--enemy-accent);
		background: color-mix(in srgb, var(--enemy-accent) 16%, transparent);
		color: var(--text-primary);

		.ed {
			color: color-mix(in srgb, var(--enemy-accent) 85%, var(--text-primary));
		}
	}

	/* The dedicated boss reads in its own gold accent (matching the boss fight),
	   distinct from the idle-spawn enemy accent. */
	&.boss .en {
		color: color-mix(in srgb, var(--boss-accent) 85%, var(--text-primary));
	}

	&.boss.on {
		border-color: var(--boss-accent);
		background: color-mix(in srgb, var(--boss-accent) 16%, transparent);

		.ed {
			color: color-mix(in srgb, var(--boss-accent) 85%, var(--text-primary));
		}
	}
}

.vs-slider {
	display: flex;
	align-items: center;
	gap: 10px;
	margin-left: auto;
	flex-shrink: 0;

	input[type='range'] {
		appearance: none;
		width: 180px;
		height: 4px;
		border-radius: 3px;
		background: color-mix(in srgb, var(--white) 12%, transparent);
		outline: none;

		&::-webkit-slider-thumb {
			appearance: none;
			width: 14px;
			height: 14px;
			border-radius: 50%;
			background: var(--enemy-accent);
			cursor: pointer;
			box-shadow: 0 0 8px color-mix(in srgb, var(--enemy-accent) 50%, transparent);
		}

		&::-moz-range-thumb {
			width: 14px;
			height: 14px;
			border: none;
			border-radius: 50%;
			background: var(--enemy-accent);
			cursor: pointer;
		}
	}
}

.vs-defval {
	min-width: 54px;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-tertiary);

	b {
		color: var(--enemy-accent);
		font-size: 14px;
		font-weight: 600;
	}
}
</style>
