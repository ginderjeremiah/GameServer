<div class="loading-screen" data-testid="loading-screen">
	<div class="loading-form">
		<DiamondMark pulsing={view.phase !== 'done'} error={view.phase === 'error'} />
		<LoadingHeader {view} />
		<ProgressBar {view} />
		<ManifestWindow items={view.items} activeIndex={view.activeIndex} phase={view.phase} />

		{#if view.phase === 'error' && view.currentItem}
			<ErrorBanner message={view.currentItem.error} onRetry={() => view.retryFailed()} />
		{/if}

		<EnterButton phase={view.phase} onEnter={() => view.enterGame()} />
	</div>
</div>

<script lang="ts">
import { onMount } from 'svelte';
import DiamondMark from './DiamondMark.svelte';
import LoadingHeader from './LoadingHeader.svelte';
import ProgressBar from './ProgressBar.svelte';
import ManifestWindow from './ManifestWindow.svelte';
import ErrorBanner from './ErrorBanner.svelte';
import EnterButton from './EnterButton.svelte';
import { LoadingView } from './loading-view.svelte';

const view = new LoadingView();

onMount(() => {
	view.start();
});
</script>

<style lang="scss">
.loading-screen {
	width: 100%;
	height: 100%;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	padding: 40px;
}

.loading-form {
	width: 380px;
	max-width: 100%;
}
</style>
