<div class="commit">
	<div class="status">
		{#if view.saved}
			<span class="saved">
				<svg width="12" height="12" viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.6">
					<path d="M3 7.3L6 10.2L11 4" stroke-linecap="round" stroke-linejoin="round" />
				</svg>
				Attributes saved
			</span>
		{:else if view.dirty}
			<span class="pending">{view.changedCount} attribute{view.changedCount === 1 ? '' : 's'} changed</span>
		{:else}
			<span class="muted">No changes</span>
		{/if}
	</div>

	<div class="actions">
		<button type="button" class="bar-btn" disabled={!view.dirty || view.saving} onclick={() => view.discard()}>
			Discard
		</button>
		<button type="button" class="bar-btn primary" disabled={!view.dirty || view.saving} onclick={() => view.save()}>
			{view.saving ? 'Saving…' : 'Confirm'}
		</button>
	</div>
</div>

<script lang="ts">
import type { AttributesView } from './attributes-view.svelte';

interface Props {
	view: AttributesView;
}

const { view }: Props = $props();
</script>

<style lang="scss">
.commit {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 16px;
}

.status {
	display: flex;
	align-items: center;
	gap: 14px;
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.3px;
}

.saved {
	display: inline-flex;
	align-items: center;
	gap: 6px;
	color: var(--success);
}

.pending {
	color: var(--text-secondary);
}

.muted {
	color: var(--text-tertiary);
}

.actions {
	display: flex;
	gap: 9px;
}

.bar-btn {
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
	padding: 8px 18px;
	border-radius: 3px;
	cursor: pointer;
	transition: all 140ms;
	background: transparent;
	border: 1px solid var(--border-medium);
	color: var(--text-secondary);

	&:hover:not(:disabled) {
		border-color: color-mix(in srgb, var(--white) 30%, transparent);
		background: color-mix(in srgb, var(--white) 5%, transparent);
	}

	&.primary {
		color: var(--accent-light);
		background: color-mix(in srgb, var(--accent) 12%, transparent);
		border-color: var(--accent);

		&:hover:not(:disabled) {
			box-shadow: 0 0 12px color-mix(in srgb, var(--accent) 33%, transparent);
		}
	}

	&:disabled {
		cursor: not-allowed;
		color: color-mix(in srgb, var(--text-primary) 30%, transparent);
		background: transparent;
		border-color: var(--border-subtle);
		box-shadow: none;
	}
}
</style>
