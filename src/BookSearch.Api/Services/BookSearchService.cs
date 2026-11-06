using System.Text.Json;
using BookSearch.Api.Models;
using BookSearch.Api.Options;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Search;
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

        // Aşama 5: from + size en fazla 10.000 sonuca kadar gider; derin sayfalama için cursor (search_after).
        var searchAfter = DecodeCursor(request.Cursor);
        var from = searchAfter is null ? (page - 1) * pageSize : 0;

        var (response, facets) = await ExecuteAsync(request, from, pageSize, searchAfter, ct);

        var items = response.Hits
            .Where(h => h.Source is not null)
            .Select(h => new BookHit(
                h.Id!, h.Score, h.Source!,
                h.Highlight?.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray())))
            .ToList();

        var lastSort = response.Hits.LastOrDefault()?.Sort;
        var nextCursor = items.Count == pageSize ? EncodeCursor(lastSort) : null;

        return new SearchResult(response.Total, page, pageSize, items, facets, nextCursor);
    }

    // Aşama 5: autocomplete. title.autocomplete alanı index zamanında edge_ngram ile bölünmüş,
    // sorgu ise standart analyzer'dan geçer ("sef" -> "sef" tokenı "sefiller"in ön ekleriyle eşleşir).
    public async Task<IReadOnlyList<string>> SuggestAsync(string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return [];
        }

        var response = await _client.SearchAsync<Book>(s => s
            .Indices(_options.IndexAlias)
            .Size(5)
            .Query(new MatchQuery("title.autocomplete", q)), ct);

        return response.Hits
            .Select(h => h.Source?.Title)
            .Where(t => t is not null)
            .Select(t => t!)
            .Distinct()
            .ToList();
    }

    // Cursor = [score, _id] değerlerinin base64 JSON'u. Sunucu durumu tutmaz.
    private static string? EncodeCursor(IReadOnlyCollection<FieldValue>? sort)
    {
        if (sort is null || sort.Count == 0)
        {
            return null;
        }

        var values = sort.Select(v => v.TryGetDouble(out var d) ? (object)d : v.TryGetString(out var s) ? s! : v.ToString()!).ToArray();
        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(values));
    }

    private static List<FieldValue>? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var elements = JsonSerializer.Deserialize<JsonElement[]>(Convert.FromBase64String(cursor))!;
            return elements
                .Select(e => e.ValueKind == JsonValueKind.Number ? FieldValue.Double(e.GetDouble()) : FieldValue.String(e.GetString()!))
                .ToList();
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            throw new ArgumentException("Geçersiz cursor.", nameof(cursor));
        }
    }

    // Aşama 4: size = 0 -> doküman getirmeden sadece facet sayıları.
    public async Task<Facets> FacetsAsync(BookSearchRequest request, CancellationToken ct)
    {
        var (_, facets) = await ExecuteAsync(request, 0, 0, null, ct);
        return facets;
    }

    // Aşama 3: skor isteyen kısım (metin) must'ta, sadece eleme yapan kısım (fiyat) filter'da.
    // Aşama 4: kategori filter'da değil post_filter'da. Böylece "categories" facet'i, seçili
    // kategoriye rağmen diğer kategorilerin sayılarını göstermeye devam eder. Yazar/fiyat/rating
    // agregasyonları ise kategori seçimine uyması için bir filter agregasyonunun içinde hesaplanır.
    private async Task<(SearchResponse<Book> Response, Facets Facets)> ExecuteAsync(
        BookSearchRequest request, int from, int size, List<FieldValue>? searchAfter, CancellationToken ct)
    {
        Query textQuery = string.IsNullOrWhiteSpace(request.Q)
            ? new MatchAllQuery()
            : new MultiMatchQuery
            {
                Query = request.Q,
                Fields = new[] { "title^3", "author^2", "description" },
                // Aşama 5: yazım hatası toleransı. AUTO: 3-5 harfte 1, 6+ harfte 2 karakter fark.
                Fuzziness = new Fuzziness("AUTO")
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
            // search_after için stabil sıralama: skor eşitse _id belirler.
            .Sort(
                so => so.Score(sc => sc.Order(SortOrder.Desc)),
                so => so.Field("_id", f => f.Order(SortOrder.Asc)))
            .SearchAfter(searchAfter)
            .Highlight(h => h
                .PreTags("<em>").PostTags("</em>")
                .Fields(new Dictionary<Field, HighlightField>
                {
                    ["title"] = new HighlightField(),
                    ["description"] = new HighlightField()
                }))
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
