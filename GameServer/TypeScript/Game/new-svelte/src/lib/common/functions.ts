import { browser } from "$app/environment";
import { goto } from "$app/navigation";
import { redirect } from "@sveltejs/kit";

export function routeTo(route: string, redirectCode?: 302 | 307) {
    browser ? goto(route) : redirect(redirectCode ?? 302, route);
}

// randomInt between num1 (inclusive) and num2 (exclusive)
export function randomInt(num1: number, num2: number): number {
    return Math.floor(num1 + Math.random() * (num2 - num1));
}

// formats a number to a specific string representation
export function formatNum(num: number): string {
    return "" + parseFloat(num.toFixed(2));
}

export async function delay(delay: number) {
    return new Promise<void>(res => {
        setTimeout(res, delay);
    });
}

export function keys(obj?: {}) {
    return obj ? Object.keys(obj) : [];
}

export function enumPairs(obj?: any) {
    const allKeys = keys(obj);
    return allKeys.slice(0, allKeys.length / 2).map(key => ({ id: key as unknown as number, name: obj[key] as string }))
}

export function capitalize(str: string) {
    return str[0].toUpperCase() + str.slice(1);
}

export function normalizeText(str: string) {
    return capitalize(str).replaceAll(/([a-z])([A-Z])/g, "$1 $2")
}

export function plural(str: string) {
    const last = str.length - 1;
    switch (str.at(last)) {
        case 'y':
            return str.slice(0, last) + 'ies';
        case 'x':
        case 's':
            return str + 'es';
        default:
            return str + 's';
    }
}

export function groupBy<T>(arr: T[], groupFn: (item: T) => string) {
    const ret: { [key: string]: T[] } = {};
    for (const t of arr) {
        const key = groupFn(t);
        if (ret[key]) {
            ret[key].push(t);
        } else {
            ret[key] = [t];
        }
    }
    return ret;
}