<div class="placeholder-screen">
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
	border: 1px dashed rgba(240, 240, 240, 0.18);
	display: flex;
	align-items: center;
	justify-content: center;
}

.placeholder-diamond {
	width: 16px;
	height: 16px;
	transform: rotate(45deg);
	border: 1px solid rgba(240, 240, 240, 0.4);
	position: relative;
}

.placeholder-diamond-inner {
	position: absolute;
	inset: 4px;
	background: rgba(240, 240, 240, 0.4);
}

.placeholder-title {
	font-size: 18px;
	color: var(--text-primary);
	font-weight: 400;
	letter-spacing: -0.1px;
}

.placeholder-subtitle {
	margin-top: 4px;
	font-family: 'Geist Mono', monospace;
	font-size: 11px;
	color: rgba(240, 240, 240, 0.5);
	letter-spacing: 1px;
	text-transform: uppercase;
}
</style>
