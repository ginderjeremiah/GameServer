<div class="heading">
	<h1 data-testid="loading-heading" class:error-text={view.phase === 'error'}>{view.title}</h1>
	<p class="subtitle">
		{#if view.phase === 'checking'}
			Verifying cached reference data…
		{:else if view.phase === 'loading' && view.currentItem}
			Loading <span class="highlight">{view.currentItem.label.toLowerCase()}</span>…
		{:else if view.phase === 'error' && view.currentItem}
			Could not load <span class="error-highlight">{view.currentItem.label.toLowerCase()}</span>.
		{:else if view.phase === 'done'}
			All systems nominal.
		{:else}
			&nbsp;
		{/if}
	</p>
</div>

<script lang="ts">
import type { LoadingView } from './loading-view.svelte';

type Props = {
	view: LoadingView;
};

let { view }: Props = $props();
</script>

<style lang="scss">
.heading {
	text-align: center;
	margin-bottom: 22px;

	h1 {
		margin: 0;
		padding: 0;
		font-size: 26px;
		font-weight: 400;
		letter-spacing: -0.3px;
		color: var(--text-primary);
		transition: color 200ms;

		&.error-text {
			color: var(--error);
		}
	}

	.subtitle {
		margin: 8px 0 0;
		font-size: 12.5px;
		color: var(--text-secondary);
		font-family: var(--mono);
		letter-spacing: 0.5px;
		min-height: 18px;
	}

	.highlight {
		color: var(--accent-light);
	}

	.error-highlight {
		color: var(--error);
	}
}
</style>
