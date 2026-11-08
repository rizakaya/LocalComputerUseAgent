# Local Computer Use Agent

Windows üzerinde çalışan yerel bir bilgisayar kullanımı demosu. Qwen3-VL, WebPortal’daki siparişleri ekrandan okuyup LegacyApp’te taslak fatura oluşturur. Sonuçları yine ekrandaki fatura listesinden doğrular.

| Proje | Görev |
| --- | --- |
| `WebPortal` | ASP.NET Core Razor Pages sipariş listesi, arama, durum ve alt tutar filtresi. Adres `http://localhost:5000`. |
| `LegacyApp` | WinForms fatura listesi ve taslak formu. Kayıtlar pencere açık kaldığı sürece bellekte tutulur. |
| `ComputerAgent` | Yerel Ollama ile ekran görüntüsünden bir sonraki fare veya klavye eylemini seçen .NET konsol uygulaması. |

## Çalıştırma

Windows’ta açık bir masaüstü oturumu, .NET 10 SDK, Microsoft Edge ve Ollama gerekir. Çözüm, .NET SDK ve Windows API’leriyle derlenir.

### Visual Studio

Ollama çalışırken `LocalComputerUseAgent.slnx` çözümünü açın, araç çubuğundan **Demo** başlatma profilini seçin ve F5'e basın. Açılan **ComputerAgent** konsolundaki `Görev>` satırına örnek görevi yapıştırıp Enter'a basın. Profil WebPortal'ı ve agent'ı başlatır. Agent ilk görevi alınca LegacyApp'i ve Edge'i açar.

### PowerShell

1. Ollama çalışıyorsa model listesini kontrol edin. Gerekirse ayrı bir terminalde `ollama serve` ile başlatın.

   ```powershell
   ollama pull qwen3-vl:8b-instruct
   ollama list
   ```

2. Çözümü derleyin ve WebPortal’ı başlatın. Bu terminal açık kalsın.

   ```powershell
   dotnet build LocalComputerUseAgent.slnx
   dotnet run --no-build --no-launch-profile --project WebPortal
   ```

3. İkinci terminalde, yine proje kökünden agent’ı başlatın.

   ```powershell
   dotnet run --no-build --project ComputerAgent
   ```

   Agent aynı derleme yapılandırmasındaki LegacyApp'i bulur. Özel bir çıktı yolu kullanıyorsanız `--legacy-exe` ile exe yolunu, başka bir yerel görsel model kullanıyorsanız `--model` ile model etiketini verin. İlk görevle birlikte agent LegacyApp’i ve ayrı bir demo profiliyle Edge kiosk penceresini açar. Çalışma sırasında odak değişirse agent durur. Raporu görmek için çalışma bittikten sonra konsola dönün. İkinci görev aynı pencereleri ve mevcut taslakları kullanır. Konsola `çıkış` yazmak demo pencerelerini kapatır.

## Örnek görev

Aşağıdaki metni `Görev>` satırına tek satır olarak girin.

```text
WebPortal'daki siparişleri incele. Uygulanan filtreler satırında Onaylandı ve 10.000,00 TL görünüyorsa portal adımı tamamdır. Görünmüyorsa Tutarın üzerinde (TL) alanına 10000 yazıp Onaylandı düğmesine tıkla. Filtreler uygulanınca sipariş no, müşteri ve tutarları not et ve LegacyApp'e geç. Fatura listesinde bulunmayan ilk siparişi al. Müşteri, sipariş no ve tutarı ayrı alanlara kopyala. Tutarı kuruşlarıyla aynen yaz, TL ekini çıkar. Üç alan kaynakla eşleşince taslağı kaydet ve satırı doğrula. Listedeki siparişleri tekrar yazma. Tüm siparişleri kontrol edip yeni doğrulananları, mevcut eşleşmeleri, uyuşmazlıkları ve eksikleri Türkçe raporla.
```

Boş bir muhasebe oturumunda beklenen iki kayıt şunlardır.

| Sipariş | Müşteri | Tutar |
| --- | --- | --- |
| SIP-1003 | Deniz Yılmaz | 10.000,01 TL |
| SIP-1004 | Elif Demir | 24.750,50 TL |

Aynı görevin ikinci çalıştırmasında iki mevcut eşleşme ve sıfır yeni taslak beklenir. LegacyApp kapatıldığında oturum verileri silinir.

## Kabul kontrolleri

- Portalda sipariş numarası ve müşteriyle aramayı deneyin. **Onaylandı** filtresi dört sipariş göstermeli. **Tutarın üzerinde (TL)** alanına `10000` girildiğinde iki sipariş kalmalı.
- LegacyApp’te geçerli bir tutarla taslak kaydedin. `0` veya `12500.50` gibi hatalı bir değer kaydedilememeli. Aynı sipariş numarasıyla ikinci kayıt denemesinde ilk satır korunmalı.
- Yeni bir oturumda örnek görevi iki kez çalıştırın. Fatura listesindeki müşteri ve tutarları yukarıdaki tabloyla karşılaştırın.
- Model yanıtı beklenirken demo dışı bir pencereyi öne alın. Agent durma nedenini ve son model gözlemini konsolda göstermeli.

Agent, izinli pencerenin görünür içerik alanından görüntü alır ve girdiden önce pencereyi tekrar doğrular. Model önce güncel kareyi okur, ardından görev için bir eylem seçer. Rapor için kaynak gözlemleri, muhasebe listesinin başlangıç ve güncel görünümleriyle karşılaştırılır. Model istekleri yerel `127.0.0.1:11434` adresine gider. Her çalıştırma en fazla 60 eylem turu veya 10 dakika sürer.
