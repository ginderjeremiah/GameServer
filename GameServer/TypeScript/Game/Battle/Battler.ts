abstract class Battler {
    abstract name: string;
    abstract level: number;
    currentHealth: number;
    attributes: BattleAttributes;
    skills: (Skill | undefined)[];
    maxSkills = 4;
    statsVersion = 0;

    abstract skillsContainer: HTMLDivElement;
    skillSlots: HTMLDivElement[] = [];
    abstract healthBar: HTMLMeterElement;
    abstract healthLabel: HTMLSpanElement;
    abstract lvlLabel: HTMLSpanElement;
    abstract nameLabel: HTMLSpanElement;
    abstract label: string;

    constructor(attributes: BattleAttributes, selectedSkills: number[]) {
        const skillDatas = DataManager.skills;
        this.attributes = attributes;
        this.currentHealth = this.attributes.getValue(AttributeTypes.MaxHealth);
        this.skills = selectedSkills.map((skillId) => new Skill(skillDatas[skillId], this));
    }

    updateHealthDisplay(): void {
        this.healthLabel.textContent = formatNum(this.currentHealth) + "/" + this.attributes.getValue(AttributeTypes.MaxHealth);
        this.healthBar.value = this.currentHealth;
        this.healthBar.max = this.attributes.getValue(AttributeTypes.MaxHealth);
    }

    updateLvlDisplay(): void {
        this.lvlLabel.textContent = "Lvl: " + this.level;
    }

    initSkillsDisplay(): void {
        let skills = this.skills;
        for (let i = 0; i < this.maxSkills; i++) {
            let skillBox = document.createElement("div");
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
        let cdMultiplier = (1 + this.attributes.getValue(AttributeTypes.CooldownRecovery) / 100);
        this.skills.forEach((skill) => {
            if (skill) {
                skill.chargeTime += timeDelta * cdMultiplier;
                if (skill.chargeTime >= skill.cooldownMS) {
                    firedSkills.push(skill);
                    skill.chargeTime = 0;
                }
            }
        })
        this.updateSkillsDisplay();
        return firedSkills;
    } 

    //returns actual damage dealt
    takeDamage(rawDamage: number): number { 
        let damage = rawDamage - this.attributes.getValue(AttributeTypes.Defense);
        if (damage <= 0) {
            damage = 0;
        }
        this.currentHealth -= damage;
        this.updateHealthDisplay();
        return damage;
    }

    get isDead() {
        return this.currentHealth <= 0;
    }

    get stats() {
        return { statsVersion: this.statsVersion, baseStats: this.attributes };
    }

    clearSkillsDisplay() {
        this.skillsContainer.replaceChildren();
    }
}
