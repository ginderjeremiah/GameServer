import { ListNode } from "./ListNode";
import { Listable } from "./GlobalInterfaces";
import { Comparator, CheckFunc, Action, Converter } from "./CustomTypes";

export class LinkedList<Type extends Listable<Type>> {
    head: ListNode<Type> | null;
    tail: ListNode<Type> | null;
    length: number;
    comparator: Comparator<Type>;

    constructor(comparator: Comparator<Type>) {
        this.head = null;
        this.tail = null;
        this.length = 0;
        this.comparator = comparator;
    }

    appendData(data: Type): void {
        let node: ListNode<Type> = new ListNode(this, data);
        if (this.tail) {
            node.prev = this.tail;
            this.tail.next = node;
        } else {
            this.head = node;
        }
        this.tail = node;
        this.length++;
    }

    prependData(data: Type): void {
        let node: ListNode<Type> = new ListNode(this, data);
        if (this.head) {
            node.next = this.head;
            this.head.prev = node;
        } else {
            this.tail = node;
        }
        this.head = node;
        this.length++;
    }

    insertSorted(data: Type): void {
        let newNode: ListNode<Type> = new ListNode(this, data);
        if (this.head) {
            let curr: ListNode<Type> | null = this.head;
            while (curr && !this.comparator(curr.data, data)) {
                curr = curr.next;
            }
            if (curr === this.head) {
                this.head.prev = newNode;
                newNode.next = this.head;
                this.head = newNode;
            } else if (curr) {
                newNode.prev = curr.prev;
                newNode.prev!.next = newNode;
                newNode.next = curr;
                curr.prev = newNode;
            } else {
                this.tail!.next = newNode;
                newNode.prev = this.tail;
                this.tail = newNode;
            }
        } else {
            this.head = newNode;
            this.tail = newNode;
        }
        this.length++;
    }

    forEach(func: Action<Type>): void {
        let curr = this.head;
        let next;
        while (curr) {
            next = curr.next;
            func(curr.data);
            curr = next;
        }
    }

    forEachFromEnd(func: Action<Type>): void {
        let curr = this.tail;
        let prev;
        while (curr) {
            prev = curr.prev
            func(curr.data);
            curr = prev;
        }
    }

    select<Type2>(func: Converter<Type, Type2>): Type2[] {
        let results = [] as Type2[];
        let curr = this.head;
        while (curr) {
            results.push(func(curr.data));
            curr = curr.next;
        }
        return results;
    }

    first(func: CheckFunc<Type>): Type | undefined {
        let curr = this.head;
        while (curr && !func(curr.data)) {
            curr = curr.next;
        }
        return curr?.data;
    }

    firstFromEnd(func: CheckFunc<Type>) {
        let curr = this.tail;
        while (curr && !func(curr.data)) {
            curr = curr.prev;
        }
        return curr?.data;
    }

    sort() {
        if (this.head && this.head.next) {
            let curr: ListNode<Type> | null = this.head.next;
            let prev: ListNode<Type> | null = this.head;
            let next = curr.next;
            while (curr) {
                if (this.comparator(prev!.data, curr.data)) {
                    if (curr.next) {
                        curr.next.prev = curr.prev;
                    }
                    curr.prev!.next = curr.next;
                    prev = prev!.prev;
                    while (prev && this.comparator(prev.data, curr.data)) {
                        prev = prev.prev;
                    }
                    curr.prev = prev;
                    if (prev) {
                        curr.next = prev.next;
                        curr.next!.prev = curr;
                        curr.prev!.next = curr;
                    } else {
                        this.head.prev = curr;
                        curr.next = this.head;
                        this.head = curr;
                    }
                }
                curr = next;
                if (curr) {
                    prev = curr.prev;
                    next = curr.next;
                }
            }
        }
    }

    remove(data: Type) {
        let curr = this.head;
        while (curr && curr.data !== data) {
            curr = curr.next;
        }
        if (curr === this.head) {
            this.head = curr!.next;
        }
        if (curr === this.tail) {
            this.tail = curr!.prev;
        }
        curr?.prev && (curr.prev.next = curr.next);
        curr?.next && (curr.next.prev = curr.prev);
    }
}
