# Mini Kitap Arama Motoru (Elasticsearch Öğrenme Projesi)

Türkçe bir kitap kataloğu üzerinde Elasticsearch'ün temel özelliklerini uygulayarak öğrenmek için küçük bir proje.

## Hedef
Bu projenin sonunda şunları yapabilmiş olacaksın:
- Index, mapping ve **Türkçe analyzer** tasarlamak
- `match`, `multi_match`, `bool` sorgularıyla arama yapmak
- Filtre ve **aggregation (facet)** üretmek
- Autocomplete, typo toleransı (fuzzy), highlight eklemek
- Sıralamayı (relevance) `function_score` ile ayarlamak ve `_explain` ile debug etmek
- Mapping değişikliğini **reindex + alias** ile yönetmek

## Stack
- Elasticsearch 8.x + Kibana (Docker)
- .NET 8 Web API (controller tabanlı) + `Elastic.Clients.Elasticsearch`
- UI yok: sorguları önce **Kibana Dev Tools**'ta dene, sonra API'ye taşı

## Hızlı başlangıç
```bash
docker compose up -d
```
- Elasticsearch: http://localhost:9200
- Kibana: http://localhost:5601 → Dev Tools

## API'yi çalıştırma (Aşama 1)
```bash
dotnet run --project src/BookSearch.Api --urls http://localhost:5209
```
- Swagger: http://localhost:5209/swagger
- Bağlantı testi: `GET http://localhost:5209/api/books/ping` → ES ayaktaysa 200 (`clusterName`, `version`), değilse 503.
- Ayarlar: `src/BookSearch.Api/appsettings.json` → `Elasticsearch:Url`, `Elasticsearch:IndexAlias`.
- Not: `Elastic.Clients.Elasticsearch` **8.x** sürümüne sabit (9.x istemci ES 8 sunucusuyla uyumlu değil).

## Veri modeli (`books` index)
| Alan | Tip | Not |
|---|---|---|
| `title` | text (Türkçe analyzer) + `.keyword` + `.autocomplete` | en önemli arama alanı |
| `author` | text + `.keyword` | facet için keyword |
| `description` | text (Türkçe analyzer) | |
| `category` | keyword | filtre / facet |
| `price` | scaled_float veya double | aralık filtresi |
| `publishYear` | integer | |
| `rating` | float | sıralama / boost |
| `popularity` | integer | `function_score` |

## API (aşama 3-5'te yazılır)
| Endpoint | Amaç |
|---|---|
| `GET /api/books/search?q=&category=&minPrice=&maxPrice=&page=` | arama + filtre + highlight |
| `GET /api/books/suggest?q=` | autocomplete |
| `GET /api/books/facets` | kategori / yazar / fiyat aralığı sayıları |
| `POST /api/books/seed` | `data/books.json` dosyasını bulk index et |

## Proje yapısı
```
docs/LEARNING-PATH.md   6 aşamalı yol haritası (buradan başla)
docs/CHEATSHEET.md      DSL sorgu özeti
data/books.json         örnek veri
.claude/skills/         Claude yardımcı skill'leri
CLAUDE.md               Claude için proje kuralları
```

## Nasıl çalışılır
1. `docs/LEARNING-PATH.md` içinden sıradaki aşamayı aç.
2. Önce Kibana'da sorguyu çalıştır, sonucu yorumla.
3. Sonra .NET servis katmanına taşı.
4. Aşama sonundaki "Kendini sına" sorularını cevapla.
