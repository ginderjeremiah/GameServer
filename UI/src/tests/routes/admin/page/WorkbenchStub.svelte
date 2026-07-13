<!-- Test stub for Workbench: renders the entity label so the reference-load test can assert the
     real tool content appears once the reference catalogues load successfully, without pulling in
     the real entity-authoring subtree. -->
<div data-testid="workbench-stub">{entity.label}</div>

<script lang="ts">
let { entity } = $props();
</script>
