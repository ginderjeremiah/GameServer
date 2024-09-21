import { Tooltippable } from "$lib/tooltips";
import { ISkill, IAttributeMultiplier } from "$lib/api";
import { Battler } from "./battler";

export class Skill implements ISkill {
    id: number
    name: string;
    chargeTime: number;
    baseDamage: number;
    damageMultipliers: IAttributeMultiplier[];
    description: string;
    cooldownMS: number;
    iconPath: string;
    owner: Battler;
    ownerStatsVersion = -1;
    target?: Battler;

    constructor(data: ISkill, owner: Battler) {
        this.id = data.id
        this.name = data.name;
        this.baseDamage = data.baseDamage;
        this.damageMultipliers = data.damageMultipliers;
        this.description = data.description;
        this.cooldownMS = data.cooldownMS;
        this.chargeTime = 0;
        this.iconPath = data.iconPath;
        this.owner = owner;
    }

    calculateDamage() {
        let dmg = this.baseDamage;
        this.damageMultipliers.forEach((dmgType) => {
            dmg += this.owner.attributes.getValue(dmgType.attributeId) * dmgType.multiplier;
        })
        return dmg
    }
}
