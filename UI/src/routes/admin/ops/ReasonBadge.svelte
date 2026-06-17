<span class="reason-badge {meta.tone}" data-testid="reason-badge" title={meta.hint}>
	<span class="dot"></span>
	{meta.label}
</span>

<script lang="ts">
import { reasonMeta } from './dead-letters.svelte';
import type { EDeadLetterReason } from '$lib/api';

interface Props {
	reason: EDeadLetterReason;
}

const { reason }: Props = $props();
const meta = $derived(reasonMeta(reason));
</script>

<style lang="scss">
.reason-badge {
	display: inline-flex;
	align-items: center;
	gap: 6px;
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	white-space: nowrap;
	padding: 3px 9px;
	border-radius: 3px;
	border: 1px solid currentColor;
	background: color-mix(in srgb, currentColor 12%, transparent);

	.dot {
		width: 6px;
		height: 6px;
		border-radius: 50%;
		background: currentColor;
		box-shadow: 0 0 6px currentColor;
		flex-shrink: 0;
	}

	// Tone keys off a dedicated ops-severity token so a theme restyles every badge by overriding the
	// variable alone — and these stay distinct from validation (--warning) / change-state (--change-*).
	&.poison {
		color: var(--dead-letter-poison);
	}
	&.warn {
		color: var(--dead-letter-warn);
	}
	&.ok {
		color: var(--dead-letter-ok);
	}
}
</style>
