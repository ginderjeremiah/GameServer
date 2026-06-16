import { describe, expect, it } from 'vitest';
import { coverageThresholds, projectCoverageExclude } from '../../coverage.config';

// Enumerate every instrumented source file. Vite resolves this glob at transform time against the
// package root, so the test sees the real tree. Declaration files carry no executable logic, so v8
// never reports them — drop them here too.
const sourceFiles = Object.keys(import.meta.glob('/src/**/*.{ts,js,svelte}'))
	.map((path) => path.replace(/^\//, ''))
	.filter((path) => !path.endsWith('.d.ts'))
	.sort();

// Minimal glob matcher for the vocabulary the coverage config uses: `**` (any number of path
// segments, including none), `*` (anything within a single segment), and literal characters. This
// mirrors how Vitest itself buckets files without pulling in an untyped runtime glob dependency.
function globToRegExp(glob: string): RegExp {
	let source = '';
	for (let i = 0; i < glob.length; i++) {
		const char = glob[i];
		if (char === '*' && glob[i + 1] === '*') {
			i++;
			if (glob[i + 1] === '/') {
				i++;
				source += '(?:.*/)?';
			} else {
				source += '.*';
			}
		} else if (char === '*') {
			source += '[^/]*';
		} else if ('.+^${}()|[]\\'.includes(char)) {
			source += `\\${char}`;
		} else {
			source += char;
		}
	}
	return new RegExp(`^${source}$`);
}

const areaMatchers = Object.keys(coverageThresholds).map((glob) => ({ glob, regExp: globToRegExp(glob) }));
const excludeMatchers = projectCoverageExclude.map(globToRegExp);

const isExcluded = (path: string) => excludeMatchers.some((regExp) => regExp.test(path));
const matchingAreas = (path: string) => areaMatchers.filter(({ regExp }) => regExp.test(path)).map(({ glob }) => glob);

// Guards the invariant the gate relies on: every source file is gated by exactly one area floor, or
// is an explicit, reviewable exclusion. Without this, a brand-new top-level folder (e.g. a future
// `src/lib/<x>/` or `src/services/`) would be measured but matched by no threshold — silently
// ungated. This is the frontend analogue of the backend gate failing on a missing gated assembly.
describe('frontend coverage gate config', () => {
	it('discovers source files to verify', () => {
		expect(sourceFiles.length).toBeGreaterThan(0);
	});

	it('gates every source file by an area threshold or an explicit exclusion', () => {
		const ungated = sourceFiles.filter((path) => !isExcluded(path) && matchingAreas(path).length === 0);
		expect(
			ungated,
			`These source files are measured but match no coverage threshold area and are not excluded. Add an ` +
				`area floor to coverageThresholds, or an explicit entry to projectCoverageExclude, in ` +
				`coverage.config.ts:\n${ungated.join('\n')}`
		).toEqual([]);
	});

	it('assigns each gated source file to exactly one area', () => {
		const ambiguous = sourceFiles
			.filter((path) => !isExcluded(path))
			.map((path) => ({ path, areas: matchingAreas(path) }))
			.filter(({ areas }) => areas.length > 1);
		expect(
			ambiguous,
			`These source files match more than one area threshold; area globs must be mutually exclusive so each ` +
				`file is gated by a single floor:\n${ambiguous.map(({ path, areas }) => `${path} -> ${areas.join(', ')}`).join('\n')}`
		).toEqual([]);
	});
});
