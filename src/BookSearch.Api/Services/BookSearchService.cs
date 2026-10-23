using Elastic.Clients.Elasticsearch;

namespace BookSearch.Api.Services;

public class BookSearchService
{
    private readonly ElasticsearchClient _client;

    public BookSearchService(ElasticsearchClient client)
    {
        _client = client;
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
}

public record PingResult(bool Connected, string? ClusterName, string? Version, string? Error);
