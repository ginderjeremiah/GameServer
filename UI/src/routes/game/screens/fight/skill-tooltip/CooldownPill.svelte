<div class="tt-cooldown-pill">
	<div class="tt-cooldown-fill" style:width="{progress}%" class:ready></div>
	<span class="tt-cooldown-text" class:ready>
		{ready ? 'READY' : `${remainingFormatted}s`}
	</span>
</div>

<script lang="ts">
interface Props {
	/** Cooldown fill width, 0–100. */
	progress: number;
	/** Whether the skill is ready to fire. */
	ready: boolean;
	/** Remaining cooldown, pre-formatted (shown when not ready). */
	remainingFormatted: string;
}

const { progress, ready, remainingFormatted }: Props = $props();
</script>

<style lang="scss">
.tt-cooldown-pill {
	margin-left: auto;
	position: relative;
	width: 60px;
	height: 18px;
	background: color-mix(in srgb, var(--text-primary) 6%, transparent);
	border: 1px solid var(--border-light);
	border-radius: 2px;
	overflow: hidden;
}

.tt-cooldown-fill {
	position: absolute;
	inset: 0;
	background: linear-gradient(
		90deg,
		color-mix(in srgb, var(--accent) 50%, transparent),
		color-mix(in srgb, var(--accent) 18%, transparent)
	);

	&.ready {
		background: linear-gradient(
			90deg,
			color-mix(in srgb, var(--success) 55%, transparent),
			color-mix(in srgb, var(--success) 20%, transparent)
		);
	}
}

.tt-cooldown-text {
	position: absolute;
	inset: 0;
	display: flex;
	align-items: center;
	justify-content: center;
	font-family: var(--mono);
	font-size: 9px;
	color: var(--accent-light);
	letter-spacing: 0.6px;

	&.ready {
		color: var(--success);
	}
}
</style>
