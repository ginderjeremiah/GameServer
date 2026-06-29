/* Synthesis "Web" view — the pure layout/edge math.

   The Bench view's `synthesis.ts` derives the per-recipe reveal/gating view-models; this module turns
   those `RecipeView[]` into a positioned **recipe graph**: input skills → fusion nodes → result skills,
   with a synthesized result that feeds a deeper recipe drawn as an edge into the next fusion (the spike's
   "recipe-graph chaining is the depth source", #1125 decision 11). It is framework-free so the layered-DAG
   layout, the chaining/masking rules, and the pan/zoom transform are all unit-testable without rendering;
   `SynthesisGraph.svelte` only positions the returned nodes/edges and wires pointer pan/zoom to `zoomAt`.

   The conservative hinted reveal carries straight over from the Bench view: hidden recipes are already
   dropped by `buildSynthesis`, a hinted recipe's result + missing inputs stay masked (no leaked identity),
   and only *owned* inputs become nodes — an unowned input on a hinted recipe is absent, never a node. */

import type { ISkill } from '$lib/api';
import type { RecipeState, RecipeView } from './synthesis';

/** A graph node's role: a base/leaf input skill, a recipe's fusion (the combine), or a result skill. A
 *  result skill that is itself an input to a deeper recipe is a single `result` node with onward edges. */
export type GraphNodeKind = 'input' | 'fusion' | 'result';

/** Per-kind node box size (px), the single source for both the rendered node and the edge endpoint math. */
export const NODE_SIZE: Record<GraphNodeKind, { w: number; h: number }> = {
	input: { w: 122, h: 40 },
	fusion: { w: 28, h: 28 },
	result: { w: 132, h: 62 }
};

/** Horizontal gap between adjacent layer centres, and vertical gap between node centres within a layer. */
const LAYER_DX = 168;
const ROW_DY = 88;
/** Margin around the laid-out content so nodes near the edge aren't clipped. */
const PADDING = 28;

/** A positioned graph node. `x`/`y` are the node-box **centre** (layout coords); the renderer offsets by
 *  half the kind's {@link NODE_SIZE}. `recipeId` is set on `fusion`/`result` (selecting either drives the
 *  recipe's dossier); `skill` is the resolved skill on an `input` node and on a revealed `result` node. */
export interface GraphNode {
	key: string;
	kind: GraphNodeKind;
	x: number;
	y: number;
	/** The recipe a `fusion`/`result` node belongs to — the selection target. Absent on `input` nodes. */
	recipeId?: number;
	/** The resolved skill (input node, or revealed result node); absent while a result is masked. */
	skill?: ISkill;
	/** The owning recipe's reveal state, for node styling (`fusion`/`result`). */
	state?: RecipeState;
	/** True for a hinted recipe's fusion/result — drawn sealed so the identity never leaks. */
	masked: boolean;
	/** Whether clicking the node selects a recipe (true for `fusion`/`result`, false for leaf `input`). */
	selectable: boolean;
}

/** A directed edge between node centres. `state` carries the feeding recipe's reveal state so the renderer
 *  can dash/colour a hinted-or-gated edge distinctly from a ready/done one. */
export interface GraphEdge {
	key: string;
	from: string;
	to: string;
	state: RecipeState;
}

/** The laid-out graph plus its content extent, so the canvas can centre/fit it. */
export interface SynthesisGraphLayout {
	nodes: GraphNode[];
	edges: GraphEdge[];
	width: number;
	height: number;
}

const skillKey = (skillId: number): string => `s${skillId}`;
const fusionKey = (recipeId: number): string => `f${recipeId}`;
const maskedResultKey = (recipeId: number): string => `m${recipeId}`;

/**
 * Lay out the discovered recipes as a layered DAG: leaf input skills on the left, then alternating fusion
 * and result columns, with a synthesized result that feeds another recipe chaining onward to its fusion.
 *
 * Each *skill* is a single node keyed by id, so a result that is reused as an input appears once with edges
 * to every recipe it feeds. A skill produced by a discovered recipe is a `result` node (carrying that
 * recipe's state); otherwise it is a leaf `input`. A hinted recipe contributes a masked fusion + masked
 * result (keyed by recipe, not skill, since its identity is sealed) and edges only from its owned inputs.
 */
export function layoutSynthesisGraph(recipes: readonly RecipeView[]): SynthesisGraphLayout {
	// Which skill each revealed recipe produces — drives "is this skill a result node?" and the chaining.
	const producedBy = new Map<number, RecipeView>();
	for (const recipe of recipes) {
		if (recipe.result) {
			producedBy.set(recipe.result.id, recipe);
		}
	}

	const nodes = new Map<string, GraphNode>();
	const edges: GraphEdge[] = [];

	// Resolve (or create) the single node for a skill: a result node when a discovered recipe produces it,
	// else a leaf input node. Idempotent so a shared/chained skill is one node with multiple edges.
	const ensureSkillNode = (skillId: number, skill?: ISkill): string => {
		const key = skillKey(skillId);
		if (nodes.has(key)) {
			return key;
		}
		const producer = producedBy.get(skillId);
		if (producer?.result) {
			nodes.set(key, {
				key,
				kind: 'result',
				x: 0,
				y: 0,
				recipeId: producer.id,
				skill: producer.result,
				state: producer.state,
				masked: false,
				selectable: true
			});
		} else {
			nodes.set(key, { key, kind: 'input', x: 0, y: 0, skill, masked: false, selectable: false });
		}
		return key;
	};

	for (const recipe of recipes) {
		const fKey = fusionKey(recipe.id);
		nodes.set(fKey, {
			key: fKey,
			kind: 'fusion',
			x: 0,
			y: 0,
			recipeId: recipe.id,
			state: recipe.state,
			masked: !recipe.result,
			selectable: true
		});

		// The result node: a revealed result is the shared skill node (enables chaining); a masked result is
		// an anonymous per-recipe node so its sealed identity can't be keyed to a skill.
		let resultKey: string;
		if (recipe.result) {
			resultKey = ensureSkillNode(recipe.result.id);
		} else {
			resultKey = maskedResultKey(recipe.id);
			nodes.set(resultKey, {
				key: resultKey,
				kind: 'result',
				x: 0,
				y: 0,
				recipeId: recipe.id,
				state: recipe.state,
				masked: true,
				selectable: true
			});
		}
		edges.push({ key: `${fKey}->${resultKey}`, from: fKey, to: resultKey, state: recipe.state });

		// Only owned inputs become nodes/edges — an unowned (masked) input stays absent from the graph.
		for (const input of recipe.inputs) {
			if (input.owned && input.skill) {
				const sKey = ensureSkillNode(input.skill.id, input.skill);
				edges.push({ key: `${sKey}->${fKey}`, from: sKey, to: fKey, state: recipe.state });
			}
		}
	}

	positionNodes(nodes, edges);
	return finalizeLayout([...nodes.values()], edges);
}

/** Assign each node a layer (longest path from the leaves) and an in-layer index (a predecessor-barycenter
 *  order to reduce edge crossings), then set node centres from those. */
function positionNodes(nodes: Map<string, GraphNode>, edges: readonly GraphEdge[]): void {
	const preds = new Map<string, string[]>();
	for (const node of nodes.keys()) {
		preds.set(node, []);
	}
	for (const edge of edges) {
		preds.get(edge.to)?.push(edge.from);
	}

	// Longest-path layering — the graph is acyclic (authoring guards acyclicity, #1125 decision 11). The
	// `inProgress` guard fails soft on a cycle (treat the back-edge as a leaf) so bad data degrades the
	// drawing rather than overflowing the stack and crashing the screen.
	const layerOf = new Map<string, number>();
	const inProgress = new Set<string>();
	const computeLayer = (key: string): number => {
		const cached = layerOf.get(key);
		if (cached !== undefined) {
			return cached;
		}
		if (inProgress.has(key)) {
			return 0;
		}
		inProgress.add(key);
		const parents = preds.get(key) ?? [];
		const layer = parents.length ? Math.max(...parents.map(computeLayer)) + 1 : 0;
		inProgress.delete(key);
		layerOf.set(key, layer);
		return layer;
	};
	for (const key of nodes.keys()) {
		computeLayer(key);
	}

	// Group keys per layer; order layer 0 by key, deeper layers by the mean index of their predecessors
	// (computed against the already-ordered lower layers), tie-broken by key for determinism.
	const maxLayer = Math.max(0, ...layerOf.values());
	const byLayer: string[][] = Array.from({ length: maxLayer + 1 }, () => []);
	for (const [key, layer] of layerOf) {
		byLayer[layer].push(key);
	}

	const indexOf = new Map<string, number>();
	for (let layer = 0; layer <= maxLayer; layer++) {
		const keys = byLayer[layer];
		const barycenter = (key: string): number => {
			const parents = preds.get(key) ?? [];
			if (!parents.length) {
				return 0;
			}
			const sum = parents.reduce((acc, p) => acc + (indexOf.get(p) ?? 0), 0);
			return sum / parents.length;
		};
		keys.sort((a, b) => barycenter(a) - barycenter(b) || (a < b ? -1 : a > b ? 1 : 0));
		keys.forEach((key, index) => indexOf.set(key, index));
	}

	for (const node of nodes.values()) {
		const layer = layerOf.get(node.key) ?? 0;
		const index = indexOf.get(node.key) ?? 0;
		const count = byLayer[layer].length;
		node.x = layer * LAYER_DX;
		// Centre each layer's column vertically around 0; finalizeLayout normalizes to positive coords.
		node.y = (index - (count - 1) / 2) * ROW_DY;
	}
}

/** Normalize node centres so every node box sits at non-negative coords (with a margin) and report the
 *  content extent the canvas fits to. */
function finalizeLayout(nodes: GraphNode[], edges: GraphEdge[]): SynthesisGraphLayout {
	if (!nodes.length) {
		return { nodes, edges, width: 0, height: 0 };
	}
	let minX = Infinity;
	let minY = Infinity;
	let maxX = -Infinity;
	let maxY = -Infinity;
	for (const node of nodes) {
		const { w, h } = NODE_SIZE[node.kind];
		minX = Math.min(minX, node.x - w / 2);
		minY = Math.min(minY, node.y - h / 2);
		maxX = Math.max(maxX, node.x + w / 2);
		maxY = Math.max(maxY, node.y + h / 2);
	}
	for (const node of nodes) {
		node.x += PADDING - minX;
		node.y += PADDING - minY;
	}
	return { nodes, edges, width: maxX - minX + PADDING * 2, height: maxY - minY + PADDING * 2 };
}

/* ── Pan / zoom ─────────────────────────────────────────────────────────────────────────────────────── */

/** The canvas viewport: a pan offset (`x`/`y`, px) plus a zoom `scale`. */
export interface Viewport {
	x: number;
	y: number;
	scale: number;
}

export const ZOOM_MIN = 0.4;
export const ZOOM_MAX = 2.5;

/** Clamp a zoom scale into the allowed band. */
export const clampScale = (scale: number): number => Math.min(ZOOM_MAX, Math.max(ZOOM_MIN, scale));

/**
 * Zoom by `factor` about a focal point (`px`/`py`, in container/screen coords) so the world point under the
 * cursor stays put — the canonical "zoom to cursor" transform. The scale is clamped, and the pan is adjusted
 * by the *effective* ratio so a clamped zoom doesn't drift.
 */
export function zoomAt(viewport: Viewport, factor: number, px: number, py: number): Viewport {
	const scale = clampScale(viewport.scale * factor);
	const ratio = scale / viewport.scale;
	return {
		x: px - (px - viewport.x) * ratio,
		y: py - (py - viewport.y) * ratio,
		scale
	};
}
