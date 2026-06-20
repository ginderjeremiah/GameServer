<!-- The Skills tab: a reference skill table on the left and the selected skill's dossier on the right.
     A read-only catalogue — no equip/loadout and none of the Skills screen's player-build DPS math
     (that stays on the Skills screen); this is master/detail like the Enemies/Zones tabs. -->
<div class="skills">
	<div class="table-col">
		<SkillTable {view} />
	</div>

	<SkillDossier {view} />
</div>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import SkillTable from './SkillTable.svelte';
import SkillDossier from './SkillDossier.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.skills {
	flex: 1;
	min-height: 0;
	display: flex;
}

.table-col {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	padding: 14px 0 14px 30px;
}
</style>
