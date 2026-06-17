<div class="mode-toggle">
	{mode === 'login' ? 'No account yet?' : 'Already a hero?'}
	<button class="mode-link" data-testid="mode-toggle" onclick={onToggle}>
		{mode === 'login' ? 'Create one' : 'Sign in'}
	</button>
</div>

<script lang="ts">
import type { LoginMode } from './login-validation';

interface Props {
	mode: LoginMode;
	onToggle: () => void;
}

const { mode, onToggle }: Props = $props();
</script>

<style lang="scss">
.mode-toggle {
	margin-top: 28px;
	text-align: center;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-secondary);
	letter-spacing: 0.4px;
}

.mode-link {
	color: var(--accent-light);
	cursor: pointer;
	background: none;
	border: none;
	border-bottom: 1px solid color-mix(in srgb, var(--accent-light) 55%, transparent);
	padding: 0 0 1px;
	font-family: inherit;
	font-size: inherit;
	letter-spacing: inherit;

	&:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
	}
}
</style>
