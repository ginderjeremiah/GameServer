<!-- Help screen (#1589) — the tutorial reading room. Lists every live lesson with its per-player
     locked/unread/read state; unread lessons are emphasized (this is where the sidebar's unread
     badge leads). Selecting an unread or read lesson reuses the shared open flow: navigate to its
     host screen and play its tour, marking it read either way. A locked lesson is a name-only
     teaser — greyed out and not clickable, so it doesn't spoil content-arc surprises. -->
<div class="help-frame" data-testid="help-screen">
	<div class="header">
		<div class="eyebrow">Settings · Help</div>
		<div class="title-line">
			<h1 class="title">Help</h1>
			<span class="sub">tutorial lessons — replay anything you've read</span>
		</div>
	</div>

	<div class="body">
		{#if view.lessonRows.length === 0}
			<div class="empty" data-testid="help-empty">No lessons yet.</div>
		{:else}
			<div class="lesson-list" data-testid="help-lesson-rows">
				{#each view.lessonRows as row (row.id)}
					<button
						type="button"
						class="lesson-row"
						class:unread={row.state === 'unread'}
						class:locked={row.state === 'locked'}
						disabled={row.state === 'locked'}
						data-testid="help-lesson-{row.id}"
						onclick={() => view.openLesson(row.id)}
					>
						<span class="dot" class:show={row.state === 'unread'}></span>
						<span class="name">{row.name}</span>
						<span class="state-label">{STATE_LABEL[row.state]}</span>
					</button>
				{/each}
			</div>
		{/if}
	</div>
</div>

<script lang="ts">
import { HelpView, type LessonState } from './help-view.svelte';

const STATE_LABEL: Record<LessonState, string> = {
	locked: 'Locked',
	unread: 'New',
	read: 'Replay'
};

const view = new HelpView();
</script>

<style lang="scss">
.help-frame {
	height: 100%;
	display: flex;
	flex-direction: column;
	position: relative;
	color: var(--text-primary);
	font-family: var(--sans);
	overflow: hidden;
}

.header {
	padding: 20px 28px 0;
	flex-shrink: 0;
}

.eyebrow {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: var(--eyebrow);
	margin-bottom: 6px;
}

.title-line {
	display: flex;
	align-items: baseline;
	gap: 12px;
	flex-wrap: wrap;
}

.title {
	margin: 0;
	font-size: 23px;
	font-weight: 500;
	letter-spacing: -0.3px;
}

.sub {
	font-size: 12.5px;
	color: var(--text-tertiary);
}

.body {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding: 16px 28px 28px;
}

.empty {
	display: flex;
	align-items: center;
	justify-content: center;
	height: 100%;
	color: var(--text-tertiary);
	font-size: 13px;
}

.lesson-list {
	display: flex;
	flex-direction: column;
	gap: 2px;
}

.lesson-row {
	width: 100%;
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 13px 14px;
	border: none;
	border-radius: 4px;
	background: transparent;
	cursor: pointer;
	text-align: left;
	font-family: var(--sans);
	color: var(--text-primary);

	&:hover:not(:disabled) {
		background: color-mix(in srgb, var(--white) 3%, transparent);
	}

	&.unread {
		background: color-mix(in srgb, var(--accent) 8%, transparent);

		.name {
			font-weight: 600;
		}
	}

	&.locked,
	&:disabled {
		cursor: default;
		color: color-mix(in srgb, var(--text-primary) 38%, transparent);
	}
}

.dot {
	width: 7px;
	height: 7px;
	flex: none;
	border-radius: 50%;
	background: var(--accent);
	box-shadow: 0 0 6px color-mix(in srgb, var(--accent) 65%, transparent);
	opacity: 0;

	&.show {
		opacity: 1;
	}
}

.name {
	flex: 1;
	min-width: 0;
	font-size: 13.5px;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.state-label {
	flex: none;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-tertiary);
}
</style>
