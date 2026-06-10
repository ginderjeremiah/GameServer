// Ratchet helper: aggregates the latest Vitest coverage-summary.json by source area
// and prints the integer floors (Math.floor of the exact %) to paste into vite.config.ts's
// `coverage.thresholds`. Run after `npm run test:unit:coverage`:
//   node scripts/coverage-floors.mjs
// Re-run when coverage rises to ratchet the floors upward.
import { readFileSync } from 'node:fs';

const summaryPath = new URL('../coverage/coverage-summary.json', import.meta.url);
let summary;
try {
	summary = JSON.parse(readFileSync(summaryPath, 'utf8'));
} catch {
	console.error('No coverage/coverage-summary.json found. Run `npm run test:unit:coverage` first.');
	process.exit(1);
}

// Area -> glob key used in vite.config.ts thresholds, and the predicate that buckets a file into it.
// Order matters only for display; the predicates are mutually exclusive by construction.
const AREAS = [
	['src/lib/battle/**', (p) => p.includes('/src/lib/battle/')],
	['src/lib/engine/**', (p) => p.includes('/src/lib/engine/')],
	['src/lib/common/**', (p) => p.includes('/src/lib/common/')],
	['src/lib/api/**', (p) => p.includes('/src/lib/api/') && !p.includes('/src/lib/api/types/')],
	['src/lib/card-game/**', (p) => p.includes('/src/lib/card-game/')],
	['src/stores/**', (p) => p.includes('/src/stores/')],
	['src/components/**', (p) => p.includes('/src/components/')],
	['src/routes/**', (p) => p.includes('/src/routes/')]
];
const METRICS = ['lines', 'functions', 'branches', 'statements'];

const acc = Object.fromEntries(AREAS.map(([k]) => [k, blank()]));
function blank() {
	return Object.fromEntries(METRICS.map((m) => [m, [0, 0]]));
}

for (const [file, m] of Object.entries(summary)) {
	if (file === 'total') {
		continue;
	}
	const norm = file.split('\\').join('/');
	const hit = AREAS.find(([, match]) => match(norm));
	if (!hit) {
		continue;
	}
	for (const metric of METRICS) {
		acc[hit[0]][metric][0] += m[metric].covered;
		acc[hit[0]][metric][1] += m[metric].total;
	}
}

const pct = ([c, t]) => (t === 0 ? 0 : (100 * c) / t);
const fmt = (v) => v.toFixed(1).padStart(5);

console.log('\nCurrent coverage by area (covered/total):\n');
console.log('area'.padEnd(22), METRICS.map((m) => m.padEnd(7)).join(''));
for (const [k] of AREAS) {
	const row = METRICS.map((m) => fmt(pct(acc[k][m])) + '  ').join('');
	console.log(k.padEnd(22), row);
}

console.log('\nPaste-ready ratchet floors for vite.config.ts (coverage.thresholds):\n');
for (const [k] of AREAS) {
	const parts = METRICS.map((m) => `${m}: ${Math.floor(pct(acc[k][m]))}`).join(', ');
	console.log(`\t\t\t\t'${k}': { ${parts} },`);
}
console.log('');
