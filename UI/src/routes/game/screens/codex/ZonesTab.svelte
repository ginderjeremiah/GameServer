<!-- The Zones tab: a vertical progression rail of zones on the left, and the selected zone's dossier
     on the right. Mirrors the Enemies tab's master/detail layout (no toolbar — the rail is ordered
     progression, not a searchable table). -->
<div class="zones">
	<div class="rail-col">
		<ZoneRail {view} />
	</div>

	<ZoneDossier {view} />
</div>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import ZoneRail from './ZoneRail.svelte';
import ZoneDossier from './ZoneDossier.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.zones {
	flex: 1;
	min-height: 0;
	display: flex;
}

.rail-col {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	padding: 14px 0 14px 30px;
}
</style>
