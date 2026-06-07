<div class="toast {type}" class:leaving role="alert" aria-live="assertive">
	<div class="surface">
		<div class="head">
			<StatusGlyph {type} size={14} />
			<span class="label">{statusLabel}</span>
			<span class="spacer"></span>
			{#if dismissible}
				<button class="dismiss" type="button" aria-label="Dismiss notification" onclick={onDismiss}>×</button>
			{/if}
		</div>
		<div class="message">{message}</div>
		{#if action}
			<div class="action-row">
				<button class="action" type="button" onclick={runAction}>{action.label} →</button>
			</div>
		{/if}
	</div>
</div>

<script lang="ts">
import type { ToastType, ToastAction } from '$stores';
import StatusGlyph from './StatusGlyph.svelte';

interface Props {
	message: string;
	type?: ToastType;
	dismissible?: boolean;
	/** Optional inline action button. Clicking it runs the action then dismisses the toast. */
	action?: ToastAction;
	/** Set while the toast is animating out, before the container removes it from the DOM. */
	leaving?: boolean;
	onDismiss?: () => void;
}

let { message, type = 'info', dismissible = true, action, leaving = false, onDismiss }: Props = $props();

const STATUS_LABELS: Record<ToastType, string> = {
	info: 'Info',
	success: 'Success',
	warning: 'Warning',
	error: 'Error'
};
const statusLabel = $derived(STATUS_LABELS[type]);

// Running the inline action resolves the notification, so it dismisses the toast afterwards.
const runAction = () => {
	action?.onClick();
	onDismiss?.();
};
</script>

<style lang="scss">
.toast {
	// Per-status accent — the single hue the tint, border, glow and glyph all derive from, so a
	// theme can restyle a status by overriding the source token alone.
	--toast-accent: var(--accent);
	pointer-events: auto;
	width: 340px;
	max-width: calc(100vw - 2rem);
	border-radius: var(--border-radius);
	// Entrance: slide in from the anchor edge + fade. Collapsed to ~instant under
	// `prefers-reduced-motion` by the global rule in common.scss.
	animation: toast-in 0.3s cubic-bezier(0.22, 0.61, 0.36, 1) both;

	&.success {
		--toast-accent: var(--success);
	}

	&.warning {
		--toast-accent: var(--warning);
	}

	&.error {
		--toast-accent: var(--error);
	}

	&.leaving {
		// Clip the surface while the height collapses so the stack above can slide up cleanly.
		overflow: hidden;
		animation: toast-out 0.28s cubic-bezier(0.22, 0.61, 0.36, 1) forwards;
		pointer-events: none;
	}
}

.surface {
	position: relative;
	overflow: hidden;
	padding: 12px 14px 13px;
	color: var(--text-primary);
	border: 1px solid color-mix(in srgb, var(--toast-accent) 26%, transparent);
	border-radius: inherit;
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--toast-accent) 6%, var(--surface)) 0%,
		var(--surface-alpha) 65%
	);
	box-shadow:
		0 6px 18px rgba(0, 0, 0, 0.5),
		0 0 14px -10px var(--toast-accent),
		inset 0 1px 0 color-mix(in srgb, var(--white) 3%, transparent);

	// Faint scanline atmosphere.
	&::before {
		content: '';
		position: absolute;
		inset: 0;
		pointer-events: none;
		background: repeating-linear-gradient(
			180deg,
			transparent 0 3px,
			color-mix(in srgb, var(--white) 1%, transparent) 3px 4px
		);
	}
}

.head {
	position: relative;
	display: flex;
	align-items: center;
	gap: 8px;
	color: var(--toast-accent);
}

.label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.12em;
	text-transform: uppercase;
	color: var(--toast-accent);
}

.spacer {
	flex: 1;
}

.dismiss {
	flex-shrink: 0;
	padding: 0 0.15em;
	background: none;
	border: none;
	color: var(--text-tertiary);
	font-size: 15px;
	line-height: 1;
	cursor: pointer;

	&:hover {
		color: var(--text-primary);
	}
}

.message {
	position: relative;
	margin-top: 7px;
	font-size: 13px;
	line-height: 1.45;
	word-break: break-word;
}

.action-row {
	position: relative;
	margin-top: 9px;
}

.action {
	padding: 0;
	background: none;
	border: none;
	cursor: pointer;
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.12em;
	text-transform: uppercase;
	color: var(--toast-accent);

	&:hover {
		text-decoration: underline;
	}
}

@keyframes toast-in {
	from {
		opacity: 0;
		transform: translateX(22px) scale(0.98);
	}
	to {
		opacity: 1;
		transform: none;
	}
}

@keyframes toast-out {
	from {
		opacity: 1;
		transform: none;
		max-height: 140px;
	}
	to {
		opacity: 0;
		transform: translateX(22px);
		max-height: 0;
		margin-top: 0;
	}
}
</style>
