<!-- The heightened boss-fight backdrop: a slow gold top-glow plus a darkening
     vignette that frames the arena while a boss is engaged. -->
<div class="boss-atmosphere" aria-hidden="true">
	<div class="glow"></div>
	<div class="vignette"></div>
</div>

<style lang="scss">
.boss-atmosphere {
	position: absolute;
	inset: 0;
	pointer-events: none;
	z-index: 0;
	overflow: hidden;
}

.glow {
	position: absolute;
	inset: 0;
	background: radial-gradient(
		120% 90% at 50% -10%,
		color-mix(in srgb, var(--boss-accent) 12%, transparent),
		transparent 55%
	);
	animation: boss-atmo 4.5s ease-in-out infinite;
}

.vignette {
	position: absolute;
	inset: 0;
	box-shadow:
		inset 0 0 160px color-mix(in srgb, var(--black) 70%, transparent),
		inset 0 0 60px color-mix(in srgb, var(--boss-accent) 6%, transparent);
}

@keyframes boss-atmo {
	0%,
	100% {
		opacity: 0.7;
	}
	50% {
		opacity: 1;
	}
}
</style>
