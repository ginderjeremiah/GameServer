import type { PlaywrightTestConfig } from '@playwright/test';

const config: PlaywrightTestConfig = {
	webServer: {
		command: 'npm run dev -- --port 4173',
		port: 4173,
		reuseExistingServer: true
	},
	testDir: 'integration-tests',
	testMatch: /(.+\.)?(test|spec)\.[jt]s/,
	timeout: 15000,
	use: {
		baseURL: 'http://localhost:4173'
	}
};

export default config;
