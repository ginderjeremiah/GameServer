import { Skill } from "./Skill";
import { BattleAttributes } from "./BattleAttributes";
import { IBattlerAttribute, EAttribute } from "../Shared/Api/Types";
import { formatNum } from "../Shared/GlobalFunctions";
import { TooltipManager } from "../Managers/TooltipManager";
import { DataManager } from "../Managers/DataManager";

export abstract class Battler {
    name: string;
    level: number;
    currentHealth!: number;
    attributes!: BattleAttributes;
    skills!: (Skill | undefined)[];
    maxSkills = 4;

    skillsContainer: HTMLDivElement;
    skillSlots: HTMLDivElement[] = [];
    healthBar: HTMLMeterElement;
    healthLabel: HTMLSpanElement;
    lvlLabel: HTMLSpanElement;
    nameLabel: HTMLSpanElement;
    label: "player" | "enemy";

    constructor(battlerData: {attributes: IBattlerAttribute[], selectedSkills: number[], name: string, level: number}, label: "player" | "enemy", additionalAtttributes?: IBattlerAttribute[]) {
        this.label = label;
        this.name = battlerData.name;
        this.level = battlerData.level;
        this.skillsContainer = document.getElementById(label + "SkillsContainer") as HTMLDivElement;
        this.healthBar = document.getElementById(label + "Health") as HTMLMeterElement;
        this.healthLabel = document.getElementById(label + "HealthLabel") as HTMLSpanElement;
        this.lvlLabel = document.getElementById(label + "Lvl") as HTMLSpanElement;
        this.nameLabel = document.getElementById(label + "Name") as HTMLSpanElement;
        this.nameLabel.textContent = this.name;
        this.reset(battlerData, additionalAtttributes);
    }

    updateHealthDisplay(): void {
        this.healthLabel.textContent = formatNum(this.currentHealth) + "/" + this.attributes.getValue(EAttribute.MaxHealth);
        this.healthBar.value = this.currentHealth;
        this.healthBar.max = this.attributes.getValue(EAttribute.MaxHealth);
    }

    updateLvlDisplay(): void {
        this.lvlLabel.textContent = "Lvl: " + this.level;
    }

    initSkillsDisplay(): void {
        this.skillsContainer.replaceChildren();
        this.skillSlots = [];
        const skills = this.skills;
        for (let i = 0; i < this.maxSkills; i++) {
            const skillBox = document.createElement("div");
            skillBox.className = "skillBox";
            skillBox.id = this.label + "Skill" + i;
            skillBox.addEventListener('mousemove', (event) => {
                if (skills[i]) {
                    TooltipManager.updatePosition(event.clientX, event.clientY);
                }
            });
            skillBox.addEventListener('mouseenter', (event) => {
                if (skills[i]) {
                    TooltipManager.setData(skills[i]!);
                }
            });
            skillBox.addEventListener('mouseleave', (event) => {
                TooltipManager.remove();
            });
            this.skillsContainer.appendChild(skillBox);
            const skill = skills[i];
            if (skill) {
                const skillElement = document.createElement("img");
                skillElement.className = "skill";
                skillElement.src = skill.iconPath;
                skillBox.appendChild(skillElement);
            }
            this.skillSlots.push(skillBox);
        }
    }

    updateSkillsDisplay(): void {
        for (let i = 0; i < this.skills.length; i++) {
            const skill = this.skills[i];
            if (skill) {
                let slot = this.skillSlots[i];
                slot.style.cssText = "--perc: " + formatNum(100 * skill.chargeTime / skill.cooldownMS) + "%";
            }
        }
    }

    //returns skills which fired
    advanceCooldown(timeDelta: number): Skill[] {
        let firedSkills: Skill[] = [];
        let cdMultiplier = (1 + this.attributes.getValue(EAttribute.CooldownRecovery) / 100);
        this.skills.forEach((skill) => {
            if (skill) {
                skill.chargeTime += timeDelta * cdMultiplier;
                if (skill.chargeTime >= skill.cooldownMS) {
                    firedSkills.push(skill);
                    skill.chargeTime = 0;
                }
            }
        });
        return firedSkills;
    }

    updateCombatDisplays() {
        this.updateSkillsDisplay();
        this.updateHealthDisplay();
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

    reset(battlerData: {attributes: IBattlerAttribute[], selectedSkills: number[], level: number}, additionalAtttributes?: IBattlerAttribute[]) {
        const skillDatas = DataManager.skills;
        const atts = additionalAtttributes ? [...battlerData.attributes, ...additionalAtttributes] : battlerData.attributes;
        this.attributes = new BattleAttributes(atts);
        this.skills = battlerData.selectedSkills.map((skillId) => new Skill(skillDatas[skillId], this));
        this.currentHealth = this.attributes.getValue(EAttribute.MaxHealth);
        this.level = battlerData.level;
        this.initSkillsDisplay();
        this.updateCombatDisplays()
        this.updateLvlDisplay();
    }
}
