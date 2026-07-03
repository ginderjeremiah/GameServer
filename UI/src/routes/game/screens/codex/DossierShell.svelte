<!-- Shared dossier chrome for the Codex right panel: the fixed-width surface, the accent border, the
     kind marker + name header. Enemy/Zone/Skill dossiers differ below the header (sub-tabs vs. stacked
     sections), so only that header/wrapper — not the body — lives here. -->
{#if selectedItem}
	<div class="dossier" data-testid={testid} style:--dossier-accent={accent}>
		<div class="head" style:border-left-color={borderAccent}>
			<div class="kind-line">
				<span class="kind-dot"></span>
				<span class="kind">{kind}</span>
				{@render headExtra?.()}
			</div>
			<div class="name">{name}</div>
			{#if description}
				<div class="desc">{description}</div>
			{/if}
		</div>

		{@render children()}
	</div>
{/if}

<script lang="ts">
import type { Snippet } from 'svelte';

interface Props {
	/** Gates the whole shell like each dossier's own `{#if}` did — pass the selected record (or null). */
	selectedItem: unknown;
	testid: string;
	/** Accent for the kind dot/label, and the head border unless `borderAccent` overrides it. */
	accent: string;
	/** Skill's rarity-tinted border is independent of the (fixed) kind-label accent; omit to reuse `accent`. */
	borderAccent?: string;
	kind: string;
	name: string;
	description?: string;
	headExtra?: Snippet;
	children: Snippet;
}

let { selectedItem, testid, accent, borderAccent, kind, name, description, headExtra, children }: Props = $props();
</script>

<style lang="scss">
.dossier {
	width: 380px;
	flex: none;
	background: var(--surface);
	border-left: 1px solid var(--border-subtle);
	display: flex;
	flex-direction: column;
}

.head {
	border-left: 3px solid var(--dossier-accent);
	padding: 18px 20px 14px;
	flex: none;
}

.kind-line {
	display: flex;
	align-items: center;
	gap: 9px;
	margin-bottom: 6px;
}

.kind-dot {
	width: 6px;
	height: 6px;
	transform: rotate(45deg);
	background: var(--dossier-accent);
	flex: none;
}

.kind {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--dossier-accent);
}

.name {
	font-size: 21px;
	font-weight: 500;
	letter-spacing: -0.3px;
	line-height: 1.05;
}

.desc {
	margin-top: 7px;
	font-size: 12px;
	line-height: 1.5;
	color: var(--text-tertiary);
}
</style>
