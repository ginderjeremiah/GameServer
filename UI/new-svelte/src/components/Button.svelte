<button class="btn round-border hover-glow" disabled={loading} onclick={onClick}>
	{#if loading}
		<Loading></Loading>
	{/if}
	<div class={textPadding}>{text}</div>
</button>

<script lang="ts">
import Loading from './Loading.svelte';
type Props = {
	text?: string;
	loading?: boolean;
	onClick: (event: MouseEvent) => void;
	textPadding?: 'comfortable' | 'minimal' | 'none';
};

let { text, loading = false, onClick, textPadding = 'comfortable' }: Props = $props();
</script>

<style lang="scss">
.btn {
	background-color: var(--btn-background);
	width: 100%;
	border-width: 1px;
	font-size: 1em;
	margin: auto;
	user-select: none;
	padding: 0;
	cursor: pointer;

	&:active:not(:disabled) {
		filter: brightness(0.8) !important;
	}

	&:hover:not(:disabled) {
		filter: brightness(0.9);
	}

	&:disabled {
		cursor: not-allowed;
	}

	.comfortable {
		padding: 0.2em 0.5em;
	}

	.minimal {
		padding: 0.1em 0.25em;
	}
}
</style>
