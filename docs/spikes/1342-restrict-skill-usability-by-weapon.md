# Spike #1342 — Restrict skill usability by equipped weapon

- **Spike issue:** [#1342](https://github.com/ginderjeremiah/GameServer/issues/1342)
- **Status:** Design decided (ideation complete); pursue. Implementation split into native sub-issues (see [Implementation issues](#implementation-issues)). Builds additively on the weapon-type damage leaves shipped in [#1340](https://github.com/ginderjeremiah/GameServer/issues/1340).
- **Related:** surfaced by the static weapon-typing decision in the proficiency-avenues spike ([#1318](https://github.com/ginderjeremiah/GameServer/issues/1318), [`docs/spikes/1318-proficiency-progression-avenues.md`](./1318-proficiency-progression-avenues.md), decision 10). Composes with item-granted skills ([#980](https://github.com/ginderjeremiah/GameServer/issues/980)) and the proficiency gear-gate ([#1124](https://github.com/ginderjeremiah/GameServer/issues/1124)).

## Goal

Static weapon typing (#1318 decision 10 / #1340) gives a martial skill a **fixed** weapon-leaf damage type (`Sword`, `Axe`, …) for proficiency routing and art/animation identity, *independent of the equipped weapon*. That opens a coherence question the project never pinned down: may a `Sword`-typed skill be **fielded while wielding an Axe**, and more broadly, how do **skills, the equipped weapon, and the loadout** relate?

The conclusion is to **pursue a hard usability gate**, but a **non-destructive, setup-time** one that composes with the coherence machinery already in the game rather than inventing new battle logic. The gate makes the **weapon slot a real loadout constraint** without putting anything new on the deterministic tick loop and without ever mutating a player's saved loadout.

## The framing that drove the design

> A weapon-leaf-typed skill is **fielded only when the matching weapon is equipped**, enforced where the battler's skill set is *assembled from the snapshot* — not in the loadout command and not in the tick loop. "No weapon" is the **virtual `Unarmed` "fists" weapon** whose signature is punch, so the gate is one uniform rule across real weapons and bare hands.

Two properties are deliberately kept. The gate is **non-destructive** — the saved selected-skills loadout is untouched by weapon swaps; mismatched skills go *dormant* (greyed) and light back up when a matching weapon returns. And it adds **no new RNG-coupled parity surface** — it is a pure function of the battle snapshot, computed once at battler assembly in the already-shared `BattleLoadout` code, not per tick.

## Current state (what already exists)

- **Three coherence mechanisms already ship.** (a) **Item-granted skills** — an `Item.GrantedSkillId` is innate and cap-exempt, assembled in `BattleSnapshot.ToBattler` via the shared `BattleLoadout.OrderSkillIds` (selected, then grants in `EEquipmentSlot` order, `Distinct`), mirrored tick-for-tick on the frontend. Combined with the `ESkillAcquisition.Item` flag, this *implicitly* enforces weapon-skill coherence for **signature** skills (an `Item`-only skill can only be fielded by equipping its weapon, and vanishes on swap). (b) The **proficiency gear-gate** (`Item.RequiredProficiencyId` / `Level`, `MeetsProficiencyRequirement`) — an equip-time anti-cheat gate with no battle-sim surface. (c) **Static weapon-leaf typing** on skills (#1340).
- **Weapon-type damage leaves shipped** (#1340): `EDamageType` carries `Sword` / `Axe` / `Bow` / `Club` / `Dagger` / `Unarmed`, each with `applies() = [<weapon>, Physical]` and an amplification attribute. These route weapon proficiency masteries and the "+sword damage" build identity.
- **The decisive gap:** an equipped weapon carries **no weapon type**. A weapon is just `Item { Category = WeaponSlot }`; `WeaponType` lives only on the damage taxonomy (hence on skills, for routing). So today there is *nothing on the equipped weapon* for a skill's static type to match against — the gate's one true prerequisite.
- **Loadout validation** (`Player.TrySetSelectedSkills`) enforces cap / no-duplicate / unlocked-only as anti-cheat, and is **left unchanged** by this spike (the gate is contextual and non-destructive, not a save-time rule).

## Directions explored

The issue framed gating along two axes: *whether* to gate, and *at which layer*.

- **No gate (cosmetic / economy only).** Rely on the implicit item-grant coherence plus the weapon-amplification economy (a `Sword` skill only benefits from `SwordAmplification`). **Rejected:** leaning on amplification for coherence forces a coercively large amp differential (a "don't off-weapon" tax that warps the amp economy) instead of letting amplification be a modest build-flavour knob; and it leaves the weapon slot mechanically inert for martial builds.
- **Activation-time gate.** Skill stays slotted but is skipped/zeroed when the weapon mismatches. **Rejected:** this injects weapon-match logic into the deterministic, RNG-coupled tick loop — a new frontend↔backend parity vector, exactly what #1318's accrual rework went out of its way to avoid.
- **Selection / setup-time gate (chosen).** The fielded set is filtered by weapon match at **battler assembly** (a pure function of the snapshot, in already-shared `BattleLoadout` code), authoritative on the backend and mirrored on the frontend, with the UI greying mismatched skills and warning before a swap. This is the same *category* of shared pure logic as the existing dedupe/order rule — not a tick-loop surface — so the parity concern that rules out activation-time gating does not apply here.

**Weapon-swap behaviour** was the deciding sub-question: **grey-out (dormant), not auto-unequip.** Auto-unequipping silently empties a loadout on every swap — hostile idle-game UX. Keeping the saved loadout intact and merely dimming the off-weapon skills (with an explicit pre-swap warning) is non-destructive and legible.

## Decisions

Settled with the project owner during the spike:

1. **Pursue a hard gate, enforced at setup, non-destructive.** A weapon-leaf-typed skill is **fielded iff** `skill.DamageType == equippedWeaponType`. Enforced at **battler assembly** (`BattleLoadout` / `BattleSnapshot.GetBattleSkillIds`), authoritative on the backend, mirrored on the frontend. The saved loadout and `TrySetSelectedSkills` are **untouched**; mismatched selected skills are dormant, not removed.

2. **Only weapon-leaf-typed skills are gated.** Generic `Physical`, the elementals, the DoT leaves, and caster types are **weapon-agnostic** and never filtered. This answers the issue's "is a staff a weapon-damage source" bullet (**no** — a staff's Fireball is a `Fire` skill, granted `Item`-only by the staff, already coherent) and keeps **hybrid kits** frictionless (a sword + two fire skills: only the sword skills dim on a swap).

3. **Reuse `EDamageType` for the weapon's type — no parallel `EWeaponType`.** Add `Item.WeaponType : EDamageType?` constrained to a weapon leaf. #1340 deliberately unified "weapon type" with "weapon-leaf damage type"; a separate enum would re-bifurcate it. The match is the plain symmetry `skill.DamageType == weapon.WeaponType`.

4. **Empty hands is the virtual `Unarmed` "fists" weapon.** The equipped weapon type resolves as `weapon?.WeaponType ?? Unarmed`, and the weapon's granted signature as `weapon?.GrantedSkillId ?? PunchSkillId`. So **punch is the fists' signature** — fielded only bare-handed and `Unarmed`-typed. A real `Unarmed` weapon (e.g. brass knuckles) **replaces** the fists and fields *its own* signature instead, so it gains **no free bonus skill** — symmetric with how a sword fields the sword's signature. `Unarmed`-*typed selected* skills work both bare-handed and with an `Unarmed` weapon (the weapon reads as "enhanced fists").

5. **The gate applies uniformly to selected *and* granted skills.** One rule, in the shared assembly. A weapon's own signature matches by authoring (no-op); the uniform rule additionally covers the oddity of a **non-weapon item granting a weapon-typed skill** (which is dormant unless the matching weapon is held) — not a planned content pattern, but cheaply enforced for free.

6. **Punch trains the `Unarmed` mastery.** Now that punch is an ordinary `Unarmed` signature, it routes XP to `Unarmed` (+ `Martial`) in proportion to its (small) damage. The former "path-less punch that earns no XP **so it doesn't dilute the pie**" carve-out (Classes section) is a **representation-model artifact** — #1318's effect-based accrual removed the shared pie, so there is nothing to dilute. The carve-out is dropped (and is a pre-existing doc contradiction this spike corrects). Bonus: a bare-knuckle/monk build now trains its mastery from level one.

7. **Every weapon grants a signature of its own type (the no-stranding invariant).** A weapon's own-type signature is what guarantees the player always fields **≥1 usable skill** even when every selected skill is dimmed. Enforced, not merely conventional: **admin save requires a `WeaponSlot` item to declare both a `WeaponType` and a `GrantedSkillId`.** Tradeoff (intended): no pure stat-stick weapons. The virtual fists satisfy this for the bare-handed case via punch.

8. **Amplification is decoupled from coherence.** With a hard gate enforcing it structurally, the weapon-amplification differential no longer has to coerce on-weapon play and can be tuned purely as build flavour.

## The gate

```
equippedWeaponType  = weapon?.WeaponType   ?? Unarmed          // empty slot = virtual "fists"
equippedSignatureId = weapon?.GrantedSkillId ?? PunchSkillId   // fists' signature is punch

fielded(skill) =
    isWeaponLeaf(skill.DamageType)                              // Sword/Axe/Bow/Club/Dagger/Unarmed
        ? skill.DamageType == equippedWeaponType                // exact leaf-match
        : true                                                  // Physical/elemental/DoT/caster: agnostic
```

Applied at battler assembly over `selected ++ granted` (the existing `Distinct` order), on the backend (authoritative, re-simulated from the snapshot — **no new snapshot field**, weapon type and grant are derived from the already-resolved equipped item) and mirrored on the frontend. Setup-time only; not in the tick loop.

| Equipped | Free signature | Selected skills usable |
| --- | --- | --- |
| Bare hands (virtual `Unarmed` fists) | **punch** | `Unarmed` + agnostic |
| Brass knuckles (`Unarmed` weapon) | its own | `Unarmed` + agnostic |
| Sword | its own | `Sword` + agnostic |

**Weapon class** (the issue's parenthetical — a skill usable by a *group* of weapons, e.g. bladed = {Sword, Dagger}) is **deferred**: V1 is exact leaf-match. A class grouping is a category over weapon leaves that the existing `applies()` category-key machinery can express later with no rework.

## Implementation issues

Created as native sub-issues of #1342:

| Issue | Area | Depends on | Scope |
| --- | --- | --- | --- |
| [#1372](https://github.com/ginderjeremiah/GameServer/issues/1372) — `Item.WeaponType` + the "weapon grants a signature" authoring invariant | reference data / authoring | shipped #1340 | medium |
| [#1373](https://github.com/ginderjeremiah/GameServer/issues/1373) — Weapon-match gate in the shared battle-loadout assembly (+ virtual-fists punch) | engine | #1372 | medium |
| [#1374](https://github.com/ginderjeremiah/GameServer/issues/1374) — Skills-screen grey-out + pre-swap weapon warning | frontend | #1373 | medium |
| [#1375](https://github.com/ginderjeremiah/GameServer/issues/1375) — Docs update for the weapon-skill usability gate | docs | #1372–#1374 | small |

Shape: **#1372** adds the one prerequisite (the weapon's type + the no-stranding invariant) and independently unblocks the planned "kills with a weapon type" statistic; **#1373** is the gate itself, in the already-shared and parity-tested assembly path, plus the virtual-fists punch; **#1374** is the player-facing grey-out + the explicit pre-swap warning (non-authoritative); **#1375** is the documentation surface. No issue adds a tick-loop parity vector; each carries unit/integration (and, for #1373, FE↔BE parity) tests.

## Documentation to update on landing

- `docs/game-design.md` — **Skills** / **Damage types**: weapon-leaf-typed skills are usable only with the matching weapon equipped (exact leaf-match), weapon-agnostic types unaffected, gate enforced at assembly and non-destructive; document `Item.WeaponType`, the virtual-fists/punch model, and the weapon-grants-a-signature invariant. **Classes**: correct the punch carve-out (now an `Unarmed` innate that trains its mastery; the "no XP so it doesn't dilute the pie" rationale is obsolete under #1318). (With #1375.)
- `docs/backend.md` — the weapon-match filter in the loadout-assembly section (authoritative, setup-time, no new snapshot field) and the admin weapon-type validation (`WeaponSlot` item must declare a `WeaponType` + a `GrantedSkillId`). (With #1372, #1373.)
- `docs/frontend-screens.md` — the Skills-screen grey-out of off-weapon skills and the pre-swap weapon warning. (With #1374.)

## Out of scope / deferred

- **Weapon class / multi-weapon usability** — exact leaf-match in V1; a "bladed/blunt/ranged" grouping is a later category-key generalization.
- **Per-weapon resistance** — unchanged from #1318: weapon leaves stay amplification-only; a weapon hit resolves to `PhysicalResistance` through the shared key.
- **Auto-unequip / activation-time gating** — rejected (UX and parity respectively); recorded here so they are not re-litigated.
