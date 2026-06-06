<button
	data-testid="enter-button"
	class="enter-button"
	class:ready={phase === 'done'}
	disabled={phase !== 'done'}
	onclick={onEnter}
	style:margin-top={phase === 'error' ? '14px' : '22px'}
>
	{label}
</button>

<script lang="ts">
import type { Phase } from './loading-view.svelte';

type Props = {
	phase: Phase;
	onEnter: () => void;
};

let { phase, onEnter }: Props = $props();

const label = $derived(phase === 'done' ? 'Enter Realm' : phase === 'error' ? 'Locked' : 'Loading…');
</script>

<style lang="scss">
.enter-button {
	width: 100%;
	padding: 12px 0;
	background: transparent;
	color: color-mix(in srgb, var(--text-primary) 45%, transparent);
	border: 1px solid color-mix(in srgb, var(--text-primary) 25%, transparent);
	border-radius: 2px;
	cursor: not-allowed;
	font-size: 13px;
	font-weight: 500;
	font-family: inherit;
	letter-spacing: 2px;
	text-transform: uppercase;
	transition: all 200ms;

	&.ready {
		background: var(--accent);
		color: var(--text-on-accent);
		border-color: var(--accent);
		cursor: pointer;
	}
}
</style>
