<div class="toggle" role="tablist" aria-label="Attribute view density">
	{#each options as opt (opt.key)}
		<button
			type="button"
			id="attr-mode-{opt.key}"
			role="tab"
			aria-selected={mode === opt.key}
			aria-controls="attr-main-panel"
			class="opt"
			class:on={mode === opt.key}
			onclick={() => onPick(opt.key)}
		>
			{opt.label}
		</button>
	{/each}
</div>

<script lang="ts">
import type { AttributeMode } from './attributes-view.svelte';

interface Props {
	mode: AttributeMode;
	onPick: (mode: AttributeMode) => void;
}

const { mode, onPick }: Props = $props();

const options: { key: AttributeMode; label: string }[] = [
	{ key: 'guided', label: 'Guided' },
	{ key: 'theory', label: 'Theorycraft' }
];
</script>

<style lang="scss">
.toggle {
	display: inline-flex;
	padding: 3px;
	border-radius: 5px;
	gap: 3px;
	background: color-mix(in srgb, var(--white) 4%, transparent);
	border: 1px solid var(--border-light);
}

.opt {
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	padding: 6px 14px;
	border-radius: 3px;
	cursor: pointer;
	border: none;
	background: transparent;
	color: var(--text-tertiary);
	transition: color 120ms;

	&.on {
		color: var(--text-primary);
		background: color-mix(in srgb, var(--accent) 12%, transparent);
		box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--accent) 53%, transparent);
	}

	&:hover:not(.on) {
		color: var(--text-secondary);
	}
}
</style>
