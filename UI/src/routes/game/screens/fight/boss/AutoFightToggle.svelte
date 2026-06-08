<!-- Toggles auto-fight: when on, defeating the boss immediately re-challenges it
     (farming) rather than handing back to the idle loop. -->
<button
	class="auto-fight"
	class:on
	class:compact
	type="button"
	title="Keep re-engaging the boss after each victory"
	aria-pressed={on}
	data-testid="auto-fight-toggle"
	onclick={() => onChange(!on)}
>
	<span class="track" aria-hidden="true"><span class="knob"></span></span>
	<span class="label">Auto-fight</span>
</button>

<script lang="ts">
type Props = {
	on: boolean;
	onChange: (on: boolean) => void;
	/** A tighter variant for the inline trigger / boss bar. */
	compact?: boolean;
};

const { on, onChange, compact = false }: Props = $props();
</script>

<style lang="scss">
.auto-fight {
	display: inline-flex;
	align-items: center;
	gap: 8px;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid color-mix(in srgb, var(--white) 16%, transparent);
	border-radius: 2px;
	cursor: pointer;
	padding: 6px 11px;
	transition: all 150ms;

	&.compact {
		padding: 5px 9px;
	}

	&.on {
		background: color-mix(in srgb, var(--boss-accent) 10%, transparent);
		border-color: color-mix(in srgb, var(--boss-accent) 53%, transparent);
	}
}

.track {
	position: relative;
	width: 26px;
	height: 14px;
	border-radius: 999px;
	background: color-mix(in srgb, var(--white) 18%, transparent);
	transition: background 150ms;
	flex-shrink: 0;

	.on & {
		background: var(--boss-accent);
	}
}

.knob {
	position: absolute;
	top: 2px;
	left: 2px;
	width: 10px;
	height: 10px;
	border-radius: 50%;
	background: var(--text-primary);
	transition:
		left 150ms,
		background 150ms;

	.on & {
		left: 14px;
		background: var(--text-on-accent);
	}
}

.label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.3px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--text-primary) 60%, transparent);
	white-space: nowrap;

	.on & {
		color: var(--boss-accent);
	}
}
</style>
