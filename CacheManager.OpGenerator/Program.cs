using System;
using System.Collections.Generic;
using System.IO;

const string OpsFilePath = "cache_ops.txt";

int operationsCount = args.Length > 0 && int.TryParse(args[0], out var parsedOps) ? parsedOps : 1000;
int keyCount = args.Length > 1 && int.TryParse(args[1], out var parsedKeys) ? parsedKeys : 100;

var random = new Random();
var keys = new List<string>();

for (int i = 0; i < keyCount; i++)
{
    keys.Add($"key{i}");
}

var lines = new List<string>();

for (int i = 0; i < operationsCount; i++)
{
    int op = random.Next(0, 3); // 0=PUT, 1=GET, 2=REMOVE
    string key = keys[random.Next(keys.Count)];

    switch (op)
    {
        case 0: // PUT
            string value = $"value_{random.Next(0, 1000)}";
            lines.Add($"PUT {key} {value}");
            break;
        case 1: // GET
            lines.Add($"GET {key}");
            break;
        case 2: // REMOVE
            lines.Add($"REMOVE {key}");
            break;
    }
}

File.WriteAllLines(OpsFilePath, lines);

var cacheStatePath = "cache_state.txt";
if (File.Exists(cacheStatePath))
{
    File.Delete(cacheStatePath);
}

Console.WriteLine($"Generated {lines.Count} operations into {OpsFilePath}.");
