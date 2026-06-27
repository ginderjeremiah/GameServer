import { tiersOfPath } from './progression-helpers';
import type { WorkbenchPath, WorkbenchProficiency } from './types';

/** A tier rendered as a node in a path column of the cross-path Map view. */
export interface MapNode {
	id: number;
	/** Stable DOM id (`t{id}`). */
	nodeId: string;
	ordinal: number;
	name: string;
	maxLevel: number;
	retired: boolean;
}

/** A path laid out as a column of its tiers, ascending by ordinal. */
export interface MapColumn {
	id: number;
	name: string;
	retired: boolean;
	nodes: MapNode[];
}

/** The DOM node id for a tier (the `data-node` attribute). */
export const tierNodeId = (tierId: number): string => `t${tierId}`;

/** Project the catalogues into one column per path, each holding its ordered tier nodes. */
export const mapColumns = (paths: WorkbenchPath[], profs: WorkbenchProficiency[]): MapColumn[] =>
	paths.map((path) => {
		const nodes: MapNode[] = tiersOfPath(profs, path.id).map((tier) => ({
			id: tier.id,
			nodeId: tierNodeId(tier.id),
			ordinal: tier.pathOrdinal,
			name: tier.name,
			maxLevel: tier.maxLevel,
			retired: tier.retiredAt != null
		}));
		return {
			id: path.id,
			name: path.name,
			retired: path.retiredAt != null,
			nodes
		};
	});
