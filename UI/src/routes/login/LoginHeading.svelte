<div class="heading">
	<h1 data-testid="login-heading">{mode === 'login' ? 'Welcome back.' : 'Begin a new run.'}</h1>
	<p class="subtitle">
		{mode === 'login' ? 'Sign in to continue.' : 'Pick a name. Forge ahead.'}
	</p>
</div>

<script lang="ts">
import type { LoginMode } from './login-validation';

const { mode }: { mode: LoginMode } = $props();
</script>

<style lang="scss">
.heading {
	text-align: center;
	margin-bottom: 42px;

	h1 {
		margin: 0;
		padding: 0;
		font-size: 32px;
		font-weight: 400;
		letter-spacing: -0.5px;
		line-height: 1.1;
		color: var(--text-primary);
	}

	.subtitle {
		margin: 10px 0 0;
		font-size: 12.5px;
		color: var(--text-secondary);
		font-family: var(--mono);
		letter-spacing: 0.6px;
	}
}
</style>
