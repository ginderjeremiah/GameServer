<div class="options-frame" data-testid="options-screen">
	<!-- screen header -->
	<div class="options-header">
		<span class="diamond"></span>
		<div>
			<div class="options-title">Options</div>
			<div class="options-subtitle">Settings · tune the game to your taste</div>
		</div>
	</div>

	<!-- body: category rail + content -->
	<div class="options-body">
		<SettingsRail active={view.category} onPick={(key) => view.pickCategory(key)} />

		<div class="content-column">
			<div class="content-scroll">
				<LoggingPanel {view} />
			</div>
			<SaveBar {view} />
		</div>
	</div>
</div>

<script lang="ts">
import { onDestroy } from 'svelte';
import SettingsRail from './SettingsRail.svelte';
import LoggingPanel from './LoggingPanel.svelte';
import SaveBar from './SaveBar.svelte';
import { OptionsView } from './options-view.svelte';

const view = new OptionsView();

onDestroy(() => view.dispose());
</script>

<style lang="scss">
.options-frame {
	height: 100%;
	display: flex;
	flex-direction: column;
	color: var(--text-primary);
	font-family: var(--sans);
	overflow: hidden;
}

.options-header {
	padding: 20px 28px 18px;
	display: flex;
	align-items: center;
	gap: 12px;
	border-bottom: 1px solid color-mix(in srgb, var(--white) 7%, transparent);
	flex-shrink: 0;
}

.diamond {
	width: 11px;
	height: 11px;
	transform: rotate(45deg);
	background: var(--accent);
	box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 60%, transparent);
	flex-shrink: 0;
}

.options-title {
	font-size: 21px;
	font-weight: 500;
	letter-spacing: -0.3px;
}

.options-subtitle {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.options-body {
	flex: 1;
	min-height: 0;
	display: flex;
}

.content-column {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	min-height: 0;
}

.content-scroll {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding: 24px 28px;
}
</style>
