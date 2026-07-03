using System.Security.Cryptography;
using System.Text;

namespace ServiceDeskLite.Infrastructure.Embeddings;

/// <summary>
/// Defines what "the ticket content" means for embedding purposes. Worker and
/// tests share this so the staleness check (hash comparison) can never drift
/// from the embedded text.
/// </summary>
public static class TicketEmbeddingContent
{
    public static string Build(string title, string description) =>
        $"{title}\n\n{description}";

    public static string Hash(string title, string description) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Build(title, description))));
}
