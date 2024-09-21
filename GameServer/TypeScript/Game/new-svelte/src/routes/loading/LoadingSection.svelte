<div class="loading-section">
	<span>{title}</span>
	{#if loading}
		<div>
			<Loading />
		</div>
	{:else}
		<span>Done</span>
	{/if}
</div>

<script lang="ts">
import Loading from '$components/Loading.svelte';
export let title: string;
export let loading: boolean;
</script>

<style lang="scss">
.loading-section {
	display: flex;
	justify-content: space-between;
	margin-bottom: 0.5rem;

	div {
		position: relative;
		aspect-ratio: 1;
		margin-right: 1rem;
	}
}
</style>
