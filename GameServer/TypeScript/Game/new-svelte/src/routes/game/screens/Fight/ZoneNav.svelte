<div class="zone-nav round-border">
	<button
		class="hover-glow"
		disabled={leftDisabled}
		id="zone-button-left"
		onclick={handleClickLeft}
	>
		&lt;
	</button>
	<div>
		<span id="zone-num">Zone {zoneNum}:</span>
		<span id="zone-title">{current?.name}</span>
	</div>
	<button
		class="hover-glow"
		disabled={rightDisabled}
		id="zone-button-right"
		onclick={handleClickRight}
	>
		&gt;
	</button>
</div>

<script lang="ts">
import { staticData, player } from '$stores';

const orderedZones = $derived(staticData.zones.slice().sort((a, b) => a.order - b.order));
const current = $derived(
	orderedZones.find((z) => z.id === player.data.currentZone) ?? orderedZones[0]
);
const zoneNum = $derived(orderedZones.indexOf(current) + 1);
const leftDisabled = $derived(zoneNum === 1);
const rightDisabled = $derived(zoneNum === orderedZones.length);

const changeZone = (amount: number) => {
	const newZone = zoneNum + amount;
	if (newZone >= 1 && newZone <= orderedZones.length) {
		const zoneData = orderedZones[newZone - 1];
		player.data.currentZone = zoneData.id;
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
	background-color: var(--container-background-color);
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
