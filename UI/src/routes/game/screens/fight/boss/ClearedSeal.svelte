<!-- The "Cleared" seal shown once a zone's boss has been defeated. -->
<span class="cleared-seal" class:compact data-testid="cleared-seal">
	<svg class="check" viewBox="0 0 14 14" fill="none" aria-hidden="true">
		<path d="M3 7.4 5.8 10 11 4.2" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
	</svg>
	<span class="label">Cleared</span>
</span>

<script lang="ts">
type Props = {
	/** A tighter variant for inline use in the trigger / overlay. */
	compact?: boolean;
};

const { compact = false }: Props = $props();
</script>

<style lang="scss">
.cleared-seal {
	display: inline-flex;
	align-items: center;
	gap: 6px;
	border: 1px solid color-mix(in srgb, var(--boss-accent) 47%, transparent);
	border-radius: 2px;
	padding: 4px 9px;
	background: color-mix(in srgb, var(--boss-accent) 7%, transparent);

	&.compact {
		gap: 5px;
		padding: 3px 7px;
	}
}

.check {
	width: 11px;
	height: 11px;

	path {
		stroke: var(--boss-accent);
	}

	.compact & {
		width: 9px;
		height: 9px;
	}
}

.label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.5px;
	text-transform: uppercase;
	color: var(--boss-accent);

	.compact & {
		font-size: 8.5px;
	}
}
</style>
