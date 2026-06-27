import { challengeEntity } from './challenge';
import { classEntity } from './class';
import { enemyEntity } from './enemy';
import { itemEntity } from './item';
import { itemModEntity } from './item-mod';
import { skillEntity } from './skill';
import { skillRecipeEntity } from './skill-recipe';
import { tagEntity } from './tag';
import type { EntityConfig, Identified } from './types';
import { zoneEntity } from './zone';

/**
 * Erase a concrete entity config to the opaque {@link Identified} form. The
 * workbench reads record fields dynamically by config-declared keys and never
 * relies on T beyond `id`/`name`, so this widening is sound at runtime; it just
 * lets all the configs share one registry/component despite differing T.
 */
const asEntity = <T extends Identified>(config: EntityConfig<T>): EntityConfig<Identified> =>
	config as unknown as EntityConfig<Identified>;

export const workbenchEntities: EntityConfig<Identified>[] = [
	asEntity(enemyEntity),
	asEntity(skillEntity),
	asEntity(itemEntity),
	asEntity(itemModEntity),
	asEntity(zoneEntity),
	asEntity(tagEntity),
	asEntity(challengeEntity),
	asEntity(classEntity),
	asEntity(skillRecipeEntity)
];

export interface WorkbenchGroup {
	key: string;
	label: string;
	entityKeys: string[];
}

export const workbenchGroups: WorkbenchGroup[] = [
	{ key: 'combat', label: 'Combat', entityKeys: ['enemies', 'skills'] },
	{ key: 'items', label: 'Items', entityKeys: ['items', 'itemMods', 'tags'] },
	{ key: 'world', label: 'World', entityKeys: ['zones'] },
	{ key: 'progression', label: 'Progression', entityKeys: ['challenges', 'classes', 'skillRecipes'] }
];

export const entityByKey = (key: string): EntityConfig<Identified> | undefined =>
	workbenchEntities.find((entity) => entity.key === key);

export const groupLabelFor = (entityKey: string): string =>
	workbenchGroups.find((group) => group.entityKeys.includes(entityKey))?.label ?? '';

export type { EntityConfig, Identified } from './types';
