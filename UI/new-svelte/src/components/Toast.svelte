<div class="toast round-border {type}" role="alert" aria-live="assertive">
	<span class="message">{message}</span>
	{#if dismissible}
		<button class="dismiss" type="button" aria-label="Dismiss notification" onclick={onDismiss}>×</button>
	{/if}
</div>

<script lang="ts">
import type { ToastType } from '$stores';

interface Props {
	message: string;
	type?: ToastType;
	dismissible?: boolean;
	onDismiss?: () => void;
}

let { message, type = 'info', dismissible = true, onDismiss }: Props = $props();
</script>

<style lang="scss">
.toast {
	display: flex;
	align-items: flex-start;
	gap: 0.6em;
	min-width: 220px;
	max-width: 340px;
	padding: 0.6em 0.75em;
	background: var(--surface-alpha);
	color: var(--text-primary);
	border: var(--default-border);
	border-left: 3px solid var(--accent);
	box-shadow: 0 4px 14px rgba(0, 0, 0, 0.45);
	pointer-events: auto;

	&.error {
		border-left-color: var(--error);
	}

	&.success {
		border-left-color: var(--success);
	}

	&.warning {
		border-left-color: var(--warning);
	}

	&.info {
		border-left-color: var(--accent);
	}

	.message {
		flex: 1;
		font-size: 0.9em;
		line-height: 1.35;
		word-break: break-word;
	}

	.dismiss {
		flex-shrink: 0;
		background: none;
		border: none;
		color: var(--text-secondary);
		font-size: 1.2em;
		line-height: 1;
		cursor: pointer;
		padding: 0 0.15em;

		&:hover {
			color: var(--text-primary);
		}
	}
}
</style>
