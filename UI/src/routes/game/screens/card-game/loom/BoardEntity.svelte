<div
	class="ent {entity.type}"
	class:resolved={entity.resolved}
	class:cancelled={entity.cancelled}
	style:width="{w}px"
	style:transform="translateX({x}px)"
>
	{#if hasWindup}<div class="windup"></div>{/if}
	{#if entity.type === 'enemychannel'}<span class="ecl">{entity.label}</span>{:else}{entity.label}{/if}
	{#if hasTip}<span class="tip"></span>{/if}
</div>

<script lang="ts">
import type { Entity } from '$lib/card-game';

interface Props {
	entity: Entity;
	/** Pre-computed horizontal offset (px) within the board. */
	x: number;
	/** Pre-computed width (px). */
	w: number;
}

const { entity, x, w }: Props = $props();

const hasWindup = $derived(entity.type === 'attack' || entity.type === 'enemychannel');
const hasTip = $derived(
	entity.type === 'enemyhit' || entity.type === 'enemychannel' || entity.type === 'attack' || entity.type === 'channel'
);
</script>

<style lang="scss">
.ent {
	position: absolute;
	height: 44px;
	border-radius: 7px;
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 5px;
	font-family: var(--mono);
	font-weight: 500;
	font-size: 12px;
	will-change: transform;
	overflow: hidden;
	z-index: 3;
	border: 1px solid;
	color: var(--text-primary);
}

.ent .tip {
	position: absolute;
	right: -1px;
	top: -1px;
	bottom: -1px;
	width: 3px;
	z-index: 4;
}

.ent .windup {
	position: absolute;
	left: 0;
	top: 0;
	bottom: 0;
	right: 4px;
	background: repeating-linear-gradient(
		45deg,
		color-mix(in srgb, var(--white) 6%, transparent) 0 6px,
		transparent 6px 12px
	);
}

.ent.enemyhit {
	top: 26px;
	border-color: color-mix(in srgb, var(--enemy-accent) 90%, transparent);
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--enemy-accent) 52%, var(--surface)),
		color-mix(in srgb, var(--enemy-accent) 34%, var(--surface))
	);
	color: color-mix(in srgb, var(--enemy-accent) 12%, var(--white));
	box-shadow:
		0 2px 10px color-mix(in srgb, var(--black) 45%, transparent),
		0 0 14px -4px color-mix(in srgb, var(--enemy-accent) 70%, transparent);

	.tip {
		background: var(--enemy-accent);
		box-shadow: 0 0 8px var(--enemy-accent);
	}
}

.ent.enemychannel {
	top: 26px;
	border-color: var(--enemy-accent);
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--enemy-accent) 42%, var(--surface)),
		color-mix(in srgb, var(--enemy-accent) 24%, var(--surface))
	);
	color: color-mix(in srgb, var(--enemy-accent) 12%, var(--white));
	letter-spacing: 0.04em;
	box-shadow:
		0 0 22px -2px color-mix(in srgb, var(--enemy-accent) 85%, transparent),
		inset 0 0 18px color-mix(in srgb, var(--enemy-accent) 25%, transparent);

	.ecl {
		position: relative;
		z-index: 1;
		font-weight: 700;
	}
	.tip {
		background: var(--white);
		box-shadow:
			0 0 10px var(--white),
			0 0 18px var(--enemy-accent);
	}
}

.ent.attack {
	top: 118px;
	border-color: color-mix(in srgb, var(--log-player) 88%, transparent);
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--log-player) 46%, var(--surface)),
		color-mix(in srgb, var(--log-player) 30%, var(--surface))
	);
	color: color-mix(in srgb, var(--log-player) 12%, var(--white));
	box-shadow: 0 2px 10px color-mix(in srgb, var(--black) 45%, transparent);

	.tip {
		background: color-mix(in srgb, var(--log-player) 60%, var(--white));
		box-shadow: 0 0 8px color-mix(in srgb, var(--log-player) 70%, transparent);
	}
}

.ent.channel {
	top: 118px;
	border-color: color-mix(in srgb, var(--rarity-epic) 92%, transparent);
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--rarity-epic) 50%, var(--surface)),
		color-mix(in srgb, var(--rarity-epic) 32%, var(--surface))
	);
	color: color-mix(in srgb, var(--rarity-epic) 12%, var(--white));
	box-shadow:
		0 2px 10px color-mix(in srgb, var(--black) 45%, transparent),
		0 0 14px -4px color-mix(in srgb, var(--rarity-epic) 60%, transparent);

	.tip {
		background: var(--rarity-epic);
		box-shadow: 0 0 8px var(--rarity-epic);
	}
}

/* defensive cover spans (76px tall, top-aligned, translucent) */
.ent.block {
	top: 8px;
	height: 76px;
	z-index: 2;
	align-items: flex-start;
	padding-top: 3px;
	border-style: dashed;
	border-color: color-mix(in srgb, var(--health-remaining-color) 50%, transparent);
	background: color-mix(in srgb, var(--health-remaining-color) 12%, transparent);
	box-shadow: inset 0 0 16px color-mix(in srgb, var(--health-remaining-color) 12%, transparent);
	color: color-mix(in srgb, var(--health-remaining-color) 35%, var(--white));
	font-size: 10px;
	letter-spacing: 0.12em;
}

.ent.enemyblock {
	top: 100px;
	height: 76px;
	z-index: 2;
	align-items: flex-start;
	padding-top: 3px;
	border-style: dashed;
	border-color: color-mix(in srgb, var(--enemy-accent) 50%, transparent);
	color: var(--log-enemy);
	background: repeating-linear-gradient(
		45deg,
		color-mix(in srgb, var(--enemy-accent) 13%, transparent) 0 6px,
		color-mix(in srgb, var(--enemy-accent) 4%, transparent) 6px 12px
	);
	font-size: 10px;
	letter-spacing: 0.12em;
}

.ent.resolved {
	opacity: 0.2;
	filter: saturate(0.4);
}
.ent.cancelled {
	opacity: 0.3;
	text-decoration: line-through;
}
</style>
