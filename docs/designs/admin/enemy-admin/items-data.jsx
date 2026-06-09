// ─────────────────────────────────────────────────────────────────────────
//  Items, Item Mods, and the categorized Tag catalogue.
//  Tags (ITag: id, name, tagCategoryId) apply to BOTH items and item mods —
//  the two old "Set Tags" tools. There are hundreds of tags; any one record
//  uses only a couple dozen. Applied tags are embedded here (assuming the
//  combined items-with-tags endpoint), so selecting a record needs no refetch.
// ─────────────────────────────────────────────────────────────────────────

// ── enums (from the live EItemCategory / ERarity / EItemModType) ──
const ITEM_CATEGORIES = [
	{ id: 1, name: 'Helm' }, { id: 2, name: 'Chest' }, { id: 3, name: 'Leg' },
	{ id: 4, name: 'Boot' }, { id: 5, name: 'Weapon' }, { id: 6, name: 'Accessory' }
];
const RARITIES = [
	{ id: 1, name: 'Common' }, { id: 2, name: 'Uncommon' }, { id: 3, name: 'Rare' },
	{ id: 4, name: 'Epic' }, { id: 5, name: 'Legendary' }, { id: 6, name: 'Mythic' }
];
const MOD_TYPES = [{ id: 1, name: 'Component' }, { id: 2, name: 'Prefix' }, { id: 3, name: 'Suffix' }];

const itemCategoryOptions = () => ITEM_CATEGORIES.map((c) => ({ value: c.id, text: c.name }));
const rarityOptions = () => RARITIES.map((r) => ({ value: r.id, text: r.name }));
const modTypeOptions = () => MOD_TYPES.map((m) => ({ value: m.id, text: m.name }));
const rarityName = (id) => (RARITIES.find((r) => r.id === id) || {}).name;
const itemCategoryName = (id) => (ITEM_CATEGORIES.find((c) => c.id === id) || {}).name;
const RARITY_COLOR = { 1: '#9aa0a6', 2: '#7fc28b', 3: '#a1c2f7', 4: '#c0a8e6', 5: '#f0c078', 6: '#e08a78' };
const rarityColor = (id) => RARITY_COLOR[id] || '#9aa0a6';

// ── Tag categories (each gets a harmonious low-chroma hue for scanning) ──
const TAG_CATEGORIES = [
	{ id: 1, name: 'Element', hue: 255 },
	{ id: 2, name: 'Damage Type', hue: 30 },
	{ id: 3, name: 'Theme', hue: 305 },
	{ id: 4, name: 'Source', hue: 160 },
	{ id: 5, name: 'Mechanic', hue: 95 },
	{ id: 6, name: 'Biome', hue: 205 },
	{ id: 7, name: 'Set', hue: 335 },
	{ id: 8, name: 'Affix Tier', hue: 65 }
];
const tagColor = (catId) => {
	const c = TAG_CATEGORIES.find((x) => x.id === catId) || { hue: 255 };
	return {
		fg: `oklch(0.85 0.06 ${c.hue})`,
		bd: `oklch(0.85 0.06 ${c.hue} / 0.45)`,
		bg: `oklch(0.85 0.06 ${c.hue} / 0.12)`
	};
};

const TAG_BASE = {
	1: ['Fire', 'Frost', 'Shadow', 'Holy', 'Poison', 'Arcane', 'Lightning', 'Earth', 'Water', 'Wind', 'Blood', 'Void', 'Nature', 'Necrotic', 'Radiant'],
	2: ['Physical', 'Magical', 'True', 'Bleed', 'Burn', 'Pierce', 'Slash', 'Blunt', 'Crush', 'Caustic'],
	3: ['Undead', 'Demonic', 'Beast', 'Construct', 'Celestial', 'Fey', 'Abyssal', 'Draconic', 'Insectoid', 'Giant', 'Goblinoid', 'Aberrant', 'Spectral', 'Elemental'],
	4: ['Drop', 'Craft', 'Quest', 'Vendor', 'Boss', 'Event', 'Login', 'Achievement', 'Dungeon', 'Raid', 'Seasonal', 'Challenge'],
	5: ['Lifesteal', 'Thorns', 'Crit', 'Dodge', 'Block', 'Cooldown', 'Stun', 'Slow', 'Shield', 'Reflect', 'Execute', 'Cleave', 'Haste', 'Regen', 'Knockback', 'Root', 'Silence', 'Pull'],
	6: ['Mistwood Fen', 'Ashfall Barrens', 'Sunken Hollows', 'Frostpeak Ascent', 'The Gravecourt', 'Emberreach', 'Saltmarsh', 'Hollow Vale'],
	7: ['Ashbound', 'Tideworn', 'Gravecaller', 'Stormrend', 'Sunforged', 'Wyrmscale', 'Nightweave', 'Ironvow', 'Emberforged', 'Verdant Oath'],
	8: ['T1', 'T2', 'T3', 'T4', 'T5', 'Low Roll', 'Mid Roll', 'High Roll', 'Perfect']
};
// Inflate a few categories with quality variants so the catalogue reaches the
// realistic "several hundred" scale the tools need to handle.
const VARIANTS = { 1: ['Greater', 'Lesser', 'Major', 'Minor'], 5: ['Greater', 'Lesser'], 7: ['Heroic', 'Ancient'] };

function buildTags() {
	const out = [];
	let id = 1;
	for (const cat of TAG_CATEGORIES) {
		for (const base of TAG_BASE[cat.id]) {
			out.push({ id: id++, name: base, tagCategoryId: cat.id });
			(VARIANTS[cat.id] || []).forEach((v) => out.push({ id: id++, name: `${v} ${base}`, tagCategoryId: cat.id }));
		}
	}
	return out;
}
const TAGS = buildTags();
const tagById = (id) => TAGS.find((t) => t.id === id);
const tagsByCategory = (catId) => TAGS.filter((t) => t.tagCategoryId === catId);

// ── seeded RNG for deterministic catalogues ──
function makeRng(seed) {
	let s = seed;
	const rnd = () => { s = (s * 1103515245 + 12345) & 0x7fffffff; return s / 0x7fffffff; };
	return {
		rnd, ri: (a, b) => a + Math.floor(rnd() * (b - a + 1)),
		pick: (arr) => arr[Math.floor(rnd() * arr.length)],
		sample: (arr, n) => [...arr].sort(() => rnd() - 0.5).slice(0, n)
	};
}

// ── Items ──
const ITEM_PREFIX = ['Ashen', 'Tideworn', 'Stormrend', 'Sunforged', 'Wyrmscale', 'Nightweave', 'Ironvow', 'Gravecaller', 'Emberforged', 'Frostbitten', 'Verdant', 'Gilded', 'Cursed', 'Runed', 'Bloodforged', 'Hollow'];
const ITEM_NOUN = {
	1: ['Greathelm', 'Coif', 'Visor', 'Crown', 'Hood'],
	2: ['Cuirass', 'Hauberk', 'Robe', 'Breastplate', 'Vestments'],
	3: ['Greaves', 'Legguards', 'Trousers', 'Faulds'],
	4: ['Sabatons', 'Treads', 'Striders', 'Boots'],
	5: ['Blade', 'Axe', 'Maul', 'Bow', 'Stave', 'Dagger', 'Spear'],
	6: ['Band', 'Amulet', 'Sigil', 'Charm', 'Loop']
};
function buildItems(n) {
	const g = makeRng(4242);
	const used = new Set();
	const out = [];
	for (let i = 0; i < n; i++) {
		const cat = g.pick(ITEM_CATEGORIES).id;
		let name = `${g.pick(ITEM_PREFIX)} ${g.pick(ITEM_NOUN[cat])}`;
		if (used.has(name)) name += ' ' + g.ri(2, 9);
		used.add(name);
		const nAttr = g.ri(0, 4);
		const attributes = g.sample(window.ATTRIBUTES, nAttr).map((a) => ({ attributeId: a.id, amount: g.ri(2, 40) }));
		const nSlot = g.ri(0, 3);
		const modSlots = Array.from({ length: nSlot }, () => ({ itemModSlotTypeId: g.pick(MOD_TYPES).id }));
		const nTag = g.ri(0, 12);
		const tags = g.sample(TAGS, nTag).map((t) => t.id);
		const rarity = g.pick([1, 1, 2, 2, 3, 3, 4, 4, 5, 6]);
		out.push({
			id: i + 1, name, description: '',
			itemCategoryId: cat, rarityId: rarity,
			iconPath: i % 7 === 0 ? '' : `items/${name.toLowerCase().replace(/[^a-z]+/g, '_')}.png`,
			attributes, modSlots, tags
		});
	}
	return out;
}
const ITEM_RECORDS = buildItems(48);

// ── Item Mods ──
const MOD_DEFS = [
	{ name: 'Sharp', type: 2 }, { name: 'Heavy', type: 2 }, { name: 'Keen', type: 2 }, { name: 'Brutal', type: 2 }, { name: 'Tempered', type: 2 },
	{ name: 'of the Bear', type: 3 }, { name: 'of Embers', type: 3 }, { name: 'of the Viper', type: 3 }, { name: 'of Warding', type: 3 }, { name: 'of the Gale', type: 3 }, { name: 'of Ruin', type: 3 },
	{ name: 'Ruby Core', type: 1 }, { name: 'Iron Plating', type: 1 }, { name: 'Arcane Lattice', type: 1 }, { name: 'Frost Sliver', type: 1 }, { name: 'Venom Sac', type: 1 }
];
function buildMods() {
	const g = makeRng(909);
	return MOD_DEFS.map((m, i) => {
		const nAttr = g.ri(1, 3);
		const attributes = g.sample(window.ATTRIBUTES, nAttr).map((a) => ({ attributeId: a.id, amount: g.ri(1, 20) }));
		const nTag = g.ri(0, 7);
		return {
			id: i + 1, name: m.name, itemModTypeId: m.type, removable: g.rnd() < 0.7,
			description: i % 5 === 0 ? '' : `Grants bonuses when slotted into a compatible item.`,
			attributes, tags: g.sample(TAGS, nTag).map((t) => t.id)
		};
	});
}
const ITEMMOD_RECORDS = buildMods();

// Reverse lookup: which items / mods reference a given tag (for the Tags Usage tab).
function tagUsage(tagId) {
	const items = ITEM_RECORDS.filter((it) => it.tags.includes(tagId));
	const mods = ITEMMOD_RECORDS.filter((m) => m.tags.includes(tagId));
	return { items: items.length, mods: mods.length, itemList: items, modList: mods };
}

Object.assign(window, {
	ITEM_CATEGORIES, RARITIES, MOD_TYPES,
	itemCategoryOptions, rarityOptions, modTypeOptions, rarityName, itemCategoryName, rarityColor,
	TAG_CATEGORIES, TAGS, tagById, tagsByCategory, tagColor,
	ITEM_RECORDS, ITEMMOD_RECORDS, tagUsage
});
