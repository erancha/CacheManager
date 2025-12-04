using System;
using System.IO;
using System.Linq;
using CacheManager.Core;

const string OpsFilePath = "cache_ops.txt";

var start = DateTime.UtcNow;

if (!File.Exists(OpsFilePath))
{
    Console.WriteLine($"Operations file not found: {OpsFilePath}");
    Console.WriteLine("Run CacheManager.OpGenerator first to create it.");
    return;
}

int iterations = args.Length > 0 && int.TryParse(args[0], out var parsedIterations) && parsedIterations > 0
    ? parsedIterations
    : 1;

int? capacity = args.Length > 1 && int.TryParse(args[1], out var parsedCapacity) && parsedCapacity > 0
    ? parsedCapacity
    : null;

string snapshotFilePath = capacity.HasValue
    ? $"cache_state_capacity_{capacity.Value}.txt"
    : "cache_state_unlimited.txt";

ICacheManager<string, string> cache = capacity.HasValue
    ? new CacheManager<string, string>(capacity.Value)
    : new CacheManager<string, string>();

var lines = File.ReadAllLines(OpsFilePath);

for (int iteration = 0; iteration < iterations; iteration++)
{
    foreach (var line in lines)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            continue;
        }

        var command = parts[0].ToUpperInvariant();

        switch (command)
        {
            case "PUT" when parts.Length >= 3:
            {
                string key = parts[1];
                string value = string.Join(' ', parts.Skip(2));
                cache.Put(key, value);
                break;
            }
            case "GET" when parts.Length >= 2:
            {
                string key = parts[1];
                cache.TryGet(key, out _);
                break;
            }
            case "REMOVE" when parts.Length >= 2:
            {
                string key = parts[1];
                cache.Remove(key);
                break;
            }
        }
    }
}

var snapshot = cache.Snapshot()
    .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
    .Select(kvp => $"{kvp.Key}={kvp.Value}")
    .ToArray();

var newContent = string.Join(Environment.NewLine, snapshot);

if (!File.Exists(snapshotFilePath))
{
    File.WriteAllText(snapshotFilePath, newContent);
    Console.WriteLine($"Snapshot file created: {snapshotFilePath}");
    Console.WriteLine("Baseline cache state written.");
}
else
{
    var previousContent = File.ReadAllText(snapshotFilePath);
    var elapsed = DateTime.UtcNow - start;
    double managedBytes = GC.GetTotalMemory(false);
    double managedMb = managedBytes / (1024 * 1024);

    int cacheItemCount = (cache as CacheManager<string, string>)?.Count
        ?? newContent.Split(Environment.NewLine).Length;
    int evictionTrackedCount = (cache as CacheManager<string, string>)?.EvictionTrackedCount ?? 0;

    if (string.Equals(previousContent, newContent, StringComparison.Ordinal))
    {
        Console.WriteLine($"✔️\tCache behavior is consistent with previous run. \tElapsed: {elapsed.TotalSeconds:F2} s, Managed memory: {managedMb:F2} MB, Items: {cacheItemCount}, Eviction entries: {evictionTrackedCount}");
    }
    else
    {
        Console.WriteLine($"❌ Cache behavior is NOT consistent with previous run. \tElapsed: {elapsed.TotalSeconds:F2} s, Managed memory: {managedMb:F2} MB, Items: {cacheItemCount}, Eviction entries: {evictionTrackedCount}");
    }
}
