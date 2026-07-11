import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import ReasonBadge from '$routes/admin/ops/ReasonBadge.svelte';
import { EDeadLetterReason } from '$lib/api';

afterEach(cleanup);

describe('ReasonBadge', () => {
	it('renders the replayable label with the ok tone', () => {
		render(ReasonBadge, { props: { reason: EDeadLetterReason.Replayable } });
		const badge = screen.getByTestId('reason-badge');
		expect(badge.textContent?.trim()).toBe('Replayable');
		expect(badge.classList.contains('ok')).toBe(true);
	});

	it('renders the malformed label with the poison tone and a hint title', () => {
		render(ReasonBadge, { props: { reason: EDeadLetterReason.Malformed } });
		const badge = screen.getByTestId('reason-badge');
		expect(badge.textContent?.trim()).toBe('Malformed');
		expect(badge.classList.contains('poison')).toBe(true);
		expect(badge.getAttribute('title')).toContain('re-fail');
	});

	it('renders the unknown-event-type label with the warn tone', () => {
		render(ReasonBadge, { props: { reason: EDeadLetterReason.UnknownEventType } });
		const badge = screen.getByTestId('reason-badge');
		expect(badge.textContent?.trim()).toBe('Unknown event type');
		expect(badge.classList.contains('warn')).toBe(true);
	});

	it('renders the not-replayable label with the warn tone', () => {
		render(ReasonBadge, { props: { reason: EDeadLetterReason.NotReplayable } });
		const badge = screen.getByTestId('reason-badge');
		expect(badge.textContent?.trim()).toBe('Not replayable');
		expect(badge.classList.contains('warn')).toBe(true);
	});
});
