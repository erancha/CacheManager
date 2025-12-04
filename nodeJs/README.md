# CacheManager Node.js

This folder contains a Node.js implementation of the CacheManager from the C# project and a small tester program that reads `../cache_ops.txt` from the project root and compares the generated cache state to the existing cache_state.txt to check for consistent behavior.

## How to run

```bash
cd /mnt/c/Projects/CacheManager/nodeJs
node tester.js [iterations] [capacity]
```

- Examples:

```bash
node tester.js
node tester.js 2 100
```
