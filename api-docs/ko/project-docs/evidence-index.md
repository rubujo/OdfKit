---
title: 기능 주장 및 증거 색인
_lang: ko
translation_source: docs/evidence-index.md
translation_source_sha256: c2b80895e2b7508134a51346e99f39437b7a2c88f2ebc9375f1c11bc8ea3a142
---

# 기능 주장 및 증거 색인

> 참고용 번역입니다. Claim ID와 기계 판독 값은 번역하지 않습니다.

이 색인은 서로를 함의하지 않는 세 차원으로 기능을 나눕니다. 기계 판독 원본은
[`claims.json`](https://github.com/rubujo/OdfKit/blob/main/docs/claims.json)입니다.

| Claim | 형식 | 차원 | 수준 | 제한 |
|---|---|---|---|---|
| `ODS-PACKAGE-001` | ODS | PackageFidelity | complete | 패키지 왕복 처리가 수식 재계산이나 완전한 스프레드시트 의미를 뜻하지는 않습니다. |
| `ODS-SEMANTIC-001` | ODS | SemanticApiDepth | semantic-facade-complete | 저장된 값과 수식을 읽지만 수식을 재계산하지 않습니다. |
| `ODT-SEMANTIC-001` | ODT | SemanticApiDepth | semantic-facade-complete | 페이지 레이아웃 또는 렌더링 엔진을 제공하지 않습니다. |
| `ODP-SEMANTIC-001` | ODP | SemanticApiDepth | semantic-facade-complete | DOM/패키지 작업이며 스트리밍 슬라이드 API를 주장하지 않습니다. |
| `ODG-SEMANTIC-001` | ODG | SemanticApiDepth | semantic-facade-complete | SmartArt 레이아웃이나 픽셀 수준 렌더링을 구현하지 않습니다. |
| `ODF-INTEROP-001` | ODF | InteropEvidence | tested | 특정 LibreOffice 버전의 시험은 모든 오피스 제품의 픽셀 동일성을 보장하지 않습니다. |

`PackageFidelity`는 패키지 처리, `SemanticApiDepth`는 문서 의미의 이해와 변경,
`InteropEvidence`는 시험한 외부 프로그램과 버전을 나타냅니다. 어느 한 차원도 다른 차원을
대체하지 않습니다. 의미 범위의 단일 원본은
[`semantic-coverage.json`](https://github.com/rubujo/OdfKit/blob/main/docs/semantic-coverage.json)이며
CI에서 `eng/Test-SemanticCoverage.ps1`로 검증합니다.
