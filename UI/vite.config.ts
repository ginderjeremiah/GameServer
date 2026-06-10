import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig, coverageConfigDefaults } from 'vitest/config';

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
			// No-logic code is excluded outright so it neither inflates nor drags the gated numbers.
			// `coverageConfigDefaults.exclude` is spread because Vitest *replaces* (not extends) the
			// default exclude list when this key is set; dropping it would start counting config and
			// test-scaffolding files. Add explicit pure-display component opt-outs to this list as they
			// arise — an exclusion should be a visible, reviewable line, never a silent gap.
			exclude: [
				...coverageConfigDefaults.exclude,
				'src/lib/api/types/**', // backend-generated TS client
				'**/index.ts', // barrel re-exports
				'**/*.scss',
				'src/app.html',
				'src/tests/**', // unit-test setup + fixtures
				'scripts/**' // dev tooling (e.g. coverage-floors.mjs)
			],
			// Per-area ratchet floors. Each glob's files are checked against its own floor (and excluded
			// from any global threshold), so every source file is gated by exactly one area below.
			// Floors are seeded at the current measured level and only ever raised — a regression below
			// the floor fails the run. Logic layers carry high floors because we rely on them; the UI
			// route/component layers are gated at-current to block backsliding without demanding tests
			// for pure-display markup. Re-seed with `node scripts/coverage-floors.mjs` to ratchet up.
			thresholds: {
				'src/lib/battle/**': { lines: 99, functions: 100, branches: 88, statements: 99 },
				'src/lib/engine/**': { lines: 91, functions: 87, branches: 85, statements: 90 },
				'src/lib/common/**': { lines: 90, functions: 89, branches: 80, statements: 90 },
				'src/lib/api/**': { lines: 89, functions: 90, branches: 82, statements: 89 },
				'src/lib/card-game/**': { lines: 94, functions: 100, branches: 81, statements: 94 },
				'src/stores/**': { lines: 98, functions: 100, branches: 90, statements: 98 },
				'src/components/**': { lines: 89, functions: 95, branches: 74, statements: 92 },
				'src/routes/**': { lines: 73, functions: 68, branches: 52, statements: 70 }
			}
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
