<div class="diamond-container">
	<div class="diamond" class:pulsing class:error>
		<div class="diamond-inner" class:error></div>
	</div>
</div>

<script lang="ts">
type Props = {
	pulsing: boolean;
	error: boolean;
};

let { pulsing, error }: Props = $props();
</script>

<style lang="scss">
.diamond-container {
	display: flex;
	justify-content: center;
	margin-bottom: 26px;
}

.diamond {
	width: 16px;
	height: 16px;
	transform: rotate(45deg);
	border: 1px solid var(--accent);
	box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 40%, transparent);
	position: relative;
	transition:
		border-color 200ms,
		box-shadow 200ms;

	&.pulsing {
		animation: pulse-glow 1.6s ease-in-out infinite;
	}

	&.error {
		border-color: var(--error);
		box-shadow: 0 0 8px color-mix(in srgb, var(--error) 45%, transparent);
	}
}

.diamond-inner {
	position: absolute;
	inset: 4px;
	background: var(--accent);
	transition: background 200ms;

	&.error {
		background: var(--error);
	}
}
</style>
