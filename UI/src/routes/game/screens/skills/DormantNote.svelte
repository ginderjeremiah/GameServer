<!-- Off-weapon dormancy note shared by the equipped + innate bands: a weapon-leaf skill the equipped
     weapon doesn't field (#1342), labelled with the weapon type it needs. -->
<span class="dormant-note" title="Off-weapon — dormant until you equip a {required} weapon">
	⊘ dormant · needs {required}
</span>

<script lang="ts">
import type { ISkill } from '$lib/api';
import { primaryDamageType } from '$lib/battle';
import { damageTypeName } from '$lib/common';

type Props = {
	skill: ISkill;
};

const { skill }: Props = $props();

/** The weapon type the dormant skill needs equipped to field — its primary leaf type's name. */
const required = $derived(damageTypeName(primaryDamageType(skill.damagePortions)));
</script>

<style lang="scss">
.dormant-note {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	// Neutral muted tone (matching the innate dupe-note); --warning is reserved for validation.
	color: var(--text-tertiary);
}
</style>
