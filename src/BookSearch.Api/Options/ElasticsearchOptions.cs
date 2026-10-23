namespace BookSearch.Api.Options;

public class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public string Url { get; set; } = "http://localhost:9200";

    // Uygulama index adı yerine alias kullanır (Aşama 6: books-v1 -> books-v2 geçişi).
    public string IndexAlias { get; set; } = "books";
}
