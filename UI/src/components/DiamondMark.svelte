<div class="diamond-container" style:margin-bottom={marginBottom != null ? `${marginBottom}px` : null}>
	<div class="diamond" class:pulsing class:error style:width="{size}px" style:height="{size}px">
		<div class="diamond-inner" class:error></div>
	</div>
</div>

<script lang="ts">
type Props = {
	pulsing?: boolean;
	error?: boolean;
	size?: number;
	marginBottom?: number;
};

let { pulsing = false, error = false, size = 16, marginBottom }: Props = $props();
</script>

<style lang="scss">
.diamond-container {
	display: flex;
	justify-content: center;
}

.diamond {
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
