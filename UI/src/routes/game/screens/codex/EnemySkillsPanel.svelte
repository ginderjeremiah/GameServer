<!-- Skills sub-tab: the enemy's skill pool. Rows are display-only for now — they'll deep-link into
     the Codex Skills tab once it's built (follow-up). -->
<div class="skills">
	<div class="label">Skill pool · {view.skillRows.length}</div>
	<div class="list">
		{#each view.skillRows as skill (skill.id)}
			<div class="skill">
				<span class="mark"></span>
				<div class="text">
					<div class="name">{skill.name}</div>
					<div class="meta">{skill.meta}</div>
				</div>
			</div>
		{/each}
	</div>
</div>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin-bottom: 10px;
}

.list {
	display: flex;
	flex-direction: column;
	gap: 7px;
}

.skill {
	display: flex;
	align-items: center;
	gap: 11px;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: 5px;
	padding: 9px 11px;
}

.mark {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
	background: var(--attr-intellect);
	flex: none;
}

.text {
	flex: 1;
	min-width: 0;
}

.name {
	font-size: 12.5px;
	color: var(--text-primary);
}

.meta {
	font-family: var(--mono);
	font-size: 8.5px;
	color: var(--text-muted);
	margin-top: 1px;
}
</style>
