namespace ServiceDeskLite.Infrastructure.Embeddings;

/// <summary>
/// Voyage distinguishes how text is embedded: documents (stored tickets) and
/// queries (search input) get different instruction prefixes, which measurably
/// improves retrieval quality.
/// </summary>
public enum EmbeddingInputType
{
    Document,
    Query,
}

public interface IEmbeddingClient
{
    /// <summary>Embeds all inputs in one API call; result order matches input order.</summary>
    Task<IReadOnlyList<float[]>> EmbedAsync(
        IReadOnlyList<string> inputs,
        EmbeddingInputType inputType,
        CancellationToken ct);
}
