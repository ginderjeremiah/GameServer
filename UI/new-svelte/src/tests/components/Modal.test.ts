// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, fireEvent, cleanup } from '@testing-library/svelte';
import Modal from '$components/Modal.svelte';

afterEach(cleanup);

const baseProps = {
	title: 'Engage the zone boss?',
	body: 'The Catacomb Warden hits harder than the field.',
	confirmLabel: 'Engage',
	cancelLabel: 'Not yet',
	onConfirm: () => {},
	onCancel: () => {}
};

describe('Modal', () => {
	it('renders the title and body as the dialog label/description', () => {
		const { getByRole } = render(Modal, { props: baseProps });
		const dialog = getByRole('dialog');

		expect(dialog.getAttribute('aria-modal')).toBe('true');
		expect(dialog.getAttribute('aria-labelledby')).toBe('modal-title');
		expect(dialog.getAttribute('aria-describedby')).toBe('modal-body');
		expect(dialog.textContent).toContain('Engage the zone boss?');
		expect(dialog.textContent).toContain('The Catacomb Warden');
	});

	it('renders confirm + cancel buttons for a confirm modal', () => {
		const { getByText } = render(Modal, { props: { ...baseProps, kind: 'confirm' } });
		expect(getByText('Engage')).toBeTruthy();
		expect(getByText('Not yet')).toBeTruthy();
	});

	it('renders only the primary button for an acknowledge modal', () => {
		const { getByText, queryByText } = render(Modal, {
			props: { ...baseProps, kind: 'acknowledge', confirmLabel: 'Continue' }
		});
		expect(getByText('Continue')).toBeTruthy();
		expect(queryByText('Not yet')).toBeNull();
	});

	it('tones the primary action as danger for a destructive modal', () => {
		const { getByText } = render(Modal, {
			props: { ...baseProps, kind: 'destructive', confirmLabel: 'Reset' }
		});
		expect(getByText('Reset').classList.contains('danger')).toBe(true);
	});

	it('fires onConfirm when the primary button is clicked', async () => {
		const onConfirm = vi.fn();
		const { getByText } = render(Modal, { props: { ...baseProps, onConfirm } });
		await fireEvent.click(getByText('Engage'));
		expect(onConfirm).toHaveBeenCalledTimes(1);
	});

	it('fires onCancel when the cancel button is clicked', async () => {
		const onCancel = vi.fn();
		const { getByText } = render(Modal, { props: { ...baseProps, onCancel } });
		await fireEvent.click(getByText('Not yet'));
		expect(onCancel).toHaveBeenCalledTimes(1);
	});
});
