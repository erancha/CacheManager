'use strict';

const fs = require('fs');
const path = require('path');
const os = require('os');
const { CacheManager } = require('./cacheManager');

const OpsFilePath = path.join(process.cwd(), '../cache_ops.txt');

if (!fs.existsSync(OpsFilePath)) {
  console.log(`Operations file not found: ${OpsFilePath}`);
  console.log('Run CacheManager.OpGenerator first to create it.');
  process.exit(0);
}

const rawArgs = process.argv.slice(2);
const iterations = rawArgs.length > 0 && Number.isInteger(Number(rawArgs[0])) && Number(rawArgs[0]) > 0 ? Number(rawArgs[0]) : 1;

const capacity = rawArgs.length > 1 && Number.isInteger(Number(rawArgs[1])) && Number(rawArgs[1]) > 0 ? Number(rawArgs[1]) : null;

const snapshotFilePathName = capacity !== null ? `../cache_state_capacity_${capacity}.txt` : '../cache_state_unlimited.txt';

const snapshotFilePath = path.join(process.cwd(), snapshotFilePathName);

const cache = capacity !== null ? new CacheManager(capacity) : new CacheManager();

const start = Date.now();
const content = fs.readFileSync(OpsFilePath, 'utf8');
const lines = content.split(/\r?\n/);

for (let iter = 0; iter < iterations; iter++) {
  for (const line of lines) {
    if (!line || !line.trim()) continue;
    const parts = line.split(' ').filter((p) => p.length > 0);
    if (parts.length === 0) continue;
    const command = parts[0].toUpperCase();

    switch (command) {
      case 'PUT':
        if (parts.length >= 3) {
          const key = parts[1];
          const value = parts.slice(2).join(' ');
          cache.put(key, value);
        }
        break;
      case 'GET':
        if (parts.length >= 2) {
          const key = parts[1];
          cache.tryGet(key);
        }
        break;
      case 'REMOVE':
        if (parts.length >= 2) {
          const key = parts[1];
          cache.remove(key);
        }
        break;
      default:
        // ignore unknown
        break;
    }
  }
}

const managedBytes = process.memoryUsage().heapUsed;
const snapshotObj = cache.snapshot();
const keys = Object.keys(snapshotObj).sort((a, b) => a.localeCompare(b));
const newContent = keys.map((k) => `${k}=${snapshotObj[k]}`).join(os.EOL);

if (!fs.existsSync(snapshotFilePath)) {
  fs.writeFileSync(snapshotFilePath, newContent, 'utf8');
  console.log(`Snapshot file created: ${snapshotFilePathName}`);
  console.log('Baseline cache state written.');
  process.exit(0);
}

const elapsedMs = Date.now() - start;
const managedMb = managedBytes / (1024 * 1024);

const cacheItemCount = cache.Count;
const evictionTrackedCount = cache.EvictionTrackedCount;

const previousContent = fs.readFileSync(snapshotFilePath, 'utf8');
if (previousContent === newContent) {
  console.log(
    `✔️\tCache behavior is consistent with previous run. \tElapsed: ${(elapsedMs / 1000).toFixed(2)} s, Managed memory: ${managedMb.toFixed(
      2
    )} MB, Items: ${cacheItemCount}, Eviction entries: ${evictionTrackedCount}`
  );
} else {
  console.log(
    `❌ Cache behavior is NOT consistent with previous run. \tElapsed: ${(elapsedMs / 1000).toFixed(2)} s, Managed memory: ${managedMb.toFixed(
      2
    )} MB, Items: ${cacheItemCount}, Eviction entries: ${evictionTrackedCount}`
  );
}
