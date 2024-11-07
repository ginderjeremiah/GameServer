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

type Props = {
	title: string;
	loading: boolean;
};

let { title, loading }: Props = $props();
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
