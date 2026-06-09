// ─────────────────────────────────────────────────────────────────────────
//  Mock data + working store for the enemy admin redesign prototype.
//  Mirrors the real game types: IEnemy, IAttributeDistribution, ISkill, IZone.
// ─────────────────────────────────────────────────────────────────────────

const ATTRIBUTES = [
	{ id: 0, name: 'Strength', short: 'STR' },
	{ id: 1, name: 'Endurance', short: 'END' },
	{ id: 2, name: 'Intellect', short: 'INT' },
	{ id: 3, name: 'Agility', short: 'AGI' },
	{ id: 4, name: 'Dexterity', short: 'DEX' },
	{ id: 5, name: 'Luck', short: 'LCK' },
	{ id: 6, name: 'Max Health', short: 'HP' },
	{ id: 7, name: 'Defense', short: 'DEF' },
	{ id: 8, name: 'Cooldown Recovery', short: 'CDR' },
	{ id: 10, name: 'Critical Chance', short: 'CRIT' },
	{ id: 11, name: 'Critical Damage', short: 'CDMG' },
	{ id: 12, name: 'Dodge Chance', short: 'DODGE' },
	{ id: 13, name: 'Block Chance', short: 'BLK' },
	{ id: 14, name: 'Block Reduction', short: 'BLKR' }
];

const SKILLS = [
	{ id: 1, name: 'Rusted Cleave', dmg: 12, cd: 2400 },
	{ id: 2, name: 'Venom Spit', dmg: 8, cd: 1800 },
	{ id: 3, name: 'Ember Lash', dmg: 15, cd: 3000 },
	{ id: 4, name: 'Frost Nova', dmg: 18, cd: 4200 },
	{ id: 5, name: 'Shadow Grasp', dmg: 10, cd: 2600 },
	{ id: 6, name: 'Bone Shatter', dmg: 22, cd: 3600 },
	{ id: 7, name: 'Howling Screech', dmg: 6, cd: 1500 },
	{ id: 8, name: 'Tail Sweep', dmg: 14, cd: 2200 },
	{ id: 9, name: 'Corrosive Bile', dmg: 9, cd: 2000 },
	{ id: 10, name: 'Stone Fist', dmg: 20, cd: 3400 },
	{ id: 11, name: 'Soul Drain', dmg: 16, cd: 3800 },
	{ id: 12, name: 'Gale Slash', dmg: 13, cd: 2100 },
	{ id: 13, name: 'Crushing Blow', dmg: 28, cd: 4800 },
	{ id: 14, name: 'Pestilent Cloud', dmg: 11, cd: 2800 }
];

const ZONES = [
	{ id: 1, name: 'Mistwood Fen', levelMin: 1, levelMax: 8 },
	{ id: 2, name: 'Ashfall Barrens', levelMin: 6, levelMax: 15 },
	{ id: 3, name: 'Sunken Hollows', levelMin: 12, levelMax: 22 },
	{ id: 4, name: 'Frostpeak Ascent', levelMin: 20, levelMax: 32 },
	{ id: 5, name: 'The Gravecourt', levelMin: 30, levelMax: 45 }
];

const A = (attributeId, base, perLevel) => ({ attributeId, base, perLevel });

// Hand-authored "feature" enemies — varied, a few intentionally incomplete so
// the validation warnings have something to flag.
const FEATURED = [
	{
		id: 1, name: 'Bog Lurker', isBoss: false,
		attrs: [A(0, 8, 1.4), A(1, 12, 2.1), A(6, 60, 9), A(7, 4, 0.8)],
		skills: [1, 9], spawns: [{ zoneId: 1, weight: 8 }, { zoneId: 3, weight: 3 }]
	},
	{
		id: 2, name: 'Cinder Imp', isBoss: false,
		attrs: [A(2, 10, 1.8), A(3, 9, 1.5), A(6, 38, 6)],
		skills: [3, 7], spawns: [{ zoneId: 2, weight: 10 }]
	},
	{
		id: 3, name: 'Rimewing Harpy', isBoss: false,
		attrs: [A(3, 14, 2.2), A(4, 11, 1.7), A(12, 6, 0.4), A(6, 44, 7)],
		skills: [4, 12], spawns: [{ zoneId: 4, weight: 6 }]
	},
	{
		id: 4, name: 'Stonebound Golem', isBoss: false,
		attrs: [A(0, 16, 2.6), A(1, 20, 3.2), A(7, 12, 1.6), A(6, 120, 14), A(14, 8, 0.6)],
		skills: [10, 13], spawns: [{ zoneId: 3, weight: 4 }, { zoneId: 5, weight: 2 }]
	},
	{
		id: 5, name: 'Gravebound Acolyte', isBoss: false,
		attrs: [A(2, 18, 2.8), A(5, 7, 0.9), A(6, 50, 8)],
		skills: [5, 11, 14], spawns: [{ zoneId: 5, weight: 7 }]
	},
	{
		// incomplete: no skills
		id: 6, name: 'Sporeback Brute', isBoss: false,
		attrs: [A(0, 13, 2.0), A(1, 15, 2.4), A(6, 80, 11)],
		skills: [], spawns: [{ zoneId: 3, weight: 5 }]
	},
	{
		// incomplete: not assigned to any zone
		id: 7, name: 'Dustkin Scavenger', isBoss: false,
		attrs: [A(3, 10, 1.6), A(4, 8, 1.2), A(5, 9, 1.1)],
		skills: [2, 8], spawns: []
	},
	{
		// boss, rich
		id: 8, name: 'Emberlord Vorrgath', isBoss: true,
		attrs: [A(0, 30, 4.2), A(1, 28, 3.8), A(2, 22, 3.0), A(6, 320, 28), A(7, 18, 2.2), A(11, 40, 2.5), A(10, 12, 0.8)],
		skills: [3, 6, 13, 11], spawns: [{ zoneId: 2, weight: 1 }, { zoneId: 5, weight: 1 }]
	},
	{
		// boss, incomplete: no attributes yet
		id: 9, name: 'The Drowned King', isBoss: true,
		attrs: [],
		skills: [4, 5], spawns: [{ zoneId: 3, weight: 1 }]
	},
	{
		id: 10, name: 'Thornback Boar', isBoss: false,
		attrs: [A(0, 11, 1.7), A(1, 13, 2.0), A(6, 70, 10)],
		skills: [1, 8], spawns: [{ zoneId: 1, weight: 9 }, { zoneId: 2, weight: 4 }]
	},
	{
		id: 11, name: 'Venomspine Adder', isBoss: false,
		attrs: [A(3, 12, 1.9), A(4, 14, 2.1), A(10, 8, 0.5)],
		skills: [2, 9], spawns: [{ zoneId: 1, weight: 6 }, { zoneId: 3, weight: 5 }]
	},
	{
		id: 12, name: 'Sablemaw', isBoss: true,
		attrs: [A(0, 26, 3.6), A(3, 20, 2.8), A(6, 260, 24), A(11, 35, 2.2)],
		skills: [6, 13, 12], spawns: [{ zoneId: 4, weight: 1 }]
	}
];

// ── Generate filler so the list reflects a real 100–200-row catalogue. ──
const PREFIX = ['Gloom', 'Ashen', 'Frost', 'Cinder', 'Bog', 'Marsh', 'Stone', 'Thorn', 'Venom', 'Wraith',
	'Dust', 'Grave', 'Spore', 'Rime', 'Sable', 'Hollow', 'Murk', 'Brack', 'Gore', 'Sallow', 'Pale', 'Coal'];
const NOUN = ['fang', 'spawn', 'crawler', 'warden', 'brute', 'lurker', 'revenant', 'harpy', 'golem', 'adder',
	'acolyte', 'scavenger', 'wretch', 'sentinel', 'maw', 'stalker', 'husk', 'broodling', 'ravager', 'knell'];

function generateEnemies(startId, count) {
	let seed = 90210;
	const rnd = () => { seed = (seed * 1103515245 + 12345) & 0x7fffffff; return seed / 0x7fffffff; };
	const ri = (a, b) => a + Math.floor(rnd() * (b - a + 1));
	const pick = (arr) => arr[Math.floor(rnd() * arr.length)];
	const used = new Set(FEATURED.map((e) => e.name));
	const out = [];
	for (let i = 0; i < count; i++) {
		let name = pick(PREFIX) + pick(NOUN);
		name = name[0].toUpperCase() + name.slice(1);
		if (used.has(name)) name = name + ' ' + ['I', 'II', 'III', 'IV'][ri(0, 3)];
		used.add(name);
		const nAttr = ri(0, 4);
		const attrIds = [...ATTRIBUTES].sort(() => rnd() - 0.5).slice(0, nAttr);
		const attrs = attrIds.map((at) => A(at.id, ri(4, 24), +(rnd() * 3 + 0.4).toFixed(1)));
		const nSkill = ri(0, 3);
		const skills = [...SKILLS].sort(() => rnd() - 0.5).slice(0, nSkill).map((s) => s.id);
		const nSpawn = ri(0, 2);
		const spawns = [...ZONES].sort(() => rnd() - 0.5).slice(0, nSpawn).map((z) => ({ zoneId: z.id, weight: ri(1, 10) }));
		out.push({ id: startId + i, name, isBoss: rnd() < 0.05, attrs, skills, spawns });
	}
	return out;
}

const ENEMIES = [...FEATURED, ...generateEnemies(13, 130)];

// ── Lookups ──
const attrById = (id) => ATTRIBUTES.find((a) => a.id === id);
const skillById = (id) => SKILLS.find((s) => s.id === id);
const zoneById = (id) => ZONES.find((z) => z.id === id);

// ── Validation: the inline warnings every direction surfaces. ──
function enemyWarnings(e) {
	const w = [];
	if (!e.name || !e.name.trim()) w.push('Missing name');
	if (!e.attrs || e.attrs.length === 0) w.push('No attribute distribution');
	if (!e.skills || e.skills.length === 0) w.push('No skills assigned');
	if (!e.spawns || e.spawns.length === 0) w.push('Not assigned to any zone');
	return w;
}
// The four facets used for the completeness meter.
function completeness(e) {
	return {
		identity: !!(e.name && e.name.trim()),
		attrs: e.attrs && e.attrs.length > 0,
		skills: e.skills && e.skills.length > 0,
		spawns: e.spawns && e.spawns.length > 0
	};
}
function completeCount(e) {
	const c = completeness(e);
	return [c.identity, c.attrs, c.skills, c.spawns].filter(Boolean).length;
}

const clone = (o) => JSON.parse(JSON.stringify(o));

// ── Working store: one per direction, so edits in one don't bleed into another.
//    Light dirty tracking (a counter) + a save() that flashes "saved". ──
function useEnemyStore() {
	const [enemies, setEnemies] = React.useState(() => clone(ENEMIES));
	const [dirty, setDirty] = React.useState(0);
	const [saved, setSaved] = React.useState(false);

	const touch = () => { setDirty((d) => d + 1); setSaved(false); };

	// Generic mutator: clone the target enemy, let the caller mutate it.
	const patchEnemy = (id, fn) => {
		setEnemies((es) => es.map((e) => {
			if (e.id !== id) return e;
			const c = clone(e);
			fn(c);
			return c;
		}));
		touch();
	};

	const addEnemy = (name) => {
		const id = Math.max(0, ...enemies.map((e) => e.id)) + 1;
		const ne = { id, name: name || '', isBoss: false, attrs: [], skills: [], spawns: [] };
		setEnemies((es) => [ne, ...es]);
		touch();
		return id;
	};

	const save = () => {
		setDirty(0);
		setSaved(true);
		setTimeout(() => setSaved(false), 1800);
	};
	const discard = () => { setEnemies(clone(ENEMIES)); setDirty(0); setSaved(false); };

	return { enemies, setEnemies, dirty, saved, touch, patchEnemy, addEnemy, save, discard };
}

Object.assign(window, {
	ATTRIBUTES, SKILLS, ZONES, ENEMIES,
	attrById, skillById, zoneById,
	enemyWarnings, completeness, completeCount, clone,
	useEnemyStore
});
