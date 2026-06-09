/* sb-stat-data.jsx — model for the Stats · Statistics page.

   Mirrors the server's statistics shape (EStatisticType + EEntityType +
   PlayerStatistic{StatisticTypeId, EntityId?, Value}). Each of the 14 stat
   types is associated with an entity kind (Enemy / Zone / Skill) and breaks
   down into one PlayerStatistic row per entity. The same entities recur across
   many stat types, which is what makes the entity-pivot view possible: pick an
   enemy and see kills + encounters + damage taken + deaths + fastest kill, all
   at once.

   Raw per-entity tables are authored below and are internally consistent
   (kills <= encounters, wins/losses per zone, etc.); STAT_DATA is derived from
   them so the breakdowns and the entity pivot always agree. */

/* ── entities ────────────────────────────────────────────────────────── */
const ENEMIES = [
  { id: 1,  name: 'Cave Bat',          boss: false, enc: 320, kill: 318, dmgTaken: 4200,  deaths: 0, fastest: 1800 },
  { id: 2,  name: 'Goblin Skirmisher', boss: false, enc: 210, kill: 205, dmgTaken: 9800,  deaths: 1, fastest: 2400 },
  { id: 3,  name: 'Plague Rat',        boss: false, enc: 254, kill: 250, dmgTaken: 6100,  deaths: 0, fastest: 2000 },
  { id: 4,  name: 'Bone Archer',       boss: false, enc: 142, kill: 138, dmgTaken: 11400, deaths: 1, fastest: 2900 },
  { id: 5,  name: 'Crypt Hound',       boss: false, enc: 166, kill: 160, dmgTaken: 13900, deaths: 2, fastest: 2600 },
  { id: 6,  name: 'Skeleton Mage',     boss: false, enc: 188, kill: 180, dmgTaken: 15200, deaths: 2, fastest: 3100 },
  { id: 7,  name: 'Tomb Wraith',       boss: false, enc: 96,  kill: 88,  dmgTaken: 21800, deaths: 4, fastest: 4200 },
  { id: 8,  name: 'Stone Golem',       boss: false, enc: 64,  kill: 57,  dmgTaken: 28600, deaths: 5, fastest: 6800 },
  { id: 9,  name: 'Ironclad Sentinel', boss: true,  enc: 22,  kill: 18,  dmgTaken: 33400, deaths: 4, fastest: 9400 },
  { id: 10, name: 'The Hollow King',   boss: true,  enc: 9,   kill: 5,   dmgTaken: 41200, deaths: 4, fastest: 14200 },
];

const ZONES = [
  { id: 1, name: 'Verdant Hollow',      num: 1, cleared: 12, won: 240, lost: 6,  timeMs: 1980000 },
  { id: 2, name: 'Sunken Crossroads',   num: 2, cleared: 8,  won: 188, lost: 11, timeMs: 2640000 },
  { id: 3, name: 'Forgotten Catacombs', num: 3, cleared: 5,  won: 156, lost: 18, timeMs: 3420000 },
  { id: 4, name: 'Emberfall Mines',     num: 4, cleared: 2,  won: 84,  lost: 14, timeMs: 2100000 },
  { id: 5, name: 'Frostspire Ascent',   num: 5, cleared: 0,  won: 23,  lost: 9,  timeMs: 980000 },
];

const SKILLS = [
  { id: 1, name: 'Cleave',    kind: 'Physical', used: 1240, dmg: 186400, highest: 1820, heal: 0 },
  { id: 2, name: 'Surge',     kind: 'Physical', used: 410,  dmg: 98600,  highest: 3240, heal: 0 },
  { id: 3, name: 'Pierce',    kind: 'Physical', used: 680,  dmg: 71200,  highest: 1410, heal: 0 },
  { id: 4, name: 'Ember Bolt',kind: 'Magical',  used: 520,  dmg: 88200,  highest: 2680, heal: 0 },
  { id: 5, name: 'Mend',      kind: 'Healing',  used: 360,  dmg: 0,      highest: 0,    heal: 42800 },
  { id: 6, name: 'Renew',     kind: 'Healing',  used: 180,  dmg: 0,      highest: 0,    heal: 28400 },
  { id: 7, name: 'Guard',     kind: 'Utility',  used: 295,  dmg: 4200,   highest: 120,  heal: 0 },
];

const ENTITY_KIND = {
  enemy: { key: 'enemy', label: 'Enemy', plural: 'Enemies', color: '#e8b6a6', list: ENEMIES },
  zone:  { key: 'zone',  label: 'Zone',  plural: 'Zones',   color: '#bde0b4', list: ZONES },
  skill: { key: 'skill', label: 'Skill', plural: 'Skills',  color: '#a1c2f7', list: SKILLS },
};
function entity(kind, id) { return ENTITY_KIND[kind].list.find((e) => e.id === id); }

/* ── stat-type catalogue (mirrors EStatisticType) ────────────────────── */
// unit: count | damage | time ; agg: sum | max | min ; comp: AtLeast | AtMost
const STAT_TYPES = [
  { id: 1,  key: 'EnemiesKilled',            name: 'Enemies Killed',           kind: 'enemy', unit: 'count',  agg: 'sum', comp: 'AtLeast', cat: 'combat',
    desc: 'Total foes felled, by enemy type.' },
  { id: 2,  key: 'BossesDefeated',           name: 'Bosses Defeated',          kind: 'enemy', unit: 'count',  agg: 'sum', comp: 'AtLeast', cat: 'combat', bossOnly: true,
    desc: 'Boss-class enemies brought down.' },
  { id: 14, key: 'SkillsUsed',               name: 'Skills Used',              kind: 'skill', unit: 'count',  agg: 'sum', comp: 'AtLeast', cat: 'combat',
    desc: 'How often each skill has been cast.' },
  { id: 4,  key: 'DamageDealt',              name: 'Damage Dealt',             kind: 'skill', unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'combat',
    desc: 'Total damage output, attributed per skill.' },
  { id: 5,  key: 'HighestSingleAttackDamage',name: 'Highest Single Hit',       kind: 'skill', unit: 'damage', agg: 'max', comp: 'AtLeast', cat: 'combat',
    desc: 'Your biggest single blow, per skill.' },

  { id: 6,  key: 'DamageTaken',              name: 'Damage Taken',             kind: 'enemy', unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'survival',
    desc: 'Damage absorbed, by the enemy that dealt it.' },
  { id: 7,  key: 'DamageHealed',             name: 'Damage Healed',            kind: 'skill', unit: 'damage', agg: 'sum', comp: 'AtLeast', cat: 'survival',
    desc: 'Health restored, by healing skill.' },
  { id: 11, key: 'PlayerDeaths',             name: 'Player Deaths',            kind: 'enemy', unit: 'count',  agg: 'sum', comp: 'AtMost',  cat: 'survival',
    desc: 'How often each enemy has bested you.' },

  { id: 3,  key: 'ZonesCleared',             name: 'Zones Cleared',            kind: 'zone',  unit: 'count',  agg: 'sum', comp: 'AtLeast', cat: 'exploration',
    desc: 'Full zone clears, by zone.' },
  { id: 8,  key: 'EnemiesEncountered',       name: 'Enemies Encountered',      kind: 'enemy', unit: 'count',  agg: 'sum', comp: 'AtLeast', cat: 'exploration',
    desc: 'Every foe met, whether or not defeated.' },
  { id: 9,  key: 'BattlesWon',               name: 'Battles Won',              kind: 'zone',  unit: 'count',  agg: 'sum', comp: 'AtLeast', cat: 'exploration',
    desc: 'Victories, by the zone they happened in.' },
  { id: 10, key: 'BattlesLost',              name: 'Battles Lost',             kind: 'zone',  unit: 'count',  agg: 'sum', comp: 'AtMost',  cat: 'exploration',
    desc: 'Defeats, by the zone they happened in.' },

  { id: 12, key: 'TotalBattleTime',          name: 'Total Battle Time',        kind: 'zone',  unit: 'time',   agg: 'sum', comp: 'AtLeast', cat: 'time',
    desc: 'Time spent in combat, by zone.' },
  { id: 13, key: 'FastestVictory',           name: 'Fastest Victory',          kind: 'enemy', unit: 'time',   agg: 'min', comp: 'AtMost',  cat: 'time',
    desc: 'Your quickest kill against each enemy.' },
];
const STAT_BY_KEY = Object.fromEntries(STAT_TYPES.map((s) => [s.key, s]));
const STAT_BY_ID = Object.fromEntries(STAT_TYPES.map((s) => [s.id, s]));

const STAT_CATEGORIES = [
  { key: 'combat',      label: 'Combat',      color: '#e8b6a6' },
  { key: 'survival',    label: 'Survival',    color: '#bde0b4' },
  { key: 'exploration', label: 'Exploration', color: '#a1c2f7' },
  { key: 'time',        label: 'Time',        color: '#f0d28a' },
];

/* ── derive STAT_DATA[statKey] = [{entityId, value}] from the raw tables ─ */
function buildStatData() {
  const d = {};
  d.EnemiesKilled       = ENEMIES.filter((e) => e.kill > 0).map((e) => ({ entityId: e.id, value: e.kill }));
  d.BossesDefeated      = ENEMIES.filter((e) => e.boss).map((e) => ({ entityId: e.id, value: e.kill }));
  d.EnemiesEncountered  = ENEMIES.map((e) => ({ entityId: e.id, value: e.enc }));
  d.DamageTaken         = ENEMIES.map((e) => ({ entityId: e.id, value: e.dmgTaken }));
  d.PlayerDeaths        = ENEMIES.filter((e) => e.deaths > 0).map((e) => ({ entityId: e.id, value: e.deaths }));
  d.FastestVictory      = ENEMIES.filter((e) => e.kill > 0).map((e) => ({ entityId: e.id, value: e.fastest }));

  d.ZonesCleared        = ZONES.map((z) => ({ entityId: z.id, value: z.cleared }));
  d.BattlesWon          = ZONES.map((z) => ({ entityId: z.id, value: z.won }));
  d.BattlesLost         = ZONES.map((z) => ({ entityId: z.id, value: z.lost }));
  d.TotalBattleTime     = ZONES.map((z) => ({ entityId: z.id, value: z.timeMs }));

  d.SkillsUsed          = SKILLS.map((s) => ({ entityId: s.id, value: s.used }));
  d.DamageDealt         = SKILLS.filter((s) => s.dmg > 0).map((s) => ({ entityId: s.id, value: s.dmg }));
  d.HighestSingleAttackDamage = SKILLS.filter((s) => s.highest > 0).map((s) => ({ entityId: s.id, value: s.highest }));
  d.DamageHealed        = SKILLS.filter((s) => s.heal > 0).map((s) => ({ entityId: s.id, value: s.heal }));
  return d;
}
const STAT_DATA = buildStatData();

/* ── query helpers ───────────────────────────────────────────────────── */
// rows for a stat type, sorted best-first (asc for "min" aggregates)
function rowsForStat(statKey) {
  const st = STAT_BY_KEY[statKey];
  const rows = (STAT_DATA[statKey] || []).map((r) => ({ ...r, entity: entity(st.kind, r.entityId) }));
  rows.sort((a, b) => st.agg === 'min' ? a.value - b.value : b.value - a.value);
  return rows;
}
// headline value for a stat type
function statHeadline(statKey) {
  const st = STAT_BY_KEY[statKey];
  const vals = (STAT_DATA[statKey] || []).map((r) => r.value);
  if (!vals.length) return 0;
  if (st.agg === 'max') return Math.max(...vals);
  if (st.agg === 'min') return Math.min(...vals);
  return vals.reduce((a, b) => a + b, 0);
}
// # of entities a stat type has rows for
function statEntityCount(statKey) { return (STAT_DATA[statKey] || []).length; }

// all stat types (with this entity's value + rank) that reference a given entity
function statsForEntity(kind, entityId) {
  const out = [];
  for (const st of STAT_TYPES) {
    if (st.kind !== kind) continue;
    const rows = rowsForStat(st.key);
    const idx = rows.findIndex((r) => r.entityId === entityId);
    if (idx < 0) continue;
    out.push({ stat: st, value: rows[idx].value, rank: idx + 1, of: rows.length, headline: statHeadline(st.key) });
  }
  return out;
}

Object.assign(window, {
  ENEMIES, ZONES, SKILLS, ENTITY_KIND, entity,
  STAT_TYPES, STAT_BY_KEY, STAT_BY_ID, STAT_CATEGORIES, STAT_DATA,
  rowsForStat, statHeadline, statEntityCount, statsForEntity,
});
