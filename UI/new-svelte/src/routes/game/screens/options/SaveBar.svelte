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
	border-top: 1px solid rgba(255, 255, 255, 0.07);
	background: rgba(0, 0, 0, 0.2);
	flex-shrink: 0;
}

.status {
	font-family: var(--mono);
	font-size: 11.5px;
	letter-spacing: 0.4px;
	color: rgba(240, 240, 240, 0.55);
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
	color: rgba(240, 240, 240, 0.8);
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
	border: 1px solid rgba(255, 255, 255, 0.16);
	color: rgba(240, 240, 240, 0.85);

	&.ghost:hover:not(:disabled) {
		background: rgba(255, 255, 255, 0.05);
		border-color: rgba(255, 255, 255, 0.3);
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
		border-color: rgba(255, 255, 255, 0.08);
		color: rgba(240, 240, 240, 0.3);
		cursor: not-allowed;
	}
}
</style>
