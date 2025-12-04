using System;
using System.IO;
using System.Linq;
using CacheManager.Core;

const string OpsFilePath = "cache_ops.txt";
const string SnapshotFilePath = "cache_state.txt";

var start = DateTime.UtcNow;

if (!File.Exists(OpsFilePath))
{
    Console.WriteLine($"Operations file not found: {OpsFilePath}");
    Console.WriteLine("Run CacheManager.OpGenerator first to create it.");
    return;
}

int iterations = args.Length > 0 && int.TryParse(args[0], out var parsedIterations) && parsedIterations > 0 ? parsedIterations : 1;

ICacheManager<string, string> cache = new CacheManager<string, string>();

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

if (!File.Exists(SnapshotFilePath))
{
    File.WriteAllText(SnapshotFilePath, newContent);
    Console.WriteLine($"Snapshot file created: {SnapshotFilePath}");
    Console.WriteLine("Baseline cache state written.");
}
else
{
    var previousContent = File.ReadAllText(SnapshotFilePath);
    var elapsed = DateTime.UtcNow - start;
    double managedBytes = GC.GetTotalMemory(false);
    double managedMb = managedBytes / (1024 * 1024);

    if (string.Equals(previousContent, newContent, StringComparison.Ordinal))
    {
        Console.WriteLine($"Cache behavior is consistent with previous run. 🙂 Elapsed: {elapsed.TotalSeconds:F2} s, Managed memory: {managedMb:F2} MB");
    }
    else
    {
        Console.WriteLine($"Cache behavior is NOT consistent with previous run. 🙁 Elapsed: {elapsed.TotalSeconds:F2} s, Managed memory: {managedMb:F2} MB");
    }
}
