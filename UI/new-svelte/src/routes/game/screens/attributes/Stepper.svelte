<div class="stepper">
	<button class="step" type="button" disabled={!canDec} aria-label="Remove a point" onclick={() => canDec && onDec()}>
		<svg
			width="11"
			height="11"
			viewBox="0 0 12 12"
			fill="none"
			stroke="currentColor"
			stroke-width="1.7"
			stroke-linecap="round"
		>
			<path d="M2 6h8" />
		</svg>
	</button>
	<button class="step plus" type="button" disabled={!canInc} aria-label="Add a point" onclick={() => canInc && onInc()}>
		<svg
			width="11"
			height="11"
			viewBox="0 0 12 12"
			fill="none"
			stroke="currentColor"
			stroke-width="1.7"
			stroke-linecap="round"
		>
			<path d="M6 2v8M2 6h8" />
		</svg>
	</button>
</div>

<script lang="ts">
interface Props {
	canDec: boolean;
	canInc: boolean;
	onDec: () => void;
	onInc: () => void;
}

const { canDec, canInc, onDec, onInc }: Props = $props();
</script>

<style lang="scss">
.stepper {
	display: flex;
	align-items: center;
	gap: 6px;
	flex-shrink: 0;
}

.step {
	width: 26px;
	height: 26px;
	border-radius: 3px;
	padding: 0;
	display: flex;
	align-items: center;
	justify-content: center;
	cursor: pointer;
	color: var(--text-secondary);
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid var(--border-medium);
	transition: all 120ms;

	&:hover:not(:disabled) {
		background: color-mix(in srgb, var(--white) 7%, transparent);
	}

	&:disabled {
		cursor: default;
		color: color-mix(in srgb, var(--text-primary) 22%, transparent);
		background: color-mix(in srgb, var(--white) 2%, transparent);
		border-color: var(--border-subtle);
	}

	&.plus {
		color: var(--accent-light);
		background: color-mix(in srgb, var(--accent) 8%, transparent);
		border-color: color-mix(in srgb, var(--accent) 40%, transparent);

		&:hover:not(:disabled) {
			background: color-mix(in srgb, var(--accent) 15%, transparent);
			border-color: var(--accent);
		}

		&:disabled {
			color: color-mix(in srgb, var(--text-primary) 22%, transparent);
			background: color-mix(in srgb, var(--white) 2%, transparent);
			border-color: var(--border-subtle);
		}
	}
}
</style>
