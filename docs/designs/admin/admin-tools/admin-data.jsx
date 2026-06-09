/* Admin tools — nav structure, per-tool table configs, and glyphs.
   Mirrors src/routes/admin/tools/types.ts (toolMap) and the sampleItem
   shapes in each *.svelte tool. Entity types become sidebar groups; each
   tool is a sidebar item, matching the game's NavSidebar pattern. */

const ADMIN_GROUPS = [
  { key: 'enemies', label: 'Enemies' },
  { key: 'items',   label: 'Items' },
  { key: 'skills',  label: 'Skills' },
  { key: 'zones',   label: 'Zones' },
];

const ADMIN_TOOLS = [
  { key: 'addEnemies',  label: 'Add / Edit Enemies', group: 'enemies', glyph: 'skull' },
  { key: 'attrDist',    label: 'Attribute Dist.',    group: 'enemies', glyph: 'bars' },
  { key: 'enemySkills', label: 'Enemy Skills',       group: 'enemies', glyph: 'rune' },
  { key: 'addItems',    label: 'Add / Edit Items',   group: 'items',   glyph: 'box' },
  { key: 'addSkills',   label: 'Add / Edit Skills',  group: 'skills',  glyph: 'bolt' },
  { key: 'skillMult',   label: 'Skill Multipliers',  group: 'skills',  glyph: 'multiply' },
  { key: 'addZones',    label: 'Add / Edit Zones',   group: 'zones',   glyph: 'map' },
  { key: 'zoneEnemies', label: 'Zone Enemies',       group: 'zones',   glyph: 'pin' },
];

/* Each config drives the generic <AdminTable>. Column types:
   id | text | mono | number | select | bool   (id = primary key, not editable)
   `gate` adds a Select control above the table (the enemy/zone picker the
   real SetAttributeDistributions / SetZoneEnemies tools require first). */
const TOOL_CONFIGS = {
  addItems: {
    title: 'Add / Edit Items',
    columns: [
      { key: 'id',          label: 'Id',            type: 'id',     w: 56 },
      { key: 'name',        label: 'Name',          type: 'text',   w: 170 },
      { key: 'iconPath',    label: 'Icon Path',     type: 'mono',   w: 170 },
      { key: 'itemCategory', label: 'Item Category', type: 'select', w: 140,
        options: ['Helm', 'Chest', 'Weapon', 'Material', 'Ring'] },
      { key: 'description', label: 'Description',   type: 'text',   w: 280 },
    ],
    rows: [
      { id: 12, name: 'Iron Helm',       iconPath: 'items/helm_iron',    itemCategory: 'Helm',     description: 'Sturdy starter headgear.' },
      { id: 13, name: 'Sapphire Shard',  iconPath: 'mats/sapphire',      itemCategory: 'Material', description: 'A glittering crafting reagent.' },
      { id: 14, name: 'Catacomb Blade',  iconPath: 'weapons/cata_blade', itemCategory: 'Weapon',   description: 'Forged in the lower crypts.' },
      { id: 15, name: 'Warden Plate',    iconPath: 'armor/warden',       itemCategory: 'Chest',    description: 'Heavy plate of the old wardens.' },
      { id: 16, name: 'Signet of Echoes', iconPath: 'rings/echo',        itemCategory: 'Ring',     description: 'Whispers boost skill charge.' },
    ],
  },
  addEnemies: {
    title: 'Add / Edit Enemies',
    columns: [
      { key: 'id',       label: 'Id',        type: 'id',     w: 56 },
      { key: 'name',     label: 'Name',      type: 'text',   w: 180 },
      { key: 'level',    label: 'Level',     type: 'number', w: 84 },
      { key: 'baseHp',   label: 'Base HP',   type: 'number', w: 100 },
      { key: 'iconPath', label: 'Icon Path', type: 'mono',   w: 180 },
      { key: 'isBoss',   label: 'Is Boss',   type: 'bool',   w: 84 },
    ],
    rows: [
      { id: 7,  name: 'Skeleton Mage', level: 11, baseHp: 220,  iconPath: 'enemies/skel_mage',   isBoss: false },
      { id: 8,  name: 'Crypt Hound',   level: 9,  baseHp: 160,  iconPath: 'enemies/crypt_hound', isBoss: false },
      { id: 9,  name: 'Bone Warden',   level: 14, baseHp: 540,  iconPath: 'enemies/bone_warden', isBoss: true },
      { id: 10, name: 'Wisp',          level: 6,  baseHp: 70,   iconPath: 'enemies/wisp',        isBoss: false },
      { id: 11, name: 'Catacomb Lich', level: 18, baseHp: 1200, iconPath: 'enemies/lich',        isBoss: true },
    ],
  },
  attrDist: {
    title: 'Set Attribute Distributions',
    gate: { label: 'Enemy', value: 'Skeleton Mage',
      options: ['Skeleton Mage', 'Crypt Hound', 'Bone Warden', 'Wisp', 'Catacomb Lich'] },
    columns: [
      { key: 'attribute',     label: 'Attribute',       type: 'select', w: 160,
        options: ['Strength', 'Dexterity', 'Intellect', 'Vitality', 'Spirit'] },
      { key: 'baseAmount',    label: 'Base Amount',     type: 'number', w: 130 },
      { key: 'amountPerLevel', label: 'Amount / Level', type: 'number', w: 140 },
    ],
    rows: [
      { attribute: 'Strength',  baseAmount: 8,  amountPerLevel: 1.5 },
      { attribute: 'Dexterity', baseAmount: 12, amountPerLevel: 2.0 },
      { attribute: 'Intellect', baseAmount: 22, amountPerLevel: 3.5 },
      { attribute: 'Vitality',  baseAmount: 14, amountPerLevel: 2.2 },
    ],
  },
  enemySkills: {
    title: 'Set Enemy Skills',
    gate: { label: 'Enemy', value: 'Skeleton Mage',
      options: ['Skeleton Mage', 'Crypt Hound', 'Bone Warden', 'Wisp', 'Catacomb Lich'] },
    columns: [
      { key: 'skill',       label: 'Skill',        type: 'select', w: 160,
        options: ['Hex', 'Drain', 'Cleave', 'Guard', 'Surge'] },
      { key: 'unlockLevel', label: 'Unlock Level', type: 'number', w: 130 },
      { key: 'weight',      label: 'Weight',       type: 'number', w: 110 },
    ],
    rows: [
      { skill: 'Hex',   unlockLevel: 1, weight: 0.6 },
      { skill: 'Drain', unlockLevel: 5, weight: 0.3 },
      { skill: 'Cleave', unlockLevel: 8, weight: 0.1 },
    ],
  },
  addSkills: {
    title: 'Add / Edit Skills',
    columns: [
      { key: 'id',         label: 'Id',          type: 'id',     w: 56 },
      { key: 'name',       label: 'Name',        type: 'text',   w: 150 },
      { key: 'chargeTime', label: 'Charge Time', type: 'number', w: 120 },
      { key: 'power',      label: 'Power',       type: 'number', w: 90 },
      { key: 'tag',        label: 'Tag',         type: 'select', w: 130,
        options: ['Physical', 'Arcane', 'Holy', 'Shadow'] },
      { key: 'iconPath',   label: 'Icon Path',   type: 'mono',   w: 160 },
    ],
    rows: [
      { id: 3, name: 'Cleave', chargeTime: 4.0,  power: 38, tag: 'Physical', iconPath: 'skills/cleave' },
      { id: 4, name: 'Guard',  chargeTime: 6.0,  power: 0,  tag: 'Physical', iconPath: 'skills/guard' },
      { id: 5, name: 'Mend',   chargeTime: 8.0,  power: 24, tag: 'Holy',     iconPath: 'skills/mend' },
      { id: 6, name: 'Surge',  chargeTime: 12.0, power: 60, tag: 'Arcane',   iconPath: 'skills/surge' },
      { id: 7, name: 'Hex',    chargeTime: 5.0,  power: 12, tag: 'Shadow',   iconPath: 'skills/hex' },
    ],
  },
  skillMult: {
    title: 'Set Skill Multipliers',
    columns: [
      { key: 'skill',      label: 'Skill',      type: 'select', w: 150,
        options: ['Cleave', 'Guard', 'Mend', 'Surge', 'Hex'] },
      { key: 'attribute',  label: 'Attribute',  type: 'select', w: 150,
        options: ['Strength', 'Dexterity', 'Intellect', 'Vitality', 'Spirit'] },
      { key: 'multiplier', label: 'Multiplier', type: 'number', w: 120 },
    ],
    rows: [
      { skill: 'Cleave', attribute: 'Strength',  multiplier: 1.25 },
      { skill: 'Surge',  attribute: 'Intellect', multiplier: 1.8 },
      { skill: 'Mend',   attribute: 'Spirit',    multiplier: 1.5 },
    ],
  },
  addZones: {
    title: 'Add / Edit Zones',
    columns: [
      { key: 'id',       label: 'Id',        type: 'id',     w: 56 },
      { key: 'name',     label: 'Name',      type: 'text',   w: 200 },
      { key: 'order',    label: 'Order',     type: 'number', w: 90 },
      { key: 'minLevel', label: 'Min Level', type: 'number', w: 110 },
      { key: 'iconPath', label: 'Icon Path', type: 'mono',   w: 170 },
    ],
    rows: [
      { id: 1, name: 'Verdant Hollow',      order: 1, minLevel: 1,  iconPath: 'zones/verdant' },
      { id: 2, name: 'Ashen Pass',          order: 2, minLevel: 6,  iconPath: 'zones/ashen' },
      { id: 3, name: 'Forgotten Catacombs', order: 3, minLevel: 10, iconPath: 'zones/catacombs' },
      { id: 4, name: 'Sunken Reliquary',    order: 4, minLevel: 16, iconPath: 'zones/reliquary' },
    ],
  },
  zoneEnemies: {
    title: 'Set Zone Enemies',
    gate: { label: 'Zone', value: 'Forgotten Catacombs',
      options: ['Verdant Hollow', 'Ashen Pass', 'Forgotten Catacombs', 'Sunken Reliquary'] },
    columns: [
      { key: 'enemy',       label: 'Enemy',        type: 'select', w: 170,
        options: ['Skeleton Mage', 'Crypt Hound', 'Bone Warden', 'Wisp', 'Catacomb Lich'] },
      { key: 'spawnWeight', label: 'Spawn Weight', type: 'number', w: 130 },
      { key: 'minCount',    label: 'Min Count',    type: 'number', w: 110 },
      { key: 'maxCount',    label: 'Max Count',    type: 'number', w: 110 },
    ],
    rows: [
      { enemy: 'Skeleton Mage', spawnWeight: 0.5, minCount: 1, maxCount: 2 },
      { enemy: 'Crypt Hound',   spawnWeight: 0.3, minCount: 1, maxCount: 3 },
      { enemy: 'Bone Warden',   spawnWeight: 0.2, minCount: 1, maxCount: 1 },
    ],
  },
};

/* Small abstract marks for the collapsed rail (16×16, stroke-based). */
function AdminGlyph({ kind, active }) {
  const stroke = active ? '#a1c2f7' : 'rgba(240,240,240,0.7)';
  const s = { width: 16, height: 16, fill: 'none', stroke, strokeWidth: 1.4 };
  const map = {
    skull:    <svg {...s} viewBox="0 0 16 16"><path d="M8 1.5c3 0 5 2.2 5 5.2 0 1.8-.8 2.7-.8 3.8 0 .8-.6 1.2-1.4 1.2H5.2c-.8 0-1.4-.4-1.4-1.2 0-1.1-.8-2-.8-3.8 0-3 2-5.2 5-5.2z" strokeLinejoin="round" /><circle cx="6" cy="7.2" r="1.1" /><circle cx="10" cy="7.2" r="1.1" /><path d="M6.5 13v1.5M9.5 13v1.5" strokeLinecap="round" /></svg>,
    bars:     <svg {...s} viewBox="0 0 16 16"><path d="M3 13.5V8.5M7 13.5V3.5M11 13.5V6.5" strokeLinecap="round" /><circle cx="3" cy="6.5" r="1.2" fill={stroke} stroke="none" /><circle cx="7" cy="2.2" r="1.2" fill={stroke} stroke="none" /><circle cx="11" cy="5" r="1.2" fill={stroke} stroke="none" /></svg>,
    rune:     <svg {...s} viewBox="0 0 16 16"><path d="M8 2v12M4 5l8 6M12 5l-8 6" strokeLinecap="round" /></svg>,
    box:      <svg {...s} viewBox="0 0 16 16"><path d="M2.5 5.2L8 2.5l5.5 2.7v5.6L8 13.5l-5.5-2.7z" strokeLinejoin="round" /><path d="M2.5 5.2L8 8l5.5-2.8M8 8v5.5" strokeLinejoin="round" /></svg>,
    bolt:     <svg {...s} viewBox="0 0 16 16"><path d="M9 1.5L3.5 9H7l-1 5.5L12.5 7H9z" strokeLinejoin="round" /></svg>,
    multiply: <svg {...s} viewBox="0 0 16 16"><path d="M4 4l8 8M12 4l-8 8" strokeLinecap="round" /></svg>,
    map:      <svg {...s} viewBox="0 0 16 16"><path d="M2 4l4-1.5 4 1.5 4-1.5v9.5L10 13.5 6 12 2 13.5z" strokeLinejoin="round" /><path d="M6 2.5V12M10 4v9.5" /></svg>,
    pin:      <svg {...s} viewBox="0 0 16 16"><path d="M8 14s4.2-4 4.2-7A4.2 4.2 0 008 2.8 4.2 4.2 0 003.8 7c0 3 4.2 7 4.2 7z" strokeLinejoin="round" /><circle cx="8" cy="6.9" r="1.4" /></svg>,
    back:     <svg {...s} viewBox="0 0 16 16"><path d="M7 3.5L2.5 8 7 12.5M2.5 8H13" strokeLinecap="round" strokeLinejoin="round" /></svg>,
  };
  return map[kind] ?? null;
}

Object.assign(window, { ADMIN_GROUPS, ADMIN_TOOLS, TOOL_CONFIGS, AdminGlyph });
