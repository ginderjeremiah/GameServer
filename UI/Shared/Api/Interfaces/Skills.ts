import { IAttributeMultiplier } from "../Types";

export interface ISkill {
  id: number;
  name: string;
  baseDamage: number;
  damageMultipliers: IAttributeMultiplier[];
  description: string;
  cooldownMs: number;
  iconPath: string;
}
