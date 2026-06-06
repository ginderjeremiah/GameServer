<button
	class="group-toggle"
	class:on
	class:mixed
	title={on ? 'Disable all in group' : 'Enable all in group'}
	aria-label={on ? 'Disable all in group' : 'Enable all in group'}
	data-testid={testId}
	onclick={onClick}
>
	<span class="knob"></span>
</button>

<script lang="ts">
interface Props {
	/** Every type in the group is enabled. */
	on: boolean;
	/** Some — but not all — types in the group are enabled. */
	mixed: boolean;
	onClick: () => void;
	testId?: string;
}

const { on, mixed, onClick, testId }: Props = $props();
</script>

<style lang="scss">
.group-toggle {
	position: relative;
	width: 36px;
	height: 20px;
	flex-shrink: 0;
	border-radius: 999px;
	cursor: pointer;
	padding: 0;
	border: 1px solid rgba(255, 255, 255, 0.2);
	background: rgba(255, 255, 255, 0.04);
	transition: all 160ms ease;

	&.on,
	&.mixed {
		border-color: var(--accent);
	}

	&.on {
		background: color-mix(in srgb, var(--accent) 15%, transparent);
		box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 27%, transparent);
	}

	&.mixed {
		background: color-mix(in srgb, var(--accent) 8%, transparent);
	}
}

.knob {
	position: absolute;
	top: 2px;
	left: 2px;
	width: 14px;
	height: 14px;
	border-radius: 50%;
	background: rgba(240, 240, 240, 0.55);
	transition:
		left 160ms cubic-bezier(0.4, 0, 0.2, 1),
		background 160ms;

	.on &,
	.mixed & {
		background: var(--accent);
	}

	.mixed & {
		left: 9px;
	}

	.on & {
		left: 17px;
		box-shadow: 0 0 7px var(--accent);
	}
}
</style>
