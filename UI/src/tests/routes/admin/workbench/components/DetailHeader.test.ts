import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';

vi.mock('$stores', () => ({ toastError: vi.fn() }));

import DetailHeader from '$routes/admin/workbench/components/DetailHeader.svelte';
import type { DetailHeaderTab } from '$routes/admin/workbench/components/DetailHeader.svelte';

const baseProps = {
	idLabel: '#1',
	isNew: false,
	name: 'Fire',
	blank: 'Unnamed',
	status: 'clean' as const,
	retired: false,
	activeTab: 'identity',
	onTab: vi.fn(),
	onRetire: vi.fn(),
	onReinstate: vi.fn(),
	onRemove: vi.fn()
};

afterEach(cleanup);

describe('DetailHeader', () => {
	it('pairs a dirty tab dot with visually-hidden "Unsaved changes" text, mirroring the Workbench tab dot', () => {
		const tabs: DetailHeaderTab[] = [{ key: 'identity', label: 'Identity', dirty: true }];
		const { container } = render(DetailHeader, { props: { ...baseProps, tabs } });
		expect(container.querySelector('.tab-dot')).toBeTruthy();
		expect(screen.getByText('Unsaved changes')).toBeTruthy();
	});

	it('renders no dot or hidden text for a clean tab', () => {
		const tabs: DetailHeaderTab[] = [{ key: 'identity', label: 'Identity', dirty: false }];
		const { container } = render(DetailHeader, { props: { ...baseProps, tabs } });
		expect(container.querySelector('.tab-dot')).toBeNull();
		expect(screen.queryByText('Unsaved changes')).toBeNull();
	});
});
