<!-- Test stub for PlaceholderScreen: renders the `label` prop so the page test can assert the
     registry's label reaches an unbuilt/action-only screen entry's fallback. -->
<div data-testid="placeholder-screen">{label}</div>

<script lang="ts">
const { label }: { label: string } = $props();
</script>
