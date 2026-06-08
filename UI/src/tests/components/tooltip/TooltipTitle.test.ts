import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';

afterEach(cleanup);

describe('TooltipTitle', () => {
	const baseProps = {
		label: 'Prefix',
		name: 'Flaming',
		diamondColor: 'var(--mod-prefix)',
		labelColor: 'var(--mod-prefix)'
	};

	it('renders the label and name, colouring the diamond and label by accent', () => {
		const { container } = render(TooltipTitle, { props: baseProps });
		const label = container.querySelector('.tt-category-label') as HTMLElement;
		expect(label.textContent).toBe('Prefix');
		expect(label.getAttribute('style')).toContain('var(--mod-prefix)');
		expect((container.querySelector('.tt-category-diamond') as HTMLElement).getAttribute('style')).toContain(
			'var(--mod-prefix)'
		);
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('Flaming');
	});

	it('does not mask the name by default', () => {
		const { container } = render(TooltipTitle, { props: baseProps });
		expect((container.querySelector('.tt-title-name') as HTMLElement).classList.contains('masked')).toBe(false);
	});

	it('marks the name masked when the masked prop is set', () => {
		const { container } = render(TooltipTitle, { props: { ...baseProps, name: '?????????', masked: true } });
		const name = container.querySelector('.tt-title-name') as HTMLElement;
		expect(name.classList.contains('masked')).toBe(true);
		expect(name.textContent).toBe('?????????');
	});
});
