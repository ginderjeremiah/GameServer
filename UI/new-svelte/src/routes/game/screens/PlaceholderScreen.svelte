<div class="placeholder-screen" data-testid="placeholder-screen">
	<div class="placeholder-icon">
		<div class="placeholder-diamond">
			<div class="placeholder-diamond-inner"></div>
		</div>
	</div>
	<div class="placeholder-text">
		<div class="placeholder-title">{label}</div>
		<div class="placeholder-subtitle">Not yet implemented</div>
	</div>
</div>

<script lang="ts">
interface Props {
	label: string;
}

const { label }: Props = $props();
</script>

<style lang="scss">
.placeholder-screen {
	height: 100%;
	padding: 36px;
	display: flex;
	align-items: center;
	justify-content: center;
	flex-direction: column;
	gap: 14px;
}

.placeholder-icon {
	width: 64px;
	height: 64px;
	border-radius: 2px;
	border: 1px dashed color-mix(in srgb, var(--white) 18%, transparent);
	display: flex;
	align-items: center;
	justify-content: center;
}

.placeholder-diamond {
	width: 16px;
	height: 16px;
	transform: rotate(45deg);
	border: 1px solid color-mix(in srgb, var(--white) 40%, transparent);
	position: relative;
}

.placeholder-diamond-inner {
	position: absolute;
	inset: 4px;
	background: color-mix(in srgb, var(--white) 40%, transparent);
}

.placeholder-title {
	font-size: 18px;
	color: var(--text-primary);
	font-weight: 400;
	letter-spacing: -0.1px;
}

.placeholder-subtitle {
	margin-top: 4px;
	font-family: var(--mono);
	font-size: 11px;
	color: color-mix(in srgb, var(--text-primary) 50%, transparent);
	letter-spacing: 1px;
	text-transform: uppercase;
}
</style>
