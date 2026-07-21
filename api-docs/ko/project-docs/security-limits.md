---
title: 로드 및 스트리밍 리더 보안 제한
_lang: ko
translation_source: docs/security-limits.md
translation_source_sha256: 717f100dcc24a436ae8dd8c64601585f59320489dc452918dd8d97837e4fd8f0
---

# 로드 및 스트리밍 리더 보안 제한

> 참고용 번역입니다. 내용이 다를 경우 zh-TW 원본이 우선합니다.

패키지 로드와 `OdsStreamReader`/`OdtStreamReader`는 신뢰할 수 없는 ZIP/XML 입력을 처리합니다. Reader는 전체 문서 DOM을 만들지 않지만 현재 행, 노드 텍스트,
ZIP 압축 해제 및 XML 리더를 위한 버퍼를 할당합니다. 낮은 상주 메모리 설계도 입력 크기의
영향을 없애지는 않습니다.

## 핵심 패키지 제한

`OdfDocument.Load`, 형식별 `Load` facade 및 `OdfPackage.Open`은 `OdfLoadOptions` 리소스 예산을 공유합니다.

| 제한 | 기본값 | 보호 목적 |
|---|---:|---|
| ZIP 엔트리 수 | 5,000 | 많은 작은 엔트리로 인한 CPU 및 메모리 고갈 방지 |
| 단일 엔트리 압축 해제 크기 | 500 MiB | ZIP 엔트리 하나의 확장량 제한 |
| 전체 압축 해제 크기 | 1 GiB | 모든 엔트리의 총 확장량 제한 |
| 검색 불가능한 원시 입력 크기 | 1 GiB | ZIP 확장 전 버퍼링 제한 |
| 단일 XML 문서 문자 수 | 64 MiB | XML 구문 분석 및 DOM 생성 비용 제한 |

네 ZIP 제한은 양수여야 하며 0 또는 음수는 즉시 `ArgumentOutOfRangeException`을 발생시킵니다. `MaxXmlCharactersInDocument = 0`만 XML 제한을 비활성화합니다. 모든 XML Reader는 외부 DTD와 resolver를 금지해야 합니다. 새 경로는 `OdfLoadOptions`를 재사용해야 합니다. 패키지와 Flat XML 검증 경로(`OdfPackageValidator`, `OdfFlatDocumentValidator`, profile 규칙 검사)에도 `MaxXmlCharactersInDocument`가 적용됩니다. 패키지 검증은 `package.LoadOptions`를 사용하고 Flat 검증은 `OdfValidationOptions.LoadOptions`를 사용하며, 생략하면 `OdfLoadOptions`의 기본값인 64 MiB가 적용됩니다. 서명, 타임스탬프, 인증서 해지 데이터 및 외부 네트워크 응답에는 각각 더 작은 별도 제한이 있으며 코어 패키지 제한으로 대체할 수 없습니다. 콘텐츠 정책에는 `OdfPackageValidator`, `SanitizeMacros`, 서명 검증 또는 `pwsh eng/Test-OdfPolicy.ps1`을 사용하십시오.

## 스트리밍 리더 제한

| 리더 | 제한 | 기본값 |
|---|---|---:|
| ODS | XML 문자 | 64 MiB |
| ODS | 워크시트당 행 | 1,048,576 |
| ODS | 행당 열 | 16,384 |
| ODS | 단일 repeat 선언 | 행 1,048,576; 열 16,384 |
| ODS | 단일 셀의 추출 텍스트 | 16 MiB |
| ODT | XML 문자 | 64 MiB |
| ODT | 반환되는 텍스트 노드 | 1,000,000 |
| ODT | 단일 노드의 추출 텍스트 | 16 MiB |

제한을 초과하면 읽기가 실패하며 repeat를 잘라 완전해 보이는 데이터를 반환하지 않습니다.
무제한 설정으로 자동 재시도하지 마십시오. `LeaveOpen`의 기본값은 `false`입니다. `true`이면
XML 엔트리 스트림과 ZIP 리더는 닫지만 호출자가 제공한 가장 바깥쪽 스트림은 열어 둡니다.

신뢰할 수 없는 문서에는 기본 제한을 유지하고 먼저 패키지와 스키마를 검증하십시오. XML 또는
텍스트 제한을 높이면 메모리와 CPU DoS 위험도 커집니다. `MaxXmlCharactersInDocument = 0`은 XML
문자 제한만 끕니다. 제한, 검증 및 정제는 위험을 줄이지만 악성 문서에 대한 절대 안전을 보장하지 않습니다.

ODS/ODT Reader options는 속성 설정 시 규칙을 검증합니다. XML 제한은 0을 허용하지만 행, 열, repeat, 노드 및 텍스트 제한은 0보다 커야 합니다.
