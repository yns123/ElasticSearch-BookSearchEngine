namespace BookSearch.Api.Models;

public class BookSearchRequest
{
    public string? Q { get; set; }
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    // Aşama 5: önceki yanıttaki NextCursor. Doluysa from/page yerine search_after kullanılır.
    public string? Cursor { get; set; }

    // Aşama 6: true ise function_score ile popularity alanı skora eklenir.
    public bool PopularityBoost { get; set; }
}

public record BookHit(string Id, double? Score, Book Book, IReadOnlyDictionary<string, string[]>? Highlights);

public record FacetBucket(string Key, long Count);

public record Facets(
    IReadOnlyList<FacetBucket> Categories,
    IReadOnlyList<FacetBucket> Authors,
    IReadOnlyList<FacetBucket> PriceRanges,
    double? AvgRating);

public record SearchResult(
    long Total, int Page, int PageSize, IReadOnlyList<BookHit> Items, Facets Facets, string? NextCursor);
