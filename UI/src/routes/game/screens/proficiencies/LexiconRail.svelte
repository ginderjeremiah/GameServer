<!-- The Lexicon path rail: the discovered paths, each shown as its root word of power plus the path
     name, with the selected path marked by a tinted row and a glowing accent bar. -->
<nav class="rail" aria-label="Lexicon paths">
	<div class="rail-label">the lexicon</div>
	{#each paths as path (path.id)}
		{@const active = path.id === selectedId}
		<button
			type="button"
			class="rail-row"
			class:active
			aria-current={active ? 'true' : undefined}
			data-testid="rail-{path.id}"
			onclick={() => onSelect(path.id)}
		>
			{#if active}
				<span class="marker"></span>
			{/if}
			<span class="rail-icon">
				{#if path.iconPath}
					<img src={path.iconPath} alt="" />
				{/if}
			</span>
			<span class="rail-body">
				<WordOfPower class="rail-word" text={path.word} label={path.name} size={20} />
				<span class="rail-name">{path.name}</span>
			</span>
		</button>
	{/each}
</nav>

<script lang="ts">
import { WordOfPower } from '$components';
import type { PathView } from './proficiencies-lexicon';

interface Props {
	/** The discovered paths to list. */
	paths: PathView[];
	/** The currently selected path's id. */
	selectedId: number | undefined;
	/** Select a path. */
	onSelect: (id: number) => void;
}

const { paths, selectedId, onSelect }: Props = $props();
</script>

<style lang="scss">
.rail {
	flex: none;
	width: 182px;
	overflow-y: auto;
	background: var(--panel);
	border-right: 1px solid var(--border-subtle);
	padding: 12px 8px;
	display: flex;
	flex-direction: column;
	gap: 1px;
}

.rail-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--text-muted);
	padding: 4px 8px 8px;
}

.rail-row {
	position: relative;
	display: flex;
	align-items: center;
	gap: 11px;
	padding: 9px;
	border: none;
	border-radius: var(--border-radius);
	background: transparent;
	color: inherit;
	cursor: pointer;
	text-align: left;

	&:hover {
		background: color-mix(in srgb, var(--white) 4%, transparent);
	}

	&.active {
		background: color-mix(in srgb, var(--accent) 8%, transparent);
	}
}

// The selected-path accent bar down the row's left edge.
.marker {
	position: absolute;
	left: 0;
	top: 6px;
	bottom: 6px;
	width: 2px;
	background: var(--accent);
	box-shadow: 0 0 10px color-mix(in srgb, var(--accent) 70%, transparent);
}

.rail-icon {
	flex: none;
	width: 30px;
	height: 30px;
	display: flex;
	align-items: center;
	justify-content: center;
	border: 1px dashed var(--border-medium);
	border-radius: var(--border-radius);
	background: color-mix(in srgb, var(--white) 3%, transparent);
	overflow: hidden;

	img {
		width: 100%;
		height: 100%;
		object-fit: contain;
	}
}

.rail-body {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	gap: 2px;
}

// The rail word is the child WordOfPower element, reached through `:global()` anchored under
// `.rail-body` so the rule stays scoped to this component's subtree.
.rail-body :global(.rail-word) {
	display: block;
	line-height: 1;
	color: var(--text-primary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.rail-name {
	font-size: 11px;
	color: var(--text-tertiary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}
</style>
