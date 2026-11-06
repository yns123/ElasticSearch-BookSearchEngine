using BookSearch.Api.Models;
using BookSearch.Api.Options;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Options;

namespace BookSearch.Api.Services;

public class BookSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _options;

    public BookSearchService(ElasticsearchClient client, IOptions<ElasticsearchOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<PingResult> PingAsync(CancellationToken ct)
    {
        var response = await _client.PingAsync(ct);
        if (!response.IsValidResponse)
        {
            return new PingResult(false, null, null, response.ApiCallDetails.OriginalException?.Message);
        }

        var info = await _client.InfoAsync(ct);
        return new PingResult(true, info.ClusterName, info.Version?.Number, null);
    }

    // Aşama 3: skor isteyen kısım (metin) must'ta, sadece eleme yapan kısımlar (kategori, fiyat) filter'da.
    public async Task<SearchResult> SearchAsync(BookSearchRequest request, CancellationToken ct)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        Query textQuery = string.IsNullOrWhiteSpace(request.Q)
            ? new MatchAllQuery()
            : new MultiMatchQuery
            {
                Query = request.Q,
                Fields = new[] { "title^3", "author^2", "description" }
            };

        var filters = new List<Query>();

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            filters.Add(new TermQuery("category", request.Category));
        }

        if (request.MinPrice is not null || request.MaxPrice is not null)
        {
            filters.Add(new NumberRangeQuery("price")
            {
                Gte = request.MinPrice is null ? null : (double)request.MinPrice,
                Lte = request.MaxPrice is null ? null : (double)request.MaxPrice
            });
        }

        var response = await _client.SearchAsync<Book>(s => s
            .Indices(_options.IndexAlias)
            .From((page - 1) * pageSize)
            .Size(pageSize)
            .Query(new BoolQuery { Must = [textQuery], Filter = filters }), ct);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }

        var items = response.Hits
            .Where(h => h.Source is not null)
            .Select(h => new BookHit(h.Id!, h.Score, h.Source!))
            .ToList();

        return new SearchResult(response.Total, page, pageSize, items);
    }
}

public record PingResult(bool Connected, string? ClusterName, string? Version, string? Error);
