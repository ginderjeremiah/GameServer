{#if !hideOverlay}
	<div class="gray-overlay"></div>
{/if}
<div class="loading-spinner-container">
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

<script lang="ts">
let { hideOverlay = false } = $props();
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
	background-color: var(--overlay-color);
	opacity: 30%;
	pointer-events: none;
	cursor: not-allowed;
}

.loading-spinner-container {
	position: absolute;
	top: 50%;
	left: 50%;
	transform: translate(-50%, -50%);
	height: 100%;
	aspect-ratio: 1;
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
		transform: rotate(0deg);
	}

	100% {
		transform: rotate(360deg);
	}
}

@keyframes spinner-container {
	0% {
		transform: rotate(0deg);
	}

	20% {
		transform: rotate(90deg);
	}

	33% {
		transform: rotate(120deg);
	}

	54% {
		transform: rotate(210deg);
	}

	67% {
		transform: rotate(240deg);
	}

	86% {
		transform: rotate(330deg);
	}

	100% {
		transform: rotate(360deg);
	}
}
</style>
