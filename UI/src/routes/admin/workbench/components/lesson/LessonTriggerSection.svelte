<!--
	A lesson's trigger: the host screen it's authored for, plus either a screen-visit or
	mechanic-event trigger (mutually exclusive — flipping the type clears the other field so a
	lesson can never carry both/neither, mirroring the backend's `FindTriggerViolation` check).
	Screen keys are a frontend-only concept (no backend registry), so both screen pickers read
	options straight from {@link GAME_SCREENS} rather than the generic numeric `select` field type.
-->
<div class="lesson-trigger-grid">
	<div class="fld host-fld">
		<span class="lbl">Host Screen</span>
		<div class="sel-wrap">
			<select
				class="sel"
				class:dirty={hostDirty}
				aria-label="Host Screen"
				value={lesson.hostScreenKey}
				onchange={(e) => setHostScreenKey(e.currentTarget.value)}
			>
				{#each screenOptions as opt (opt.key)}
					<option value={opt.key}>{opt.label}</option>
				{/each}
			</select>
			<SelectCaret />
			{#if hostDirty}<DirtyDot />{/if}
		</div>
	</div>
	<div class="fld type-fld">
		<span class="lbl">Trigger Type</span>
		<div class="sel-wrap">
			<select
				class="sel"
				class:dirty={typeDirty}
				aria-label="Trigger Type"
				value={lesson.triggerType}
				onchange={(e) => setTriggerType(+e.currentTarget.value)}
			>
				<option value={ELessonTriggerType.ScreenVisit}>Screen Visit</option>
				<option value={ELessonTriggerType.MechanicEvent}>Mechanic Event</option>
			</select>
			<SelectCaret />
			{#if typeDirty}<DirtyDot />{/if}
		</div>
	</div>
	{#if lesson.triggerType === ELessonTriggerType.ScreenVisit}
		<div class="fld trigger-fld">
			<span class="lbl">Trigger Screen</span>
			<div class="sel-wrap">
				<select
					class="sel"
					class:dirty={triggerScreenDirty}
					class:invalid={!lesson.triggerScreenKey}
					aria-label="Trigger Screen"
					value={lesson.triggerScreenKey ?? ''}
					onchange={(e) => setTriggerScreenKey(e.currentTarget.value)}
				>
					<option value="" disabled>Select a screen…</option>
					{#each screenOptions as opt (opt.key)}
						<option value={opt.key}>{opt.label}</option>
					{/each}
				</select>
				<SelectCaret />
				{#if triggerScreenDirty}<DirtyDot />{/if}
			</div>
		</div>
	{:else}
		<div class="fld trigger-fld">
			<span class="lbl">Trigger Event</span>
			<div class="sel-wrap">
				<select
					class="sel"
					class:dirty={triggerEventDirty}
					class:invalid={lesson.triggerMechanicEvent === undefined}
					aria-label="Trigger Event"
					value={lesson.triggerMechanicEvent ?? ''}
					onchange={(e) => setTriggerMechanicEvent(+e.currentTarget.value)}
				>
					<option value="" disabled>Select an event…</option>
					{#each mechanicEventOptions as opt (opt.value)}
						<option value={opt.value}>{opt.text}</option>
					{/each}
				</select>
				<SelectCaret />
				{#if triggerEventDirty}<DirtyDot />{/if}
			</div>
		</div>
	{/if}
</div>

<script lang="ts">
import { ELessonTriggerType, EMechanicEvent, type ILesson } from '$lib/api';
import { GAME_SCREENS } from '../../../../game/screens/screen-defs';
import type { EntityStore } from '../../entity-store.svelte';
import { recordsEqual } from '../../entity-store.svelte';
import type { Identified } from '../../entities/types';
import DirtyDot from '../DirtyDot.svelte';
import SelectCaret from '../SelectCaret.svelte';

interface Props {
	record: Identified;
	baseline: Identified | undefined;
	store: EntityStore<Identified>;
}

const { record, baseline, store }: Props = $props();

const lesson = $derived(record as unknown as ILesson);
const base = $derived(baseline as unknown as ILesson | undefined);

const screenOptions = GAME_SCREENS.map((s) => ({ key: s.key, label: s.label }));
const mechanicEventOptions: { value: EMechanicEvent; text: string }[] = [
	{ value: EMechanicEvent.FirstCrit, text: 'First Crit' },
	{ value: EMechanicEvent.FirstDodge, text: 'First Dodge' },
	{ value: EMechanicEvent.FirstCooldownRecharge, text: 'First Cooldown Recharge' }
];

const hostDirty = $derived(base ? lesson.hostScreenKey !== base.hostScreenKey : false);
const typeDirty = $derived(base ? lesson.triggerType !== base.triggerType : false);
const triggerScreenDirty = $derived(base ? !recordsEqual(lesson.triggerScreenKey, base.triggerScreenKey) : false);
const triggerEventDirty = $derived(
	base ? !recordsEqual(lesson.triggerMechanicEvent, base.triggerMechanicEvent) : false
);

const setHostScreenKey = (key: string) =>
	store.patch(lesson.id, (d) => {
		(d as unknown as ILesson).hostScreenKey = key;
	});

const setTriggerType = (type: ELessonTriggerType) =>
	store.patch(lesson.id, (d) => {
		const l = d as unknown as ILesson;
		l.triggerType = type;
		// Clear whichever trigger field is now irrelevant so a lesson never ends up with both or
		// neither set — the invalid state the backend's FindTriggerViolation would otherwise reject.
		if (type === ELessonTriggerType.ScreenVisit) {
			l.triggerMechanicEvent = undefined;
		} else {
			l.triggerScreenKey = undefined;
		}
	});

const setTriggerScreenKey = (key: string) =>
	store.patch(lesson.id, (d) => {
		(d as unknown as ILesson).triggerScreenKey = key;
	});

const setTriggerMechanicEvent = (event: EMechanicEvent) =>
	store.patch(lesson.id, (d) => {
		(d as unknown as ILesson).triggerMechanicEvent = event;
	});
</script>

<style lang="scss">
.lesson-trigger-grid {
	display: flex;
	gap: 18px;
	flex-wrap: wrap;
	align-items: flex-end;
}
.host-fld {
	width: 220px;
}
.type-fld {
	width: 180px;
}
.trigger-fld {
	width: 220px;
}
.sel-wrap {
	position: relative;
}
</style>
