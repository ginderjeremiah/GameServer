/* sb-attr-data.jsx — model + engine for the Stats · Attribute Breakdown page.

   Mirrors the server's attribute pipeline (Game.Core/Attributes):
     · A flat list of AttributeModifier objects, each tagged with a SOURCE
       (BaseValue / PlayerStatPoints / Item / ItemMod / Derived) and a TYPE
       (Additive / Multiplicative).
     · computeAttributes() aggregates them exactly like AttributeCollection:
       additive modifiers are summed first, then multiplicative modifiers are
       applied to that subtotal — and a Derived modifier multiplies its amount
       by the *final* value of the attribute it derives from.
     · Every attribute therefore knows precisely where its total came from, so
       the UI can show a faithful, summing breakdown.

   The loadout below is the same character the Inventory screen ships equipped
   (Aegis Greathelm, Bulwark Cuirass + Iron Core/Tempered, Titan Greaves,
   Fleetfoot Boots, Aegis-Forged Saber + Vampiric, Eye of the Storm) plus a
   level-12 stat-point allocation. The numbers are self-consistent: the bars
   literally add up.
*/

/* ── source taxonomy (mirrors EAttributeModifierSource) ──────────────── */
const SOURCES = {
  base:    { key: 'base',    label: 'Base value',  short: 'BASE',    color: '#8b919e', enumName: 'BaseValue' },
  points:  { key: 'points',  label: 'Stat points', short: 'POINTS',  color: '#a1c2f7', enumName: 'PlayerStatPoints' },
  item:    { key: 'item',    label: 'Equipment',   short: 'GEAR',    color: '#bde0b4', enumName: 'Item' },
  mod:     { key: 'mod',     label: 'Item mods',   short: 'MODS',    color: '#c4b6ec', enumName: 'ItemMod' },
  derived: { key: 'derived', label: 'Derived',     short: 'DERIVED', color: '#f0d28a', enumName: 'Derived' },
};
const SOURCE_ORDER = ['base', 'points', 'item', 'mod', 'derived'];

/* ── attribute catalogue (mirrors EAttribute 0..14) ──────────────────── */
const A = {
  STR: 'Strength', END: 'Endurance', INT: 'Intellect', AGI: 'Agility',
  DEX: 'Dexterity', LUK: 'Luck', HP: 'MaxHealth', DEF: 'Defense',
  CDR: 'CooldownRecovery', DROP: 'DropBonus', CC: 'CriticalChance',
  CD: 'CriticalDamage', DODGE: 'DodgeChance', BLK: 'BlockChance', BLKR: 'BlockReduction',
};

const ATTR_META = [
  { key: A.STR, id: 0, short: 'STR', name: 'Strength',          group: 'core',    unit: '',  dec: 0,
    desc: 'Raw physical force. Increases the damage of some physical skills and contributes to Max Health.' },
  { key: A.END, id: 1, short: 'END', name: 'Endurance',         group: 'core',    unit: '',  dec: 0,
    desc: 'Resilience and fortitude. Contributes to Max Health and Defense.' },
  { key: A.INT, id: 2, short: 'INT', name: 'Intellect',         group: 'core',    unit: '',  dec: 0,
    desc: 'Mental acuity and command of the arcane. Increases the damage of magical skills.' },
  { key: A.AGI, id: 3, short: 'AGI', name: 'Agility',           group: 'core',    unit: '',  dec: 0,
    desc: 'Speed and reflexes. Improves Cooldown Recovery and contributes to Defense.' },
  { key: A.DEX, id: 4, short: 'DEX', name: 'Dexterity',         group: 'core',    unit: '',  dec: 0,
    desc: 'Precision and finesse. Increases the damage of some physical skills and improves Cooldown Recovery.' },
  { key: A.LUK, id: 5, short: 'LUK', name: 'Luck',              group: 'core',    unit: '',  dec: 0,
    desc: 'Fortune, influencing various chance-based outcomes.' },

  { key: A.HP,  id: 6, short: 'HP',  name: 'Max Health',        group: 'derived', unit: '',  dec: 0,
    desc: 'The amount of health a character has at the start of a battle.' },
  { key: A.DEF, id: 7, short: 'DEF', name: 'Defense',           group: 'derived', unit: '',  dec: 0,
    desc: 'A flat reduction applied to all incoming damage.' },
  { key: A.CDR, id: 8, short: 'CDR', name: 'Cooldown Recovery', group: 'derived', unit: '×', dec: 2,
    desc: 'A multiplier to the rate at which skills become available again after use.' },

  { key: A.DROP, id: 9,  short: 'DROP', name: 'Drop Bonus',       group: 'derived', unit: '%', dec: 0, obsolete: true,
    desc: 'Obsolete. Previously increased the rate at which items were dropped by enemies.' },
  { key: A.CC,   id: 10, short: 'CC',   name: 'Critical Chance',  group: 'derived', unit: '%', dec: 0, unused: true,
    desc: 'The chance for an attack to deal increased damage.' },
  { key: A.CD,   id: 11, short: 'CD',   name: 'Critical Damage',  group: 'derived', unit: '%', dec: 0, unused: true,
    desc: 'The additional damage dealt when a critical hit occurs.' },
  { key: A.DODGE,id: 12, short: 'DODGE',name: 'Dodge Chance',     group: 'derived', unit: '%', dec: 0, unused: true,
    desc: 'The chance to completely avoid the damage from an incoming attack.' },
  { key: A.BLK,  id: 13, short: 'BLK',  name: 'Block Chance',     group: 'derived', unit: '%', dec: 0, unused: true,
    desc: 'The chance to block part of the damage from an incoming attack.' },
  { key: A.BLKR, id: 14, short: 'BLKR', name: 'Block Reduction',  group: 'derived', unit: '',  dec: 0, unused: true,
    desc: 'A flat reduction applied to damage received when an attack is blocked.' },
];
const ATTR_BY_KEY = Object.fromEntries(ATTR_META.map((a) => [a.key, a]));

/* ── the player ──────────────────────────────────────────────────────── */
const PLAYER = { name: 'Aelara', level: 12, statPointsGained: 66, statPointsUsed: 55 };

/* stat-point allocation (PlayerStatPoints → core attributes, additive) */
const ALLOC = { [A.STR]: 14, [A.END]: 12, [A.INT]: 6, [A.AGI]: 10, [A.DEX]: 8, [A.LUK]: 5 };

/* equipped loadout — each entry is one equipped item with its contributions.
   `mods` are the applied item-mods on that item. Values mirror inv-data.jsx. */
const LOADOUT = [
  { slot: 'Helm',      name: 'Aegis Greathelm',   rarity: 'epic',
    item: [[A.DEF, 14, 'add'], [A.END, 8, 'add']], mods: [] },
  { slot: 'Chest',     name: 'Bulwark Cuirass',   rarity: 'legendary',
    item: [[A.DEF, 22, 'add'], [A.HP, 30, 'add'], [A.AGI, -3, 'add'], [A.HP, 1.1, 'mult']],
    mods: [
      { name: 'Iron Core', type: 'Component', vals: [[A.DEF, 4, 'add']] },
      { name: 'Tempered',  type: 'Prefix',    vals: [[A.END, 10, 'add']] },
    ] },
  { slot: 'Legs',      name: 'Titan Greaves',     rarity: 'epic',
    item: [[A.DEF, 12, 'add'], [A.BLK, 5, 'add']], mods: [] },
  { slot: 'Boots',     name: 'Fleetfoot Boots',   rarity: 'rare',
    item: [[A.AGI, 9, 'add'], [A.DODGE, 5, 'add']], mods: [] },
  { slot: 'Weapon',    name: 'Aegis-Forged Saber', rarity: 'legendary',
    item: [[A.STR, 18, 'add'], [A.DEX, 12, 'add'], [A.CC, 5, 'add'], [A.DEF, -2, 'add'], [A.STR, 1.08, 'mult']],
    mods: [
      { name: 'Vampiric', type: 'Suffix', vals: [[A.LUK, 4, 'add']] },
    ] },
  { slot: 'Accessory', name: 'Eye of the Storm',  rarity: 'mythic',
    item: [[A.INT, 12, 'add'], [A.CD, 14, 'add'], [A.LUK, 6, 'add']], mods: [] },
];

/* static engine modifiers (Game.Core StaticAttributeModifiers) */
const STATIC = [
  { attr: A.DEF, amount: 2,  type: 'add', source: 'base',    from: 'Engine base' },
  { attr: A.HP,  amount: 50, type: 'add', source: 'base',    from: 'Engine base' },

  { attr: A.HP,  amount: 20, type: 'add', source: 'derived', from: A.END },
  { attr: A.HP,  amount: 5,  type: 'add', source: 'derived', from: A.STR },
  { attr: A.DEF, amount: 1,  type: 'add', source: 'derived', from: A.END },
  { attr: A.DEF, amount: 0.5,type: 'add', source: 'derived', from: A.AGI },
  { attr: A.CDR, amount: 0.4,type: 'add', source: 'derived', from: A.AGI },
  { attr: A.CDR, amount: 0.1,type: 'add', source: 'derived', from: A.DEX },
];

/* ── assemble the full modifier list (Player.GetAllModifiers + statics) ── */
function buildModifiers() {
  const mods = [];
  // stat points
  for (const [attr, amount] of Object.entries(ALLOC)) {
    if (amount) mods.push({ attr, amount, type: 'add', source: 'points', from: 'Allocated' });
  }
  // equipment + item mods
  for (const it of LOADOUT) {
    for (const [attr, amount, type] of it.item) {
      mods.push({ attr, amount, type, source: 'item', from: it.name, slot: it.slot, rarity: it.rarity });
    }
    for (const m of it.mods) {
      for (const [attr, amount, type] of m.vals) {
        mods.push({ attr, amount, type, source: 'mod', from: m.name, modType: m.type, host: it.name, slot: it.slot });
      }
    }
  }
  // static (base + derived)
  for (const s of STATIC) mods.push({ ...s });
  return mods;
}

const MODIFIERS = buildModifiers();

/* ── compute exactly like AttributeCollection ────────────────────────── */
function computeAttributes(mods = MODIFIERS) {
  const byAttr = {};
  for (const m of mods) (byAttr[m.attr] || (byAttr[m.attr] = [])).push(m);

  const cache = {};
  function valueOf(key) {
    if (cache[key]) return cache[key].total;
    return computeOne(key).total;
  }
  function computeOne(key) {
    if (cache[key]) return cache[key];
    const list = byAttr[key] || [];
    const adds = list.filter((m) => m.type === 'add');
    const mults = list.filter((m) => m.type === 'mult');
    let running = 0;
    const lines = [];
    for (const m of adds) {
      const applied = m.source === 'derived' ? m.amount * valueOf(m.from) : m.amount;
      running += applied;
      lines.push({ ...m, applied, running, derivedValue: m.source === 'derived' ? valueOf(m.from) : null });
    }
    const additiveSubtotal = running;
    for (const m of mults) {
      const factor = m.source === 'derived' ? m.amount * valueOf(m.from) : m.amount;
      const before = running;
      running *= factor;
      lines.push({ ...m, factor, applied: running - before, running, multiplied: true });
    }
    const res = { key, total: running, additiveSubtotal, multUplift: running - additiveSubtotal, lines };
    cache[key] = res;
    return res;
  }
  ATTR_META.forEach((a) => computeOne(a.key));
  return cache;
}

/* group a computed attribute's additive lines by source category, with a
   per-source subtotal. Multiplicative lines are returned separately. */
function groupBySource(computed) {
  const groups = {};
  const mults = [];
  for (const ln of computed.lines) {
    if (ln.multiplied) { mults.push(ln); continue; }
    const g = groups[ln.source] || (groups[ln.source] = { source: ln.source, total: 0, lines: [] });
    g.total += ln.applied;
    g.lines.push(ln);
  }
  const ordered = SOURCE_ORDER.filter((s) => groups[s]).map((s) => groups[s]);
  return { groups: ordered, mults };
}

/* ── formatting ──────────────────────────────────────────────────────── */
function fmtNum(n, dec = 0) {
  const r = dec > 0 ? Math.round(n * 10 ** dec) / 10 ** dec : Math.round(n);
  return r.toLocaleString('en-US', { minimumFractionDigits: dec, maximumFractionDigits: dec });
}
function fmtAttr(n, meta) {
  return fmtNum(n, meta.dec) + (meta.unit && meta.unit !== '×' ? meta.unit : '');
}
function fmtSigned(n, dec = 0) {
  const s = fmtNum(Math.abs(n), dec);
  return (n < 0 ? '−' : '+') + s;
}

Object.assign(window, {
  SOURCES, SOURCE_ORDER, ATTR_META, ATTR_BY_KEY, A,
  PLAYER, ALLOC, LOADOUT, MODIFIERS,
  computeAttributes, groupBySource, fmtNum, fmtAttr, fmtSigned,
});
