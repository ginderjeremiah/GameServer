import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import Loading from '$components/Loading.svelte';

afterEach(cleanup);

describe('Loading', () => {
	it('shows the spinner when loading=true', () => {
		const { container } = render(Loading, { props: { loading: true } });
		expect(container.querySelector('.loading-spinner-container')).toBeTruthy();
	});

	it('hides the spinner when loading=false', () => {
		const { container } = render(Loading, { props: { loading: false } });
		expect(container.querySelector('.loading-spinner-container')).toBeNull();
	});

	it('shows the overlay by default', () => {
		const { container } = render(Loading, { props: { loading: true } });
		expect(container.querySelector('.overlay')).toBeTruthy();
	});

	it('hides the overlay when hideOverlay=true', () => {
		const { container } = render(Loading, { props: { loading: true, hideOverlay: true } });
		expect(container.querySelector('.overlay')).toBeNull();
	});

	it('announces a loading status with a visually-hidden label', () => {
		const { container } = render(Loading, { props: { loading: true } });
		const status = container.querySelector('.loading-spinner-container') as HTMLElement;
		expect(status.getAttribute('role')).toBe('status');
		expect(status.getAttribute('aria-live')).toBe('polite');
		expect(container.querySelector('.sr-only')?.textContent).toContain('Loading');
		// The decorative spinner itself is hidden from assistive tech.
		expect(container.querySelector('.loading-spinner')?.getAttribute('aria-hidden')).toBe('true');
	});

	it('suppresses the spinner immediately when a delay is set', () => {
		// With delay > 0, waitingOnDelay stays true after onMount, so the spinner is hidden
		// until the timeout fires even though loading=true.
		const { container } = render(Loading, { props: { loading: true, delay: 500 } });
		expect(container.querySelector('.loading-spinner-container')).toBeNull();
	});

	it('clears the reveal-delay timer on unmount so it never fires after destroy', () => {
		const clearSpy = vi.spyOn(globalThis, 'clearTimeout');
		const { unmount } = render(Loading, { props: { loading: true, delay: 500 } });
		unmount();
		expect(clearSpy).toHaveBeenCalled();
		clearSpy.mockRestore();
	});

	it('does not arm a timer when no delay is set', () => {
		const setSpy = vi.spyOn(globalThis, 'setTimeout');
		const { unmount } = render(Loading, { props: { loading: true } });
		expect(setSpy).not.toHaveBeenCalled();
		unmount();
		setSpy.mockRestore();
	});
});
