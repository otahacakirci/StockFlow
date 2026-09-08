# StockFlow konuşmacı kartı

**Hedef:** 18–22 dakika anlatım, yaklaşık 5 dakika soru-cevap.

**Açılış:** “StockFlow, stok ve sipariş bilgisini aynı uygulamada yöneten bir mini ERP. Bir satış siparişini gösterip, onayın arkasındaki tasarımı anlatacağım.”

1. Problem: eldeki stok ve değişimin kaynağı.
2. Roller: Employee hazırlar, Admin onaylar.
3. Draft stok ve rezervasyon üretmez. Confirmed / Cancelled terminal.
4. Örnek: 10 stok, 7 satış, 3 kalan. Eşik 3. Toplam 1.750 TL.
5. DEMO: ürün → taslak → Admin onayı → hareket → düşük stok. 4 dakika.
6. MVC: ViewModel → Controller → Service → EF Core → SQL Server.
7. Veri: OrderItem fiyatı, StockMovement sipariş ve ürün bağlantısı.
8. Onay: oku, doğrula, bütün kalemleri planla, uygula, kaydet, commit.
9. Fiyat: sunucudan snapshot. Mevcut Draft satırı eski fiyatı korur.
10. Güvenlik: cookie, roller, antiforgery, güvenli mesaj.
11. Kullanım: filtre, sayfalama, projection, UTC ve TL.
12. Kapanış: tutarlı iş akışı, sorumluluk ayrımı, kalan geliştirme alanları.

**Geçiş:** “Ekranda gördüğümüz bu değişimin arkasında tek bir onay işlemi var.”

**Sınırlar:** Transaction ≠ bütün eşzamanlılık sorunlarının çözümü. Başlangıç stoğu hareket üretmez. Onaylı satış toplamı ≠ tahsilat veya kâr. Purchase de Product.Price kullanır.

**Demo açılmazsa:** “Canlı ortamı bekletmeden aynı akışı kurgusal model üzerinde göstereyim.” Çevrimdışı HTML'ye geç.

**Soru haritası:** 15 kod, 16 roller, 17 silme, 18 eşzamanlılık.

**Kapanış:** “Sipariş hazırlığını, yetkili onayı ve stok etkisini ayrı sorumluluklarla birleştirdim. Sorularınız üzerinden koda veya veri modeline geçebilirim.”
