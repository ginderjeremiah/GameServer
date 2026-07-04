import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { ELessonTriggerType, EMechanicEvent, type ILesson } from '$lib/api';

const { SCREENS } = vi.hoisted(() => ({
	SCREENS: [
		{ key: 'fight', label: 'Fight' },
		{ key: 'inventory', label: 'Inventory' }
	]
}));
vi.mock('$routes/game/screens/screen-defs', () => ({ GAME_SCREENS: SCREENS }));

import LessonTriggerSection from '$routes/admin/workbench/components/lesson/LessonTriggerSection.svelte';
import { EntityStore } from '$routes/admin/workbench/entity-store.svelte';
import type { EntityConfig, Identified } from '$routes/admin/workbench/entities/types';

const config = (): EntityConfig<Identified> =>
	({
		key: 'lessons',
		label: 'Lessons',
		singular: 'Lesson',
		glyph: 'pin',
		blankName: 'New lesson',
		newItem: (id: number) => ({ id }),
		meta: () => [],
		sections: [],
		refresh: async () => [],
		persist: async () => []
	}) as unknown as EntityConfig<Identified>;

const setup = (over: Partial<ILesson> = {}) => {
	const lesson: ILesson = {
		id: 1,
		key: 'first-crit',
		name: 'First Crit',
		triggerType: ELessonTriggerType.ScreenVisit,
		triggerScreenKey: 'fight',
		triggerMechanicEvent: undefined,
		hostScreenKey: 'fight',
		displayOrder: 0,
		steps: [],
		...over
	};
	const store = new EntityStore(config(), [lesson as unknown as Identified]);
	const record = store.items[0];
	return { store, record, baseline: store.baselineOf(1) };
};

afterEach(cleanup);
beforeEach(() => vi.clearAllMocks());

describe('LessonTriggerSection', () => {
	it('renders the host-screen and trigger-type selects with the current values', () => {
		const { store, record, baseline } = setup();
		const { container } = render(LessonTriggerSection, { props: { record, baseline, store } });
		const host = container.querySelector('[aria-label="Host Screen"]') as HTMLSelectElement;
		const type = container.querySelector('[aria-label="Trigger Type"]') as HTMLSelectElement;
		expect(host.value).toBe('fight');
		expect(host.querySelectorAll('option')).toHaveLength(2);
		expect(type.value).toBe(String(ELessonTriggerType.ScreenVisit));
	});

	it('shows the trigger-screen select for a screen-visit lesson', () => {
		const { store, record, baseline } = setup({ triggerType: ELessonTriggerType.ScreenVisit });
		const { container } = render(LessonTriggerSection, { props: { record, baseline, store } });
		expect(container.querySelector('[aria-label="Trigger Screen"]')).toBeTruthy();
		expect(container.querySelector('[aria-label="Trigger Event"]')).toBeNull();
	});

	it('shows the trigger-event select for a mechanic-event lesson', () => {
		const { store, record, baseline } = setup({
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerScreenKey: undefined,
			triggerMechanicEvent: EMechanicEvent.FirstCrit
		});
		const { container } = render(LessonTriggerSection, { props: { record, baseline, store } });
		expect(container.querySelector('[aria-label="Trigger Event"]')).toBeTruthy();
		expect(container.querySelector('[aria-label="Trigger Screen"]')).toBeNull();
	});

	it('clears the trigger event when flipping to screen-visit', async () => {
		const { store, record, baseline } = setup({
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerScreenKey: undefined,
			triggerMechanicEvent: EMechanicEvent.FirstCrit
		});
		const { container } = render(LessonTriggerSection, { props: { record, baseline, store } });
		const type = container.querySelector('[aria-label="Trigger Type"]') as HTMLSelectElement;
		await fireEvent.change(type, { target: { value: String(ELessonTriggerType.ScreenVisit) } });
		const updated = store.items[0] as unknown as ILesson;
		expect(updated.triggerType).toBe(ELessonTriggerType.ScreenVisit);
		expect(updated.triggerMechanicEvent).toBeUndefined();
	});

	it('clears the trigger screen when flipping to mechanic-event', async () => {
		const { store, record, baseline } = setup({
			triggerType: ELessonTriggerType.ScreenVisit,
			triggerScreenKey: 'fight'
		});
		const { container } = render(LessonTriggerSection, { props: { record, baseline, store } });
		const type = container.querySelector('[aria-label="Trigger Type"]') as HTMLSelectElement;
		await fireEvent.change(type, { target: { value: String(ELessonTriggerType.MechanicEvent) } });
		const updated = store.items[0] as unknown as ILesson;
		expect(updated.triggerType).toBe(ELessonTriggerType.MechanicEvent);
		expect(updated.triggerScreenKey).toBeUndefined();
	});

	it('changing the trigger-screen select patches the field', async () => {
		const { store, record, baseline } = setup({ triggerScreenKey: 'fight' });
		const { container } = render(LessonTriggerSection, { props: { record, baseline, store } });
		const select = container.querySelector('[aria-label="Trigger Screen"]') as HTMLSelectElement;
		await fireEvent.change(select, { target: { value: 'inventory' } });
		expect((store.items[0] as unknown as ILesson).triggerScreenKey).toBe('inventory');
	});

	it('changing the trigger-event select patches the field', async () => {
		const { store, record, baseline } = setup({
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerScreenKey: undefined,
			triggerMechanicEvent: EMechanicEvent.FirstCrit
		});
		const { container } = render(LessonTriggerSection, { props: { record, baseline, store } });
		const select = container.querySelector('[aria-label="Trigger Event"]') as HTMLSelectElement;
		await fireEvent.change(select, { target: { value: String(EMechanicEvent.FirstDodge) } });
		expect((store.items[0] as unknown as ILesson).triggerMechanicEvent).toBe(EMechanicEvent.FirstDodge);
	});

	it('changing the host screen patches the field', async () => {
		const { store, record, baseline } = setup({ hostScreenKey: 'fight' });
		const { container } = render(LessonTriggerSection, { props: { record, baseline, store } });
		const select = container.querySelector('[aria-label="Host Screen"]') as HTMLSelectElement;
		await fireEvent.change(select, { target: { value: 'inventory' } });
		expect((store.items[0] as unknown as ILesson).hostScreenKey).toBe('inventory');
	});

	it('flags the host-screen select dirty against a differing baseline', () => {
		const { store, baseline } = setup({ hostScreenKey: 'fight' });
		store.patch(1, (d) => ((d as unknown as ILesson).hostScreenKey = 'inventory'));
		const record = store.items[0];
		const { container } = render(LessonTriggerSection, { props: { record, baseline, store } });
		expect(container.querySelector('.host-fld .dirty-dot')).toBeTruthy();
	});
});
