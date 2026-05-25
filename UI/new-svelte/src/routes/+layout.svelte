<div class="page-base">
	{@render children()}
	<TooltipBase />
</div>

<script lang="ts">
import { page } from '$app/stores';
import { routeTo } from '$lib/common';
import { TooltipBase } from '$components';
import { playerManager } from '$lib/engine';
import '$styles/common.scss';

let { children } = $props();

$effect(() => {
	if (!playerManager.name && $page.url.pathname !== '/') {
		routeTo('/');
	}
});
</script>

<style lang="scss">
@use '../styles/colors.scss';

.page-base {
	height: 100vh;
	user-select: none;
	background: colors.$dark-light-gray-grad;
	color: colors.$text-primary;
	font-family: Geist, Arial, Helvetica, sans-serif;

	--border-radius: 3px;
	--default-glow-color: #{colors.$accent};
	--btn-background: #{colors.$accent};
	--spinner-color: #{colors.$accent};
	--overlay-color: #{colors.$light-gray};
	--title-color: #{colors.$off-white};
	--default-shadow: 0 0 0.25rem #{colors.$black};
	--default-border: 1px solid #{colors.$black};
	--error-color: #{colors.$error};
	--health-missing-color: #{colors.$hp-red-bg};
	--health-remaining-color: #{colors.$hp-green};
	--health-disappearing-color: #{colors.$hp-red-disappearing};
	--container-background-color: #{colors.$off-white};
	--slot-background-color: #{colors.$white};
	--slot-highlight-color: #{colors.$red};
	--surface: #{colors.$surface};
	--surface-alpha: #{colors.$surface-alpha};
	--accent: #{colors.$accent};
	--accent-light: #{colors.$accent-light};
	--text-primary: #{colors.$text-primary};
	--text-secondary: #{colors.$text-secondary};
	--text-tertiary: #{colors.$text-tertiary};
	--text-muted: #{colors.$text-muted};
	--border-subtle: #{colors.$border-subtle};
	--border-light: #{colors.$border-light};
	--border-medium: #{colors.$border-medium};
	--success: #{colors.$success};
	--warning: #{colors.$warning};
	--enemy-accent: #{colors.$enemy-accent};
}
</style>
