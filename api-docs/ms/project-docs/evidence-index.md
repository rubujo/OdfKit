---
title: Indeks tuntutan keupayaan dan bukti
_lang: ms
translation_source: docs/evidence-index.md
translation_source_sha256: d99bcf07e600d948fde4bf3629b9b2781999ba303ec05c568075d83bd48762a2
---

# Indeks tuntutan keupayaan dan bukti

> Terjemahan maklumat; ID dan nilai boleh baca mesin tidak diterjemahkan.

Tiga dimensi ini tidak saling menyiratkan. Sumber boleh baca mesin ialah
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Format | Dimensi | Tahap | Batasan |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | Perjalanan pergi balik pakej tidak bermakna formula dikira semula atau semantik hamparan lengkap. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | Membaca nilai dan formula tersimpan tanpa mengira semula formula. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | Tiada enjin susun atur halaman atau pemaparan. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | Beban DOM/pakej; tiada API slaid penstriman. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | Tiada susun atur SmartArt atau pemaparan per piksel. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | Satu versi LibreOffice yang diuji tidak menjamin hasil serupa piksel dalam semua suite. |

`PackageFidelity` berkenaan pakej, `SemanticApiDepth` semantik dokumen dan `InteropEvidence` aplikasi serta
versi yang diuji. Tiada dimensi menggantikan yang lain. Sumber liputan tunggal ialah
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json), disemak
oleh `eng/Test-SemanticCoverage.ps1` dalam CI.

Semantic coverage schema v3 turut menghendaki setiap topik mempunyai bukti untuk `Create`, `Get`,
`Find`, `Set`, `Update`, `Remove`, `Clear`, `RoundTrip` dan `Interop`, yang dipautkan kepada
spesifikasi, pelaksanaan, ujian, batasan dan provenans clean-room. Setiap keluarga juga mesti mempunyai
bukti yang disahkan mesin untuk dokumen sedia ada, pengekalan kandungan tidak diketahui, ODF 1.1–1.3,
diagnostik penurunan versi dan input tidak sah. Lihat [panduan migrasi](https://github.com/rubujo/OdfKit/blob/main/docs/migration-high-level-api.md)
dan [rujukan facade semantik empat format](https://github.com/rubujo/OdfKit/blob/main/docs/reference/semantic-facades.md).
