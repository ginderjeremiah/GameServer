<!-- The gold "Challenge" / "Re-challenge" action that starts a boss fight. Fills with
     the boss accent on hover (the diamond inverts to read on the filled background). -->
<button class="challenge-btn" type="button" data-testid="challenge-boss" onclick={onClick}>
	<span class="btn-diamond" aria-hidden="true"></span>
	{@render children()}
</button>

<script lang="ts">
import type { Snippet } from 'svelte';

type Props = {
	onClick: () => void;
	children: Snippet;
};

const { onClick, children }: Props = $props();
</script>

<style lang="scss">
.challenge-btn {
	display: inline-flex;
	align-items: center;
	gap: 8px;
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	font-weight: 600;
	white-space: nowrap;
	color: var(--boss-accent);
	background: color-mix(in srgb, var(--boss-accent) 10%, transparent);
	border: 1px solid var(--boss-accent);
	border-radius: 2px;
	cursor: pointer;
	padding: 6px 12px;
	transition: all 150ms;

	&:hover {
		color: var(--text-on-accent);
		background: var(--boss-accent);
		box-shadow: 0 0 18px color-mix(in srgb, var(--boss-accent) 40%, transparent);

		.btn-diamond {
			background: var(--text-on-accent);
			box-shadow: none;
		}
	}
}

.btn-diamond {
	display: inline-block;
	width: 6px;
	height: 6px;
	transform: rotate(45deg);
	background: var(--boss-accent);
	box-shadow: 0 0 8px color-mix(in srgb, var(--boss-accent) 80%, transparent);
	flex-shrink: 0;
}
</style>
