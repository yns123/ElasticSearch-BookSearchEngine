using System.Text.Json.Serialization;

namespace BookSearch.Api.Models;

public class Book
{
    // _id olarak kullanılır; mapping "dynamic: strict" olduğu için _source'a yazılmaz.
    [JsonIgnore]
    public string Id { get; set; } = default!;

    [JsonPropertyName("title")]
    public string Title { get; set; } = default!;

    [JsonPropertyName("author")]
    public string Author { get; set; } = default!;

    [JsonPropertyName("description")]
    public string Description { get; set; } = default!;

    [JsonPropertyName("category")]
    public string Category { get; set; } = default!;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("publishYear")]
    public int PublishYear { get; set; }

    [JsonPropertyName("rating")]
    public float Rating { get; set; }

    [JsonPropertyName("popularity")]
    public int Popularity { get; set; }
}
