import { onDestroy, SvelteComponent } from "svelte";
import { ReadableEx, readableEx } from "$lib/common";

export interface TooltipComponent extends SvelteComponent {
   getBaseNode: () => HTMLDivElement;
}

export interface TooltipData {
   component: ReadableEx<TooltipComponent>;
   position?: Position
   visible: boolean
}

export interface Position {
   x: number,
   y: number
}

export const tooltips = readableEx<ReadableEx<TooltipData>[]>([]);

export const registerTooltipComponent = <T extends TooltipComponent>(component: ReadableEx<T>) => {
   //this will capture the private setter for the individual tooltip data stores
   let tooltipDataSet: (data: TooltipData) => void;

   //use local variable to track data in case multiple sets are called in a row
   //this will allow all the changes to be saved instead of only the most recent.
   let data: TooltipData = { component, visible: false };

   const tooltipDataStore = readableEx(data, (set) => { tooltipDataSet = set })

   const setTooltipPosition = (position: Position) => {
      data.position = position;
      tooltipDataSet(data);
   }

   const showTooltip = () => {
      data.visible = true;
      tooltipDataSet(data);
   }

   const hideTooltip = () => {
      data.visible = false;
      tooltipDataSet(data);
   }

   tooltips.value.push(tooltipDataStore);
   tooltips.refresh();

   onDestroy(() => {
      const index = tooltips.value.findIndex((dataStore) => dataStore === tooltipDataStore)
      tooltips.value.splice(index, 1);
      tooltips.refresh();
   });

   return {
      setTooltipPosition,
      showTooltip,
      hideTooltip,
   }
}