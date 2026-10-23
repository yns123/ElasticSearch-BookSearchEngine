using System.Text.Json;
using System.Text.Json.Serialization;
using BookSearch.Api.Models;
using BookSearch.Api.Options;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.Options;

namespace BookSearch.Api.Services;

public class BookIndexService
{
    public const string IndexName = "books-v1";

    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _options;
    private readonly IWebHostEnvironment _env;

    public BookIndexService(ElasticsearchClient client, IOptions<ElasticsearchOptions> options, IWebHostEnvironment env)
    {
        _client = client;
        _options = options.Value;
        _env = env;
    }

    // Mapping JSON'u Resources/books-v1.json içinde; alias (books) aynı istekte eklenir.
    public async Task<OperationResult> CreateIndexAsync(CancellationToken ct)
    {
        var existing = await _client.Indices.ExistsAsync(IndexName, ct);
        if (existing.Exists)
        {
            return new OperationResult(false, $"'{IndexName}' zaten var. Mapping değişikliği için yeni index (books-v2) + reindex + alias geçişi kullan.");
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "books-v1.json");
        var body = await File.ReadAllTextAsync(path, ct);

        var response = await _client.Transport.RequestAsync<StringResponse>(
            Elastic.Transport.HttpMethod.PUT, IndexName, PostData.String(body), cancellationToken: ct);

        return response.ApiCallDetails.HasSuccessfulStatusCode
            ? new OperationResult(true, $"'{IndexName}' oluşturuldu, alias: '{_options.IndexAlias}'.")
            : new OperationResult(false, response.Body);
    }

    public async Task<OperationResult> SeedAsync(CancellationToken ct)
    {
        var file = Path.GetFullPath(Path.Combine(_env.ContentRootPath, _options.SeedFile));
        if (!File.Exists(file))
        {
            return new OperationResult(false, $"Seed dosyası bulunamadı: {file}");
        }

        await using var stream = File.OpenRead(file);
        var items = await JsonSerializer.DeserializeAsync<List<SeedRecord>>(stream, cancellationToken: ct) ?? [];
        var books = items.Select(i => i.ToBook()).ToList();

        var response = await _client.BulkAsync(b => b
            .Index(_options.IndexAlias)
            .IndexMany(books, (op, book) => op.Id(book.Id)), ct);

        if (!response.IsValidResponse || response.Errors)
        {
            var failures = response.ItemsWithErrors
                .Select(i => $"{i.Id}: {i.Error?.Reason}")
                .Take(5);
            return new OperationResult(false, string.Join("; ", failures.DefaultIfEmpty(response.DebugInformation)));
        }

        await _client.Indices.RefreshAsync(_options.IndexAlias, ct);
        return new OperationResult(true, $"{books.Count} kitap index'lendi.");
    }

    // _analyze: bir metnin analyzer'dan geçince hangi token'lara dönüştüğünü gösterir.
    public async Task<IReadOnlyList<string>> AnalyzeAsync(string analyzer, string text, CancellationToken ct)
    {
        var response = await _client.Indices.AnalyzeAsync(a => a
            .Index(_options.IndexAlias)
            .Analyzer(analyzer)
            .Text(text), ct);

        return response.Tokens?.Select(t => t.Token).ToList() ?? [];
    }

    private class SeedRecord
    {
        [JsonPropertyName("id")] public string Id { get; set; } = default!;
        [JsonPropertyName("title")] public string Title { get; set; } = default!;
        [JsonPropertyName("author")] public string Author { get; set; } = default!;
        [JsonPropertyName("description")] public string Description { get; set; } = default!;
        [JsonPropertyName("category")] public string Category { get; set; } = default!;
        [JsonPropertyName("price")] public decimal Price { get; set; }
        [JsonPropertyName("publishYear")] public int PublishYear { get; set; }
        [JsonPropertyName("rating")] public float Rating { get; set; }
        [JsonPropertyName("popularity")] public int Popularity { get; set; }

        public Book ToBook() => new()
        {
            Id = Id, Title = Title, Author = Author, Description = Description, Category = Category,
            Price = Price, PublishYear = PublishYear, Rating = Rating, Popularity = Popularity
        };
    }
}

public record OperationResult(bool Success, string Message);
