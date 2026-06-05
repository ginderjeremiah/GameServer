<button data-testid="submit-button" type="submit" class="submit-button" class:ready disabled={!ready || submitting}>
	{#if submitting}
		<div class="submit-spinner"></div>
		...
	{:else}
		{label}
	{/if}
</button>

<script lang="ts">
interface Props {
	/** Whether the form is valid and ready to submit (drives the active styling). */
	ready: boolean;
	/** Whether a submission is in flight. */
	submitting?: boolean;
	label: string;
}

let { ready, submitting = false, label }: Props = $props();
</script>

<style lang="scss">
.submit-button {
	width: 100%;
	padding: 13px 0;
	background: transparent;
	color: var(--text-tertiary);
	border: 1px solid color-mix(in srgb, var(--text-primary) 35%, transparent);
	border-radius: 2px;
	cursor: not-allowed;
	font-size: 13px;
	font-weight: 500;
	font-family: inherit;
	letter-spacing: 2px;
	text-transform: uppercase;
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 10px;
	transition: all 180ms;

	&.ready {
		background: var(--accent);
		color: var(--text-on-accent);
		border-color: var(--accent);
		cursor: pointer;
	}

	&:disabled {
		cursor: not-allowed;
	}
}

.submit-spinner {
	width: 11px;
	height: 11px;
	border: 2px solid color-mix(in srgb, var(--text-on-accent) 25%, transparent);
	border-top-color: var(--text-on-accent);
	border-radius: 50%;
	animation: spin 0.8s linear infinite;
}

@keyframes spin {
	to {
		transform: rotate(360deg);
	}
}
</style>
