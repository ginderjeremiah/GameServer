{#if showSpinner}
	{#if !hideOverlay}
		<div class="gray-overlay"></div>
	{/if}
	<div class="loading-spinner-container" {style}>
		<div class="loading-spinner">
			<div class="spinner-dot-container">
				<div class="loading-spinner-dot"></div>
			</div>
			<div class="spinner-dot-container">
				<div class="loading-spinner-dot"></div>
			</div>
			<div class="spinner-dot-container">
				<div class="loading-spinner-dot"></div>
			</div>
			<div class="spinner-dot-container">
				<div class="loading-spinner-dot"></div>
			</div>
			<div class="spinner-dot-container">
				<div class="loading-spinner-dot"></div>
			</div>
		</div>
	</div>
{/if}

<script lang="ts">
import { untrack } from 'svelte';

const { hideOverlay = false, loading = true, minimumLoadMs = 0 } = $props();

let lockSpinner = performance.now();
let showSpinner = $state(loading);

const offset = Math.floor(Math.random() * 360);
const style = `--spinner-start-offset: ${offset}deg`;

$effect(() => {
	if (loading) {
		lockSpinner = performance.now() + minimumLoadMs;
		showSpinner = true;
	} else if (minimumLoadMs) {
		setTimeout(() => {
			showSpinner = false;
		}, lockSpinner - performance.now());
	} else {
		showSpinner = false;
	}
});
</script>

<style lang="scss">
$duration: 1.2s;
$rotation-step: 15deg;
$delay-step: -0.04;
$outer-rotation-count: 3;

.gray-overlay {
	position: absolute;
	width: 100%;
	height: 100%;
	top: 0;
	left: 0;
	border-radius: inherit;
	background-color: var(--overlay-color);
	opacity: 60%;
	cursor: not-allowed;
	z-index: 10;
}

.loading-spinner-container {
	position: absolute;
	top: 50%;
	left: 50%;
	transform: translate(-50%, -50%);
	height: 100%;
	max-height: 10em;
	aspect-ratio: 1;
	z-index: 11;
	cursor: not-allowed;
}

.loading-spinner {
	animation: spinner-container $duration * $outer-rotation-count linear infinite;
	transform-origin: 50% 50%;
	height: 100%;
	aspect-ratio: 1;
}

.spinner-dot-container {
	animation: loading-spinner $duration cubic-bezier(0.7, 0, 0.3, 1) infinite;
	width: 70%;
	height: 70%;
	transform-origin: 50% 50%;
	position: absolute;
	top: 15%;
	left: 15%;
}

.loading-spinner-dot {
	width: 100%;
	height: 100%;
}

.loading-spinner-dot:after {
	content: ' ';
	display: block;
	width: 15%;
	height: 15%;
	border-radius: 50%;
	background: var(--spinner-color);
}

.spinner-dot-container:nth-child(1) {
	animation-delay: $duration * $delay-step;

	.loading-spinner-dot {
		transform: rotate($rotation-step);
		transform-origin: 50% 50%;
	}
}

.spinner-dot-container:nth-child(2) {
	animation-delay: $duration * $delay-step * 2;

	.loading-spinner-dot {
		transform: rotate($rotation-step * 2);
		transform-origin: 50% 50%;
	}
}

.spinner-dot-container:nth-child(3) {
	animation-delay: $duration * $delay-step * 3;

	.loading-spinner-dot {
		transform: rotate($rotation-step * 3);
		transform-origin: 50% 50%;
	}
}

.spinner-dot-container:nth-child(4) {
	animation-delay: $duration * $delay-step * 4;

	.loading-spinner-dot {
		transform: rotate($rotation-step * 4);
		transform-origin: 50% 50%;
	}
}

.spinner-dot-container:nth-child(5) {
	animation-delay: $duration * $delay-step * 5;

	.loading-spinner-dot {
		transform: rotate($rotation-step * 5);
		transform-origin: 50% 50%;
	}
}

@keyframes loading-spinner {
	0% {
		transform: rotate(calc(0deg + var(--spinner-start-offset)));
	}

	100% {
		transform: rotate(calc(360deg + var(--spinner-start-offset)));
	}
}

@keyframes spinner-container {
	0% {
		transform: rotate(calc(0deg + var(--spinner-start-offset)));
	}

	20% {
		transform: rotate(calc(90deg + var(--spinner-start-offset)));
	}

	33% {
		transform: rotate(calc(120deg + var(--spinner-start-offset)));
	}

	54% {
		transform: rotate(calc(210deg + var(--spinner-start-offset)));
	}

	67% {
		transform: rotate(calc(240deg + var(--spinner-start-offset)));
	}

	86% {
		transform: rotate(calc(330deg + var(--spinner-start-offset)));
	}

	100% {
		transform: rotate(calc(360deg + var(--spinner-start-offset)));
	}
}
</style>
