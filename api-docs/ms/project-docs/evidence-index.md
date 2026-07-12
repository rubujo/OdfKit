---
title: Indeks tuntutan keupayaan dan bukti
_lang: ms
translation_source: docs/evidence-index.md
translation_source_sha256: 931370015f608c7efcf929a70e06c14f533aa58a4869d98b67f6a62debd20b9e
---

# Indeks tuntutan keupayaan dan bukti

> Terjemahan maklumat; ID dan nilai boleh baca mesin tidak diterjemahkan.

Tiga dimensi ini tidak saling menyiratkan. Sumber boleh baca mesin ialah
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json).

| Claim | Format | Dimensi | Tahap | Batasan |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | round-trip-verified | Perjalanan pergi balik pakej tidak bermakna formula dikira semula atau semantik hamparan lengkap. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-contract-verified | Membaca nilai dan formula tersimpan tanpa mengira semula formula. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-contract-verified | Tiada enjin susun atur halaman atau pemaparan. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-contract-verified | Beban DOM/pakej; tiada API slaid penstriman. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-contract-verified | Tiada susun atur SmartArt atau pemaparan per piksel. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | interop-tested | Satu versi LibreOffice yang diuji tidak menjamin hasil serupa piksel dalam semua suite. |

`PackageFidelity` berkenaan pakej, `SemanticApiDepth` semantik dokumen dan `InteropEvidence` aplikasi serta
versi yang diuji. Tiada dimensi menggantikan yang lain. Sumber liputan tunggal ialah
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json), disemak
oleh `eng/Test-SemanticCoverage.ps1` dalam CI.

Semantic coverage schema v4 turut menghendaki setiap topik mempunyai bukti untuk `Create`, `Get`,
`Find`, `Set`, `Update`, `Remove`, `Clear`, `RoundTrip` dan `Interop`, yang dipautkan kepada
spesifikasi, pelaksanaan, ujian, batasan dan provenans clean-room. Setiap keluarga juga mesti mempunyai
bukti yang disahkan mesin untuk dokumen sedia ada, pengekalan kandungan tidak diketahui, ODF 1.1–1.3,
diagnostik penurunan versi dan input tidak sah. Lihat [panduan migrasi](https://github.com/rubujo/OdfKit/blob/main/docs/migration-high-level-api.md)
dan [rujukan facade semantik empat format](https://github.com/rubujo/OdfKit/blob/main/docs/reference/semantic-facades.md).
