using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Umbrella.AI.Tools.Bundling.Services;

internal static class HashUtility
{
    public static string ComputeFileHash(string path) => ComputeStringHash(File.ReadAllText(path));

    public static string ComputeStringHash(string content)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content.Replace("\r\n", "\n", StringComparison.Ordinal)));
        return Convert.ToHexString(bytes);
    }

    public static string ComputeJsonHash(JsonNode node)
        => ComputeStringHash(node.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}