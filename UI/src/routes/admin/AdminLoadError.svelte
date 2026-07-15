<div class="admin-load-error" role="alert" data-testid={testid}>
	<p>{message}</p>
	<button type="button" class="btn" onclick={onRetry}>Refresh</button>
</div>

<script lang="ts">
interface Props {
	message: string;
	testid: string;
	onRetry: () => void;
}

const { message, testid, onRetry }: Props = $props();
</script>

<style lang="scss">
.admin-load-error {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 14px;
	padding: 20px;
	text-align: center;

	p {
		max-width: 480px;
		color: var(--error);
		font-size: 13px;
	}
}

.btn {
	display: inline-flex;
	align-items: center;
	gap: 7px;
	background: transparent;
	border: 1px solid var(--border-light);
	color: var(--text-secondary);
	font-family: var(--mono);
	font-size: 11.5px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
	padding: 8px 15px;
	border-radius: 3px;
	cursor: pointer;
	transition: all 0.14s ease;
	white-space: nowrap;

	&:hover:not(:disabled) {
		border-color: color-mix(in srgb, var(--white) 32%, transparent);
	}
}
</style>
