<!--
	Modal dialog card — the tightened "Atmospheric" treatment: a centered card over a dimmed game,
	with a gradient tint, accent border, a soft static glow and a radial top sheen. No header chrome
	or eyebrow; just title, body and the action buttons. Presentation only — the `ModalHost` owns the
	backdrop, focus management and dismissal wiring.

	kind: 'confirm' (cancel + confirm) · 'acknowledge' (single button) · 'destructive' (danger primary).
-->
<div
	class="modal-card {kind}"
	role="dialog"
	aria-modal="true"
	aria-labelledby="modal-title"
	aria-describedby="modal-body"
>
	<div class="title" id="modal-title">{title}</div>
	<div class="body" id="modal-body">{body}</div>
	<div class="actions">
		{#if kind !== 'acknowledge'}
			<button class="btn ghost" type="button" data-modal-cancel onclick={onCancel}>{cancelLabel}</button>
		{/if}
		<button
			class="btn"
			class:danger={kind === 'destructive'}
			class:accent={kind !== 'destructive'}
			type="button"
			data-modal-primary
			onclick={onConfirm}>{confirmLabel}</button
		>
	</div>
</div>

<script lang="ts">
import type { ModalKind } from '$stores';

interface Props {
	title: string;
	body: string;
	kind?: ModalKind;
	confirmLabel: string;
	cancelLabel: string;
	onConfirm: () => void;
	onCancel: () => void;
}

let { title, body, kind = 'confirm', confirmLabel, cancelLabel, onConfirm, onCancel }: Props = $props();
</script>

<style lang="scss">
.modal-card {
	// Accent the whole card derives from; danger swaps it to the error hue.
	--modal-accent: var(--accent);
	position: relative;
	overflow: hidden;
	width: 400px;
	max-width: calc(100vw - 2rem);
	padding: 24px 26px 22px;
	text-align: center;
	border: 1px solid color-mix(in srgb, var(--modal-accent) 28%, transparent);
	border-radius: 4px;
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--modal-accent) 7%, var(--surface)) 0%,
		var(--surface-alpha) 60%
	);
	box-shadow:
		0 20px 50px rgba(0, 0, 0, 0.6),
		0 0 18px -10px var(--modal-accent);
	// Entrance: rise + settle in. Collapsed to ~instant under `prefers-reduced-motion`.
	animation: modal-in 0.32s cubic-bezier(0.22, 0.61, 0.36, 1) both;

	&.destructive {
		--modal-accent: var(--error);
	}

	// Radial top sheen.
	&::before {
		content: '';
		position: absolute;
		inset: 0;
		pointer-events: none;
		background: radial-gradient(
			120% 70% at 50% -10%,
			color-mix(in srgb, var(--modal-accent) 9%, transparent),
			transparent 62%
		);
	}
}

.title {
	position: relative;
	font-size: 22px;
	font-weight: 500;
	letter-spacing: -0.4px;
	color: var(--text-primary);
}

.body {
	position: relative;
	max-width: 320px;
	margin: 11px auto 0;
	font-size: 13px;
	line-height: 1.6;
	color: var(--text-secondary);
}

.actions {
	position: relative;
	display: flex;
	justify-content: center;
	gap: 10px;
	margin-top: 20px;
}

.btn {
	padding: 8px 18px;
	border-radius: 2px;
	cursor: pointer;
	font-family: var(--sans);
	font-size: 12.5px;
	font-weight: 500;
	letter-spacing: 0.2px;
	transition: background 0.14s;

	&.accent {
		color: var(--accent-light);
		background: color-mix(in srgb, var(--accent) 16%, transparent);
		border: 1px solid var(--accent);

		&:hover {
			background: color-mix(in srgb, var(--accent) 26%, transparent);
		}
	}

	&.danger {
		color: var(--error);
		background: color-mix(in srgb, var(--error) 15%, transparent);
		border: 1px solid color-mix(in srgb, var(--error) 70%, transparent);

		&:hover {
			background: color-mix(in srgb, var(--error) 24%, transparent);
		}
	}

	&.ghost {
		color: var(--text-secondary);
		background: transparent;
		border: 1px solid var(--border-light);

		&:hover {
			background: color-mix(in srgb, var(--white) 5%, transparent);
		}
	}
}

@keyframes modal-in {
	from {
		opacity: 0;
		transform: translateY(10px) scale(0.965);
	}
	to {
		opacity: 1;
		transform: none;
	}
}
</style>
