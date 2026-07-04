// Dedicated module worker driving `LogicalEngine`'s tick source (see `tick-source.ts`). A worker's
// own timers keep running at full rate while the page is hidden, and posting a message to a hidden
// page is not throttled either — so this is the whole fix for background-tab throttling.
import { pollingIntervalMs } from './tick-source';

setInterval(() => postMessage(null), pollingIntervalMs);
