<!-- Shown to a player who has not recorded any statistics yet (no battles
     fought), so every tracked total would otherwise read zero. -->
<div class="empty" data-testid="statistics-empty">
	<div class="diamond"></div>
	<div class="empty-title">No statistics yet</div>
	<p class="empty-copy">
		Your tracked totals appear here as you play. Head into a zone and defeat some enemies — kills, damage dealt, zones
		cleared and more will start filling in.
	</p>
</div>

<style lang="scss">
.empty {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	text-align: center;
	gap: 12px;
	padding: 24px;
}

.diamond {
	width: 10px;
	height: 10px;
	transform: rotate(45deg);
	background: var(--accent);
	box-shadow: 0 0 10px color-mix(in srgb, var(--accent) 60%, transparent);
}

.empty-title {
	font-size: 18px;
	font-weight: 500;
	color: var(--text-primary);
}

.empty-copy {
	margin: 0;
	max-width: 420px;
	font-size: 13px;
	line-height: 1.6;
	color: var(--text-tertiary);
}
</style>
