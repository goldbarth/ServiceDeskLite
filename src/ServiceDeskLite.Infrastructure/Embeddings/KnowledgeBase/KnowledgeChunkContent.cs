using System.Security.Cryptography;
using System.Text;

namespace ServiceDeskLite.Infrastructure.Embeddings.KnowledgeBase;

/// <summary>
/// Defines what "a knowledge chunk" means for embedding purposes. The worker and
/// search share this so the staleness check (hash comparison) can never drift from
/// the embedded text. The article title and section heading are prefixed onto the
/// body so a short passage still carries the context that makes it findable.
/// </summary>
public static class KnowledgeChunkContent
{
    public static string Build(string title, string heading, string body) =>
        $"{title} — {heading}\n\n{body}";

    public static string Hash(string title, string heading, string body) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(Build(title, heading, body))));
}
