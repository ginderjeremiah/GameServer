/* chal-data.jsx — sample data + config for the redesigned Challenges screen.

   Mirrors src/.../Challenges + src/lib/api IChallenge / IPlayerChallenge:
     challenge: { id, name, description, challengeTypeId, targetEntityId?,
                  targetCount, rewardItemId?, rewardItemModId? }
     player:    { challengeId, progress, completed, completedAt? }

   The reward (an item OR an item-mod) is the thing we're "sprucing up": the
   name becomes an interactive, rarity-aware affordance that reveals the full
   item / mod tooltip on hover. Rewards reference the SAME pools the Inventory
   screen uses (ITEMS, MODS from inv-data.jsx) so a reward tooltip is identical
   to inspecting that item in the bag — single source of truth.

   challengeTypeId has no enum in the codebase yet, so we define a small set of
   plausible types here (Combat / Exploration / Progression / Collection /
   Mastery). Each carries a label + geometric glyph stand-in.
*/

/* ─── Challenge types (label + glyph) ───────────────────────────────────
   Glyphs are neutral geometric stand-ins (16×16, stroked) — same convention
   as the item category glyphs. */
const CHALLENGE_TYPES = {
  1: { key: 'combat',      label: 'Combat',      glyph: 'M11.5 2l2 .5.5 2-6 6-2.5-2.5zM5 8.5L2.5 12 4 13.5 7.5 11' },
  2: { key: 'exploration', label: 'Exploration', glyph: 'M8 1.6l1.7 4.7 4.7 1.7-4.7 1.7L8 14.4l-1.7-4.7L1.6 8l4.7-1.7z' },
  3: { key: 'progression', label: 'Progression', glyph: 'M3 9l5-5 5 5M3 13l5-5 5 5' },
  4: { key: 'collection',  label: 'Collection',  glyph: 'M3 6l5-2.5L13 6 8 8.5zM3 6v4.5l5 2.5M13 6v4.5l-5 2.5' },
  5: { key: 'mastery',     label: 'Mastery',     glyph: 'M8 1.6l1.9 3.9 4.3.6-3.1 3 .7 4.3L8 11.4 4.3 13.4l.7-4.3-3.1-3 4.3-.6z' },
};

/* Per-type accent — kept within the existing cool/warm/neutral family so the
   screen never invents new hues. Combat warm, exploration teal, progression
   blue, collection gold, mastery violet. */
const CHALLENGE_TYPE_ACCENT = {
  1: '#e08778', // combat — warm (matches weapon accent)
  2: '#9bc7d9', // exploration — teal (matches prefix accent)
  3: '#a1c2f7', // progression — blue (primary)
  4: '#e8c878', // collection — gold
  5: '#c0a8e6', // mastery — violet (matches suffix accent)
};

/* ─── Challenges (static) ──────────────────────────────────────────────
   `unit` is a short noun for the progress readout ("32 / 50 enemies"). */
const CHALLENGES = [
  { id: 1,  name: 'First Blood',        typeId: 1, targetCount: 10,  unit: 'enemies',
    description: 'Defeat your first 10 enemies in the field.',           rewardItemId: 3 },
  { id: 2,  name: 'Catacomb Cleaner',   typeId: 2, targetCount: 1,   unit: 'zone',
    description: 'Clear every encounter in the Forgotten Catacombs.',    rewardItemModId: 211 },
  { id: 12, name: 'Apprentice',         typeId: 3, targetCount: 5,   unit: 'levels',
    description: 'Reach character level 5.',                             rewardItemId: 19 },
  { id: 3,  name: 'Giant Slayer',       typeId: 1, targetCount: 50,  unit: 'enemies',
    description: 'Defeat 50 enemies across all zones.',                  rewardItemId: 1 },
  { id: 10, name: 'Critical Mass',      typeId: 1, targetCount: 100, unit: 'crits',
    description: 'Land 100 critical hits in combat.',                    rewardItemId: 21 },
  { id: 4,  name: 'Into the Deep',      typeId: 2, targetCount: 5,   unit: 'zones',
    description: 'Descend to Zone 5 and survive the arrival.',           rewardItemId: 12 },
  { id: 5,  name: 'Long Road',          typeId: 3, targetCount: 15,  unit: 'levels',
    description: 'Reach character level 15.',                            rewardItemModId: 223 },
  { id: 11, name: 'Completionist',      typeId: 3, targetCount: 10,  unit: 'challenges',
    description: 'Complete 10 challenges of any type.',                  rewardItemId: 15 },
  { id: 6,  name: 'Hoarder',            typeId: 4, targetCount: 20,  unit: 'items',
    description: 'Unlock 20 distinct items.',                            rewardItemId: 25 },
  { id: 7,  name: 'Tinkerer',           typeId: 5, targetCount: 5,   unit: 'mods',
    description: 'Install 5 mods into your gear.',                       rewardItemModId: 213 },
  { id: 8,  name: 'Untouchable',        typeId: 1, targetCount: 5,   unit: 'fights',
    description: 'Win 5 fights without taking a single hit.',            rewardItemId: 5 },
  { id: 9,  name: 'Quicksilver',        typeId: 2, targetCount: 1,   unit: 'run',
    description: 'Clear any zone in under sixty seconds.',               rewardItemModId: 224 },
];

/* ─── Player progress (keyed by challengeId) ───────────────────────────── */
const PLAYER_PROGRESS = {
  1:  { progress: 10,  completed: true,  completedAt: '2 days ago' },
  2:  { progress: 1,   completed: true,  completedAt: 'yesterday' },
  12: { progress: 5,   completed: true,  completedAt: '4 days ago' },
  3:  { progress: 32,  completed: false },
  10: { progress: 67,  completed: false },
  4:  { progress: 3,   completed: false },
  5:  { progress: 12,  completed: false },
  11: { progress: 4,   completed: false },
  6:  { progress: 18,  completed: false },
  7:  { progress: 2,   completed: false },
  8:  { progress: 0,   completed: false },
  9:  { progress: 0,   completed: false },
};

Object.assign(window, {
  CHALLENGE_TYPES, CHALLENGE_TYPE_ACCENT, CHALLENGES, PLAYER_PROGRESS,
});
