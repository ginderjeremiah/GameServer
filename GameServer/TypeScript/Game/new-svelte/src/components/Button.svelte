<button class="btn round-border hover-glow" disabled="{loading}" on:click>
	{#if loading}
		<Loading></Loading>
	{/if}
	<div class="btn-text">{text}</div>
</button>

<script lang="ts">
import Loading from './Loading.svelte';

export let text: string;
export let loading = false;
</script>

<style lang="scss">
.btn {
	background-color: var(--btn-background);
	width: 100%;
	border-width: 1px;
	font-size: 1rem;
	margin: auto;
	user-select: none;
	padding: 0;

	&:active:not(:disabled) {
		filter: brightness(0.8) !important;
	}

	&:hover:not(:disabled) {
		filter: brightness(0.9);
	}

	&:disabled {
		cursor: not-allowed;
	}

	.btn-text {
		padding: 0.2rem 0.5rem;
	}
}
</style>
