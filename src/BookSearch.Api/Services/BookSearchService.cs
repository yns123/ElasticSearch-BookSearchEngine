using BookSearch.Api.Models;
using BookSearch.Api.Options;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
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

    public async Task<SearchResult> SearchAsync(BookSearchRequest request, CancellationToken ct)
    {
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var (response, facets) = await ExecuteAsync(request, (page - 1) * pageSize, pageSize, ct);

        var items = response.Hits
            .Where(h => h.Source is not null)
            .Select(h => new BookHit(h.Id!, h.Score, h.Source!))
            .ToList();

        return new SearchResult(response.Total, page, pageSize, items, facets);
    }

    // Aşama 4: size = 0 -> doküman getirmeden sadece facet sayıları.
    public async Task<Facets> FacetsAsync(BookSearchRequest request, CancellationToken ct)
    {
        var (_, facets) = await ExecuteAsync(request, 0, 0, ct);
        return facets;
    }

    // Aşama 3: skor isteyen kısım (metin) must'ta, sadece eleme yapan kısım (fiyat) filter'da.
    // Aşama 4: kategori filter'da değil post_filter'da. Böylece "categories" facet'i, seçili
    // kategoriye rağmen diğer kategorilerin sayılarını göstermeye devam eder. Yazar/fiyat/rating
    // agregasyonları ise kategori seçimine uyması için bir filter agregasyonunun içinde hesaplanır.
    private async Task<(SearchResponse<Book> Response, Facets Facets)> ExecuteAsync(
        BookSearchRequest request, int from, int size, CancellationToken ct)
    {
        Query textQuery = string.IsNullOrWhiteSpace(request.Q)
            ? new MatchAllQuery()
            : new MultiMatchQuery
            {
                Query = request.Q,
                Fields = new[] { "title^3", "author^2", "description" }
            };

        var filters = new List<Query>();
        if (request.MinPrice is not null || request.MaxPrice is not null)
        {
            filters.Add(new NumberRangeQuery("price")
            {
                Gte = request.MinPrice is null ? null : (double)request.MinPrice,
                Lte = request.MaxPrice is null ? null : (double)request.MaxPrice
            });
        }

        Query categoryFilter = string.IsNullOrWhiteSpace(request.Category)
            ? new MatchAllQuery()
            : new TermQuery("category", request.Category);

        var response = await _client.SearchAsync<Book>(s => s
            .Indices(_options.IndexAlias)
            .From(from)
            .Size(size)
            .Query(new BoolQuery { Must = [textQuery], Filter = filters })
            .PostFilter(categoryFilter)
            .Aggregations(aggs => aggs
                .Add("categories", a => a.Terms(t => t.Field("category").Size(20)))
                .Add("selected", a => a
                    .Filter(categoryFilter)
                    .Aggregations(sub => sub
                        .Add("authors", x => x.Terms(t => t.Field("author.keyword").Size(10)))
                        .Add("price_ranges", x => x.Range(r => r
                            .Field("price")
                            .Ranges(
                                new AggregationRange { Key = "0-50", To = 50 },
                                new AggregationRange { Key = "50-150", From = 50, To = 150 },
                                new AggregationRange { Key = "150+", From = 150 })))
                        .Add("avg_rating", x => x.Avg(v => v.Field("rating")))))),
            ct);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(response.DebugInformation);
        }

        var selected = response.Aggregations?.GetFilter("selected");

        var facets = new Facets(
            Categories: response.Aggregations?.GetStringTerms("categories")?.Buckets
                .Select(b => new FacetBucket(b.Key.ToString()!, b.DocCount)).ToList() ?? [],
            Authors: selected?.Aggregations?.GetStringTerms("authors")?.Buckets
                .Select(b => new FacetBucket(b.Key.ToString()!, b.DocCount)).ToList() ?? [],
            PriceRanges: selected?.Aggregations?.GetRange("price_ranges")?.Buckets
                .Select(b => new FacetBucket(b.Key ?? "", b.DocCount)).ToList() ?? [],
            AvgRating: selected?.Aggregations?.GetAverage("avg_rating")?.Value);

        return (response, facets);
    }
}

public record PingResult(bool Connected, string? ClusterName, string? Version, string? Error);
