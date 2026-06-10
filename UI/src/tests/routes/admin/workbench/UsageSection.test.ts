import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import type { ITag } from '$lib/api';

interface UsageRecord {
	id: number;
	name: string;
	rarityId?: number;
}

const { mockReference } = vi.hoisted(() => ({
	mockReference: {
		tagColor: vi.fn(() => ({ fg: '#aaa', bd: '#888', bg: '#222' })),
		rarityColor: vi.fn(() => 'var(--rarity-rare)'),
		tagUsage: vi.fn(() => ({ items: 0, mods: 0, itemList: [], modList: [] }) as ReturnType<typeof mockUsage>)
	}
}));
vi.mock('$routes/admin/workbench/reference.svelte', () => ({ reference: mockReference }));

import UsageSection from '$routes/admin/workbench/components/UsageSection.svelte';

const mockUsage = (items: UsageRecord[], mods: UsageRecord[]) => ({
	items: items.length,
	mods: mods.length,
	itemList: items,
	modList: mods
});

const tag: ITag = { id: 5, name: 'Fire', tagCategoryId: 10 } as ITag;

beforeEach(() => {
	mockReference.tagColor.mockClear();
	mockReference.rarityColor.mockClear();
});

afterEach(cleanup);

describe('UsageSection', () => {
	it('summarises total usage with correct pluralisation (singular)', () => {
		mockReference.tagUsage.mockReturnValue(mockUsage([{ id: 1, name: 'Helm', rarityId: 3 }], []));
		const { container } = render(UsageSection, { props: { record: tag } });
		const text = (container.querySelector('.usage-count') as HTMLElement).textContent ?? '';
		expect(text).toContain('Applied to');
		expect(text).toContain('1 record');
		expect(text).not.toContain('records');
	});

	it('pluralises the summary for multiple records', () => {
		mockReference.tagUsage.mockReturnValue(
			mockUsage(
				[
					{ id: 1, name: 'Helm', rarityId: 3 },
					{ id: 2, name: 'Blade', rarityId: 5 }
				],
				[{ id: 3, name: 'Sharp', rarityId: 4 }]
			)
		);
		const { container } = render(UsageSection, { props: { record: tag } });
		expect((container.querySelector('.usage-count') as HTMLElement).textContent).toContain('3 records');
	});

	it('lists the items and mods that use the tag', () => {
		mockReference.tagUsage.mockReturnValue(
			mockUsage([{ id: 1, name: 'Helm', rarityId: 3 }], [{ id: 3, name: 'Sharp', rarityId: 4 }])
		);
		render(UsageSection, { props: { record: tag } });
		expect(screen.getByText('Helm')).toBeTruthy();
		expect(screen.getByText('Sharp')).toBeTruthy();
	});

	it('shows empty-group messages when nothing uses the tag', () => {
		mockReference.tagUsage.mockReturnValue(mockUsage([], []));
		render(UsageSection, { props: { record: tag } });
		expect(screen.getByText('No items use this tag.')).toBeTruthy();
		expect(screen.getByText('No mods use this tag.')).toBeTruthy();
	});

	it('caps the rendered list at 40 and shows a +N overflow chip', () => {
		const many = Array.from({ length: 45 }, (_, i) => ({ id: i, name: `Item ${i}`, rarityId: 3 }));
		mockReference.tagUsage.mockReturnValue(mockUsage(many, []));
		render(UsageSection, { props: { record: tag } });
		expect(screen.getByText('+5')).toBeTruthy();
	});
});
