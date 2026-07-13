<!-- Test stub for AdminSidebar: not exercised by the reference-load tests (the default 'enemies'
     tool never needs a nav switch), so it renders nothing but still accepts the bindable `pinned`
     prop the real page wires up. -->
<div data-testid="admin-sidebar-stub" data-pinned={pinned}></div>

<script lang="ts">
let { pinned = $bindable(false) } = $props();
</script>
