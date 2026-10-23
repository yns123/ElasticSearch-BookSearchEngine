---
name: es-seed-data
description: Kitap arama projesi için gerçekçi Türkçe örnek kitap verisi üretmek ve Elasticsearch'e bulk import etmek gerektiğinde kullan (data/books.json genişletme, _bulk komutu, POST /api/books/seed).
---

# Seed Veri Üretimi ve Bulk Import

## Veri şeması (`data/books.json`, JSON dizisi)
```json
{ "id": "1", "title": "...", "author": "...", "description": "1-2 cümle", "category": "Roman",
  "price": 89.9, "publishYear": 2012, "rating": 4.3, "popularity": 1200 }
```

## Üretim kuralları
- Hedef: 200-500 kayıt; önce `data/books.json` içindeki mevcut kayıtları koru, yenileri ekle, `id`'leri tekrar etme.
- Kategoriler sabit liste: Roman, Hikaye, Şiir, Bilim, Tarih, Felsefe, Çocuk, Kişisel Gelişim, Biyografi.
- Gerçek ve bilinen kitaplar/yazarlar kullan; açıklamalar kısa ve özgün olsun (kitaptan alıntı yok).
- Öğrenme amaçlı **kasıtlı çeşitlilik** ekle:
  - Türkçe karakterler ve büyük harf: `ILIK`, `İstanbul`, `Şiir`
  - Ek içeren kelimeler: "kitapların", "çocukların" (stemmer testi)
  - Aynı yazardan çok kitap (yazar facet'i)
  - Fiyat aralığı 20-400, rating 2.5-5, popularity 10-5000 (function_score farkı görünsün)
  - Birkaç benzer başlık ("Sefiller", "Sefiller Özet") (relevance testi)

## Bulk import
Kibana / HTTP için NDJSON (her kayıt 2 satır, dosya sonunda boş satır):
```
POST books/_bulk
{ "index": { "_id": "1" } }
{ "title": "Sefiller", ... }
```
.NET tarafı: `POST /api/books/seed` → `data/books.json` oku → `client.BulkAsync` ile 100'lük gruplar halinde gönder. Yanıtta `Errors` kontrol et, hatalı kayıtları logla.

## Doğrulama
- `GET books/_count` == dosyadaki kayıt sayısı
- `GET books/_search?q=title:kitap` sonuç dönüyor
- JSON geçerli (`ConvertFrom-Json`) ve `id`'ler benzersiz
