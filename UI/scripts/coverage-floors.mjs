// Ratchet helper: aggregates the latest Vitest coverage-summary.json by source area
// and prints the integer floors (Math.floor of the exact %) to paste into coverage.config.ts's
// `coverageThresholds`. Run after `npm run test:unit:coverage`:
//   node scripts/coverage-floors.mjs
// Re-run when coverage rises to ratchet the floors upward.
import { readFileSync } from 'node:fs';
import { coverageThresholds, projectCoverageExclude } from '../coverage.config.ts';
import { globToRegExp } from './coverage-glob.ts';

const summaryPath = new URL('../coverage/coverage-summary.json', import.meta.url);
let summary;
try {
	summary = JSON.parse(readFileSync(summaryPath, 'utf8'));
} catch {
	console.error('No coverage/coverage-summary.json found. Run `npm run test:unit:coverage` first.');
	process.exit(1);
}

// Areas come straight from coverage.config.ts's coverageThresholds — the same source
// src/tests/coverage-config.test.ts gates against — so a new area added there is picked up here
// automatically instead of needing a hand-maintained duplicate list.
const AREAS = Object.keys(coverageThresholds);
const areaMatchers = AREAS.map((glob) => ({ glob, regExp: globToRegExp(glob) }));
const excludeMatchers = projectCoverageExclude.map(globToRegExp);
const METRICS = ['lines', 'functions', 'branches', 'statements'];

const acc = Object.fromEntries(AREAS.map((k) => [k, blank()]));
function blank() {
	return Object.fromEntries(METRICS.map((m) => [m, [0, 0]]));
}

// Strip everything before the `src/` root so an absolute (or Windows-backslashed) path from the
// summary matches the same repo-relative form the area/exclude globs are anchored against. Anchors
// on `/src/` (leading slash) rather than `src/` so a parent directory ending in `src` (e.g.
// `.../mysrc/UI/src/lib/...`) doesn't match its own trailing `src/` prematurely.
function toRelative(file) {
	const norm = file.split('\\').join('/');
	const idx = norm.indexOf('/src/');
	return idx === -1 ? norm : norm.slice(idx + 1);
}

const unmatched = [];
for (const [file, m] of Object.entries(summary)) {
	if (file === 'total') {
		continue;
	}
	const rel = toRelative(file);
	if (excludeMatchers.some((regExp) => regExp.test(rel))) {
		continue;
	}
	const hit = areaMatchers.find(({ regExp }) => regExp.test(rel));
	if (!hit) {
		unmatched.push(rel);
		continue;
	}
	for (const metric of METRICS) {
		acc[hit.glob][metric][0] += m[metric].covered;
		acc[hit.glob][metric][1] += m[metric].total;
	}
}

if (unmatched.length > 0) {
	console.error(
		'The following covered files match no coverage area and are not excluded — add an area to ' +
			'coverageThresholds or an exclusion to projectCoverageExclude in coverage.config.ts:\n' +
			unmatched.map((f) => `  ${f}`).join('\n')
	);
	process.exit(1);
}

const pct = ([c, t]) => (t === 0 ? 0 : (100 * c) / t);
const fmt = (v) => v.toFixed(1).padStart(5);

console.log('\nCurrent coverage by area (covered/total):\n');
console.log('area'.padEnd(22), METRICS.map((m) => m.padEnd(7)).join(''));
for (const k of AREAS) {
	const row = METRICS.map((m) => fmt(pct(acc[k][m])) + '  ').join('');
	console.log(k.padEnd(22), row);
}

console.log('\nPaste-ready ratchet floors for vite.config.ts (coverage.thresholds):\n');
for (const k of AREAS) {
	const parts = METRICS.map((m) => `${m}: ${Math.floor(pct(acc[k][m]))}`).join(', ');
	console.log(`\t\t\t\t'${k}': { ${parts} },`);
}
console.log('');
