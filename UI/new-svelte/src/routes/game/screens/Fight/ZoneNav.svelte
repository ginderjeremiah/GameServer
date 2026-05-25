<div class="zone-nav">
	<button class="zone-btn" disabled={leftDisabled} onclick={handleClickLeft}>&#8249;</button>
	<div class="zone-info">
		<span class="zone-num">Zone · {String(zoneNum).padStart(2, '0')}</span>
		<span class="zone-name">{current?.name}</span>
	</div>
	<button class="zone-btn" disabled={rightDisabled} onclick={handleClickRight}>&#8250;</button>
</div>

<script lang="ts">
import { staticData } from '$stores';
import { playerManager } from '$lib/engine';

const orderedZones = $derived(staticData.zones?.slice().sort((a, b) => a.order - b.order));
const current = $derived(
	orderedZones?.find((z) => z.id === playerManager.currentZone) ?? orderedZones?.[0]
);
const zoneNum = $derived((orderedZones?.indexOf(current) ?? -1) + 1);
const leftDisabled = $derived(zoneNum === 1);
const rightDisabled = $derived(zoneNum === orderedZones?.length);

const changeZone = (amount: number) => {
	const newZone = zoneNum + amount;
	if (newZone >= 1 && newZone <= orderedZones.length) {
		const zoneData = orderedZones[newZone - 1];
		playerManager.currentZone = zoneData.id;
		return true;
	}
	return false;
};

const handleClickLeft = () => changeZone(-1);
const handleClickRight = () => changeZone(1);
</script>

<style lang="scss">
.zone-nav {
	display: inline-flex;
	align-items: center;
	gap: 12px;
	background: rgba(255, 255, 255, 0.04);
	border: 1px solid rgba(255, 255, 255, 0.14);
	border-radius: 3px;
	padding: 6px 10px 6px 6px;
}

.zone-btn {
	width: 24px;
	height: 24px;
	background: rgba(255, 255, 255, 0.03);
	border: 1px solid rgba(255, 255, 255, 0.18);
	color: rgba(240, 240, 240, 0.85);
	border-radius: 2px;
	cursor: pointer;
	display: flex;
	align-items: center;
	justify-content: center;
	font-family: 'Geist Mono', monospace;
	font-size: 12px;
	transition: background 140ms;

	&:hover:not(:disabled) {
		background: rgba(255, 255, 255, 0.08);
	}

	&:disabled {
		opacity: 0.4;
		cursor: not-allowed;
	}
}

.zone-info {
	display: flex;
	align-items: baseline;
	gap: 10px;
	min-width: 200px;
	justify-content: center;
}

.zone-num {
	font-family: 'Geist Mono', monospace;
	font-size: 10.5px;
	letter-spacing: 1.5px;
	text-transform: uppercase;
	color: rgba(192, 216, 255, 0.85);
}

.zone-name {
	font-size: 16px;
	color: #f0f0f0;
	font-weight: 400;
	letter-spacing: 0.1px;
}
</style>
