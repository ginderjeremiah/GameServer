# Battle Mechanics

Battling is the core mechanic of the game, and it is designed to be a continuous and engaging experience for players. Players will idle in different zones, automatically fighting monsters that spawn in those zones. During battle players and enemies will automatically use their skills, until one side is defeated or a timeout is reached (or the player changes zones). Players will earn experience points and progress towards Challenges as they battle.

It is important to note that the game is designed to replicate battles on both the frontend and backend as an anti-cheat measure. The backend is the source of truth for all game logic and state, but the frontend also simulates battles in real-time to provide a responsive and engaging user experience. This means that all battle logic must be implemented in both the frontend and backend, the results must be consistent between the two, and the backend must be able to simulate the battle very quickly (performance matters significantly there).

There are some choices already made in the design of the game to support this:

- All battle RNG MUST use the same implementation (currently Mulberry32) that can be seeded with the same value on both the frontend and backend to ensure consistent results.
- The "state" of a player is snapshot when combat starts. This means that all relevant player stats, items, and skills are included in the combat state, and no external factors will influence the result of the battle once it has started. Since the backend does not simulate the battle until AFTER it is resolved in the frontend, this ensures that the battle will be consistent regardless of any changes that may occur to the player's state during combat.
- The battle simulation "runs" in 40ms ticks, meaning that all actions are processed in discrete intervals of 40ms. This allows for a consistent simulation and is a reasonable approach for a performant simulation on the backend while still providing a responsive experience on the frontend. The frontend will contain additional logic to interpolate values between ticks for smoother animations, but the core battle logic will be processed in these discrete ticks.

# Character Progression and Attributes

Character progression in the game is primarily driven by leveling up and allocating stat points. As players defeat monsters and complete challenges, they earn experience points (XP) that contribute to their character's level. When a player levels up, they receive stat points that can be allocated to core Attributes such as Strength, Agility, Intelligence, etc.

Attributes are somewhat of a work-in-progress feature, but the general idea is that they will provide various bonuses and effects that influence combat, other Attributes, and potentially other mechanics in the game. Currently they come from stat allocations and equipment, but this will be expanded in the future. For example, a Skill may receive a damage bonus based on the player's Strength Attribute, and the player's MaxHealth Attribute will receive a bonus based on their Strength as well. Attributes are designed to go above and beyond the traditional RPG stats and provide a system for complex effects. For example, a Skill may cause a damage-over-time effect, which would be implemented by giving the target a bonus to their "DamageReceivedPerTick" or "PoisonDamagePerTick" Attribute for a certain duration. The battle simulation would then have logic implemented to apply that damage over time based on the value of that Attribute.

# Skills

Skills are a fairly underdeveloped feature in the game, but the general idea is that they will provide active abilities for players that are automatically used during battles. Skills currently only deal damage and are not unlockable or (un)equippable.

In the future, Skills will be expanded to include a wider variety of effects, such as buffs, debuffs, healing, and utility effects. They will also be unlockable through Challenges and potentially other means, and players will be able to equip a certain number of Skills to use in battle.

# Items, Item Mods, and Tags

In early versions of this game, items dropped randomly from enemies with random modifications based on matching tags. This tag system has data like: "Weapon", "Sharp", "Two-Handed", etc. An item would have a set of tags, and item modifications would also have tags. When an item dropped, the game would roll for modifications that had matching tags to the item.

This system was ultimately scrapped in favor of a more deterministic system where items and item modifications are unlocked through Challenges. This change was made to provide a more engaging and rewarding progression system for players, as well as to allow for more strategic decision-making when it comes to character builds and playstyles. The tag system still exists as a means of determining which item modifications can be applied to which items. For example, a "Sharpened" modification may only be applicable to items with the "Sharp" tag.

# Challenges and Statistics

Challenges and Statistics are relatively new additions to the game, and their design is still evolving. Challenges are a way for players to unlock new content and progression by completing specific tasks or reaching certain milestones in the game. They can range from simple objectives, like defeating a certain number of monsters, to more complex ones, like completing a zone without taking any damage.

Not all Challenges are tied to Statistics and vice versa, but many will be. Statistics are tracked metrics that can be used to gate content and progression in the game. For example, a Challenge may require a player to have a certain number of kills with a specific weapon type, which would be tracked as a Statistic.

## Challenge Goal Comparison Direction

Most challenges are *accumulating* goals: the tracked statistic increases over time and the challenge is completed once it reaches **at least** the goal (e.g. "defeat 100 enemies"). However, some statistics are minimized rather than maximized — for example, `FastestVictory` records the lowest victory time, where lower is better. A `TimeTrial` challenge ("win a battle within N seconds") is therefore satisfied when the tracked value is **at or below** the goal, which is the opposite comparison.

To handle both cases, each challenge type declares a goal comparison direction (`EChallengeGoalComparison`): `AtLeast` for accumulating goals and `AtMost` for minimization goals. This is intrinsic to the challenge type (derived in `ChallengeType`, not persisted) so the completion rule lives in one place. `PlayerChallenge.UpdateProgress` applies the appropriate comparison.

For `AtMost` goals, a tracked value of `0` is treated as "no data yet" (e.g. the player has not won a qualifying battle) and does **not** complete the challenge — otherwise a brand-new player with no victories would instantly satisfy every time trial. This matches how `FastestVictory` is stored, where `0` is the sentinel for "no victory recorded".

> **Note:** Progress for an `AtMost` challenge is stored as the player's current best value (e.g. their fastest victory time), not as a 0→goal accumulation. A future frontend change may need to interpret these challenges differently when rendering a progress indicator, since a lower value represents *better* progress.

# Logs

Logs here refer to messages displayed in the UI of the game for various events, such as combat actions, item drops, level-ups, and other significant occurrences. Logs are an important part of the user experience, as they provide feedback and information to players about what is happening in the game. Logs can be filtered by type (e.g., damage dealt, experience gained, level-ups) and are meant to be informative enough that the player can step away from the game and see if anything significant happened while they were away or to have a record of what happened during a battle. Logs should only ever be generated on the frontend, as they are purely a UI element and do not affect any game logic or state. The backend should not be concerned with these kinds of logs, as its primary responsibility is to maintain the integrity of the game state and logic.
