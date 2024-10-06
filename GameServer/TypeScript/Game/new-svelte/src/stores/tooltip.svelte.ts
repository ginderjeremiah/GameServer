import { onDestroy } from "svelte";

export interface TooltipComponent {
   getBaseNode: () => HTMLDivElement;
};

export interface TooltipData {
   component: () => TooltipComponent | undefined;
   position?: Position
   visible: boolean
}

export interface Position {
   x: number,
   y: number
}

let tooltipsData = $state<TooltipData[]>([]);

export const tooltips = {
   get data() {
      return tooltipsData;
   }
};

export const registerTooltipComponent = <T extends TooltipComponent>(component: () => T | undefined) => {
   let data = $state<TooltipData>({ component, visible: false, position: undefined });

   const setTooltipPosition = (position: Position) => {
      data.position = position;
   }

   const showTooltip = () => {
      data.visible = true;
   }

   const hideTooltip = () => {
      data.visible = false;
   }

   tooltipsData.push(data);

   onDestroy(() => {
      const index = tooltipsData.findIndex((tData) => tData === data)
      tooltipsData.splice(index, 1);
   });

   return {
      setTooltipPosition,
      showTooltip,
      hideTooltip,
   }
}