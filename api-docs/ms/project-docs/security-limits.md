---
title: Had keselamatan pemuatan dan pembaca penstriman
_lang: ms
translation_source: docs/security-limits.md
translation_source_sha256: cb823c0029b8beeed0a9f910875163ea7d358fc9a7903f571f4136c59e968ff9
---

# Had keselamatan pemuatan dan pembaca penstriman

> Terjemahan maklumat; jika terdapat perbezaan, sumber zh-TW yang berwibawa mengatasi terjemahan.

Pemuatan pakej dan `OdsStreamReader`/`OdtStreamReader` memproses input ZIP/XML yang tidak dipercayai. Pembaca tidak membina DOM dokumen penuh, tetapi memperuntukkan penimbal
untuk baris semasa, teks nod, penyahmampatan ZIP dan pembaca XML. Reka bentuk memori rendah tidak
menghapuskan kesan saiz input.

## Had pakej teras

`OdfDocument.Load`, facade `Load` mengikut format dan `OdfPackage.Open` berkongsi belanjawan sumber `OdfLoadOptions`.

| Had | Lalai | Tujuan perlindungan |
|---|---:|---|
| Entri ZIP | 5,000 | Mencegah kehabisan CPU dan memori akibat banyak entri kecil |
| Saiz nyahmampat satu entri | 500 MiB | Mengehadkan pengembangan satu entri ZIP |
| Jumlah saiz nyahmampat | 1 GiB | Mengehadkan jumlah pengembangan pakej |
| Saiz input mentah tidak boleh dicari | 1 GiB | Mengehadkan penimbalan sebelum pengembangan ZIP |
| Aksara dalam satu dokumen XML | 64 MiB | Mengehadkan kos penghuraian XML dan pembinaan DOM |

Empat had ZIP mesti positif; sifar atau nilai negatif segera menghasilkan `ArgumentOutOfRangeException`. Hanya `MaxXmlCharactersInDocument = 0` mematikan had XML. Semua XML Reader mesti melarang DTD dan resolver luaran. Laluan baharu mesti menggunakan `OdfLoadOptions`. Laluan pengesahan pakej dan Flat XML (`OdfPackageValidator`, `OdfFlatDocumentValidator` serta imbasan peraturan profile) turut menggunakan `MaxXmlCharactersInDocument`: pengesahan pakej menggunakan `package.LoadOptions`, manakala pengesahan Flat menggunakan `OdfValidationOptions.LoadOptions` (lalai 64 MiB daripada `OdfLoadOptions` jika tidak ditetapkan). Tandatangan, cap masa, data pembatalan sijil dan respons rangkaian luaran mempunyai had tersendiri yang lebih kecil; had pakej teras tidak menggantikannya. Untuk dasar kandungan gunakan `OdfPackageValidator`, `SanitizeMacros`, pengesahan tandatangan atau `pwsh eng/Test-OdfPolicy.ps1`.

## Perlindungan sumber dan output yang lain

Selain had pakej dan pembaca penstriman, had tetap berikut turut melindungi daripada input yang tidak dipercayai. Nilai ini pada masa ini ialah pemalar tetap dalam kod dan belum boleh dikonfigurasikan melalui `OdfLoadOptions` atau objek pilihan. Nilaikan kesan pada memori dan tindanan sebelum menaikkan atau membuang sesuatu had.

| Aspek | Had | Tingkah laku apabila melebihi |
|---|---|---|
| Saiz sebenar entri ZIP selepas dinyahmampat | Tidak boleh melebihi saiz tidak dimampatkan yang diisytiharkan dalam pengepala (laluan MMF bagi pemuatan melalui laluan fail) | `SecurityException` |
| Direktori pusat ZIP rosak atau ZIP64 | Laluan pantas MMF tidak menyokong ZIP64, dan mana-mana rekod yang tidak dapat dihuraikan sepenuhnya tidak boleh dilangkau secara senyap | Kembali kepada pengesahan dan bacaan melalui `ZipArchive` |
| Kedalaman sarang elemen XML | 256 peringkat (`OdfXmlReader.MaxElementDepth`); terpakai pada pemuatan DOM, pemuatan Flat ODF, pengesahan peraturan profil dan penghuraian RDF | Pemuatan melontar `SecurityException`; pengesahan melaporkan `ODF0303` atau `ODF0301` |
| Kedalaman sarang penghuraian formula | Masing-masing 256 peringkat untuk kurungan, argumen fungsi, tatasusunan sebaris dan operator awalan berturutan | `InvalidOperationException` |
| Jumlah nod operator formula | 4,096 (`FormulaParser.MaxOperatorNodes`); operator binari, unari, peratus dan rujukan digabungkan. Formula berantai membentuk pokok dalam ke kiri yang penilaian dan pensirian sirinya berulang peringkat demi peringkat; had ini memastikan tindanan benang biasa mencukupi | `InvalidOperationException` |
| Ruang tindanan untuk rekursi formula | Penghuraian, penilaian, pendapatan julat dan pensirian memeriksa tindanan yang berbaki (`RuntimeHelpers.EnsureSufficientExecutionStack`) sebelum memasuki setiap peringkat rekursi; walaupun formula hampir had tidak menyebabkan proses ranap pada benang bertindanan kecil 128–256 KB | `InsufficientExecutionStackException`; `EvaluateFormulas` menukarnya kepada pengecualian penilaian formula atau `#VALUE!` |
| Panjang hasil rentetan formula | 1,048,576 aksara (`&`, hasil fungsi, `SUBSTITUTE`, `REPT`) | Memulangkan `#VALUE!` |
| Fungsi formula yang had gelungnya ialah argumen | `BINOMDIST` kumulatif 100,000; `CRITBINOM`, `HYPGEOMDIST` kumulatif 100,000; `POISSON` kumulatif 1,000,000; `DB`, `DDB`, `VDB`, `CUMIPMT` tempoh 1,000,000 | Memulangkan `#NUM!` |
| Indeks baris/lajur hamparan | Baris 1,048,575, lajur 16,383 (`OdfSpreadsheetLimits`) | `ArgumentOutOfRangeException` |
| Penambahan dokumen | Dokumen tidak boleh ditambah kepada dirinya sendiri | `ArgumentException` |
| Collaboration `addColumns` | Dihadkan oleh bilangan lajur dan jumlah sel dalam `OdtOperationSafetyOptions` | Merekodkan had keselamatan dan melangkau operasi itu |
| Imej sandaran carta | 4,096 px setiap sisi | Dihadkan kepada had |
| Format penukaran LibreOffice | Bahagian sambungan sebelum tanda titik bertindih tidak boleh kosong dan tidak boleh mengandungi `..`, NUL, CR atau LF | `ArgumentException` |
| Pertanyaan SPARQL | Klausa `SERVICE` tidak dibenarkan (menghalang enjin pertanyaan menghantar permintaan rangkaian ke titik akhir sewenang-wenangnya) | `ArgumentException` |

## Had pembaca penstriman

| Pembaca | Had | Lalai |
|---|---|---:|
| ODS | Aksara XML | 64 MiB |
| ODS | Baris setiap lembaran | 1,048,576 |
| ODS | Lajur setiap baris | 16,384 |
| ODS | Satu pengisytiharan repeat | baris 1,048,576; lajur 16,384 |
| ODS | Teks satu sel | 16 MiB |
| ODT | Aksara XML | 64 MiB |
| ODT | Nod teks yang dikembalikan | 1,000,000 |
| ODT | Teks satu nod | 16 MiB |

Pembacaan gagal apabila had dilepasi; repeat tidak dipotong untuk mengembalikan data yang kelihatan
lengkap. Jangan cuba semula secara automatik tanpa had. `LeaveOpen` lalai kepada `false`; apabila `true`,
strim entri XML dan pembaca ZIP ditutup tetapi strim terluar pemanggil kekal terbuka.

Kekalkan had bagi dokumen tidak dipercayai serta sahkan pakej dan skema. Had yang lebih tinggi meningkatkan
risiko memori dan CPU DoS. `MaxXmlCharactersInDocument = 0` hanya mematikan had aksara XML. Had, pengesahan
dan sanitasi mengurangkan risiko tetapi tidak menjamin keselamatan mutlak.

Options Reader ODS dan ODT mengesahkan peraturan semasa sifat ditetapkan: had XML menerima sifar, manakala had baris, lajur, repeat, nod dan teks mesti melebihi sifar.
