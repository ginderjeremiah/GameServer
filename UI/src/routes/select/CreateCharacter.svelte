<form class="create-form" onsubmit={preventDefault(onSubmit)} data-testid="create-form">
	<ClassPicker {classes} {selectedClassId} onSelect={onSelectClass} disabled={creating} />

	{#if classes.length === 0}
		<!-- The picker self-hides until the options arrive. A class is required, so cover the gap: a brief
		     loading hint, then an unavailable + retry state if the fetch fails (the player isn't stuck). -->
		<p class="class-status" data-testid="class-status">
			{#if classesLoading}
				<span class="loading">Loading classes…</span>
			{:else}
				<span class="unavailable">Class options couldn’t be loaded.</span>
				<button type="button" class="retry" data-testid="retry-classes" disabled={creating} onclick={onRetryClasses}>
					Retry
				</button>
			{/if}
		</p>
	{/if}

	<UnderlineInput
		testid="new-name-input"
		id="new-character-name"
		label="Character name"
		autocomplete="off"
		placeholder="Character name"
		bind:value
		onblur={() => (touched = true)}
		error={!!showError}
		valid={!!value.trim() && valid}
	/>

	<StatusLine type={showError ? 'err' : 'idle'} text={showError ?? ''} />

	<div class="create-actions">
		<button type="button" class="ghost-button" data-testid="cancel-create" disabled={creating} onclick={onCancel}>
			Cancel
		</button>
		<SubmitButton ready={valid && selectedClassId !== null && !creating} submitting={creating} label="Create" />
	</div>
</form>

<script lang="ts">
import type { ICreatableClass } from '$lib/api';
import { preventDefault } from '$lib/common/event-wrappers';
import UnderlineInput from '../login/UnderlineInput.svelte';
import StatusLine from '../login/StatusLine.svelte';
import SubmitButton from '../login/SubmitButton.svelte';
import ClassPicker from './ClassPicker.svelte';

interface Props {
	/** Two-way bound name input. */
	value: string;
	/** Whether the current name passes client-side validation. */
	valid: boolean;
	/** The client-side validation message for the current name (empty when valid). */
	validationMsg: string;
	/** A surfaced backend error (name rejected / cap reached). */
	error: string | null;
	/** Whether a create request is in flight. */
	creating: boolean;
	/** The creatable class options; the picker shows only when this is non-empty (it's empty while the
	 *  bespoke payload loads and if it fails to load). */
	classes: ICreatableClass[];
	/** Whether the class options are still loading — drives the loading-vs-unavailable status shown while
	 *  the picker is hidden. */
	classesLoading: boolean;
	/** The chosen class id, or null before the options load / a choice is made. A class is required to
	 *  submit, so the Create button stays disabled until one is selected. */
	selectedClassId: number | null;
	/** Notifies the parent of a class choice. */
	onSelectClass: (classId: number) => void;
	/** Re-fetches the class options after a failed load (the retry affordance). */
	onRetryClasses: () => void;
	onSubmit: () => void;
	onCancel: () => void;
}

let {
	value = $bindable(),
	valid,
	validationMsg,
	error,
	creating,
	classes,
	classesLoading,
	selectedClassId,
	onSelectClass,
	onRetryClasses,
	onSubmit,
	onCancel
}: Props = $props();

// Don't nag with the validation hint before the field has been touched; once it has (or a backend
// error arrived) surface whichever message applies — a backend error takes precedence.
let touched = $state(false);
const showError = $derived(error ?? (touched && !valid ? validationMsg : null));
</script>

<style lang="scss">
.create-form {
	margin-top: 8px;
	padding: 16px 18px 18px;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: var(--border-radius);
}

.class-status {
	display: flex;
	align-items: center;
	gap: 10px;
	margin: 0 0 14px;
	font-size: 12.5px;
	font-family: var(--mono);
	letter-spacing: 0.4px;

	.loading {
		color: var(--text-tertiary);
	}

	.unavailable {
		color: var(--error);
	}

	.retry {
		padding: 4px 10px;
		background: transparent;
		color: var(--text-secondary);
		border: 1px solid color-mix(in srgb, var(--text-primary) 25%, transparent);
		border-radius: 2px;
		font-family: inherit;
		font-size: 11.5px;
		letter-spacing: 0.6px;
		cursor: pointer;
		transition: all 160ms;

		&:hover:not(:disabled),
		&:focus-visible {
			color: var(--text-primary);
			border-color: var(--accent);
		}

		&:focus-visible {
			outline: 2px solid var(--accent);
			outline-offset: 2px;
		}

		&:disabled {
			cursor: not-allowed;
			opacity: 0.6;
		}
	}
}

.create-actions {
	display: flex;
	align-items: center;
	gap: 12px;
}

.ghost-button {
	flex-shrink: 0;
	padding: 13px 18px;
	background: transparent;
	color: var(--text-secondary);
	border: 1px solid color-mix(in srgb, var(--text-primary) 25%, transparent);
	border-radius: 2px;
	font-size: 13px;
	font-weight: 500;
	font-family: inherit;
	letter-spacing: 1px;
	text-transform: uppercase;
	cursor: pointer;
	transition: all 160ms;

	&:hover:not(:disabled),
	&:focus-visible {
		color: var(--text-primary);
		border-color: color-mix(in srgb, var(--text-primary) 45%, transparent);
	}

	&:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
	}

	&:disabled {
		cursor: not-allowed;
		opacity: 0.6;
	}
}
</style>
