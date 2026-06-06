<div class="save-bar" data-testid="save-bar">
	<div class="status">
		{#if view.saved}
			<span class="status-saved" data-testid="save-status-saved">
				<svg width="13" height="13" viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.6">
					<path d="M3 7.3L6 10.2L11 4" stroke-linecap="round" stroke-linejoin="round" />
				</svg>
				Preferences saved
			</span>
		{:else if view.dirtyCount === 0}
			<span>No unsaved changes</span>
		{:else}
			<span class="status-dot"></span>
			<span class="status-count" data-testid="save-status-dirty">
				{view.dirtyCount} unsaved {view.dirtyCount === 1 ? 'change' : 'changes'}
			</span>
		{/if}
	</div>

	<div class="actions">
		<button
			class="ds-button ghost"
			disabled={!view.isDirty || view.saving}
			data-testid="discard-button"
			onclick={() => view.discard()}
		>
			Discard
		</button>
		<button
			class="ds-button primary"
			disabled={!view.isDirty || view.saving}
			data-testid="save-button"
			onclick={() => view.save()}
		>
			Save Changes
		</button>
	</div>
</div>

<script lang="ts">
import type { OptionsView } from './options-view.svelte';

interface Props {
	view: OptionsView;
}

const { view }: Props = $props();
</script>

<style lang="scss">
.save-bar {
	display: flex;
	align-items: center;
	justify-content: space-between;
	padding: 14px 24px;
	border-top: 1px solid color-mix(in srgb, var(--white) 7%, transparent);
	background: color-mix(in srgb, var(--black) 20%, transparent);
	flex-shrink: 0;
}

.status {
	font-family: var(--mono);
	font-size: 11.5px;
	letter-spacing: 0.4px;
	color: var(--text-tertiary);
	display: flex;
	align-items: center;
	gap: 9px;
}

.status-saved {
	color: var(--success);
	display: inline-flex;
	align-items: center;
	gap: 7px;
}

.status-dot {
	width: 6px;
	height: 6px;
	border-radius: 50%;
	background: var(--warning);
	box-shadow: 0 0 6px color-mix(in srgb, var(--warning) 90%, transparent);
}

.status-count {
	color: color-mix(in srgb, var(--text-primary) 80%, transparent);
}

.actions {
	display: flex;
	gap: 10px;
}

.ds-button {
	display: inline-flex;
	align-items: center;
	gap: 7px;
	font-family: var(--mono);
	font-size: 11.5px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
	padding: 8px 18px;
	border-radius: 3px;
	cursor: pointer;
	transition: all 140ms ease;
	background: transparent;
	border: 1px solid color-mix(in srgb, var(--white) 16%, transparent);
	color: color-mix(in srgb, var(--text-primary) 85%, transparent);

	&.ghost:hover:not(:disabled) {
		background: color-mix(in srgb, var(--white) 5%, transparent);
		border-color: color-mix(in srgb, var(--white) 30%, transparent);
	}

	&.primary {
		background: color-mix(in srgb, var(--accent) 12%, transparent);
		border-color: var(--accent);
		color: var(--accent-light);

		&:hover:not(:disabled) {
			box-shadow: 0 0 12px color-mix(in srgb, var(--accent) 33%, transparent);
		}
	}

	&:disabled {
		background: transparent;
		border-color: var(--border-subtle);
		color: color-mix(in srgb, var(--text-primary) 30%, transparent);
		cursor: not-allowed;
	}
}
</style>
