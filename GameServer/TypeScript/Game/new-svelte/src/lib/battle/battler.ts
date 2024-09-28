import { Skill } from "./skill";
import { BattleAttributes } from "./battle-attributes";
import { IBattlerAttribute, EAttribute } from "$lib/api";
import { skills } from "$stores";

interface BattlerData {
    attributes: IBattlerAttribute[];
    selectedSkills: number[];
    level: number;
    name: string;
}

export class Battler {
    name: string;
    level: number;
    currentHealth!: number;
    attributes!: BattleAttributes;
    skills!: (Skill | undefined)[];
    maxSkills = 4;

    get cdMultiplier() {
        return 1 + (this.attributes.getValue(EAttribute.CooldownRecovery) / 100);
    }

    constructor(battlerData: BattlerData, additionalAtttributes?: IBattlerAttribute[]) {
        this.level = battlerData.level;
        this.name = battlerData.name;
        this.reset(battlerData, additionalAtttributes);
    }

    //returns skills which fired
    advanceCooldown(timeDelta: number): Skill[] {
        let firedSkills: Skill[] = [];
        this.skills.forEach((skill) => {
            if (skill) {
                skill.chargeTime += timeDelta * this.cdMultiplier;
                if (skill.chargeTime >= skill.cooldownMS) {
                    firedSkills.push(skill);
                    skill.chargeTime = 0;
                }
            }
        });
        return firedSkills;
    }

    //returns actual damage dealt
    takeDamage(rawDamage: number): number {
        let damage = rawDamage - this.attributes.getValue(EAttribute.Defense);
        if (damage <= 0) {
            damage = 0;
        }
        this.currentHealth -= damage;
        return damage;
    }

    get isDead() {
        return this.currentHealth <= 0;
    }

    reset(battlerData: { attributes: IBattlerAttribute[], selectedSkills: number[], level: number }, additionalAtttributes?: IBattlerAttribute[]) {
        const skillData = skills.value;
        const atts = additionalAtttributes ? [...battlerData.attributes, ...additionalAtttributes] : battlerData.attributes;
        this.attributes = new BattleAttributes(atts);
        this.skills = battlerData.selectedSkills.map((skillId) => new Skill(skillData[skillId], this));
        while (this.skills.length < this.maxSkills) {
            this.skills.push(undefined);
        }
        this.currentHealth = this.attributes.getValue(EAttribute.MaxHealth);
        this.level = battlerData.level;
    }
}
