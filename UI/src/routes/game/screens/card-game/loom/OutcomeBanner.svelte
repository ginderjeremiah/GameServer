<div class="banner {outcome}" role="status" aria-live="polite">
	<h2>{outcome === 'win' ? 'VICTORY' : 'DOWNED'}</h2>
	<span class="bsub">{sub}</span>
</div>

<script lang="ts">
interface Props {
	outcome: 'win' | 'lose';
	/** Flavour line shown beneath the headline. */
	sub: string;
}

const { outcome, sub }: Props = $props();
</script>

<style lang="scss">
.banner {
	position: absolute;
	inset: 0;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 6px;
	z-index: 12;
	background: color-mix(in srgb, var(--surface) 82%, transparent);
	backdrop-filter: blur(3px);
	font-family: var(--sans);

	h2 {
		font-size: 38px;
		margin: 0;
		letter-spacing: 0.06em;
		font-weight: 700;
	}

	.bsub {
		font-family: var(--mono);
		font-size: 12px;
		color: var(--text-tertiary);
	}

	&.win h2 {
		color: var(--health-remaining-color);
		text-shadow: 0 0 24px color-mix(in srgb, var(--health-remaining-color) 60%, transparent);
	}
	&.lose h2 {
		color: var(--enemy-accent);
		text-shadow: 0 0 24px color-mix(in srgb, var(--enemy-accent) 60%, transparent);
	}
}
</style>
