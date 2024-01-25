class ListNode<Type extends Listable<Type>> {
    data: Type;
    prev: ListNode<Type> | null;
    next: ListNode<Type> | null;
    list: LinkedList<Type>;

    constructor(list: LinkedList<Type>, data: Type) {
        this.list = list;
        data.lNode = this;
        this.data = data;
        this.next = null;
        this.prev = null;
    }

    sortSelf(): void {
        if (this.prev && this.list.comparator(this.prev.data, this.data)) {
            if (this.list.tail === this) {
                this.list.tail = this.prev;
            }
            if (this.next) {
                this.next.prev = this.prev;
            }
            this.prev.next = this.next;
            let prev = this.prev.prev;
            while (prev && this.list.comparator(prev.data, this.data)) {
                prev = prev.prev;
            }
            this.prev = prev;
            if (prev) {
                this.next = prev.next;
                this.next!.prev = this;
                this.prev!.next = this;
            } else {
                this.list.head!.prev = this;
                this.next = this.list.head;
                this.list.head = this;
            }
        } else if (this.next && !this.list.comparator(this.next.data, this.data)) {
            if (this.list.head === this) {
                this.list.head = this.next;
            }
            if (this.prev) {
                this.prev.next = this.next;
            }
            this.next.prev = this.prev;
            let next = this.next.next;
            while (next && !this.list.comparator(next.data, this.data)) {
                next = next.next;
            }
            this.next = next;
            if (next) {
                this.prev = next.prev;
                this.prev!.next = this;
                this.next!.prev = this;
            } else {
                this.list.tail!.next = this;
                this.prev = this.list.tail;
                this.list.tail = this;
            }
        }
    }
    /*
    sortSelf() {
        if(this.prev && this.list.comparator(this.prev.data, this.data)) {
            if(this.list.tail === this) {
                this.list.tail = this.prev;
            }
            if(this.next) {
                this.next.prev = this.prev;
            }
            this.prev.next = this.next;
            let curr = this.prev;
            let prev = curr.prev;
            while(curr && )
        } else if (this.next && !this.list.comparator(this.prev.data, this.data))
    }
    */

    removeSelf(): void {
        if (this === this.list.head) {
            this.list.head = this.next;
        }
        if (this === this.list.tail) {
            this.list.tail = this.prev;
        }
        this.prev && (this.prev.next = this.next);
        this.next && (this.next.prev = this.prev);
    }
}
