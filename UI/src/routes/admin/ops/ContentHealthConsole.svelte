<div class="ops-console" data-testid="content-health-console">
	<div class="head">
		<div class="eyebrow">Admin Console · Ops</div>
		<div class="title-row">
			<h1 class="title" data-testid="ch-title">Content Health</h1>
			<div class="summary" data-testid="ch-summary">
				{#if health.loaded}
					{#if health.isHealthy}
						<span class="ok">graph healthy</span>
					{:else}
						{#if health.errorCount > 0}
							<span class="err" data-testid="ch-error-count"
								>{health.errorCount} error{health.errorCount === 1 ? '' : 's'}</span
							>
						{/if}
						{#if health.warningCount > 0}
							<span class="warn" data-testid="ch-warning-count"
								>{health.warningCount} warning{health.warningCount === 1 ? '' : 's'}</span
							>
						{/if}
					{/if}
				{/if}
			</div>
		</div>
		<p class="blurb">
			Whole-graph reachability lint over the live reference caches — the cross-entity breakage a per-entity save can't
			see (a zone gated by a retired challenge, a challenge rewarding a nonexistent item, an orphan skill no source
			grants). <em>Errors</em> are genuine breaks and gate the content-lint CI build; <em>warnings</em> are dead / unreachable
			content the runtime tolerates. Read-only — this never changes content.
		</p>
	</div>

	<div class="toolbar">
		<button type="button" class="btn" data-testid="ch-refresh" disabled={health.loading} onclick={refresh}>
			Refresh
		</button>
	</div>

	{#if health.error}
		<div class="error-panel" role="alert" data-testid="ch-error">{health.error}</div>
	{/if}

	<div class="body">
		{#if !health.loaded && health.loading}
			<Loading loading={true} delay={150} />
		{:else if health.isHealthy}
			<div class="empty-state" data-testid="ch-healthy">
				<div class="glyph">
					<svg
						width="26"
						height="26"
						viewBox="0 0 16 16"
						fill="none"
						stroke="currentColor"
						stroke-width="1.3"
						aria-hidden="true"
					>
						<path d="M2 8.5l3.5 3.5L14 3.5" stroke-linecap="round" stroke-linejoin="round" />
					</svg>
				</div>
				<div class="et">The content graph is healthy</div>
				<div class="es">No unreachable or broken cross-references were found.</div>
			</div>
		{:else if health.loaded}
			<div class="ch-groups">
				{#each health.groups as group (group.check)}
					<section class="ch-group" data-testid="ch-group">
						<h2 class="ch-group-head" class:err={group.hasError}>
							<span class="ch-check">{group.check}</span>
							<span class="ch-group-count">{group.findings.length}</span>
						</h2>
						<ul class="ch-list">
							{#each group.findings as finding (finding.entityKind + finding.entityId + finding.message)}
								{@const meta = severityMeta(finding.severity)}
								<li class="ch-finding" data-testid="ch-finding">
									<span class="sev {meta.tone}" data-testid="ch-severity">{meta.label}</span>
									<span class="ch-entity">
										{finding.entityKind}
										<span class="ch-entity-name"
											>{entityDisplayName(finding.entityKind, finding.entityId, nameSources)}</span
										>
										<span class="ch-entity-id">#{finding.entityId}</span>
									</span>
									<span class="ch-message">{finding.message}</span>
								</li>
							{/each}
						</ul>
					</section>
				{/each}
			</div>
		{/if}
	</div>
</div>

<script lang="ts">
import { onMount } from 'svelte';
import Loading from '$components/Loading.svelte';
import { toastError } from '$stores';
import { staticData } from '$stores';
import './ops-console.scss';
import { ContentHealthState, entityDisplayName, severityMeta, type EntityNameSources } from './content-health.svelte';

const health = new ContentHealthState();

// Name resolution reads the admin reference caches the shell loads on mount; a kind not held there
// (Class / SkillRecipe) falls back to its id.
const nameSources = $derived<EntityNameSources>({
	zones: staticData.zones,
	challenges: staticData.challenges,
	enemies: staticData.enemies,
	items: staticData.items,
	skills: staticData.skills,
	proficiencies: staticData.proficiencies
});

onMount(() => {
	void refresh();
});

const refresh = async () => {
	if (!(await health.load())) {
		toastError(health.error ?? 'Failed to load the content health report.');
	}
};
</script>

<style lang="scss">
.summary {
	.err {
		color: var(--error);
	}
	.warn {
		color: var(--warning);
	}
	.ok {
		color: var(--accent);
	}
}
.blurb {
	max-width: 820px;
}

.ch-groups {
	display: flex;
	flex-direction: column;
	gap: 18px;
}
.ch-group {
	border: 1px solid var(--border-subtle);
	border-radius: 5px;
	overflow: hidden;
}
.ch-group-head {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 12px;
	margin: 0;
	padding: 9px 14px;
	background: color-mix(in srgb, var(--warning) 8%, transparent);
	border-bottom: 1px solid var(--border-subtle);
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.4px;

	&.err {
		background: color-mix(in srgb, var(--error) 10%, transparent);
	}
}
.ch-check {
	color: var(--text-secondary);
	font-weight: 500;
	text-transform: uppercase;
}
.ch-group-count {
	color: var(--text-muted);
}

.ch-list {
	list-style: none;
	margin: 0;
	padding: 0;
}
.ch-finding {
	display: grid;
	grid-template-columns: 74px minmax(160px, 240px) 1fr;
	gap: 14px;
	align-items: baseline;
	padding: 10px 14px;
	font-size: 12.5px;

	& + & {
		border-top: 1px solid var(--border-subtle);
	}
}
.sev {
	justify-self: start;
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	padding: 2px 7px;
	border-radius: 3px;
	border: 1px solid transparent;

	&.error {
		color: var(--error);
		border-color: color-mix(in srgb, var(--error) 45%, transparent);
		background: color-mix(in srgb, var(--error) 12%, transparent);
	}
	&.warning {
		color: var(--warning);
		border-color: color-mix(in srgb, var(--warning) 45%, transparent);
		background: color-mix(in srgb, var(--warning) 12%, transparent);
	}
}
.ch-entity {
	font-family: var(--mono);
	font-size: 11.5px;
	color: var(--text-muted);
	overflow-wrap: anywhere;

	.ch-entity-name {
		color: var(--text-secondary);
	}
	.ch-entity-id {
		color: var(--text-muted);
	}
}
.ch-message {
	color: var(--text-tertiary);
	line-height: 1.5;
}

// The healthy-graph checkmark reads as a positive confirmation, not a neutral empty state,
// so its glyph keeps the accent color instead of the shared .empty-state's muted default.
.empty-state .glyph {
	color: var(--accent);
}
</style>
