---
name: es-mapping-design
description: Kitap arama projesinde Elasticsearch index mapping'i ve Türkçe analyzer tasarlarken veya değiştirirken kullan (alan tipi seçimi, multi-field, analyzer, books-v2 geçişi).
---

# ES Mapping Tasarımı (Türkçe kitap kataloğu)

## Adımlar
1. **Alanı sınıflandır:** aranacak mı (text), filtre/facet/sıralama mı (keyword/sayısal), ikisi de mi (multi-field)?
2. **Tip seç** (tablo aşağıda).
3. **Analyzer'ı** `_analyze` ile test et; sonucu kullanıcıya göster.
4. Mapping'i `books-vN` olarak oluştur, alias `books` üzerinden kullan.
5. Mevcut index'te değişiklik gerekiyorsa: yeni index → `_reindex` → alias geçişi. Yerinde değiştirme önerme.

## Tip seçimi
| Alan | Tip | Neden |
|---|---|---|
| title | `text` (turkish_custom) + `.keyword` + `.autocomplete` | arama + sıralama + öneri |
| author | `text` + `.keyword` | arama + facet |
| description | `text` (turkish_custom) | sadece arama |
| category | `keyword` | filtre/facet, analiz gereksiz |
| price | `scaled_float` (factor 100) | aralık filtresi, para için float hatasından kaçın |
| publishYear, popularity | `integer` | |
| rating | `float` | |

## Türkçe analyzer kuralı
`standard` tokenizer → `apostrophe` → `lowercase` → `turkish_stop` → `turkish_stemmer`.
- `lowercase` stemmer'dan **önce** gelmeli (İ/ı dönüşümü).
- `apostrophe` filtresi "Türkiye'nin" → "Türkiye" yapar.
- `.autocomplete`: index analyzer = edge_ngram, **search_analyzer = standard** (aksi halde sorgu da n-gram'lanır, yanlış eşleşmeler çıkar).

## Kurallar
- `dynamic: strict` kullan; beklenmeyen alanlar hata versin (öğrenirken yanlış alanı fark etmek için).
- `text` alanında sort/agg yapma; `.keyword` kullan.
- Çok alt-alan açma: her biri index boyutunu ve yazma maliyetini artırır.
- Mapping örneği: `docs/CHEATSHEET.md` → "Mapping + Türkçe analyzer".

## Çıktı formatı
1. Mapping JSON (Kibana'da çalıştırılabilir)
2. Her seçim için 1 cümle gerekçe
3. Doğrulama için 1-2 `_analyze` çağrısı
