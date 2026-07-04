{#if section.kind === 'fields'}
	<FieldsSection {section} {record} {baseline} {store} />
{:else if section.kind === 'table'}
	<SectionTable {section} {record} {baseline} {store} />
{:else if section.kind === 'chips'}
	<ChipsSection {section} {record} {baseline} {store} />
{:else if section.kind === 'tags'}
	<TagsSection {section} {record} {baseline} {store} />
{:else if section.kind === 'usage'}
	<UsageSection record={record as unknown as ITag} />
{:else if section.kind === 'challenge-condition'}
	<ChallengeConditionSection {record} {baseline} {store} />
{:else if section.kind === 'challenge-reward'}
	<ChallengeRewardSection {record} {baseline} {store} />
{:else if section.kind === 'lesson-trigger'}
	<LessonTriggerSection {record} {baseline} {store} />
{:else if section.kind === 'lesson-steps'}
	<LessonStepsSection {record} {baseline} {store} />
{/if}

<script lang="ts">
import type { ITag } from '$lib/api';
import type { EntityStore } from '../entity-store.svelte';
import type { Identified, SectionConfig } from '../entities/types';
import ChipsSection from './ChipsSection.svelte';
import FieldsSection from './FieldsSection.svelte';
import SectionTable from './SectionTable.svelte';
import TagsSection from './TagsSection.svelte';
import UsageSection from './UsageSection.svelte';
import ChallengeConditionSection from './challenge/ChallengeConditionSection.svelte';
import ChallengeRewardSection from './challenge/ChallengeRewardSection.svelte';
import LessonTriggerSection from './lesson/LessonTriggerSection.svelte';
import LessonStepsSection from './lesson/LessonStepsSection.svelte';

interface Props {
	section: SectionConfig<Identified>;
	record: Identified;
	baseline: Identified | undefined;
	store: EntityStore<Identified>;
}

const { section, record, baseline, store }: Props = $props();
</script>
