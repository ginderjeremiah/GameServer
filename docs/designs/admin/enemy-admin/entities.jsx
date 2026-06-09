// ─────────────────────────────────────────────────────────────────────────
//  Entity-driven config + diffing store for the interactive Workbench.
//  The SAME Workbench renders any entity described here — Enemies and Skills
//  are two configs; adding Zones/Items later is just another config object.
// ─────────────────────────────────────────────────────────────────────────

// ── Richer skill records (the live ISkill shape: + description, cooldown,
//    iconPath, and the damageMultipliers child collection). A few are left
//    intentionally incomplete so warnings have something to flag. ──
const SKILL_DESC = {
	1: 'A heavy horizontal swing with a corroded blade. Reliable, slow, brutal.',
	2: 'Launches a glob of venom that deals damage over the following seconds.',
	3: 'A whip of fire that scales hard with the caster’s Intellect.',
	4: 'Bursts outward in a ring of frost, briefly slowing nearby foes.',
	5: '', // missing description → warning
	6: 'Shatters bone and armor alike with a downward smash.',
	7: 'A piercing screech that rattles defenses.',
	8: 'A sweeping low strike that catches grounded targets.',
	9: 'Acidic bile that eats through Defense over time.',
	10: 'A granite-fisted blow built on raw Strength.',
	11: 'Drains the target’s vitality, healing the caster slightly.',
	12: 'A fast slashing gust favoring Agility and Dexterity.',
	13: 'A devastating overhead blow with a long wind-up.',
	14: 'Releases a lingering cloud of plague spores.'
};
const SKILL_MULT = {
	1: [{ attributeId: 0, multiplier: 1.4 }],
	2: [{ attributeId: 4, multiplier: 0.9 }, { attributeId: 2, multiplier: 0.5 }],
	3: [{ attributeId: 2, multiplier: 1.8 }],
	4: [{ attributeId: 2, multiplier: 1.2 }],
	5: [{ attributeId: 2, multiplier: 1.1 }],
	6: [{ attributeId: 0, multiplier: 1.6 }],
	7: [], // no multipliers → warning
	8: [{ attributeId: 0, multiplier: 0.8 }, { attributeId: 3, multiplier: 0.6 }],
	9: [{ attributeId: 4, multiplier: 1.0 }],
	10: [{ attributeId: 0, multiplier: 2.0 }],
	11: [{ attributeId: 2, multiplier: 1.3 }],
	12: [{ attributeId: 3, multiplier: 1.1 }, { attributeId: 4, multiplier: 0.7 }],
	13: [{ attributeId: 0, multiplier: 2.4 }],
	14: [] // no multipliers → warning
};
const SKILL_RECORDS = window.SKILLS.map((s) => ({
	id: s.id,
	name: s.name,
	baseDamage: s.dmg,
	cooldownMs: s.cd,
	iconPath: 'skills/' + s.name.toLowerCase().replace(/[^a-z]+/g, '_') + '.png',
	description: SKILL_DESC[s.id] || '',
	damageMultipliers: SKILL_MULT[s.id] || []
}));

// ── helpers ──
const firstFree = (taken, pool) => (pool.find((p) => !taken.includes(p.id)) || pool[0]).id;
const attrOptions = () => window.ATTRIBUTES.map((a) => ({ value: a.id, text: a.name }));
const zoneOptions = () => window.ZONES.map((z) => ({ value: z.id, text: `${z.name} · L${z.levelMin}–${z.levelMax}` }));

// ── Unified validation ─────────────────────────────────────────────────────
//  Warnings are declared where they live: a field is `required` (with a custom
//  reqMsg), or a non-field section carries a `warn(record)` predicate. Both the
//  record-level list dot and the per-tab / per-field triangles read from these,
//  so a warning can never appear on the record without also pointing at the
//  exact tab and field that caused it.
function fieldWarn(field, rec) {
	if (!field.required) return null;
	const v = rec[field.key];
	return (v === null || v === undefined || String(v).trim() === '') ? (field.reqMsg || `${field.label} required`) : null;
}
function sectionWarnings(section, rec) {
	if (section.kind === 'fields') return section.fields.map((f) => fieldWarn(f, rec)).filter(Boolean);
	if (section.warn) { const m = section.warn(rec); return m ? [m] : []; }
	return [];
}
function entityWarnings(entity, rec) {
	return entity.sections.flatMap((s) => sectionWarnings(s, rec));
}

// ════════════════════════════════════════════════════════════════════════
//  ENTITY CONFIGS
// ════════════════════════════════════════════════════════════════════════
const ENEMY_ENTITY = {
	key: 'enemies', label: 'Enemies', singular: 'Enemy', glyph: 'skull',
	seed: window.ENEMIES,
	blankName: 'Unnamed enemy',
	newItem: (id) => ({ id, name: '', isBoss: false, attrs: [], skills: [], spawns: [] }),
	listBadge: (e) => (e.isBoss ? 'boss' : null),
	badgeColor: () => 'var(--enemy)',
	meta: (e) => [['attr', e.attrs.length], ['skill', e.skills.length], ['zone', e.spawns.length]],
	warnings: window.enemyWarnings,
	sections: [
		{
			key: 'identity', label: 'Identity', glyph: 'tag', desc: 'Name & classification',
			complete: (e) => !!(e.name && e.name.trim()), detail: (e) => (e.name ? `“${e.name}”` : 'Name required'),
			kind: 'fields', fields: [
				{ key: 'name', label: 'Enemy Name', type: 'text', placeholder: 'Name this enemy…', grow: true, required: true, reqMsg: 'Missing name' },
				{ key: 'isBoss', label: 'Classification', type: 'toggle', onLabel: 'Boss enemy', offLabel: 'Standard enemy' }
			]
		},
		{
			key: 'attrs', label: 'Attributes', glyph: 'bars', desc: 'Stat distribution per level',
			count: (e) => e.attrs.length, warn: (e) => (e.attrs.length ? null : 'No attribute distribution'),
			kind: 'table', itemsKey: 'attrs', addLabel: 'Add attribute',
			emptyIcon: 'bars', emptyTitle: 'No attributes set', emptySub: 'This enemy has no stat distribution yet.',
			newRow: (e) => ({ attributeId: firstFree(e.attrs.map((a) => a.attributeId), window.ATTRIBUTES), base: 0, perLevel: 0 }),
			columns: [
				{ key: 'attributeId', label: 'Attribute', type: 'select', options: attrOptions, min: 190, unique: true },
				{ key: 'base', label: 'Base', type: 'number', align: 'r', width: 110 },
				{ key: 'perLevel', label: 'Per Level', type: 'number', align: 'r', width: 110 }
			]
		},
		{
			key: 'skills', label: 'Skills', glyph: 'rune', desc: 'Skill pool used in battle',
			count: (e) => e.skills.length, warn: (e) => (e.skills.length ? null : 'No skills assigned'),
			kind: 'chips', itemsKey: 'skills', catalogue: () => window.SKILLS,
			labelOf: (s) => s.name, metaOf: (s) => s.dmg + ' dmg',
			emptyIcon: 'rune', emptyTitle: 'No skills in pool', emptySub: "Enemies with no skills can't act in battle.", addLabel: 'Add skill from pool…'
		},
		{
			key: 'spawns', label: 'Spawns', glyph: 'pin', desc: 'Zones this enemy appears in',
			count: (e) => e.spawns.length, warn: (e) => (e.spawns.length ? null : 'Not assigned to any zone'),
			kind: 'table', itemsKey: 'spawns', addLabel: 'Assign zone',
			emptyIcon: 'pin', emptyTitle: 'Not assigned to any zone', emptySub: 'This enemy will never spawn in the world.',
			newRow: (e) => ({ zoneId: firstFree(e.spawns.map((z) => z.zoneId), window.ZONES), weight: 5 }),
			columns: [
				{ key: 'zoneId', label: 'Zone', type: 'select', options: zoneOptions, min: 200, unique: true },
				{ key: 'weight', label: 'Weight', type: 'number', align: 'r', width: 100 },
				{
					key: '__share', label: 'Spawn share', type: 'share', width: 150,
					// Share of this enemy's weight among ALL enemies that spawn in the
					// same zone (not among this enemy's own zone assignments).
					shareTotal: (row, rows, enemy) => {
						let sum = row.weight || 0;
						for (const e of window.ENEMIES) {
							if (e.id === enemy.id) continue;
							const sp = e.spawns.find((s) => s.zoneId === row.zoneId);
							if (sp) sum += sp.weight || 0;
						}
						return sum || 1;
					}
				}
			]
		}
	]
};

function skillWarnings(s) {
	const w = [];
	if (!s.name || !s.name.trim()) w.push('Missing name');
	if (!s.description || !s.description.trim()) w.push('No description');
	if (!s.damageMultipliers || s.damageMultipliers.length === 0) w.push('No damage multipliers');
	return w;
}

const SKILL_ENTITY = {
	key: 'skills', label: 'Skills', singular: 'Skill', glyph: 'bolt',
	seed: SKILL_RECORDS,
	blankName: 'Unnamed skill',
	newItem: (id) => ({ id, name: '', baseDamage: 10, cooldownMs: 2000, iconPath: '', description: '', damageMultipliers: [] }),
	listBadge: () => null,
	meta: (s) => [['dmg', s.baseDamage], ['×mult', s.damageMultipliers.length], ['cd', (s.cooldownMs / 1000).toFixed(1) + 's']],
	warnings: skillWarnings,
	sections: [
		{
			key: 'identity', label: 'Identity', glyph: 'tag', desc: 'Name, damage & cooldown',
			complete: (s) => !!(s.name && s.name.trim()), detail: (s) => (s.name ? `“${s.name}”` : 'Name required'),
			kind: 'fields', fields: [
				{ key: 'name', label: 'Skill Name', type: 'text', placeholder: 'Name this skill…', grow: true, required: true, reqMsg: 'Missing name' },
				{ key: 'baseDamage', label: 'Base Damage', type: 'number', suffix: 'dmg', width: 150 },
				{ key: 'cooldownMs', label: 'Cooldown', type: 'number', suffix: 'ms', width: 150 },
				{ key: 'iconPath', label: 'Icon Path', type: 'text', placeholder: 'skills/icon.png', grow: true },
				{ key: 'description', label: 'Description', type: 'textarea', placeholder: 'Describe what this skill does…', grow: true, required: true, reqMsg: 'No description' }
			]
		},
		{
			key: 'multipliers', label: 'Multipliers', glyph: 'multiply', desc: 'How attributes scale this skill',
			count: (s) => s.damageMultipliers.length, warn: (s) => (s.damageMultipliers.length ? null : 'No damage multipliers'),
			kind: 'table', itemsKey: 'damageMultipliers', addLabel: 'Add multiplier',
			emptyIcon: 'multiply', emptyTitle: 'No damage multipliers', emptySub: 'Damage won’t scale with any attribute.',
			newRow: (s) => ({ attributeId: firstFree(s.damageMultipliers.map((m) => m.attributeId), window.ATTRIBUTES), multiplier: 1.0 }),
			columns: [
				{ key: 'attributeId', label: 'Attribute', type: 'select', options: attrOptions, min: 200, unique: true },
				{ key: 'multiplier', label: 'Multiplier ×', type: 'number', align: 'r', width: 120 }
			]
		}
	]
};

function itemWarnings(it) {
	const w = [];
	if (!it.name || !it.name.trim()) w.push('Missing name');
	if (!it.iconPath || !it.iconPath.trim()) w.push('No icon path');
	if (!it.attributes || it.attributes.length === 0) w.push('No attributes');
	return w;
}

// Shared Tags section config — identical for Items and Item Mods (the two old
// SetItemTags / SetItemModTags tools), proving the tag UX generalizes.
const TAGS_SECTION = {
	key: 'tags', label: 'Tags', glyph: 'tag', desc: 'Categorized tags applied to this record',
	count: (r) => r.tags.length, detail: (r) => (r.tags.length ? `${r.tags.length} tag${r.tags.length > 1 ? 's' : ''}` : 'None'),
	kind: 'tags', itemsKey: 'tags'
};

const ITEM_ENTITY = {
	key: 'items', label: 'Items', singular: 'Item', glyph: 'box',
	seed: window.ITEM_RECORDS,
	blankName: 'Unnamed item',
	newItem: (id) => ({ id, name: '', description: '', itemCategoryId: 1, rarityId: 1, iconPath: '', attributes: [], modSlots: [], tags: [] }),
	listBadge: (it) => window.rarityName(it.rarityId),
	badgeColor: (it) => window.rarityColor(it.rarityId),
	meta: (it) => [['', window.itemCategoryName(it.itemCategoryId)], ['attr', it.attributes.length], ['tag', it.tags.length]],
	warnings: itemWarnings,
	sections: [
		{
			key: 'identity', label: 'Identity', glyph: 'tag', desc: 'Name, category & rarity',
			complete: (it) => !!(it.name && it.name.trim()), detail: (it) => (it.name ? `“${it.name}”` : 'Name required'),
			kind: 'fields', fields: [
				{ key: 'name', label: 'Item Name', type: 'text', placeholder: 'Name this item…', grow: true, required: true, reqMsg: 'Missing name' },
				{ key: 'itemCategoryId', label: 'Category', type: 'select', options: window.itemCategoryOptions, width: 170 },
				{ key: 'rarityId', label: 'Rarity', type: 'select', options: window.rarityOptions, width: 170 },
				{ key: 'iconPath', label: 'Icon Path', type: 'text', placeholder: 'items/icon.png', grow: true, required: true, reqMsg: 'No icon path' },
				{ key: 'description', label: 'Description', type: 'textarea', placeholder: 'Flavor text…', grow: true }
			]
		},
		{
			key: 'attributes', label: 'Attributes', glyph: 'bars', desc: 'Flat stat bonuses granted',
			count: (it) => it.attributes.length, warn: (it) => (it.attributes.length ? null : 'No attributes'),
			kind: 'table', itemsKey: 'attributes', addLabel: 'Add bonus',
			emptyIcon: 'bars', emptyTitle: 'No attribute bonuses', emptySub: 'This item grants no stats.',
			newRow: (it) => ({ attributeId: firstFree(it.attributes.map((a) => a.attributeId), window.ATTRIBUTES), amount: 1 }),
			columns: [
				{ key: 'attributeId', label: 'Attribute', type: 'select', options: attrOptions, min: 200, unique: true },
				{ key: 'amount', label: 'Amount', type: 'number', align: 'r', width: 120 }
			]
		},
		{
			key: 'modSlots', label: 'Mod Slots', glyph: 'box', desc: 'Slots that accept item mods',
			count: (it) => it.modSlots.length,
			kind: 'table', itemsKey: 'modSlots', addLabel: 'Add slot',
			emptyIcon: 'box', emptyTitle: 'No mod slots', emptySub: 'This item can’t hold mods.',
			newRow: () => ({ itemModSlotTypeId: 1 }),
			columns: [{ key: 'itemModSlotTypeId', label: 'Slot Type', type: 'select', options: window.modTypeOptions, min: 200 }]
		},
		TAGS_SECTION
	]
};

function modWarnings(m) {
	const w = [];
	if (!m.name || !m.name.trim()) w.push('Missing name');
	if (!m.description || !m.description.trim()) w.push('No description');
	if (!m.attributes || m.attributes.length === 0) w.push('No attributes');
	return w;
}

const ITEMMOD_ENTITY = {
	key: 'itemMods', label: 'Item Mods', singular: 'Item Mod', glyph: 'rune',
	seed: window.ITEMMOD_RECORDS,
	blankName: 'Unnamed mod',
	newItem: (id) => ({ id, name: '', itemModTypeId: 1, removable: true, description: '', attributes: [], tags: [] }),
	listBadge: (m) => (window.MOD_TYPES.find((t) => t.id === m.itemModTypeId) || {}).name,
	badgeColor: () => 'var(--accent)',
	meta: (m) => [['attr', m.attributes.length], ['tag', m.tags.length]],
	warnings: modWarnings,
	sections: [
		{
			key: 'identity', label: 'Identity', glyph: 'tag', desc: 'Name, type & description',
			complete: (m) => !!(m.name && m.name.trim()), detail: (m) => (m.name ? `“${m.name}”` : 'Name required'),
			kind: 'fields', fields: [
				{ key: 'name', label: 'Mod Name', type: 'text', placeholder: 'Name this mod…', grow: true, required: true, reqMsg: 'Missing name' },
				{ key: 'itemModTypeId', label: 'Type', type: 'select', options: window.modTypeOptions, width: 170 },
				{ key: 'removable', label: 'Removable', type: 'toggle', onLabel: 'Removable', offLabel: 'Permanent' },
				{ key: 'description', label: 'Description', type: 'textarea', placeholder: 'Describe this mod…', grow: true, required: true, reqMsg: 'No description' }
			]
		},
		{
			key: 'attributes', label: 'Attributes', glyph: 'bars', desc: 'Flat stat bonuses granted',
			count: (m) => m.attributes.length, warn: (m) => (m.attributes.length ? null : 'No attributes'),
			kind: 'table', itemsKey: 'attributes', addLabel: 'Add bonus',
			emptyIcon: 'bars', emptyTitle: 'No attribute bonuses', emptySub: 'This mod grants no stats.',
			newRow: (m) => ({ attributeId: firstFree(m.attributes.map((a) => a.attributeId), window.ATTRIBUTES), amount: 1 }),
			columns: [
				{ key: 'attributeId', label: 'Attribute', type: 'select', options: attrOptions, min: 200, unique: true },
				{ key: 'amount', label: 'Amount', type: 'number', align: 'r', width: 120 }
			]
		},
		TAGS_SECTION
	]
};

// ── Zones (IZone + the SetZoneEnemies child collection, folded in) ──
const enemyOptions = () => window.ENEMIES.map((e) => ({ value: e.id, text: e.name }));
const ZONE_RECORDS = window.ZONES.map((z, i) => ({
	id: z.id,
	name: z.name,
	description: i === 1 ? '' : `A ${z.levelMin < 10 ? 'starting' : z.levelMin < 25 ? 'mid-game' : 'late-game'} region for levels ${z.levelMin}–${z.levelMax}.`,
	order: (i + 1) * 10,
	levelMin: z.levelMin,
	levelMax: z.levelMax,
	zoneEnemies: window.ENEMIES
		.filter((e) => e.spawns.some((s) => s.zoneId === z.id))
		.map((e) => ({ enemyId: e.id, weight: e.spawns.find((s) => s.zoneId === z.id).weight }))
}));

const ZONE_ENTITY = {
	key: 'zones', label: 'Zones', singular: 'Zone', glyph: 'map',
	seed: ZONE_RECORDS,
	blankName: 'Unnamed zone',
	newItem: (id) => ({ id, name: '', description: '', order: 0, levelMin: 1, levelMax: 10, zoneEnemies: [] }),
	listBadge: () => null,
	meta: (z) => [['', `L${z.levelMin}–${z.levelMax}`], ['enemy', z.zoneEnemies.length]],
	sections: [
		{
			key: 'identity', label: 'Identity', glyph: 'tag', desc: 'Name, level range & ordering',
			kind: 'fields', fields: [
				{ key: 'name', label: 'Zone Name', type: 'text', placeholder: 'Name this zone…', grow: true, required: true, reqMsg: 'Missing name' },
				{ key: 'order', label: 'Order', type: 'number', width: 110 },
				{ key: 'levelMin', label: 'Level Min', type: 'number', suffix: 'lv', width: 130 },
				{ key: 'levelMax', label: 'Level Max', type: 'number', suffix: 'lv', width: 130 },
				{ key: 'description', label: 'Description', type: 'textarea', placeholder: 'Describe this zone…', grow: true, required: true, reqMsg: 'No description' }
			]
		},
		{
			key: 'zoneEnemies', label: 'Enemies', glyph: 'skull', desc: 'Enemies that spawn here & their weights',
			count: (z) => z.zoneEnemies.length, warn: (z) => (z.zoneEnemies.length ? null : 'No enemies spawn here'),
			kind: 'table', itemsKey: 'zoneEnemies', addLabel: 'Assign enemy',
			emptyIcon: 'skull', emptyTitle: 'No enemies assigned', emptySub: 'Nothing will spawn in this zone.',
			newRow: (z) => ({ enemyId: firstFree(z.zoneEnemies.map((e) => e.enemyId), window.ENEMIES), weight: 5 }),
			columns: [
				{ key: 'enemyId', label: 'Enemy', type: 'select', options: enemyOptions, min: 220, unique: true },
				{ key: 'weight', label: 'Weight', type: 'number', align: 'r', width: 100 },
				{ key: '__share', label: 'Spawn share', type: 'share', width: 150, weightKey: 'weight' }
			]
		}
	]
};

// ── Tags (AddEditTags) — a simple, high-volume taxonomy entity, plus a
//    read-only Usage tab so the page earns its space. ──
const tagCategoryOptions = () => window.TAG_CATEGORIES.map((c) => ({ value: c.id, text: c.name }));
const TAG_ENTITY = {
	key: 'tags', label: 'Tags', singular: 'Tag', glyph: 'tag',
	seed: window.TAGS,
	blankName: 'Unnamed tag',
	newItem: (id) => ({ id, name: '', tagCategoryId: 1 }),
	listBadge: (t) => (window.TAG_CATEGORIES.find((c) => c.id === t.tagCategoryId) || {}).name,
	badgeColor: (t) => window.tagColor(t.tagCategoryId).fg,
	meta: (t) => {
		const u = window.tagUsage(t.id);
		return [['item', u.items], ['mod', u.mods]];
	},
	sections: [
		{
			key: 'identity', label: 'Identity', glyph: 'tag', desc: 'Name & category',
			kind: 'fields', fields: [
				{ key: 'name', label: 'Tag Name', type: 'text', placeholder: 'Name this tag…', grow: true, required: true, reqMsg: 'Missing name' },
				{ key: 'tagCategoryId', label: 'Category', type: 'select', options: tagCategoryOptions, width: 200 }
			]
		},
		{
			key: 'usage', label: 'Usage', glyph: 'box', desc: 'Where this tag is applied (read-only)',
			count: (t) => { const u = window.tagUsage(t.id); return u.items + u.mods; },
			kind: 'usage'
		}
	]
};

const ENTITIES = { enemies: ENEMY_ENTITY, skills: SKILL_ENTITY, items: ITEM_ENTITY, itemMods: ITEMMOD_ENTITY, zones: ZONE_ENTITY, tags: TAG_ENTITY };

// ════════════════════════════════════════════════════════════════════════
//  DIFFING STORE — real per-record status (added / modified / deleted) and
//  per-field dirty, with save / discard / reset / restore.
// ════════════════════════════════════════════════════════════════════════
const eq = (a, b) => JSON.stringify(a) === JSON.stringify(b);

function useEntityStore(entity) {
	const [items, setItems] = React.useState(() => entity.seed.map(window.clone));
	const [base, setBase] = React.useState(() => entity.seed.map(window.clone));
	const [deleted, setDeleted] = React.useState(() => new Set());
	const [saved, setSaved] = React.useState(false);
	const nextId = React.useRef(-1);

	const baseMap = React.useMemo(() => {
		const m = new Map();
		base.forEach((b) => m.set(b.id, b));
		return m;
	}, [base]);

	const status = (item) => {
		if (deleted.has(item.id)) return 'deleted';
		if (!baseMap.has(item.id)) return 'added';
		return eq(item, baseMap.get(item.id)) ? 'clean' : 'modified';
	};
	const baselineOf = (id) => baseMap.get(id);

	const counts = React.useMemo(() => {
		let added = 0, modified = 0, del = 0;
		items.forEach((it) => {
			const s = status(it);
			if (s === 'added') added++;
			else if (s === 'modified') modified++;
			else if (s === 'deleted') del++;
		});
		return { added, modified, deleted: del, total: added + modified + del };
	}, [items, deleted, baseMap]);

	const patch = (id, fn) => {
		setItems((arr) => arr.map((it) => { if (it.id !== id) return it; const c = window.clone(it); fn(c); return c; }));
		setSaved(false);
	};
	const addItem = () => {
		const id = nextId.current--;
		const ne = entity.newItem(id);
		setItems((arr) => [ne, ...arr]);
		setSaved(false);
		return id;
	};
	const removeItem = (id) => {
		if (!baseMap.has(id)) { setItems((arr) => arr.filter((it) => it.id !== id)); } // never-saved → just drop
		else setDeleted((d) => new Set(d).add(id));
		setSaved(false);
	};
	const restoreItem = (id) => setDeleted((d) => { const n = new Set(d); n.delete(id); return n; });
	const resetItem = (id) => {
		const b = baseMap.get(id);
		if (b) setItems((arr) => arr.map((it) => (it.id === id ? window.clone(b) : it)));
		setDeleted((d) => { const n = new Set(d); n.delete(id); return n; });
		setSaved(false);
	};
	const save = () => {
		const keep = items.filter((it) => !deleted.has(it.id));
		setItems(keep.map(window.clone));
		setBase(keep.map(window.clone));
		setDeleted(new Set());
		setSaved(true);
		setTimeout(() => setSaved(false), 1900);
	};
	const discard = () => { setItems(base.map(window.clone)); setDeleted(new Set()); setSaved(false); };

	return { items, status, baselineOf, counts, saved, patch, addItem, removeItem, restoreItem, resetItem, save, discard };
}

Object.assign(window, { SKILL_RECORDS, ZONE_RECORDS, ENEMY_ENTITY, SKILL_ENTITY, ITEM_ENTITY, ITEMMOD_ENTITY, ZONE_ENTITY, TAG_ENTITY, ENTITIES, skillWarnings, itemWarnings, modWarnings, entityWarnings, sectionWarnings, fieldWarn, useEntityStore, eq });
