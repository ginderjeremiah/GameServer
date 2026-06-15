<!-- A small countdown pill for a tooltip title's trailing slot, mirroring the skill tooltip's
     CooldownPill: a depleting fill plus the remaining time, tinted by the supplied accent (the
     attribute tooltip passes the effect's buff/debuff colour). -->
<div class="tt-duration-pill" style:--dp-accent={color}>
	<div class="tt-duration-fill" style:width="{progress}%"></div>
	<span class="tt-duration-text">{text}</span>
</div>

<script lang="ts">
interface Props {
	/** Remaining-time fill width, 0–100 (depletes as the timer runs down). */
	progress: number;
	/** Remaining time, pre-formatted (e.g. `2.4s`). */
	text: string;
	/** Accent colour for the fill and text (a CSS colour or `var(...)`). */
	color: string;
}

const { progress, text, color }: Props = $props();
</script>

<style lang="scss">
.tt-duration-pill {
	margin-left: auto;
	position: relative;
	width: 52px;
	height: 18px;
	background: color-mix(in srgb, var(--text-primary) 6%, transparent);
	border: 1px solid var(--border-light);
	border-radius: 2px;
	overflow: hidden;
}

.tt-duration-fill {
	position: absolute;
	inset: 0;
	background: linear-gradient(
		90deg,
		color-mix(in srgb, var(--dp-accent) 50%, transparent),
		color-mix(in srgb, var(--dp-accent) 18%, transparent)
	);
}

.tt-duration-text {
	position: absolute;
	inset: 0;
	display: flex;
	align-items: center;
	justify-content: center;
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.6px;
	color: var(--dp-accent);
}
</style>
