<!-- The no-combat Home sanctuary's fight-screen body: shown in place of the combatants while the player
     is resting at Home. The zone-nav stays above it so the player can head back out to resume battling. -->
<div class="home-rest" data-testid="home-rest">
	<div class="home-glyph" aria-hidden="true">&#8962;</div>
	<p class="home-title">Resting at Home</p>
	<p class="home-text">
		A quiet refuge where no enemies spawn. Take a breather, manage your build, and head back out through the zone arrows
		when you're ready to fight again.
	</p>
</div>

<style lang="scss">
.home-rest {
	flex: 1;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 16px;
	padding: 40px 36px 96px;
	text-align: center;
}

.home-glyph {
	font-size: 56px;
	line-height: 1;
	color: color-mix(in srgb, var(--accent-light) 80%, transparent);
}

.home-title {
	font-family: var(--mono);
	font-size: 13px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--accent-light) 85%, transparent);
	margin: 0;
}

.home-text {
	max-width: 420px;
	font-size: 14px;
	line-height: 1.6;
	color: color-mix(in srgb, var(--text-primary) 65%, transparent);
	margin: 0;
}
</style>
