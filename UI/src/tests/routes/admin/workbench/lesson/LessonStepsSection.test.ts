import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import type { ILesson } from '$lib/api';

vi.mock('$stores', () => ({ toastError: vi.fn() }));

import LessonStepsSection from '$routes/admin/workbench/components/lesson/LessonStepsSection.svelte';
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

const setup = (steps: ILesson['steps']) => {
	const lesson: Partial<ILesson> = { id: 1, steps };
	const store = new EntityStore(config(), [lesson as unknown as Identified]);
	const record = store.items[0];
	return { store, record, baseline: store.baselineOf(1) };
};

afterEach(cleanup);

describe('LessonStepsSection', () => {
	it('renders the empty state with an add control when there are no steps', async () => {
		const { store, record, baseline } = setup([]);
		render(LessonStepsSection, { props: { record, baseline, store } });
		expect(screen.getByText('This lesson has no steps yet.')).toBeTruthy();
		await fireEvent.click(screen.getByText('Add step'));
		expect((store.items[0] as unknown as ILesson).steps).toEqual([{ text: '', anchorKey: undefined }]);
	});

	it('renders a row per step with its text and anchor key', () => {
		const { store, record, baseline } = setup([
			{ text: 'Land a crit.', anchorKey: 'skill-bar' },
			{ text: 'Check your stats.' }
		]);
		const { container } = render(LessonStepsSection, { props: { record, baseline, store } });
		const rows = container.querySelectorAll('.step-row');
		expect(rows).toHaveLength(2);
		expect((rows[0].querySelector('textarea') as HTMLTextAreaElement).value).toBe('Land a crit.');
		expect((rows[0].querySelector('input') as HTMLInputElement).value).toBe('skill-bar');
		expect((rows[1].querySelector('input') as HTMLInputElement).value).toBe('');
	});

	it('appends a blank step via Add step', async () => {
		const { store, record, baseline } = setup([{ text: 'first' }]);
		render(LessonStepsSection, { props: { record, baseline, store } });
		await fireEvent.click(screen.getByText('Add step'));
		expect((store.items[0] as unknown as ILesson).steps).toEqual([
			{ text: 'first' },
			{ text: '', anchorKey: undefined }
		]);
	});

	it('removes a step via its remove control', async () => {
		const { store, record, baseline } = setup([{ text: 'first' }, { text: 'second' }]);
		const { container } = render(LessonStepsSection, { props: { record, baseline, store } });
		await fireEvent.click(container.querySelectorAll('.row-x')[0] as HTMLElement);
		expect((store.items[0] as unknown as ILesson).steps).toEqual([{ text: 'second' }]);
	});

	it('edits step text and anchor key', async () => {
		const { store, record, baseline } = setup([{ text: 'first' }]);
		const { container } = render(LessonStepsSection, { props: { record, baseline, store } });
		await fireEvent.input(container.querySelector('textarea') as HTMLTextAreaElement, { target: { value: 'edited' } });
		await fireEvent.input(container.querySelector('input') as HTMLInputElement, { target: { value: 'skill-bar' } });
		expect((store.items[0] as unknown as ILesson).steps).toEqual([{ text: 'edited', anchorKey: 'skill-bar' }]);
	});

	it('clearing the anchor key input clears the field back to undefined', async () => {
		const { store, record, baseline } = setup([{ text: 'first', anchorKey: 'skill-bar' }]);
		const { container } = render(LessonStepsSection, { props: { record, baseline, store } });
		await fireEvent.input(container.querySelector('input') as HTMLInputElement, { target: { value: '' } });
		expect((store.items[0] as unknown as ILesson).steps).toEqual([{ text: 'first', anchorKey: undefined }]);
	});

	it('disables move-up on the first row and move-down on the last row', () => {
		const { store, record, baseline } = setup([{ text: 'a' }, { text: 'b' }, { text: 'c' }]);
		const { container } = render(LessonStepsSection, { props: { record, baseline, store } });
		const rows = container.querySelectorAll('.step-row');
		const buttonsOf = (row: Element) => row.querySelectorAll('.icon-btn');
		expect((buttonsOf(rows[0])[0] as HTMLButtonElement).disabled).toBe(true); // move up on first row
		expect((buttonsOf(rows[2])[1] as HTMLButtonElement).disabled).toBe(true); // move down on last row
		expect((buttonsOf(rows[1])[0] as HTMLButtonElement).disabled).toBe(false);
		expect((buttonsOf(rows[1])[1] as HTMLButtonElement).disabled).toBe(false);
	});

	it('moves a step down and a step up', async () => {
		const { store, record, baseline } = setup([{ text: 'a' }, { text: 'b' }, { text: 'c' }]);
		const { container } = render(LessonStepsSection, { props: { record, baseline, store } });
		const rows = container.querySelectorAll('.step-row');
		await fireEvent.click(rows[0].querySelectorAll('.icon-btn')[1] as HTMLElement); // move "a" down
		expect((store.items[0] as unknown as ILesson).steps.map((s) => s.text)).toEqual(['b', 'a', 'c']);
	});
});
