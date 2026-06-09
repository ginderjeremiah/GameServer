<div class="legend">
	{#each ITEMS as item (item.cls)}
		<span class="it"><span class="sw {item.cls}"></span> {item.label}</span>
	{/each}
</div>

<script lang="ts">
const ITEMS = [
	{ cls: 'eh', label: 'enemy strike' },
	{ cls: 'eb', label: 'enemy guard' },
	{ cls: 'ech', label: 'boss channel' },
	{ cls: 'at', label: 'your strike' },
	{ cls: 'ch', label: 'channel (skill)' },
	{ cls: 'bl', label: 'your block' },
	{ cls: 'cr', label: 'crit tick ×2' }
] as const;
</script>

<style lang="scss">
.legend {
	display: flex;
	gap: 20px;
	flex-wrap: wrap;
	justify-content: center;
	margin-top: 22px;
	font-family: var(--mono);
	font-size: 10.5px;
	color: var(--text-tertiary);
}

.it {
	display: flex;
	align-items: center;
	gap: 7px;
}

.sw {
	width: 18px;
	height: 11px;
	border-radius: 3px;
	border: 1px solid;
}

.sw.eh {
	border-color: var(--enemy-accent);
	background: color-mix(in srgb, var(--enemy-accent) 22%, transparent);
}
.sw.eb {
	border-color: var(--log-enemy);
	border-style: dashed;
	background: repeating-linear-gradient(
		45deg,
		color-mix(in srgb, var(--enemy-accent) 18%, transparent) 0 4px,
		transparent 4px 8px
	);
}
.sw.at {
	border-color: color-mix(in srgb, var(--log-enemy) 60%, transparent);
	background: color-mix(in srgb, var(--log-enemy) 14%, transparent);
}
.sw.ch {
	border-color: var(--rarity-epic);
	background: color-mix(in srgb, var(--rarity-epic) 20%, transparent);
}
.sw.bl {
	border-color: var(--health-remaining-color);
	border-style: dashed;
	background: color-mix(in srgb, var(--health-remaining-color) 18%, transparent);
}
.sw.cr {
	border-color: var(--gold);
	background: radial-gradient(circle, color-mix(in srgb, var(--gold) 50%, transparent), transparent);
}
.sw.ech {
	border-color: var(--enemy-accent);
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--enemy-accent) 42%, var(--surface)),
		color-mix(in srgb, var(--enemy-accent) 24%, var(--surface))
	);
	box-shadow: 0 0 6px color-mix(in srgb, var(--enemy-accent) 50%, transparent);
}
</style>
