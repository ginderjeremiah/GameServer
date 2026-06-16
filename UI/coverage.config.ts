// Single source of truth for the frontend coverage gate's per-area floors and project-specific
// exclusions. Shared by vite.config.ts (which enforces the floors) and the coverage-config test
// (which asserts every source file is gated by exactly one area). See docs/infrastructure.md.

// No-logic code is excluded outright so it neither inflates nor drags the gated numbers. These are
// layered on top of `coverageConfigDefaults.exclude` in vite.config.ts because Vitest *replaces*
// (not extends) the default exclude list when the key is set; dropping the defaults would start
// counting config and test-scaffolding files. Add explicit pure-display opt-outs here as they
// arise — an exclusion should be a visible, reviewable line, never a silent gap.
export const projectCoverageExclude = [
	'src/lib/api/types/**', // backend-generated TS client
	'**/index.ts', // barrel re-exports
	'**/*.scss',
	'src/app.html',
	'src/tests/**', // unit-test setup + fixtures
	'scripts/**', // dev tooling (e.g. coverage-floors.mjs)
	'coverage.config.ts' // this file — pure config, instrumented only because the gate test imports it
];

// Per-area ratchet floors. Each glob's files are checked against its own floor (and excluded from
// any global threshold), so every source file is gated by exactly one area — an invariant enforced
// by src/tests/coverage-config.test.ts so a new top-level folder can never slip in ungated. Floors
// are seeded at the current measured level and only ever raised — a regression below the floor
// fails the run. Logic layers carry high floors because we rely on them; the UI route/component
// layers are gated at-current to block backsliding without demanding tests for pure-display markup.
// Re-seed with `node scripts/coverage-floors.mjs` to ratchet up.
export const coverageThresholds = {
	'src/lib/battle/**': { lines: 99, functions: 100, branches: 90, statements: 99 },
	'src/lib/engine/**': { lines: 97, functions: 96, branches: 91, statements: 97 },
	'src/lib/common/**': { lines: 97, functions: 93, branches: 85, statements: 97 },
	'src/lib/api/**': { lines: 96, functions: 98, branches: 91, statements: 96 },
	'src/lib/card-game/**': { lines: 94, functions: 100, branches: 81, statements: 94 },
	'src/stores/**': { lines: 98, functions: 100, branches: 90, statements: 98 },
	'src/components/**': { lines: 93, functions: 96, branches: 79, statements: 94 },
	'src/routes/**': { lines: 93, functions: 91, branches: 72, statements: 92 }
};
