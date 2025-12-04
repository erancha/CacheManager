# CacheManager Node.js

This folder contains a Node.js implementation of the CacheManager from the C# project and a small tester program that reads `../cache_ops.txt` from the project root and writes snapshot files.

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
