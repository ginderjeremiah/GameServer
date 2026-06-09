/* inv-data.jsx — sample data + config for the redesigned Inventory screen.

   Reflects the engine refactor:
   - Items are UNLOCKED, not dropped — exactly one copy of each exists.
   - Item mods slot into an item's mod slots (Component / Prefix / Suffix).
   - Equipment slots are DATA-DRIVEN (see EQUIP_SLOTS) so the layout copes
     with future changes — extra weapon/accessory slots, split categories
     (rings, necklaces), etc. — without a redesign. Nothing below hard-codes
     "6 slots".

   Depends on tooltip-shared.jsx for ITEM_CATEGORIES / CATEGORY_ACCENT /
   MOD_TYPE_LABEL / MOD_TYPE_ACCENT (loaded earlier in the HTML). Those are
   read at render time, never at module-eval time, so load order is safe.
*/

/* ─── Equipment slots (extensible) ──────────────────────────────────────
   Each slot declares which item categories it accepts. Adding a second
   weapon slot, or a Ring slot that accepts a future "Ring" category, is a
   one-line edit here — every layout renders from this array. `group` lets
   the paper-doll layout flank the figure; unknown groups simply append.   */
const EQUIP_SLOTS = [
  { id: 0, key: 'helm',   label: 'Helm',      accepts: [1], group: 'armor' },
  { id: 1, key: 'chest',  label: 'Chest',     accepts: [2], group: 'armor' },
  { id: 2, key: 'legs',   label: 'Legs',      accepts: [3], group: 'armor' },
  { id: 3, key: 'boots',  label: 'Boots',     accepts: [4], group: 'armor' },
  { id: 4, key: 'weapon', label: 'Weapon',    accepts: [5], group: 'arms'  },
  { id: 5, key: 'acc',    label: 'Accessory', accepts: [6], group: 'arms'  },
];

/* Rarity drives the grid-cell BORDER COLOR (white→green→blue→purple→gold→red)
   and the glow intensity (scales with level). Category is shown separately as
   a small corner icon, since the item's own art will carry it once real icons
   land. Six tiers — `mythic` (red) is the apex; add more by extending here. */
const RARITY = {
  common:    { label: 'Common',    level: 1, color: '#d4d4d4', glow: 0.0 },
  uncommon:  { label: 'Uncommon',  level: 2, color: '#86c98f', glow: 0.22 },
  rare:      { label: 'Rare',      level: 3, color: '#a1c2f7', glow: 0.38 },
  epic:      { label: 'Epic',      level: 4, color: '#bfa2ec', glow: 0.54 },
  legendary: { label: 'Legendary', level: 5, color: '#e8c878', glow: 0.72 },
  mythic:    { label: 'Mythic',    level: 6, color: '#e8806f', glow: 0.9 },
};

/* Geometric category glyphs — stand-ins for the game's real item icons
   (item.iconPath). Stroke is set by the consumer to the category accent. */
const CATEGORY_GLYPH = {
  1: 'M3.5 8.5C3.5 5 5.5 3 8 3s4.5 2 4.5 5.5v3h-9zM3.5 11.5h9M6 11.5v1.5M10 11.5v1.5', // helm
  2: 'M4 4l-1.5 2 1.5 1.5v4.5h8V7.5L13.5 6 12 4 9.5 5h-3zM6.5 4.5C6.5 5.5 7 6 8 6s1.5-.5 1.5-1.5', // chest
  3: 'M5 2.5h6l-.5 5L11 13H9l-1-4-1 4H5l.5-5.5z', // legs
  4: 'M5 2.5h3v6l4.5 2v2.5H5z M5 9h3', // boot
  5: 'M11.5 2l2 .5.5 2-6 6-2.5-2.5zM5 8.5L2.5 12 4 13.5 7.5 11', // weapon (blade)
  6: 'M8 3.5l4.5 3.2-1.7 5.3H5.2L3.5 6.7zM8 7.2v0.01', // accessory (gem)
};

/* ─── Unlocked mod pool ─────────────────────────────────────────────────
   Mods the player has unlocked. itemModTypeId matches EItemModType
   (1 Component, 2 Prefix, 3 Suffix). A mod slots only into a matching slot
   type. `removable:false` mods can't be pulled back out once applied.      */
const MODS = {
  201: { id: 201, name: 'Iron Core',   itemModTypeId: 1, removable: false, description: 'Reduce durability loss by 20%.',          attributes: [{ name: 'Defense', value: 4 }] },
  202: { id: 202, name: 'Hollow Core', itemModTypeId: 1, removable: true,  description: 'Lighter frame. +6% dodge, −2 defense.',     attributes: [{ name: 'Dodge Chance', value: 6, suffix: '%' }, { name: 'Defense', value: -2 }] },
  203: { id: 203, name: 'Runed Core',  itemModTypeId: 1, removable: true,  description: '+8 Intellect to the bearer.',               attributes: [{ name: 'Intellect', value: 8 }] },
  211: { id: 211, name: 'Honed',       itemModTypeId: 2, removable: true,  description: '+15% damage to the first hit each fight.',   attributes: [{ name: 'Critical Damage', value: 15, suffix: '%' }] },
  212: { id: 212, name: 'Tempered',    itemModTypeId: 2, removable: true,  description: '+10 Endurance.',                            attributes: [{ name: 'Endurance', value: 10 }] },
  213: { id: 213, name: 'Savage',      itemModTypeId: 2, removable: true,  description: '+7% critical chance.',                      attributes: [{ name: 'Critical Chance', value: 7, suffix: '%' }] },
  214: { id: 214, name: 'Swift',       itemModTypeId: 2, removable: true,  description: '+5 Agility.',                               attributes: [{ name: 'Agility', value: 5 }] },
  221: { id: 221, name: 'Vampiric',    itemModTypeId: 3, removable: true,  description: 'Heal 8% of damage dealt.',                  attributes: [{ name: 'Luck', value: 4 }] },
  222: { id: 222, name: 'of Warding',  itemModTypeId: 3, removable: true,  description: '+6% block chance.',                         attributes: [{ name: 'Block Chance', value: 6, suffix: '%' }] },
  223: { id: 223, name: 'of the Bear', itemModTypeId: 3, removable: false, description: '+40 Max Health.',                          attributes: [{ name: 'Max Health', value: 40 }] },
  224: { id: 224, name: 'of Fortune',  itemModTypeId: 3, removable: true,  description: '+12% drop bonus.',                          attributes: [{ name: 'Drop Bonus', value: 12, suffix: '%' }] },
};

/* Build a mod-slot list of the given shape. Helper keeps item defs terse. */
let __slotSeq = 1;
const slots = (...types) => types.map((t) => ({ id: __slotSeq++, type: t }));

/* ─── Unlocked items (one copy each) ────────────────────────────────────
   `applied` maps a mod-slot id → mod id (the persisted appliedMods shape,
   flattened for the prototype). `equipSlot` (when set) is the equipment
   slot this item currently occupies.                                      */
const ITEMS = [
  // Helms (cat 1)
  { id: 1,  name: 'Aegis Greathelm',     cat: 1, rarity: 'epic',      fav: true,  equipSlot: 0,
    desc: 'Forged to turn aside even a giant\u2019s blow.', attrs: [{ name: 'Defense', value: 14 }, { name: 'Endurance', value: 8 }],
    modSlots: slots(1, 2), applied: {} },
  { id: 2,  name: 'Hood of Whispers',    cat: 1, rarity: 'rare',
    desc: 'The fabric drinks sound.', attrs: [{ name: 'Agility', value: 6 }, { name: 'Dodge Chance', value: 4, suffix: '%' }],
    modSlots: slots(2), applied: {} },
  { id: 3,  name: 'Ironscale Coif',      cat: 1, rarity: 'uncommon',
    desc: 'Standard issue, dependable.', attrs: [{ name: 'Defense', value: 7 }],
    modSlots: slots(1), applied: {} },
  { id: 4,  name: 'Circlet of Insight',  cat: 1, rarity: 'rare',
    desc: 'Cool against the brow.', attrs: [{ name: 'Intellect', value: 11 }],
    modSlots: slots(2, 3), applied: {} },

  // Chest (cat 2)
  { id: 5,  name: 'Bulwark Cuirass',     cat: 2, rarity: 'legendary', fav: true,  equipSlot: 1,
    desc: 'A wall you can wear.', attrs: [{ name: 'Defense', value: 22 }, { name: 'Max Health', value: 30 }, { name: 'Agility', value: -3 }],
    modSlots: slots(1, 2, 3), applied: { /* slot 8->Iron Core, slot 9->Tempered */ } },
  { id: 6,  name: 'Shadeweave Vest',     cat: 2, rarity: 'epic',
    desc: 'Light as a held breath.', attrs: [{ name: 'Dodge Chance', value: 9, suffix: '%' }, { name: 'Agility', value: 7 }],
    modSlots: slots(2, 3), applied: {} },
  { id: 7,  name: 'Padded Jerkin',       cat: 2, rarity: 'common',
    desc: 'Better than nothing.', attrs: [{ name: 'Defense', value: 4 }],
    modSlots: slots(1), applied: {} },
  { id: 8,  name: 'Mantle of Embers',    cat: 2, rarity: 'rare',
    desc: 'Warm to the touch.', attrs: [{ name: 'Intellect', value: 9 }, { name: 'Max Health', value: 18 }],
    modSlots: slots(2), applied: {} },

  // Legs (cat 3)
  { id: 9,  name: 'Titan Greaves',       cat: 3, rarity: 'epic',      equipSlot: 2,
    desc: 'Each step a foundation.', attrs: [{ name: 'Defense', value: 12 }, { name: 'Block Chance', value: 5, suffix: '%' }],
    modSlots: slots(1, 3), applied: {} },
  { id: 10, name: 'Wanderer\u2019s Leggings', cat: 3, rarity: 'uncommon',
    desc: 'Worn soft by long roads.', attrs: [{ name: 'Agility', value: 5 }],
    modSlots: slots(2), applied: {} },
  { id: 11, name: 'Runed Legplates',     cat: 3, rarity: 'rare',
    desc: 'Glyphs hum faintly.', attrs: [{ name: 'Intellect', value: 7 }, { name: 'Defense', value: 6 }],
    modSlots: slots(2, 3), applied: {} },

  // Boots (cat 4)
  { id: 12, name: 'Fleetfoot Boots',     cat: 4, rarity: 'rare',      fav: true, equipSlot: 3,
    desc: 'They want to run.', attrs: [{ name: 'Agility', value: 9 }, { name: 'Dodge Chance', value: 5, suffix: '%' }],
    modSlots: slots(2), applied: {} },
  { id: 13, name: 'Stonetread Sabatons', cat: 4, rarity: 'uncommon',
    desc: 'Unmoved by shifting ground.', attrs: [{ name: 'Defense', value: 8 }, { name: 'Block Chance', value: 3, suffix: '%' }],
    modSlots: slots(1), applied: {} },
  { id: 14, name: 'Worn Travel Shoes',   cat: 4, rarity: 'common',
    desc: 'Comfortable, if plain.', attrs: [{ name: 'Agility', value: 3 }],
    modSlots: [], applied: {} },

  // Weapons (cat 5)
  { id: 15, name: 'Aegis-Forged Saber',  cat: 5, rarity: 'legendary', fav: true, equipSlot: 4,
    desc: 'A blade forged in the Aegis foundry. Whispers when drawn.',
    attrs: [{ name: 'Strength', value: 18 }, { name: 'Dexterity', value: 12 }, { name: 'Critical Chance', value: 5, suffix: '%' }, { name: 'Defense', value: -2 }],
    modSlots: slots(1, 2, 3), applied: {} },
  { id: 16, name: 'Reaver\u2019s Cleaver', cat: 5, rarity: 'epic',
    desc: 'Heavy, hungry, honest.', attrs: [{ name: 'Strength', value: 22 }, { name: 'Critical Damage', value: 10, suffix: '%' }, { name: 'Agility', value: -4 }],
    modSlots: slots(2, 3), applied: {} },
  { id: 17, name: 'Whisperthorn Dagger', cat: 5, rarity: 'rare',
    desc: 'Quick as a rumor.', attrs: [{ name: 'Dexterity', value: 15 }, { name: 'Critical Chance', value: 8, suffix: '%' }],
    modSlots: slots(2), applied: {} },
  { id: 18, name: 'Emberbrand Mace',     cat: 5, rarity: 'epic',
    desc: 'Glows when blood is near.', attrs: [{ name: 'Strength', value: 16 }, { name: 'Intellect', value: 8 }],
    modSlots: slots(1, 2), applied: {} },
  { id: 19, name: 'Rusted Shortsword',   cat: 5, rarity: 'common',
    desc: 'It has seen better decades.', attrs: [{ name: 'Strength', value: 6 }],
    modSlots: slots(2), applied: {} },
  { id: 20, name: 'Glassfang Rapier',    cat: 5, rarity: 'rare',
    desc: 'Beautiful and brittle.', attrs: [{ name: 'Dexterity', value: 13 }, { name: 'Critical Damage', value: 12, suffix: '%' }],
    modSlots: slots(2, 3), applied: {} },

  // Accessories (cat 6)
  { id: 21, name: 'Band of the Fox',     cat: 6, rarity: 'epic',      fav: true,
    desc: 'Luck favors the cunning.', attrs: [{ name: 'Luck', value: 9 }, { name: 'Critical Chance', value: 4, suffix: '%' }],
    modSlots: slots(3), applied: {} },
  { id: 22, name: 'Amulet of Vigor',     cat: 6, rarity: 'rare',      equipSlot: 5,
    desc: 'A steady, warm pulse.', attrs: [{ name: 'Max Health', value: 35 }, { name: 'Endurance', value: 6 }],
    modSlots: slots(2, 3), applied: {} },
  { id: 23, name: 'Sigil of Haste',      cat: 6, rarity: 'epic',
    desc: 'Time bends, just slightly.', attrs: [{ name: 'Cooldown Recovery', value: 11, suffix: '%' }],
    modSlots: slots(3), applied: {} },
  { id: 24, name: 'Copper Trinket',      cat: 6, rarity: 'common',
    desc: 'Sentimental, mostly.', attrs: [{ name: 'Luck', value: 2 }],
    modSlots: [], applied: {} },
  { id: 25, name: 'Eye of the Storm',    cat: 6, rarity: 'mythic',
    desc: 'It watches the horizon for you.', attrs: [{ name: 'Intellect', value: 12 }, { name: 'Critical Damage', value: 14, suffix: '%' }, { name: 'Luck', value: 6 }],
    modSlots: slots(1, 2, 3), applied: {} },
  { id: 26, name: 'Charm of Warding',    cat: 6, rarity: 'rare',
    desc: 'Quietly protective.', attrs: [{ name: 'Block Reduction', value: 8, suffix: '%' }, { name: 'Defense', value: 5 }],
    modSlots: slots(3), applied: {} },
  { id: 27, name: 'Pendant of Focus',    cat: 6, rarity: 'uncommon',
    desc: 'Clears the mind.', attrs: [{ name: 'Intellect', value: 6 }],
    modSlots: slots(2), applied: {} },
  { id: 28, name: 'Knucklebone Ring',    cat: 6, rarity: 'uncommon',
    desc: 'A gambler\u2019s keepsake.', attrs: [{ name: 'Luck', value: 5 }],
    modSlots: slots(3), applied: {} },
];

/* Seed a couple of pre-applied mods so the mod UI shows real state.
   Maps by finding the right slot ids on those items. */
(function seedAppliedMods() {
  const cuirass = ITEMS.find((i) => i.id === 5);
  if (cuirass) {
    const comp = cuirass.modSlots.find((s) => s.type === 1);
    const pre = cuirass.modSlots.find((s) => s.type === 2);
    if (comp) cuirass.applied[comp.id] = 201; // Iron Core
    if (pre) cuirass.applied[pre.id] = 212;    // Tempered
  }
  const saber = ITEMS.find((i) => i.id === 15);
  if (saber) {
    const suf = saber.modSlots.find((s) => s.type === 3);
    if (suf) saber.applied[suf.id] = 221; // Vampiric
  }
})();

/* ─── Derived helpers ───────────────────────────────────────────────── */

// Which equip slots can accept this item (by category). Returns slot ids.
function slotsForItem(item) {
  return EQUIP_SLOTS.filter((s) => s.accepts.includes(item.cat)).map((s) => s.id);
}
// The default slot click/Ctrl-click equips into (first compatible free one,
// else first compatible).
function defaultSlotForItem(item, equippedBySlot) {
  const candidates = EQUIP_SLOTS.filter((s) => s.accepts.includes(item.cat));
  const free = candidates.find((s) => !equippedBySlot[s.id]);
  return (free || candidates[0])?.id;
}
// Unlocked mods compatible with a given slot type, excluding ones already on
// this item.
function compatibleMods(slotType, item) {
  const used = new Set(Object.values(item.applied || {}));
  return Object.values(MODS).filter((m) => m.itemModTypeId === slotType && !used.has(m.id));
}

const SORTS = {
  name:     { label: 'Name',     cmp: (a, b) => a.name.localeCompare(b.name) },
  category: { label: 'Category', cmp: (a, b) => a.cat - b.cat || a.name.localeCompare(b.name) },
};

Object.assign(window, {
  EQUIP_SLOTS, RARITY, CATEGORY_GLYPH, MODS, ITEMS, SORTS,
  slotsForItem, defaultSlotForItem, compatibleMods,
});
