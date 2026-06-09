/* chal2-data.jsx — sample data for the TYPE-DRIVEN Challenges redesign.

   Faithful to the refactored backend model (Game.Api.Models.Progress):
     Challenge   { id, name, description, challengeTypeId (EChallengeType),
                   statisticType (EStatisticType?), entityType (EEntityType),
                   targetEntityId?, progressGoal (decimal),
                   rewardItemId?, rewardItemModId? }
     ChallengeType declares an intrinsic goalComparison: AtLeast for
                   accumulating goals, AtMost for minimization (TimeTrial).
     PlayerChallenge { challengeId, progress, completed, completedAt? }

   New since last pass:
   - ChallengeType is now a real, organizing concept (8 of them).
   - Item mods carry RARITY (same 6-tier ERarity as items) → MOD_RARITY below.
   - Drops are gone: every item/mod is UNLOCKED by a challenge. That fiction is
     why a locked reward is shown as a SEALED teaser (rarity + category) and is
     only revealed on completion.

   Rewards reference the same ITEMS / MODS pools the Inventory screen uses
   (inv-data.jsx) so a revealed reward's tooltip is identical to inspecting it
   in the bag — single source of truth.
*/

/* ─── EChallengeType (the organizing axis) ──────────────────────────────
   Matches Game.Core EChallengeType 1..8. `comparison` mirrors
   EChallengeGoalComparison (atLeast | atMost). `entity` mirrors the type's
   StatisticType.EntityType. `unit`/`fmt` drive the progress readout. */
const ECHALLENGE_TYPE = {
  EnemiesKilled: 1, BossesDefeated: 2, ZonesCleared: 3, TimeTrial: 4,
  LevelReached: 5, DamageDealt: 6, BattlesWon: 7, SkillsUsed: 8,
};

const CHALLENGE_TYPE_META = {
  1: { key: 'enemiesKilled',  label: 'Enemies Killed',  comparison: 'atLeast', entity: 'enemy', unit: 'kills',    accent: '#df8470', blurb: 'Cut down foes in the field.' },
  2: { key: 'bossesDefeated', label: 'Bosses Defeated', comparison: 'atLeast', entity: 'none',  unit: 'bosses',   accent: '#d9a441', blurb: 'Bring down the great threats.' },
  3: { key: 'zonesCleared',   label: 'Zones Cleared',   comparison: 'atLeast', entity: 'zone',  unit: 'zones',    accent: '#8bb060', blurb: 'Sweep a region clean.' },
  4: { key: 'timeTrial',      label: 'Time Trial',      comparison: 'atMost',  entity: 'none',  unit: 'time',     accent: '#9b8fd4', blurb: 'Win faster than the mark.' },
  5: { key: 'levelReached',   label: 'Level Reached',   comparison: 'atLeast', entity: 'none',  unit: 'level',    accent: '#6f9fd9', blurb: 'Grow in power.' },
  6: { key: 'damageDealt',    label: 'Damage Dealt',    comparison: 'atLeast', entity: 'skill', unit: 'damage',   accent: '#cf7a9e', blurb: 'Pour on the hurt.' },
  7: { key: 'battlesWon',     label: 'Battles Won',     comparison: 'atLeast', entity: 'enemy', unit: 'wins',     accent: '#5fae9a', blurb: 'Come out on top.' },
  8: { key: 'skillsUsed',     label: 'Skills Used',     comparison: 'atLeast', entity: 'skill', unit: 'casts',    accent: '#5aa6bd', blurb: 'Master your abilities.' },
};

/* Reference entities (subset) so target-scoped challenges read naturally
   ("Defeat 50 Goblins", "Clear the Forgotten Catacombs"). */
const ENTITY_NAMES = {
  enemy: { 1: 'Goblin', 2: 'Skeleton', 3: 'Wraith', 4: 'Ogre Brute', 5: 'Lich King' },
  zone:  { 1: 'Verdant Hollow', 2: 'Forgotten Catacombs', 3: 'Ashen Wastes', 4: 'Sunken Vault', 5: 'Frostspire' },
  skill: { 1: 'Cleave', 2: 'Firebolt', 3: 'Rend', 4: 'Mend' },
};

/* Item mods now carry rarity — augment the inv-data MODS pool by id. */
const MOD_RARITY = {
  201: 'uncommon', 202: 'rare', 203: 'epic', 211: 'rare', 212: 'uncommon',
  213: 'rare', 214: 'uncommon', 221: 'rare', 222: 'uncommon', 223: 'rare', 224: 'epic',
};

/* ─── Challenges (static reference data) ────────────────────────────────
   For TimeTrial (atMost), progressGoal is the TARGET time in seconds and a
   player's progress is their current BEST time (0 = no qualifying victory). */
const CHALLENGES = [
  // 1 · Enemies Killed
  { id: 1,  name: 'First Blood',     typeId: 1, goal: 10,    desc: 'Defeat your first 10 enemies.',                rewardItemId: 3 },
  { id: 2,  name: 'Goblin Bane',     typeId: 1, goal: 50,    targetEntityId: 1, desc: 'Defeat 50 Goblins.',        rewardItemModId: 213 },
  { id: 3,  name: 'Reaper',          typeId: 1, goal: 500,   desc: 'Defeat 500 enemies across all zones.',         rewardItemId: 25 },
  // 2 · Bosses Defeated
  { id: 4,  name: 'Giant Slayer',    typeId: 2, goal: 1,     desc: 'Defeat your first boss.',                      rewardItemId: 1 },
  { id: 5,  name: 'Bane of Kings',   typeId: 2, goal: 10,    desc: 'Defeat 10 bosses.',                            rewardItemId: 5 },
  // 3 · Zones Cleared
  { id: 6,  name: 'Catacomb Cleaner', typeId: 3, goal: 1,    targetEntityId: 2, desc: 'Clear the Forgotten Catacombs.', rewardItemModId: 211 },
  { id: 7,  name: 'Into the Deep',   typeId: 3, goal: 5,     desc: 'Clear 5 distinct zones.',                      rewardItemId: 12 },
  { id: 8,  name: 'World Walker',    typeId: 3, goal: 20,    desc: 'Clear zones 20 times over.',                   rewardItemId: 21 },
  // 4 · Time Trial (atMost — best time vs target)
  { id: 9,  name: 'Quicksilver',     typeId: 4, goal: 60,    desc: 'Win a battle in under a minute.',              rewardItemModId: 214 },
  { id: 10, name: 'Blink and Done',  typeId: 4, goal: 30,    desc: 'Win a battle in 30 seconds or less.',          rewardItemId: 17 },
  { id: 11, name: 'Untouched Record', typeId: 4, goal: 20,   desc: 'Win a battle in 20 seconds or less.',          rewardItemId: 15 },
  // 5 · Level Reached
  { id: 12, name: 'Apprentice',      typeId: 5, goal: 5,     desc: 'Reach character level 5.',                     rewardItemId: 19 },
  { id: 13, name: 'Veteran',         typeId: 5, goal: 15,    desc: 'Reach character level 15.',                    rewardItemModId: 223 },
  { id: 14, name: 'Ascendant',       typeId: 5, goal: 30,    desc: 'Reach character level 30.',                    rewardItemId: 23 },
  // 6 · Damage Dealt
  { id: 15, name: 'Heavy Hitter',    typeId: 6, goal: 10000, desc: 'Deal 10,000 total damage.',                    rewardItemModId: 212 },
  { id: 16, name: 'Annihilator',     typeId: 6, goal: 100000, desc: 'Deal 100,000 total damage.',                  rewardItemId: 16 },
  // 7 · Battles Won
  { id: 17, name: 'Victor',          typeId: 7, goal: 25,    desc: 'Win 25 battles.',                              rewardItemId: 13 },
  { id: 18, name: 'Unstoppable',     typeId: 7, goal: 100,   desc: 'Win 100 battles.',                             rewardItemModId: 221 },
  // 8 · Skills Used
  { id: 19, name: 'Tactician',       typeId: 8, goal: 100,   desc: 'Use skills 100 times in battle.',              rewardItemId: 10 },
  { id: 20, name: 'Spellweaver',     typeId: 8, goal: 1000,  targetEntityId: 2, desc: 'Cast Firebolt 1,000 times.', rewardItemModId: 203 },
];

/* Player progress, keyed by challengeId.
   TimeTrial progress = current best time in seconds (0 = no data). */
const PLAYER_CHALLENGES = {
  1:  { progress: 10,    completed: true,  completedAt: '3 days ago' },
  2:  { progress: 32,    completed: false },
  3:  { progress: 184,   completed: false },
  4:  { progress: 1,     completed: true,  completedAt: 'yesterday' },
  5:  { progress: 3,     completed: false },
  6:  { progress: 1,     completed: true,  completedAt: '5 days ago' },
  7:  { progress: 3,     completed: false },
  8:  { progress: 0,     completed: false },
  9:  { progress: 52,    completed: true,  completedAt: '2 days ago' }, // 52s ≤ 60s target
  10: { progress: 41,    completed: false },                            // 41s, target 30s
  11: { progress: 0,     completed: false },                            // no qualifying win
  12: { progress: 5,     completed: true,  completedAt: '6 days ago' },
  13: { progress: 12,    completed: false },
  14: { progress: 12,    completed: false },
  15: { progress: 6700,  completed: false },
  16: { progress: 6700,  completed: false },
  17: { progress: 25,    completed: true,  completedAt: 'today' },
  18: { progress: 41,    completed: false },
  19: { progress: 100,   completed: true,  completedAt: 'last week' },
  20: { progress: 340,   completed: false },
};

Object.assign(window, {
  ECHALLENGE_TYPE, CHALLENGE_TYPE_META, ENTITY_NAMES, MOD_RARITY,
  CHALLENGES, PLAYER_CHALLENGES,
});
