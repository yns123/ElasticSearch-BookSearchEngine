---
name: es-query-builder
description: Kitap arama projesinde bir arama isteğini (metin, kategori, fiyat, yazar, sayfalama, öneri, facet) Elasticsearch DSL sorgusuna ve .NET Web API (BookSearchService) koduna çevirirken kullan.
---

# ES Sorgu Üretici

## Adımlar
1. İsteği parçala: **skor isteyen** kısım (metin) ve **sadece eleme** yapan kısım (kategori, fiyat, yıl).
2. DSL'i kur:
   - metin → `multi_match` (`title^3`, `author^2`, `description`), `fuzziness: AUTO`
   - eleme → `bool.filter` (`term`, `range`)
   - tercih (örn. yeni basım) → `bool.should` ile küçük boost
   - öneri → `title.autocomplete` üzerinde `match`
   - facet → `size: 0` + `aggs` (`terms` keyword alanlarda)
3. **Önce Kibana Dev Tools sorgusunu** ver, sonra C# karşılığını yaz.
4. Sorgunun neden böyle kurulduğunu 2-3 madde ile açıkla.

## Kurallar
- Filtre skoru etkilemez ve cache'lenir → `filter` kullan, `must` içine koyma.
- `q` boşsa `match_all` + filtre; `multi_match`'i boş sorguyla çalıştırma.
- Sayfalama: ilk 10.000 sonuç için `from/size`, derin sayfa için `search_after` (stabil sort: `_score` + `_id`).
- Kullanıcı girdisini sorguya string birleştirme ile gömme; client'ın tipli API'sini veya parametreyi kullan.
- Highlight yalnızca gösterilen alanlarda açılır (`title`, `description`).

## .NET kalıbı
`Controllers/BooksController` sadece parametre alır ve `BookSearchService.SearchAsync(SearchRequest)` çağırır; sorgu kurma mantığı serviste kalır. İstemci: `Elastic.Clients.Elasticsearch` (`ElasticsearchClient`, DI singleton). Index yerine **alias `books`** kullan.

## Çıktı formatı
1. Kibana sorgusu
2. C# servis metodu
3. "Neden bu sorgu" maddeleri
4. Test için örnek HTTP isteği (`GET /api/books/search?...`)
