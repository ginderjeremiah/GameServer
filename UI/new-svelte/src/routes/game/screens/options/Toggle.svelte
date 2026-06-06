<button
	class="toggle"
	class:on
	role="switch"
	aria-checked={on}
	aria-label={label}
	data-testid={testId}
	onclick={() => onToggle(!on)}
>
	<span class="knob"></span>
</button>

<script lang="ts">
interface Props {
	on: boolean;
	onToggle: (next: boolean) => void;
	label?: string;
	testId?: string;
}

const { on, onToggle, label, testId }: Props = $props();
</script>

<style lang="scss">
.toggle {
	position: relative;
	width: 40px;
	height: 22px;
	flex-shrink: 0;
	border-radius: 999px;
	cursor: pointer;
	padding: 0;
	border: 1px solid color-mix(in srgb, var(--white) 20%, transparent);
	background: color-mix(in srgb, var(--white) 4%, transparent);
	transition: all 160ms ease;

	&.on {
		border-color: var(--accent);
		background: color-mix(in srgb, var(--accent) 15%, transparent);
		box-shadow:
			0 0 10px color-mix(in srgb, var(--accent) 33%, transparent),
			inset 0 0 6px color-mix(in srgb, var(--accent) 13%, transparent);
	}
}

.knob {
	position: absolute;
	top: 2px;
	left: 2px;
	width: 16px;
	height: 16px;
	border-radius: 50%;
	background: var(--text-tertiary);
	transition:
		left 160ms cubic-bezier(0.4, 0, 0.2, 1),
		background 160ms;

	.on & {
		left: 19px;
		background: var(--accent);
		box-shadow: 0 0 8px var(--accent);
	}
}
</style>
