<div
	class="hero"
	style:background="linear-gradient(135deg, {tintColor(accent, 0.14)}, {tintColor(accent, 0.03)} 60%, transparent)"
	style:border="1px solid {tintColor(accent, 0.28)}"
>
	<div class="hero-watermark">
		<TypeGlyph typeId={group.typeId} color={accent} size={150} strokeWidth={0.7} />
	</div>
	<div class="hero-content">
		<RingMeter {pct} {accent} size={58} stroke={4} done={complete}>
			<TypeGlyph typeId={group.typeId} color={complete ? 'var(--success)' : accent} size={24} />
		</RingMeter>
		<div class="hero-text">
			<div class="hero-eyebrow" style:color={tintColor(accent, 0.9)}>Challenge Type</div>
			<div class="hero-title">{group.label}</div>
			<div class="hero-blurb">{blurb}</div>
		</div>
		<div class="hero-stats">
			<div class="hero-count">
				<span class="hero-done" class:complete>{stats.done}</span>
				<span class="hero-total">/ {stats.total}</span>
			</div>
			<div class="hero-pct">unlocked · {pct}%</div>
		</div>
	</div>
	<div
		class="hero-line"
		style:background="linear-gradient(90deg, transparent, {tintColor(accent, 0.7)}, transparent)"
	></div>
</div>

<script lang="ts">
import { tintColor } from '$lib/common';
import { challengeTypeBlurb } from './challenge-meta';
import { typeStats, type TypeGroup } from './challenges-view.svelte';
import RingMeter from './RingMeter.svelte';
import TypeGlyph from './TypeGlyph.svelte';

interface Props {
	group: TypeGroup;
}

const { group }: Props = $props();

const accent = $derived(group.accent);
const stats = $derived(typeStats(group.items));
const pct = $derived(Math.round((stats.done / Math.max(1, stats.total)) * 100));
const complete = $derived(stats.done === stats.total);
const blurb = $derived(challengeTypeBlurb(group.typeId));
</script>

<style lang="scss">
.hero {
	position: relative;
	overflow: hidden;
	border-radius: 5px;
	margin-bottom: 18px;
	padding: 18px 22px;
}

.hero-watermark {
	position: absolute;
	right: -14px;
	top: -22px;
	opacity: 0.08;
	pointer-events: none;
}

.hero-content {
	position: relative;
	display: flex;
	align-items: center;
	gap: 20px;
}

.hero-text {
	flex: 1;
	min-width: 0;
}

.hero-eyebrow {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
}

.hero-title {
	font-size: 26px;
	font-weight: 400;
	letter-spacing: -0.4px;
	line-height: 1.05;
	margin: 4px 0 5px;
}

.hero-blurb {
	font-size: 12.5px;
	color: var(--text-secondary);
}

.hero-stats {
	text-align: right;
	flex-shrink: 0;
}

.hero-count {
	display: flex;
	align-items: baseline;
	gap: 6px;
	justify-content: flex-end;
}

.hero-done {
	font-family: var(--mono);
	font-size: 26px;
	color: var(--text-primary);
	line-height: 1;

	&.complete {
		color: var(--success);
	}
}

.hero-total {
	font-family: var(--mono);
	font-size: 14px;
	color: var(--text-muted);
}

.hero-pct {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.hero-line {
	position: absolute;
	left: 0;
	right: 0;
	bottom: 0;
	height: 2px;
}

@media (prefers-reduced-motion: no-preference) {
	.hero-line {
		animation: hero-shimmer 3.6s ease-in-out infinite;
	}

	@keyframes hero-shimmer {
		0%,
		100% {
			opacity: 0.5;
		}
		50% {
			opacity: 1;
		}
	}
}
</style>
