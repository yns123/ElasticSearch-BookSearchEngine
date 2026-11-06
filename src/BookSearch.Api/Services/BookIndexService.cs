using System.Text.Json;
using System.Text.RegularExpressions;
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

    // Aşama 6: mapping değişikliği = yeni index + _reindex + alias geçişi. Eski index silinmez
    // (geri dönüş için kalır); alias remove+add tek istekte olduğu için geçiş atomiktir.
    public async Task<OperationResult> MigrateAsync(string target, CancellationToken ct)
    {
        if (!Regex.IsMatch(target, @"^books-v\d+$"))
        {
            return new OperationResult(false, "Hedef index 'books-vN' biçiminde olmalı.");
        }

        var mappingPath = Path.Combine(AppContext.BaseDirectory, "Resources", $"{target}.json");
        if (!File.Exists(mappingPath))
        {
            return new OperationResult(false, $"Mapping dosyası yok: Resources/{target}.json");
        }

        // GET _alias/books -> { "books-v1": { "aliases": { "books": {} } } }; anahtar = alias'ın bağlı olduğu index.
        var aliasResponse = await _client.Transport.RequestAsync<StringResponse>(
            Elastic.Transport.HttpMethod.GET, $"_alias/{_options.IndexAlias}", cancellationToken: ct);
        var current = aliasResponse.ApiCallDetails.HasSuccessfulStatusCode
            ? JsonDocument.Parse(aliasResponse.Body).RootElement.EnumerateObject().FirstOrDefault().Name
            : null;
        if (string.IsNullOrEmpty(current))
        {
            current = null;
        }
        if (current is null)
        {
            return new OperationResult(false, $"'{_options.IndexAlias}' alias'ı bir index'e bağlı değil. Önce POST /api/books/index çalıştır.");
        }

        if (current == target || (await _client.Indices.ExistsAsync(target, ct)).Exists)
        {
            return new OperationResult(false, $"'{target}' zaten var.");
        }

        var body = await File.ReadAllTextAsync(mappingPath, ct);
        var create = await _client.Transport.RequestAsync<StringResponse>(
            Elastic.Transport.HttpMethod.PUT, target, PostData.String(body), cancellationToken: ct);
        if (!create.ApiCallDetails.HasSuccessfulStatusCode)
        {
            return new OperationResult(false, create.Body);
        }

        var reindex = await _client.ReindexAsync(r => r
            .Source(s => s.Indices(current))
            .Dest(d => d.Index(target))
            .WaitForCompletion(true), ct);
        if (!reindex.IsValidResponse || reindex.Failures?.Count > 0)
        {
            return new OperationResult(false, $"Reindex başarısız, alias taşınmadı: {reindex.DebugInformation}");
        }

        var swap = await _client.Indices.UpdateAliasesAsync(u => u.Actions(
            a => a.Remove(r => r.Index(current).Alias(_options.IndexAlias)),
            a => a.Add(x => x.Index(target).Alias(_options.IndexAlias))), ct);

        return swap.IsValidResponse
            ? new OperationResult(true, $"{reindex.Total} doküman '{current}' -> '{target}' taşındı; '{_options.IndexAlias}' alias'ı artık '{target}' üzerinde. Eski index silinmedi.")
            : new OperationResult(false, $"Alias geçişi başarısız: {swap.DebugInformation}");
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
