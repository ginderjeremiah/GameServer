// Shared by coverage-config.test.ts (which gates every source file against these areas) and
// coverage-floors.mjs (which re-derives the same areas to compute ratchet floors) so the two never
// drift apart on what counts as a match.

// Minimal glob matcher for the vocabulary the coverage config uses: `**` (any number of path
// segments, including none), `*` (anything within a single segment), and literal characters. This
// mirrors how Vitest itself buckets files without pulling in an untyped runtime glob dependency.
export function globToRegExp(glob: string): RegExp {
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
