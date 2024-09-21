<div class="zone-nav round-border">
	<button
		class="hover-glow"
		disabled="{leftDisabled}"
		id="zone-button-left"
		on:click="{handleClickLeft}"
	>
		&lt;
	</button>
	<div>
		<span id="zone-num">Zone {zoneNum}:</span>
		<span id="zone-title">{current?.name}</span>
	</div>
	<button
		class="hover-glow"
		disabled="{rightDisabled}"
		id="zone-button-right"
		on:click="{handleClickRight}"
	>
		&gt;
	</button>
</div>

<script lang="ts">
import { zones, player } from '$stores';

$: orderedZones = $zones.slice().sort((a, b) => a.order - b.order);
$: current = orderedZones.find((z) => z.id === $player.currentZone) ?? orderedZones[0];
$: zoneNum = orderedZones.indexOf(current) + 1;
$: leftDisabled = zoneNum === 1;
$: rightDisabled = zoneNum === orderedZones.length;

const changeZone = (amount: number) => {
	const newZone = zoneNum + amount;
	if (newZone >= 1 && newZone <= orderedZones.length) {
		const zoneData = orderedZones[newZone - 1];
		$player.currentZone = zoneData.id;
		$player = $player;
		return true;
	}
	return false;
};

const handleClickLeft = () => changeZone(-1);
const handleClickRight = () => changeZone(1);
</script>

<style lang="scss">
.zone-nav {
	width: fit-content;
	min-width: 25%;
	margin: 0 auto;
	padding: 0.2em;
	border: var(--default-border);
	background-color: var(--default-title-color);
	display: flex;
	justify-content: space-between;
	margin-bottom: 1em;

	button {
		font-size: 1rem;
		border: none;
		cursor: pointer;

		&:disabled {
			cursor: not-allowed;
		}
	}
}
</style>
