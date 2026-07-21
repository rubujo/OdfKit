---
title: Had keselamatan pemuatan dan pembaca penstriman
_lang: ms
translation_source: docs/security-limits.md
translation_source_sha256: 717f100dcc24a436ae8dd8c64601585f59320489dc452918dd8d97837e4fd8f0
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
