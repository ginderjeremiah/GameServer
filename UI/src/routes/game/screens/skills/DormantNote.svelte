<!-- Off-weapon dormancy note shared by the equipped + innate bands: a weapon-leaf skill the equipped
     weapon doesn't field (#1342), labelled with the weapon type it needs. -->
<SharedDormantNote
	text="dormant · needs {required}"
	title="Off-weapon — dormant until you equip a {required} weapon"
	letterSpacing={0.8}
/>

<script lang="ts">
import type { ISkill } from '$lib/api';
import { primaryDamageType } from '$lib/battle';
import { damageTypeName } from '$lib/common';
import SharedDormantNote from '$components/DormantNote.svelte';

type Props = {
	skill: ISkill;
};

const { skill }: Props = $props();

/** The weapon type the dormant skill needs equipped to field — its primary leaf type's name. */
const required = $derived(damageTypeName(primaryDamageType(skill.damagePortions)));
</script>
