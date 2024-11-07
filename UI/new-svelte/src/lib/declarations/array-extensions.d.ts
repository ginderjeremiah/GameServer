interface Array<T> {
   mapNotNull<U>(selector: NullableSelector<T, U>): Array<U>
   getNotNull(): Array<NonNullable<T>>
}

type NullableSelector<T, U> = (data: T) => U | undefined | null

Object.defineProperty(Array.prototype, "mapNotNull", {
   value: (selector: NullableSelector<T, U>) => {
      return Array.from(notNullGenerator(mapGenerator(this, selector)));
   }
});

Object.defineProperty(Array.prototype, "getNotNull", {
   value: (selector: NullableSelector<T, U>) => {
      return Array.from(notNullGenerator(this))
   }
});

function* notNullGenerator<T>(array: Iterable<T> | ArrayLike<T>) {
   for (const item of array) {
      if (item !== null && item !== undefined) {
         yield item;
      }
   }
}

function* mapGenerator<T, U>(array: Iterable<T> | ArrayLike<T>, selector: (data: T) => U) {
   for (const item of array) {
      yield selector(item);
   }
}
