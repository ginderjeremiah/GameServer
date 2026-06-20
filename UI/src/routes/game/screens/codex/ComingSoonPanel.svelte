<!-- Placeholder body for a Codex tab that isn't built yet (Zones / Skills). Mirrors the global
     PlaceholderScreen's dashed-diamond language, tinted with the tab's section accent. -->
<div class="coming-soon" data-testid="codex-coming-soon" style:--cs-accent={accent}>
	<div class="icon">
		<div class="diamond"><div class="diamond-inner"></div></div>
	</div>
	<div class="title">{label}</div>
	<div class="subtitle">Coming soon</div>
</div>

<script lang="ts">
interface Props {
	label: string;
	/** Section accent for this tab (a themeable `var(--…)` token). */
	accent?: string;
}

const { label, accent = 'var(--accent)' }: Props = $props();
</script>

<style lang="scss">
.coming-soon {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 14px;
	padding: 36px;
}

.icon {
	width: 64px;
	height: 64px;
	border-radius: 2px;
	border: 1px dashed color-mix(in srgb, var(--cs-accent) 35%, transparent);
	display: flex;
	align-items: center;
	justify-content: center;
}

.diamond {
	width: 16px;
	height: 16px;
	transform: rotate(45deg);
	border: 1px solid color-mix(in srgb, var(--cs-accent) 60%, transparent);
	position: relative;
}

.diamond-inner {
	position: absolute;
	inset: 4px;
	background: color-mix(in srgb, var(--cs-accent) 60%, transparent);
}

.title {
	font-size: 18px;
	color: var(--text-primary);
	font-weight: 400;
}

.subtitle {
	margin-top: 4px;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-muted);
	letter-spacing: 1px;
	text-transform: uppercase;
}
</style>
