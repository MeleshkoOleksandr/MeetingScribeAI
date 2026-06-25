using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MeetingScribe.Logic;

public static class SecretsManager
{
    private static readonly string SecretPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "secrets.json");

    public static Dictionary<string, string> LoadKeys()
    {
        if (!File.Exists(SecretPath)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SecretPath)) ?? new();
        }
        catch { return new Dictionary<string, string>(); }
    }

    public static void SaveKey(string providerId, string key)
    {
        var keys = LoadKeys();
        keys[providerId] = key;
        File.WriteAllText(SecretPath, JsonSerializer.Serialize(keys, new JsonSerializerOptions { WriteIndented = true }));
    }
}