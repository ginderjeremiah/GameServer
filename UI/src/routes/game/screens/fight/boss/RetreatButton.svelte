<!-- The "Retreat" action shown during a boss fight: abandon the boss and return to
     the normal idle field. Mirrors ChallengeButton's fill-on-hover, in the
     destructive enemy accent, without the diamond. -->
<button
	class="retreat-btn"
	type="button"
	title="Retreat to the normal field"
	data-testid="retreat-boss"
	onclick={onClick}
>
	Retreat
</button>

<script lang="ts">
type Props = {
	onClick: () => void;
};

const { onClick }: Props = $props();
</script>

<style lang="scss">
.retreat-btn {
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	font-weight: 600;
	white-space: nowrap;
	color: var(--enemy-accent);
	background: color-mix(in srgb, var(--enemy-accent) 10%, transparent);
	border: 1px solid var(--enemy-accent);
	border-radius: 2px;
	cursor: pointer;
	padding: 6px 12px;
	transition: all 150ms;

	&:hover {
		color: var(--text-on-accent);
		background: var(--enemy-accent);
		box-shadow: 0 0 18px color-mix(in srgb, var(--enemy-accent) 40%, transparent);
	}
}
</style>
