// randomInt between num1 (inclusive) and num2 (exclusive)
function randomInt(num1: number, num2: number): number {
    return Math.floor(num1 + Math.random() * (num2 - num1));
}

// formats a number to a specific string representation
function formatNum(num: number): string {
    return "" + parseFloat(num.toFixed(2));
}

async function delay(delay: number) {
    return new Promise<void>(res => {
        setTimeout(() => res(), delay);
    });
    
}

function keys(obj?: {}) {
    return obj ? Object.keys(obj) : [];
}

function capitalize(str: string) {
    return str[0].toUpperCase() + str.slice(1);
}

function normalizeText(str: string) {
    return capitalize(str).replaceAll(/([a-z])([A-Z])/g, "$1 $2")
}

function plural(str: string) {
    const last = str.length - 1;
    switch (str.at(last)) {
        case 'y':
            return str.slice(0, last) + 'ies';
        case 'x':
            return str + 'es';
        case 's':
            return str;
        default:
            return str + 's';
    }
}

function groupBy<T>(arr: T[], groupFn: (item: T) => string) {
    const ret: { [key: string]: T[] } = {};
    arr.forEach(t => {
        const key = groupFn(t);
        if (ret[key]) {
            ret[key].push(t);
        } else {
            ret[key] = [t];
        }
    })
    return ret;
}