namespace BookSearch.Api.Models;

public class BookSearchRequest
{
    public string? Q { get; set; }
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public record BookHit(string Id, double? Score, Book Book);

public record SearchResult(long Total, int Page, int PageSize, IReadOnlyList<BookHit> Items);
