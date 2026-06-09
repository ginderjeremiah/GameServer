// ─────────────────────────────────────────────────────────────────────────
//  Challenges data layer. Mirrors the Game.Api.Models.Progress DTOs:
//
//   • CHALLENGE_TYPES  ← GET /api/Challenges/ChallengeTypes
//        { id, statisticType: { id, entityType, name } | null, goalComparison, name }
//        (glyph is a mock-only UI concern, not part of the DTO)
//
//   • CHALLENGES       ← GET /api/Challenges
//        { id, name, description, type (=ChallengeTypeId), statisticType, entityType,
//          targetEntityId, progressGoal, rewardItemId, rewardItemModId }
//        statisticType + entityType are DERIVED from the type on the server, so the
//        page reads them from the challenge-type metadata, never editing them directly.
// ─────────────────────────────────────────────────────────────────────────

const ENTITY_TYPE_NAME = { 0: 'None', 1: 'Enemy', 2: 'Zone', 3: 'Skill' };

// Challenge-type metadata exactly as the /ChallengeTypes endpoint returns it. The
// nested statisticType carries the entity dimension the statistic is bucketed by;
// `null` means the type has no statistic (Level Reached → tracks the player's level).
const CHALLENGE_TYPES = [
	{ id: 1, name: 'Enemies Killed', glyph: 'skull', goalComparison: 'AtLeast', statisticType: { id: 1, entityType: 1, name: 'Enemies Killed' } },
	{ id: 2, name: 'Bosses Defeated', glyph: 'trophy', goalComparison: 'AtLeast', statisticType: { id: 2, entityType: 0, name: 'Bosses Defeated' } },
	{ id: 3, name: 'Zones Cleared', glyph: 'map', goalComparison: 'AtLeast', statisticType: { id: 3, entityType: 2, name: 'Zones Cleared' } },
	{ id: 4, name: 'Time Trial', glyph: 'gauge', goalComparison: 'AtMost', statisticType: { id: 13, entityType: 0, name: 'Fastest Victory' } },
	{ id: 5, name: 'Level Reached', glyph: 'bars', goalComparison: 'AtLeast', statisticType: null },
	{ id: 6, name: 'Damage Dealt', glyph: 'bolt', goalComparison: 'AtLeast', statisticType: { id: 4, entityType: 3, name: 'Damage Dealt' } },
	{ id: 7, name: 'Battles Won', glyph: 'flag', goalComparison: 'AtLeast', statisticType: { id: 9, entityType: 1, name: 'Battles Won' } },
	{ id: 8, name: 'Skills Used', glyph: 'rune', goalComparison: 'AtLeast', statisticType: { id: 14, entityType: 3, name: 'Skills Used' } }
];
const LEVEL_REACHED = 5;
const challengeTypeById = (id) => CHALLENGE_TYPES.find((t) => t.id === id) || {};

// ── derivations off the challenge type (the metadata is the source of truth) ──
const typeStat = (typeId) => challengeTypeById(typeId).statisticType || null;
const suggestedStat = (typeId) => { const s = typeStat(typeId); return s ? s.id : null; };
const typeExpectsStat = (typeId) => typeStat(typeId) != null;
const typeEntityType = (typeId) => { const s = typeStat(typeId); return s ? s.entityType : 0; };
const goalComparisonOf = (typeId) => challengeTypeById(typeId).goalComparison || 'AtLeast';

// ── lookups ──
const typeName = (id) => (challengeTypeById(id).name) || '—';
const typeGlyph = (id) => (challengeTypeById(id).glyph) || 'target';
const entityTypeName = (etype) => ENTITY_TYPE_NAME[etype] || 'None';

// What a challenge tracks progress against (mirrors Challenge.UpdateChallengeProgress):
// its statistic, the player's level (Level Reached), or nothing.
const trackedKind = (c) => (c.statisticType != null ? 'stat' : (c.type === LEVEL_REACHED ? 'level' : 'none'));
const trackedLabel = (c) => {
	if (c.statisticType != null) { const s = typeStat(c.type); return s ? s.name : 'Statistic'; }
	return c.type === LEVEL_REACHED ? 'Player Level' : 'No statistic';
};

// Goal unit per statistic id (UI-only; the comparison/goal live on the record).
const STAT_UNIT = {
	1: 'kills', 2: 'bosses', 3: 'zones', 4: 'damage', 5: 'damage', 6: 'damage', 7: 'healed',
	8: 'encounters', 9: 'wins', 10: 'losses', 11: 'deaths', 12: 'ms', 13: 'ms', 14: 'skills'
};
const goalUnit = (c) => (c.statisticType ? STAT_UNIT[c.statisticType] : (c.type === LEVEL_REACHED ? 'level' : 'goal'));

// ── target-entity catalogs (drawn from the matching entity admin data) ──
const entityCatalog = (etype) => (etype === 1 ? window.ENEMIES : etype === 2 ? window.ZONES : etype === 3 ? window.SKILLS : []);
const entityName = (etype, id) => { const e = entityCatalog(etype).find((x) => x.id === id); return e ? e.name : null; };

// ── formatting ──
const fmtNum = (n) => Number(n || 0).toLocaleString('en-US');
const fmtMs = (ms) => (ms >= 1000 ? (ms / 1000).toFixed(ms % 1000 ? 1 : 0) + 's' : ms + 'ms');

// Player-facing objective sentence — makes the type / target / goal combination
// legible at a glance.
function challengeSentence(c) {
	const g = c.progressGoal;
	const tgt = c.targetEntityId != null ? entityName(c.entityType, c.targetEntityId) : null;
	switch (c.type) {
		case 1: return tgt ? `Defeat ${fmtNum(g)} ${tgt}` : `Defeat ${fmtNum(g)} enemies`;
		case 2: return `Defeat ${fmtNum(g)} ${g == 1 ? 'boss' : 'bosses'}`;
		case 3: return tgt ? `Clear ${tgt} ${fmtNum(g)} ${g == 1 ? 'time' : 'times'}` : `Clear ${fmtNum(g)} ${g == 1 ? 'zone' : 'zones'}`;
		case 4: return tgt ? `Defeat ${tgt} in under ${fmtMs(g)}` : `Win a battle in under ${fmtMs(g)}`;
		case 5: return `Reach level ${fmtNum(g)}`;
		case 6: return tgt ? `Deal ${fmtNum(g)} damage with ${tgt}` : `Deal ${fmtNum(g)} total damage`;
		case 7: return tgt ? `Win ${fmtNum(g)} battles against ${tgt}` : `Win ${fmtNum(g)} battles`;
		case 8: return tgt ? `Use ${tgt} ${fmtNum(g)} times` : `Use skills ${fmtNum(g)} times`;
		default: return `${fmtNum(g)} ${goalUnit(c)}`;
	}
}

// ── reward exclusivity ──────────────────────────────────────────────────
//  Each item / mod may be unlocked by AT MOST ONE challenge. These build a
//  map of already-claimed reward → the owning challenge, excluding the
//  challenge currently being edited so its own reward stays selectable.
function claimedItemMap(allChallenges, exceptId) {
	const m = new Map();
	allChallenges.forEach((c) => { if (c.id !== exceptId && c.rewardItemId != null) m.set(c.rewardItemId, c); });
	return m;
}
function claimedModMap(allChallenges, exceptId) {
	const m = new Map();
	allChallenges.forEach((c) => { if (c.id !== exceptId && c.rewardItemModId != null) m.set(c.rewardItemModId, c); });
	return m;
}

const itemModName = (id) => (window.ITEMMOD_RECORDS.find((m) => m.id === id) || {}).name;
const itemModTypeName = (id) => {
	const m = window.ITEMMOD_RECORDS.find((x) => x.id === id);
	return m ? ((window.MOD_TYPES.find((t) => t.id === m.itemModTypeId) || {}).name) : null;
};
const itemRecName = (id) => (window.ITEM_RECORDS.find((i) => i.id === id) || {}).name;
const itemRarityId = (id) => (window.ITEM_RECORDS.find((i) => i.id === id) || {}).rarityId;

// Build a challenge record from a seed, deriving statistic + entity from the type.
function makeChallenge(id, s) {
	const st = typeStat(s.type);
	return {
		id,
		name: s.name,
		description: s.desc,
		type: s.type,
		statisticType: st ? st.id : null,
		entityType: st ? st.entityType : 0,
		targetEntityId: s.target,
		progressGoal: s.goal,
		rewardItemId: s.item,
		rewardItemModId: s.mod
	};
}

// ── seed challenges — varied, with intentional edge cases:
//    • global + entity-scoped statistics (incl. Skill-bucketed Damage Dealt)
//    • Level Reached — tracks player level, no statistic, no target
//    • one reward-less challenge (Persistence) — warns
//    Every reward is unique so the exclusivity picker is demonstrable. ──
const RAW_SEED = [
	{ name: 'First Blood', desc: 'Defeat your very first foes in battle.', type: 1, target: null, goal: 10, item: 3, mod: null },
	{ name: 'Bog Hunter', desc: 'Thin out the lurkers haunting Mistwood Fen.', type: 1, target: 1, goal: 50, item: 7, mod: 2 },
	{ name: 'Imp Extermination', desc: 'Purge the cinder imps of the Ashfall Barrens.', type: 1, target: 2, goal: 100, item: null, mod: 1 },
	{ name: 'Trailblazer', desc: 'Clear zones across the realm.', type: 3, target: null, goal: 5, item: 11, mod: null },
	{ name: 'Frostpeak Conqueror', desc: 'Master the frozen ascent again and again.', type: 3, target: 4, goal: 10, item: 15, mod: null },
	{ name: 'Boss Breaker', desc: 'Topple the realm’s great bosses.', type: 2, target: null, goal: 3, item: 22, mod: 6 },
	{ name: 'Glass Cannon', desc: 'Rack up devastating damage with a single skill.', type: 6, target: null, goal: 100000, item: 30, mod: null },
	{ name: 'Blitz', desc: 'Win a battle at blistering speed.', type: 4, target: null, goal: 30000, item: 18, mod: null },
	{ name: 'Battle-Hardened', desc: 'Prove yourself across a long campaign of wins.', type: 7, target: null, goal: 200, item: 9, mod: 11 },
	{ name: 'Spellweaver', desc: 'Lean on your skills throughout many battles.', type: 8, target: null, goal: 500, item: null, mod: 14 },
	{ name: 'Apprentice', desc: 'Grow in raw character power.', type: 5, target: null, goal: 20, item: 5, mod: null },
	{ name: 'Adder Cull', desc: 'Hunt the venomspine adders to extinction.', type: 1, target: 11, goal: 75, item: 33, mod: null },
	{ name: 'Emberlord’s Bane', desc: 'Slay the Emberlord Vorrgath himself.', type: 1, target: 8, goal: 1, item: 40, mod: 9 },
	{ name: 'Persistence', desc: 'Keep winning — reward still being designed.', type: 7, target: null, goal: 25, item: null, mod: null }
];

const CHALLENGES = RAW_SEED.map((s, i) => makeChallenge(i + 1, s));

Object.assign(window, {
	CHALLENGE_TYPES, ENTITY_TYPE_NAME, LEVEL_REACHED, challengeTypeById,
	typeStat, suggestedStat, typeExpectsStat, typeEntityType, goalComparisonOf,
	goalUnit, trackedKind, trackedLabel,
	typeName, typeGlyph, entityCatalog, entityName, entityTypeName,
	fmtNum, fmtMs, challengeSentence,
	claimedItemMap, claimedModMap, itemModName, itemModTypeName, itemRecName, itemRarityId,
	makeChallenge, CHALLENGES
});
