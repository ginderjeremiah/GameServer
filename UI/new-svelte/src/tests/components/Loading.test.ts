import { describe, it, expect, afterEach } from 'vitest';
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

	it('suppresses the spinner immediately when a delay is set', () => {
		// With delay > 0, waitingOnDelay stays true after onMount, so the spinner is hidden
		// until the timeout fires even though loading=true.
		const { container } = render(Loading, { props: { loading: true, delay: 500 } });
		expect(container.querySelector('.loading-spinner-container')).toBeNull();
	});
});
