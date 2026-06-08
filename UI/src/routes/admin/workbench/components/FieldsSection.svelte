<div class="fields-row">
	{#each section.fields as field (field.key)}
		<FieldControl {field} {record} {baseline} {store} />
	{/each}
</div>

<script lang="ts">
import type { EntityStore } from '../entity-store.svelte';
import type { FieldsSectionConfig, Identified } from '../entities/types';
import FieldControl from './FieldControl.svelte';

interface Props {
	section: FieldsSectionConfig<Identified>;
	record: Identified;
	baseline: Identified | undefined;
	store: EntityStore<Identified>;
}

const { section, record, baseline, store }: Props = $props();
</script>

<style lang="scss">
.fields-row {
	display: flex;
	gap: 18px;
	flex-wrap: wrap;
	align-items: flex-end;
}
</style>
