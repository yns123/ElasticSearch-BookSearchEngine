# Öğrenme Yolu

Her aşama: **Hedef → Yapılacaklar → Doğrulama → Kendini sına**. Sıradaki aşamaya geçmeden doğrulamayı geçmiş ol.

---

## Aşama 1 — Kurulum ve temel CRUD
**Hedef:** ES'in bir doküman deposu olduğunu ve REST ile konuştuğunu görmek.

**Yapılacaklar**
- `docker compose up -d`, Kibana → Dev Tools aç.
- Geçici bir index'e 3 kitap ekle, getir, güncelle, sil.
```
PUT test/_doc/1
{ "title": "Sefiller", "author": "Victor Hugo" }
GET test/_doc/1
POST test/_update/1 { "doc": { "author": "V. Hugo" } }
DELETE test/_doc/1
```
- .NET Web API projesini oluştur, `ElasticsearchClient`'ı DI'a kaydet, `GET /api/books/ping` ile bağlantıyı doğrula.

**Doğrulama:** `GET _cluster/health` yeşil/sarı; ping endpoint'i 200 dönüyor.

**Kendini sına**
1. Index, document ve shard nedir?
2. `PUT` ile `POST _update` farkı nedir?

---

## Aşama 2 — Mapping ve Analyzer
**Hedef:** Aramanın kalitesini belirleyen şeyin mapping/analyzer olduğunu kavramak.

**Yapılacaklar**
- `books-v1` index'ini Türkçe analyzer ile oluştur (skill: `es-mapping-design`).
- `title` için multi-field: `.keyword` (sıralama/facet), `.autocomplete` (aşama 5).
- `_analyze` ile dene:
```
POST books-v1/_analyze
{ "analyzer": "turkish_custom", "text": "Kitapların Güzelliği ILIK" }
```
- `data/books.json` dosyasını bulk import et (skill: `es-seed-data`), alias `books` ekle.

**Doğrulama:** "kitaplar" araması "kitap" içeren dokümanı buluyor; `ILIK` küçük harfe doğru çevriliyor (`ılık`).

**Kendini sına**
1. `text` ile `keyword` farkı? Hangisinde aggregation yapılır?
2. Analyzer = char filter + tokenizer + token filter; Türkçe için hangileri seçildi, neden?

---

## Aşama 3 — Arama sorguları
**Hedef:** Skorlu arama ile filtrelemeyi ayırmak.

**Yapılacaklar**
- `match` → `multi_match` (`title^3`, `author^2`, `description`) → `match_phrase`.
- `bool`: `must` (arama), `filter` (kategori, fiyat aralığı), `should` (boost).
- `GET /api/books/search` endpoint'ini `BookSearchService` içinde yaz (skill: `es-query-builder`).

**Doğrulama:** `q=sefiller&category=Roman&maxPrice=200` beklenen sonucu döndürüyor; filtre skoru değiştirmiyor (`_score` aynı).

**Kendini sına**
1. `must` ile `filter` farkı?
2. `multi_match` türleri (`best_fields`, `most_fields`) ne zaman hangisi?

---

## Aşama 4 — Filtre ve Aggregation
**Hedef:** "Sol menü" facet'lerini üretmek.

**Yapılacaklar**
- `terms` (kategori, yazar), `range` (fiyat kovaları), `avg` (ortalama rating).
- `GET /api/books/facets` ve arama sonucuna facet'leri ekle.
- `post_filter` ile "seçili facet sayıları kaybolmasın" davranışını dene.

**Doğrulama:** Facet sayıları toplamı filtresiz sorgunun `hits.total`'ı ile tutarlı.

**Kendini sına**
1. Neden `author` yerine `author.keyword` üzerinde terms agg?
2. `filter` ile `post_filter` farkı?

---

## Aşama 5 — Arama deneyimi
**Hedef:** Kullanıcıya yakın hissettiren özellikler.

**Yapılacaklar**
- **Autocomplete:** `title.autocomplete` (edge_ngram, search analyzer standart) → `GET /api/books/suggest?q=sef`.
- **Fuzzy:** `"fuzziness": "AUTO"` → "sefilller" → "Sefiller".
- **Highlight:** `<em>` ile eşleşen kısımlar.
- **Sayfalama:** `from/size` ve `search_after` farkı.

**Doğrulama:** "sef" → Sefiller öneriliyor; "sefilller" sonuç veriyor; `from+size > 10000` hatasını gör, `search_after` ile çöz.

**Kendini sına**
1. Edge n-gram index zamanında mı sorgu zamanında mı uygulanır, neden?
2. Fuzzy sorgunun maliyeti ve sınırlamaları?

---

## Aşama 6 — Relevance ve operasyon
**Hedef:** Sıralamayı kontrol etmek ve şemayı canlıyken değiştirmek.

**Yapılacaklar**
- `function_score`: `popularity` ve `rating` ile hafif boost (`field_value_factor`, `log1p`).
- Beklenmedik sıralamayı `_explain` / `profile` ile incele (skill: `es-explain-results`).
- Mapping değişikliği: `books-v2` oluştur → `_reindex` → alias'ı atomik taşı → `books-v1`'i sil.

**Doğrulama:** Alias geçişi sırasında API kesintisiz çalışıyor; popüler kitap eşit metin skorunda üste çıkıyor.

**Kendini sına**
1. BM25'te TF, IDF ve alan uzunluğu skoru nasıl etkiler?
2. Neden uygulama doğrudan index adı yerine alias kullanmalı?

---

## Bitirme ölçütü
`/search`, `/suggest`, `/facets` çalışıyor, bir mapping değişikliğini alias ile kesintisiz yaptın ve her aşamanın "Kendini sına" sorularını kendi cümlelerinle cevaplayabiliyorsun.
