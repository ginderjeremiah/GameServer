<div class="tt-equipped-badge">
	<div class="tt-equipped-dot"></div>
	<span>Equipped</span>
</div>

<style lang="scss">
.tt-equipped-badge {
	margin-left: auto;
	display: flex;
	align-items: center;
	gap: 6px;
	padding: 2px 8px;
	background: color-mix(in srgb, var(--success) 10%, transparent);
	border: 1px solid color-mix(in srgb, var(--success) 45%, transparent);
	border-radius: 2px;

	span {
		font-family: var(--mono);
		font-size: 9px;
		color: var(--success);
		letter-spacing: 1.2px;
		text-transform: uppercase;
	}
}

.tt-equipped-dot {
	width: 5px;
	height: 5px;
	border-radius: 50%;
	background: var(--success);
	box-shadow: 0 0 4px color-mix(in srgb, var(--success) 90%, transparent);
}
</style>
