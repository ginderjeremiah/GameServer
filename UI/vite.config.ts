import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig, coverageConfigDefaults } from 'vitest/config';
import { coverageThresholds, projectCoverageExclude } from './coverage.config';

export default defineConfig({
	plugins: [sveltekit()],
	test: {
		include: ['src/**/*.{test,spec}.{js,ts}'],
		environment: 'jsdom',
		setupFiles: ['./src/tests/setup.ts'],
		execArgv: ['--no-experimental-webstorage'],
		coverage: {
			provider: 'v8',
			reporter: ['text', 'json-summary', 'html'],
			// Exclusions and per-area ratchet floors live in coverage.config.ts (shared with the
			// coverage-config test). `coverageConfigDefaults.exclude` is spread because Vitest *replaces*
			// (not extends) the default exclude list when this key is set.
			exclude: [...coverageConfigDefaults.exclude, ...projectCoverageExclude],
			thresholds: coverageThresholds
		}
	},
	resolve: {
		conditions: ['browser']
	},
	server: {
		proxy: {
			'/api': 'http://localhost:5008',
			'/socket': {
				target: 'ws://localhost:5008',
				ws: true
			}
		}
	}
});
