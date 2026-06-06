<div class="error-banner">
	<div class="error-message">{message}</div>
	<button class="retry-button" data-testid="retry-button" onclick={onRetry}>Retry</button>
</div>

<script lang="ts">
type Props = {
	message: string | null;
	onRetry: () => void;
};

let { message, onRetry }: Props = $props();
</script>

<style lang="scss">
.error-banner {
	margin-top: 16px;
	padding: 10px 12px;
	background: color-mix(in srgb, var(--error) 8%, transparent);
	border: 1px solid color-mix(in srgb, var(--error) 35%, transparent);
	border-radius: 3px;
	display: flex;
	align-items: center;
	gap: 12px;

	.error-message {
		font-family: var(--mono);
		font-size: 11px;
		color: var(--error);
		flex: 1;
		line-height: 1.4;
	}
}

.retry-button {
	background: color-mix(in srgb, var(--text-primary) 8%, transparent);
	border: 1px solid var(--text-muted);
	color: var(--text-primary);
	padding: 7px 14px;
	border-radius: 2px;
	font-family: inherit;
	font-size: 11.5px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
	cursor: pointer;
	transition: all 140ms;
}
</style>
