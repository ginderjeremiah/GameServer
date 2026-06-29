// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import DamageTypeIcon from '$components/DamageTypeIcon.svelte';
import { EDamageTypeKey } from '$lib/api';

afterEach(cleanup);

describe('DamageTypeIcon', () => {
	it('renders an img pointing at the damage-type art under /img', () => {
		const { container } = render(DamageTypeIcon, { props: { dmgKey: EDamageTypeKey.Fire } });
		const img = container.querySelector('img.dmg-icon');
		expect(img).toBeTruthy();
		expect(img?.getAttribute('src')).toBe('/img/Fire.png');
	});

	it('maps the cross-cutting and weapon keys to their own art', () => {
		const dot = render(DamageTypeIcon, { props: { dmgKey: EDamageTypeKey.Dot } });
		expect(dot.container.querySelector('img')?.getAttribute('src')).toBe('/img/Damage Over Time.png');
		cleanup();
		const sword = render(DamageTypeIcon, { props: { dmgKey: EDamageTypeKey.Sword } });
		expect(sword.container.querySelector('img')?.getAttribute('src')).toBe('/img/Sword.png');
	});

	it('is decorative (empty alt) by default and accepts an override', () => {
		const dflt = render(DamageTypeIcon, { props: { dmgKey: EDamageTypeKey.Fire } });
		expect(dflt.container.querySelector('img')?.getAttribute('alt')).toBe('');
		cleanup();
		const labelled = render(DamageTypeIcon, { props: { dmgKey: EDamageTypeKey.Fire, alt: 'Fire' } });
		expect(labelled.container.querySelector('img')?.getAttribute('alt')).toBe('Fire');
	});

	it('applies the size prop to width and height (default 14px)', () => {
		const sized = render(DamageTypeIcon, { props: { dmgKey: EDamageTypeKey.Fire, size: 20 } });
		const img = sized.container.querySelector('img') as HTMLElement;
		expect(img.style.width).toBe('20px');
		expect(img.style.height).toBe('20px');
		cleanup();
		const dflt = render(DamageTypeIcon, { props: { dmgKey: EDamageTypeKey.Fire } });
		expect((dflt.container.querySelector('img') as HTMLElement).style.width).toBe('14px');
	});
});
