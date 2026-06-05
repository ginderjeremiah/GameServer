<div class="diamond-container">
	<div class="diamond">
		<div class="diamond-inner"></div>
	</div>
</div>

<style lang="scss">
.diamond-container {
	display: flex;
	justify-content: center;
	margin-bottom: 36px;
}

.diamond {
	width: 18px;
	height: 18px;
	transform: rotate(45deg);
	border: 1px solid var(--accent);
	box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 40%, transparent);
	position: relative;
}

.diamond-inner {
	position: absolute;
	inset: 4px;
	background: var(--accent);
}
</style>
