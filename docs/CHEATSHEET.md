# Elasticsearch DSL Cheatsheet (books)

## Mapping + Türkçe analyzer
```
PUT books-v1
{
  "settings": {
    "analysis": {
      "filter": {
        "turkish_stop": { "type": "stop", "stopwords": "_turkish_" },
        "turkish_stemmer": { "type": "stemmer", "language": "turkish" }
      },
      "analyzer": {
        "turkish_custom": {
          "tokenizer": "standard",
          "filter": ["apostrophe", "lowercase", "turkish_stop", "turkish_stemmer"]
        },
        "autocomplete_index": {
          "tokenizer": "edge_ngram_tokenizer",
          "filter": ["lowercase"]
        }
      },
      "tokenizer": {
        "edge_ngram_tokenizer": { "type": "edge_ngram", "min_gram": 2, "max_gram": 15, "token_chars": ["letter", "digit"] }
      }
    }
  },
  "mappings": {
    "properties": {
      "title": {
        "type": "text", "analyzer": "turkish_custom",
        "fields": {
          "keyword": { "type": "keyword" },
          "autocomplete": { "type": "text", "analyzer": "autocomplete_index", "search_analyzer": "standard" }
        }
      },
      "author": { "type": "text", "fields": { "keyword": { "type": "keyword" } } },
      "description": { "type": "text", "analyzer": "turkish_custom" },
      "category": { "type": "keyword" },
      "price": { "type": "scaled_float", "scaling_factor": 100 },
      "publishYear": { "type": "integer" },
      "rating": { "type": "float" },
      "popularity": { "type": "integer" }
    }
  }
}
```

## Arama
```
GET books/_search
{
  "query": {
    "bool": {
      "must": [{ "multi_match": { "query": "sefiller", "fields": ["title^3", "author^2", "description"], "fuzziness": "AUTO" } }],
      "filter": [
        { "term": { "category": "Roman" } },
        { "range": { "price": { "lte": 200 } } }
      ]
    }
  },
  "highlight": { "fields": { "title": {}, "description": {} } }
}
```

## Aggregation (facet)
```
GET books/_search
{
  "size": 0,
  "aggs": {
    "categories": { "terms": { "field": "category" } },
    "authors": { "terms": { "field": "author.keyword", "size": 10 } },
    "price_ranges": { "range": { "field": "price", "ranges": [{ "to": 50 }, { "from": 50, "to": 150 }, { "from": 150 }] } },
    "avg_rating": { "avg": { "field": "rating" } }
  }
}
```

## Autocomplete
```
GET books/_search
{ "query": { "match": { "title.autocomplete": "sef" } }, "_source": ["title"], "size": 5 }
```

## function_score
```
GET books/_search
{
  "query": {
    "function_score": {
      "query": { "match": { "title": "roman" } },
      "field_value_factor": { "field": "popularity", "modifier": "log1p", "factor": 0.1 },
      "boost_mode": "sum"
    }
  }
}
```

## search_after
```
GET books/_search
{ "size": 10, "sort": [{ "rating": "desc" }, { "_id": "asc" }], "search_after": [4.5, "abc123"] }
```

## Debug
```
POST books/_analyze { "analyzer": "turkish_custom", "text": "Kitapların" }
GET books/_explain/1 { "query": { "match": { "title": "sefiller" } } }
GET books/_search { "profile": true, "query": { "match": { "title": "sefiller" } } }
```

## Reindex + alias
```
POST _reindex { "source": { "index": "books-v1" }, "dest": { "index": "books-v2" } }
POST _aliases { "actions": [
  { "remove": { "index": "books-v1", "alias": "books" } },
  { "add":    { "index": "books-v2", "alias": "books" } }
] }
```
