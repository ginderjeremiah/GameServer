<div class="logging-panel" data-testid="logging-panel">
	<!-- panel header -->
	<div class="panel-header">
		<div>
			<div class="panel-eyebrow">Logging · Combat Log</div>
			<h2 class="panel-title">Message Types</h2>
			<p class="panel-desc">
				Choose which kinds of events show up in your combat log. Disabled types are hidden from the feed but still
				recorded by the engine.
			</p>
		</div>
		<div class="panel-tally">
			<span class="tally-on">{view.enabledCount}</span> / {LOG_TYPES.length} shown
		</div>
	</div>

	<div class="panel-columns">
		<!-- grouped list -->
		<div class="group-list">
			{#each groups as group (group.key)}
				<LogGroup group={group.def} types={group.types} {view} />
			{/each}
		</div>

		<!-- live preview — sticks in place while a long list scrolls -->
		<div class="preview-column">
			<LivePreview prefs={view.draft} />
		</div>
	</div>
</div>

<script lang="ts">
import LogGroup from './LogGroup.svelte';
import LivePreview from './LivePreview.svelte';
import { LOG_GROUPS, LOG_TYPES, type OptionsView } from './options-view.svelte';

interface Props {
	view: OptionsView;
}

const { view }: Props = $props();

// Only render groups that actually contain log types.
const groups = LOG_GROUPS.map((def) => ({
	key: def.key,
	def,
	types: LOG_TYPES.filter((lt) => lt.group === def.key)
})).filter((g) => g.types.length);
</script>

<style lang="scss">
.logging-panel {
	max-width: 1100px;
	margin: 0 auto;
	display: flex;
	flex-direction: column;
	gap: 22px;
}

.panel-header {
	display: flex;
	align-items: flex-end;
	justify-content: space-between;
	gap: 20px;
}

.panel-eyebrow {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--eyebrow);
	margin-bottom: 7px;
}

.panel-title {
	margin: 0;
	font-size: 19px;
	font-weight: 500;
	letter-spacing: -0.2px;
}

.panel-desc {
	margin: 8px 0 0;
	font-size: 13.5px;
	color: color-mix(in srgb, var(--text-primary) 50%, transparent);
	max-width: 560px;
	line-height: 1.5;
}

.panel-tally {
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.5px;
	color: color-mix(in srgb, var(--text-primary) 50%, transparent);
	white-space: nowrap;
	padding-bottom: 2px;
}

.tally-on {
	color: var(--accent);
}

.panel-columns {
	display: flex;
	gap: 24px;
	align-items: flex-start;
	flex-wrap: wrap;
}

.group-list {
	flex: 1 1 480px;
	min-width: 360px;
	display: flex;
	flex-direction: column;
	gap: 14px;
}

.preview-column {
	flex: 1 1 330px;
	min-width: 300px;
	display: flex;
	height: 440px;
	position: sticky;
	top: 0;
	align-self: flex-start;
}
</style>
