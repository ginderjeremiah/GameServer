import { tiersOfPath } from './progression-helpers';
import type { WorkbenchPath, WorkbenchProficiency } from './types';

/** A tier rendered as a node in a path column of the cross-path Map view. */
export interface MapNode {
	id: number;
	/** Stable DOM id (`t{id}`) used to look the node up when measuring gateway edges. */
	nodeId: string;
	ordinal: number;
	name: string;
	maxLevel: number;
	/** Gated when it carries cross-path prerequisites (vs. a starter tier). */
	gated: boolean;
	retired: boolean;
}

/** A path laid out as a column of its tiers, ascending by ordinal. */
export interface MapColumn {
	id: number;
	name: string;
	/** True when any tier in the column is gated — used to accent the column header. */
	gatedPath: boolean;
	retired: boolean;
	nodes: MapNode[];
}

/** A directed gateway edge from a prerequisite tier to the gated tier it opens. */
export interface MapEdge {
	from: string;
	to: string;
}

/** The DOM node id for a tier (the `data-node` attribute the edge measurer queries). */
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
			gated: tier.prerequisiteIds.length > 0,
			retired: tier.retiredAt != null
		}));
		return {
			id: path.id,
			name: path.name,
			gatedPath: nodes.some((node) => node.gated),
			retired: path.retiredAt != null,
			nodes
		};
	});

/**
 * Every cross-path gateway as a `prerequisite → gated tier` edge. A prerequisite referencing a tier
 * absent from the current set (e.g. never authored) still yields a def; the measurer drops it when
 * its node element can't be found, mirroring the mockup.
 */
export const mapEdgeDefs = (profs: WorkbenchProficiency[]): MapEdge[] => {
	const defs: MapEdge[] = [];
	for (const prof of profs) {
		for (const prereqId of prof.prerequisiteIds) {
			defs.push({ from: tierNodeId(prereqId), to: tierNodeId(prof.id) });
		}
	}
	return defs;
};

/** A horizontal cubic bezier between two points (control points at the mid-x), as an SVG path `d`. */
export const bezierPath = (x1: number, y1: number, x2: number, y2: number): string => {
	const midX = (x1 + x2) / 2;
	return `M ${x1} ${y1} C ${midX} ${y1} ${midX} ${y2} ${x2} ${y2}`;
};
