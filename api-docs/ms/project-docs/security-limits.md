---
title: Had keselamatan pembaca penstriman
_lang: ms
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# Had keselamatan pembaca penstriman

> Terjemahan maklumat; jika terdapat perbezaan, sumber zh-TW yang berwibawa mengatasi terjemahan.

`OdsStreamReader` dan `OdtStreamReader` tidak membina DOM dokumen penuh, tetapi memperuntukkan penimbal
untuk baris semasa, teks nod, penyahmampatan ZIP dan pembaca XML. Reka bentuk memori rendah tidak
menghapuskan kesan saiz input.

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
