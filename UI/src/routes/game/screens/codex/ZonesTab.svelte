<!-- The Zones tab: a progression rail of every non-retired zone on the left, and the selected zone's
     dossier (level band, boss card, spawn table and unlock gate) on the right. -->
<div class="zones">
	<ZoneRail {view} />
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
</style>
