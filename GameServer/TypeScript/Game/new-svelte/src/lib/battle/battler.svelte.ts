import { newSkill, Skill } from "./skill";
import { BattleAttributes, newBattleAttributes } from "./battle-attributes";
import { IBattlerAttribute, EAttribute } from "$lib/api";
import { staticData } from "$stores";

interface BattlerData {
    attributes: IBattlerAttribute[];
    selectedSkills: number[];
    level: number;
    name: string;
}

export interface Battler {
    id: number;
    name: string;
    level: number;
    currentHealth: number;
    attributes: BattleAttributes;
    skills: (Skill | undefined)[];
    maxSkills: typeof maxSkills;
    cdMultiplier: number;
    advanceCooldowns(timeDelta: number): Skill[];
    takeDamage(rawDamage: number): number;
    updateRenderCooldowns(renderDelta: number): void;
    isDead: boolean;
    reset(battlerData: { attributes: IBattlerAttribute[], selectedSkills: number[], level: number }, additionalAtttributes?: IBattlerAttribute[]): void;
}

const maxSkills = 4;
let id = 0;

const fillSelectedSkills = (battlerData: BattlerData, battler: Battler) => {
    const skillData = staticData.skills;
    const skills: (Skill | undefined)[] = battlerData.selectedSkills.map((skillId) => newSkill(skillData[skillId], battler));
    while (skills.length < maxSkills) {
        skills.push(undefined);
    };
    return skills;
}

export const newBattler = (battlerData: BattlerData, additionalAtttributes?: IBattlerAttribute[]) => {
    const atts = additionalAtttributes ? [...battlerData.attributes, ...additionalAtttributes] : battlerData.attributes;
    let attributes = $state(newBattleAttributes(atts));
    let currentHealth = $state(attributes.getValue(EAttribute.MaxHealth));
    let skills = $state<(Skill | undefined)[]>([]);
    let level = $state(battlerData.level);
    let name = $state(battlerData.name);
    const cdMultiplier = $derived(1 + (attributes.getValue(EAttribute.CooldownRecovery) / 100));
    const isDead = $derived(currentHealth <= 0);
    const advanceCooldowns = (timeDelta: number) => {
        const firedSkills: Skill[] = [];
        for (const skill of skills) {
            if (skill) {
                skill.chargeTime += timeDelta * cdMultiplier;
                if (skill.chargeTime >= skill.cooldownMS) {
                    firedSkills.push(skill);
                    skill.chargeTime = 0;
                }
            }
        }
        return firedSkills;
    };
    const updateRenderCooldowns = (renderDelta: number) => {
        for (const skill of skills) {
            if (skill) {
                skill.renderChargeTime = Math.min(skill.chargeTime + (renderDelta * cdMultiplier), skill.cooldownMS);
            }
        }
    }
    const takeDamage = (rawDamage: number) => {
        let damage = rawDamage - attributes.getValue(EAttribute.Defense);
        if (damage < 0) {
            damage = 0;
        }
        currentHealth -= damage;
        return damage;
    };
    const battler = {
        id,
        get level() {
            return level;
        },
        get name() {
            return name;
        },
        get currentHealth() {
            return currentHealth;
        },
        get attributes() {
            return attributes;
        },
        get skills() {
            return skills;
        },
        maxSkills,
        get cdMultiplier() {
            return cdMultiplier;
        },
        get isDead() {
            return isDead;
        },
        advanceCooldowns,
        updateRenderCooldowns,
        takeDamage
    } as Battler;

    id++;
    skills = fillSelectedSkills(battlerData, battler);
    battler.reset = (battlerData: BattlerData, additionalAtttributes?: IBattlerAttribute[]) => {
        const atts = additionalAtttributes ? [...battlerData.attributes, ...additionalAtttributes] : battlerData.attributes;
        attributes = newBattleAttributes(atts);
        currentHealth = attributes.getValue(EAttribute.MaxHealth);
        level = battlerData.level;
        skills = fillSelectedSkills(battlerData, battler);
    };

    return battler;
}