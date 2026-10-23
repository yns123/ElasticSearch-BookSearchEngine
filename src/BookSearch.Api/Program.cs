using BookSearch.Api.Options;
using BookSearch.Api.Services;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ElasticsearchOptions>(
    builder.Configuration.GetSection(ElasticsearchOptions.SectionName));

// ElasticsearchClient thread-safe'tir ve connection pool tutar: singleton olmalı.
builder.Services.AddSingleton(sp =>
{
    var options = builder.Configuration
        .GetSection(ElasticsearchOptions.SectionName)
        .Get<ElasticsearchOptions>() ?? new ElasticsearchOptions();

    var settings = new ElasticsearchClientSettings(new Uri(options.Url))
        .DefaultIndex(options.IndexAlias);

    if (builder.Environment.IsDevelopment())
    {
        // Gönderilen/alınan JSON'u görmek öğrenmek için faydalı.
        settings.DisableDirectStreaming().EnableDebugMode();
    }

    return new ElasticsearchClient(settings);
});

builder.Services.AddScoped<BookSearchService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
