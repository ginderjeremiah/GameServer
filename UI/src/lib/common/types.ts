export type Action<T extends unknown[] = []> = (...args: T) => void;
