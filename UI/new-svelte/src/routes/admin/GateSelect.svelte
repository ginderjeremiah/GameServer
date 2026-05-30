<div class="gate">
	<span class="gate-label">{label}</span>
	<div class="gate-control">
		<select class="gate-select" bind:value>
			<option value={-1}>{placeholder}</option>
			{#each options as option (option.id)}
				<option value={option.id}>{option.name}</option>
			{/each}
		</select>
		<svg class="gate-caret" width="10" height="10" viewBox="0 0 10 10" fill="none" stroke="rgba(240,240,240,0.6)" stroke-width="1.4" aria-hidden="true">
			<path d="M2 3.5L5 6.5L8 3.5" stroke-linecap="round" stroke-linejoin="round" />
		</svg>
	</div>
</div>

<script lang="ts">
interface Props {
	label: string;
	options: { id: number; name: string }[];
	value: number;
	placeholder?: string;
}

let { label, options, value = $bindable(), placeholder = 'Select…' }: Props = $props();
</script>

<style lang="scss">
.gate {
	display: flex;
	align-items: center;
	gap: 14px;
}

.gate-label {
	font-family: 'Geist Mono', monospace;
	font-size: 10px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.5);
}

.gate-control {
	position: relative;
	width: 240px;
}

.gate-select {
	width: 100%;
	appearance: none;
	-webkit-appearance: none;
	background: rgba(255, 255, 255, 0.03);
	border: 1px solid rgba(255, 255, 255, 0.1);
	border-radius: 3px;
	color: #f0f0f0;
	font-family: Geist, sans-serif;
	font-size: 13.5px;
	padding: 8px 28px 8px 12px;
	outline: none;
	cursor: pointer;
	transition: border-color 140ms ease, box-shadow 140ms ease;

	&:hover {
		border-color: rgba(255, 255, 255, 0.2);
	}

	&:focus {
		border-color: var(--accent);
		box-shadow: 0 0 0 1px rgba(161, 194, 247, 0.4), 0 0 8px rgba(161, 194, 247, 0.35);
	}

	option {
		background: #14151b;
		color: #f0f0f0;
	}
}

.gate-caret {
	position: absolute;
	right: 11px;
	top: 50%;
	transform: translateY(-50%);
	pointer-events: none;
}
</style>
