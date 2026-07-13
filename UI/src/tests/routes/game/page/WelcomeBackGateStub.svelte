<!-- Test stub for WelcomeBackGate: a single button that invokes the real `onEnter` callback the shell
     wires to `welcome.enter()`, so the page test can drive the 'summary' -> 'entered' transition. -->
<button data-testid="welcome-gate-enter" onclick={onEnter}>Enter</button>

<script lang="ts">
const { onEnter }: { summary: unknown; onEnter: () => void } = $props();
</script>
