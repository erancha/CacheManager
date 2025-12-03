# CacheManager (.NET 8)

This solution contains a simple generic in-memory cache manager and two console applications:

- **CacheManager.Core**

  - Class library with a generic cache manager interface and implementation using `Dictionary<TKey, TValue>`.
  - Types:
    - `ICacheManager<TKey, TValue>`: basic `Put`, `TryGet`, `Remove`, and `Snapshot` operations.
    - `CacheManager<TKey, TValue>`: concrete implementation backed by `Dictionary<TKey, TValue>`.

- **CacheManager.OpGenerator**

  - Console app that generates a randomized sequence of cache operations (PUT / GET / REMOVE).
  - Writes them into a text file named `cache_ops.txt` in the solution root.

- **CacheManager.Tester**
  - Console app that reads `cache_ops.txt`, executes the operations on the cache, and then:
    - Writes the final cache state into `cache_state.txt` (if it does not yet exist).
    - On subsequent runs, compares the new cache state to the existing `cache_state.txt` to check for consistent behavior.

## Prerequisites

- .NET SDK 8 (or later compatible) installed.

## How to run

From a terminal / bash shell, go to the solution folder:

```bash
cd /mnt/c/Projects/CacheManager
```

### 1. Generate randomized operations

By default, the generator creates **1000 operations** over **100 keys**.

```bash
dotnet run --project ./CacheManager.OpGenerator/CacheManager.OpGenerator.csproj
```

You can optionally pass:

- **First argument**: number of operations
- **Second argument**: number of distinct keys

Example (5000 operations over 200 keys):

```bash
dotnet run --project ./CacheManager.OpGenerator/CacheManager.OpGenerator.csproj -- 1000000 10000
```

This creates or overwrites `cache_ops.txt` in `/mnt/c/Projects/CacheManager`.

### 2. Execute operations and write/compare cache state

```bash
dotnet run --project ./CacheManager.Tester/CacheManager.Tester.csproj -- 100
```

In this example, the tester will execute the operations loop **5 times** over the same `cache_ops.txt` contents before taking the snapshot / comparison (default is **1** iteration if no argument is provided).

Behavior:

- **First run**: creates `cache_state.txt` from the current final cache contents and reports that a baseline snapshot was written.
- **Subsequent runs with the same `cache_ops.txt`**: prints a message indicating whether the behavior is consistent with the previous run and shows the elapsed time for the execution.
