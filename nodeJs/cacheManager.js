'use strict';

class DoublyLinkedListNode {
  constructor(key) {
    this.key = key;
    this.prev = null;
    this.next = null;
  }
}

class DoublyLinkedList {
  constructor() {
    this.head = null;
    this.tail = null;
    this.count = 0;
  }

  addFirst(key) {
    const node = new DoublyLinkedListNode(key);
    if (!this.head) {
      this.head = this.tail = node;
    } else {
      node.next = this.head;
      this.head.prev = node;
      this.head = node;
    }
    this.count++;
    return node;
  }

  remove(node) {
    if (!node) return;
    if (node.prev) node.prev.next = node.next;
    else this.head = node.next;

    if (node.next) node.next.prev = node.prev;
    else this.tail = node.prev;

    node.prev = node.next = null;
    this.count--;
  }

  removeLast() {
    if (!this.tail) return null;
    const node = this.tail;
    this.remove(node);
    return node;
  }
}

class EvictionManager {
  constructor(capacity) {
    this.capacity = capacity;
    this.freqMap = new Map(); // frequency -> DoublyLinkedList of keys (head = most recent)
    this.entryByKey = new Map(); // key -> { count, node }
  }

  get trackedCount() {
    return this.entryByKey.size;
  }

  onAddNew(key) {
    const initial = 1;
    let list = this.freqMap.get(initial);
    if (!list) {
      list = new DoublyLinkedList();
      this.freqMap.set(initial, list);
    }
    const node = list.addFirst(key);
    this.entryByKey.set(key, { count: initial, node });
  }

  onAccess(key) {
    const entry = this.entryByKey.get(key);
    if (!entry) {
      console.warn(`[WARNING] EvictionManager.onAccess: key '${key}' not found`);
      return;
    }

    const oldCount = entry.count;
    const oldList = this.freqMap.get(oldCount);
    if (oldList) {
      oldList.remove(entry.node);
      if (oldList.count === 0) this.freqMap.delete(oldCount);
    }

    const newCount = oldCount + 1;
    let newList = this.freqMap.get(newCount);
    if (!newList) {
      newList = new DoublyLinkedList();
      this.freqMap.set(newCount, newList);
    }
    const newNode = newList.addFirst(key);
    entry.count = newCount;
    entry.node = newNode;
  }

  onRemove(key) {
    const entry = this.entryByKey.get(key);
    if (!entry) {
      console.warn(`[WARNING] EvictionManager.onRemove: key '${key}' not found`);
      return;
    }

    const list = this.freqMap.get(entry.count);
    if (list) {
      list.remove(entry.node);
      if (list.count === 0) this.freqMap.delete(entry.count);
    }

    this.entryByKey.delete(key);
  }

  tryGetEvictionCandidate(currentItemCount) {
    if (currentItemCount <= this.capacity || this.entryByKey.size === 0) {
      return { found: false };
    }

    // find the smallest frequency
    let min = Infinity;
    for (const f of this.freqMap.keys()) {
      if (f < min) min = f;
    }

    const list = this.freqMap.get(min);
    const node = list.removeLast();
    const key = node.key;

    if (list.count === 0) this.freqMap.delete(min);
    this.entryByKey.delete(key);

    return { found: true, key };
  }
}

class CacheManager {
  constructor(maxCapacity = null) {
    this.items = new Map();
    this.evictionManager = typeof maxCapacity === 'number' && maxCapacity > 0 ? new EvictionManager(maxCapacity) : null;
  }

  put(key, value) {
    const exists = this.items.has(key);
    this.items.set(key, value);

    if (this.evictionManager) {
      if (exists) {
        this.evictionManager.onAccess(key);
      } else {
        this.evictionManager.onAddNew(key);
        const result = this.evictionManager.tryGetEvictionCandidate(this.items.size);
        if (result.found && result.key !== key) {
          this.items.delete(result.key);
        }
      }
    }
  }

  tryGet(key) {
    if (this.items.has(key)) {
      const v = this.items.get(key);
      if (this.evictionManager) this.evictionManager.onAccess(key);
      return { found: true, value: v };
    }
    return { found: false, value: undefined };
  }

  remove(key) {
    const removed = this.items.delete(key);
    if (removed && this.evictionManager) this.evictionManager.onRemove(key);
    return removed;
  }

  snapshot() {
    const obj = Object.create(null);
    for (const [k, v] of this.items.entries()) obj[k] = v;
    return obj;
  }

  get Count() {
    return this.items.size;
  }

  get EvictionTrackedCount() {
    return this.evictionManager ? this.evictionManager.trackedCount : 0;
  }
}

module.exports = { CacheManager };
