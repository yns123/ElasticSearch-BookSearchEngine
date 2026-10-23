# CLAUDE.md

## Proje
Türkçe kitap kataloğu üzerinde Elasticsearch öğrenme projesi. Ayrıntılar: `README.md`. Yol haritası: `docs/LEARNING-PATH.md`.

## Stack
- Elasticsearch 8.x + Kibana (`docker-compose.yml`, security kapalı, sadece dev)
- .NET 8 Web API, **controller tabanlı** (Minimal API kullanma)
- Katmanlar: `Controllers/BooksController` → `Services/BookSearchService` → `Elastic.Clients.Elasticsearch`
- Index adı: `books` (gerçekte `books-v1`, alias `books`)

## Öğretmen modu (kullanıcı öğreniyor)
- Yeni bir ES kavramında: 2 cümle "neden" + Kibana Dev Tools'ta çalışan sorgu ver, sonra C# karşılığını yaz.
- Hazır büyük kod bloğu dökme; aşamanın hedefine odaklan, gereksiz özellik ekleme.
- Aşama numarasına referans ver (örn. "Aşama 3: bool sorgusu").
- Cevaplar kısa ve Türkçe.

## Kurallar
- Mapping değişikliği = yeni index (`books-v2`) + `_reindex` + alias geçişi. Yerinde değişiklik önerme.
- Filtreleme `bool.filter` içinde (skor etkilemez, cache'lenir); skor gereken yer `must`/`should`.
- Facet/sıralama alanları `keyword` veya sayısal olmalı; `text` alanında aggregation yapma.
- Türkçe analyzer: `lowercase` + `turkish_stop` + `turkish_stemmer`. Analyzer'ı `_analyze` ile doğrulamadan onaylama.
- Kapsam dışı: auth, UI, deployment, cluster/shard tuning.

## Skill'ler (`.claude/skills/`)
- `es-mapping-design` — mapping/analyzer tasarımı
- `es-query-builder` — istekten DSL sorgusu üretme
- `es-seed-data` — Türkçe örnek veri üretme ve bulk import
- `es-explain-results` — sıralama/analiz debug
