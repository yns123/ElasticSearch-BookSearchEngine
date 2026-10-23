---
name: es-explain-results
description: Elasticsearch'te "neden bu sonuç çıktı / neden bulunmadı / sıralama neden böyle" sorularını _analyze, _explain ve profile ile debug etmek için kullan (kitap arama projesi).
---

# Sonuç Debug (Neden bulundu / bulunamadı / bu sırada?)

## Teşhis akışı
1. **Doküman hiç bulunmuyorsa → analiz sorunu.**
   - `POST books/_analyze` ile hem **index** hem **sorgu** metnini aynı analyzer'dan geçir; token'lar eşleşiyor mu?
   - Alan `keyword` ise tam eşleşme gerekir; `text` ise analiz edilir.
   - Alan gerçekten dokümanda var mı? `GET books/_doc/<id>`
2. **Yanlış sırada geliyorsa → skor sorunu.**
   - `GET books/_explain/<id>` ile BM25 bileşenlerini oku (tf, idf, fieldNorm).
   - Boost'ları (`title^3`) ve `function_score` katkısını ayır.
   - Kısa alanlar (title) uzun alanlara göre daha yüksek fieldNorm alır; bu normaldir.
3. **Yavaşsa → `"profile": true`** ile hangi sorgu bileşeninin zaman harcadığına bak (fuzzy ve wildcard genellikle pahalıdır).
4. **Facet sayıları şaşıyorsa:** `post_filter` mı `filter` mı kullanıldığını, agg alanının `keyword` olduğunu kontrol et.

## Komutlar
```
POST books/_analyze { "analyzer": "turkish_custom", "text": "Kitapların" }
GET  books/_explain/1 { "query": { "multi_match": { "query": "sefiller", "fields": ["title^3","description"] } } }
GET  books/_search   { "profile": true, "query": { "match": { "title": "sefiller" } } }
GET  books/_mapping
```

## Kurallar
- Tahmin yürütme; her teşhisi bir çıktıyla (token listesi, explain ağacı) destekle.
- Çıktıyı sadeleştir: _explain ağacından sadece en büyük 2-3 skor katkısını özetle.
- Çözüm mapping/analyzer değişikliği gerektiriyorsa `es-mapping-design` skill'ine yönlendir (yeni index + alias).

## Çıktı formatı
1. Belirti (1 cümle)
2. Kanıt (ilgili `_analyze`/`_explain` çıktısı özeti)
3. Neden (1-2 cümle)
4. Düzeltme + doğrulama sorgusu
