import { devices, type PlaywrightTestConfig } from '@playwright/test';

const config: PlaywrightTestConfig = {
	webServer: {
		command: 'npm run dev -- --port 4173',
		port: 4173,
		reuseExistingServer: true
	},
	testDir: 'e2e-tests',
	testMatch: /(.+\.)?(test|spec)\.[jt]s/,
	// Each flow drives a real backend (signup → loading → game), which routinely runs 12–15s,
	// so the per-test budget needs headroom above that. Retries absorb the transient hiccups
	// inherent to a shared live stack (a dropped hydration click, an auth/token blip under load).
	timeout: 30000,
	retries: process.env.CI ? 2 : 1,
	fullyParallel: true,
	use: {
		baseURL: 'http://localhost:4173',
		// Capture a full Playwright trace (DOM snapshots, network, console, every action) of the
		// failed attempt whenever a test is retried. Traces land in the uploaded test-results
		// artifact, so an intermittent CI flake can be opened in the trace viewer and root-caused
		// instead of guessed at from log lines.
		trace: 'on-first-retry',
		// Emulate the OS "reduce motion" preference so the app's CSS transitions/animations collapse
		// to ~0ms (see the reduced-motion block in styles/common.scss). This removes a class of
		// flakiness where a click landed on a still-animating element — e.g. the hover-expand admin
		// sidebar shifting buttons mid-click — and was dropped, retrying until the 30s test timeout.
		contextOptions: {
			reducedMotion: 'reduce'
		}
	},
	workers: process.env.CI ? 4 : undefined,
	projects: [
		{
			name: 'chromium',
			use: { ...devices['Desktop Chrome'] }
		},
		{
			name: 'firefox',
			use: { ...devices['Desktop Firefox'] }
		},
		{
			name: 'webkit',
			use: { ...devices['Desktop Safari'] }
		}
	]
};

export default config;
