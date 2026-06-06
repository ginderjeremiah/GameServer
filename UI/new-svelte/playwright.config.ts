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
		baseURL: 'http://localhost:4173'
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
