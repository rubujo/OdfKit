---
title: 스트리밍 리더 보안 제한
_lang: ko
translation_source: docs/security-limits.md
translation_source_sha256: d39a797850c3029188edd8e376c71dfc55d69060d40c33f74f07341376cbce05
---

# 스트리밍 리더 보안 제한

> 참고용 번역입니다. 내용이 다를 경우 zh-TW 원본이 우선합니다.

`OdsStreamReader`와 `OdtStreamReader`는 전체 문서 DOM을 만들지 않지만 현재 행, 노드 텍스트,
ZIP 압축 해제 및 XML 리더를 위한 버퍼를 할당합니다. 낮은 상주 메모리 설계도 입력 크기의
영향을 없애지는 않습니다.

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
