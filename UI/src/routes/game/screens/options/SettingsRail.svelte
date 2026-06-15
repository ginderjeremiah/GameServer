<div class="settings-rail" data-testid="settings-rail">
	<RailNavGroup label="Categories" first expanded>
		{#each SETTINGS_CATS as cat (cat.key)}
			<RailNavItem
				active={active === cat.key}
				disabled={!cat.built}
				label={cat.label}
				title={cat.label}
				testid="settings-cat-{cat.key}"
				expanded
				onclick={() => onPick(cat.key)}
			>
				{#snippet glyph(isActive)}
					<SettingsGlyph glyph={cat.glyph} color={isActive ? 'var(--accent)' : 'currentColor'} />
				{/snippet}
				{#snippet trailing()}
					{#if !cat.built}
						<span class="soon-badge">soon</span>
					{/if}
				{/snippet}
			</RailNavItem>
		{/each}
	</RailNavGroup>
</div>

<script lang="ts">
import RailNavGroup from '$components/sidebar/RailNavGroup.svelte';
import RailNavItem from '$components/sidebar/RailNavItem.svelte';
import SettingsGlyph from './SettingsGlyph.svelte';
import { SETTINGS_CATS } from './options-view.svelte';

interface Props {
	active: string;
	onPick: (key: string) => void;
}

const { active, onPick }: Props = $props();
</script>

<style lang="scss">
.settings-rail {
	width: 210px;
	flex-shrink: 0;
	border-right: 1px solid color-mix(in srgb, var(--white) 7%, transparent);
	padding: 22px 0;
	display: flex;
	flex-direction: column;
}

.soon-badge {
	margin-right: 12px;
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--text-primary) 30%, transparent);
	border: 1px solid color-mix(in srgb, var(--text-primary) 13%, transparent);
	border-radius: 2px;
	padding: 1px 4px;
}
</style>
