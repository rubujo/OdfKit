---
title: 지식재산권 및 규정 준수
_lang: ko
translation_source: docs/ip-compliance.md
translation_source_sha256: bccec797a382b4bf3fae941a34d0dd406fdc97cac84a38d6c20dc09109164b6f
---

# 지식재산권 및 규정 준수

> 참고용 번역이며 법률 자문이 아닙니다. 원문 법률 문서가 우선합니다.

이 문서는 도입자의 규정 준수·조달 실사와 기여자를 위한 것입니다. 자세한 출처는
[provenance 색인](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/README.md)을 참조하십시오.

## 1. 복합 라이선스 모델
원본 OdfKit 코드는 CC0 1.0 Universal을 사용합니다. 종속성은 MIT, BSD 등 각각의 라이선스를,
OASIS 스키마는 OASIS Copyright를, fixture는 manifest의 라이선스를 유지합니다. 배포 시 `LICENSE`와
[THIRD-PARTY-NOTICES.md](https://github.com/rubujo/OdfKit/blob/main/THIRD-PARTY-NOTICES.md)를 모두
준수해야 하며 전체 제품이 퍼블릭 도메인이라고 주장해서는 안 됩니다.

### CC0의 특허권 및 상표권 범위

CC0 1.0 제4(a)항에 따라 특허권과 상표권은 허여되거나 포기되지 않습니다. OdfKit은 특허 라이선스,
비침해 보증, 특허 조사 또는 면책을 제공하지 않습니다. 채택자는 직접 실사해야 합니다. 내용이
다를 경우 [CC0 법적 원문](https://creativecommons.org/publicdomain/zero/1.0/legalcode)이 우선합니다.

## 2. 권리자와 AI 제작 콘텐츠
공개 코드, 문서, 예제와 테스트는 현재 대부분 AI 도구로 작성·정리·생성되었습니다. CC0
Affirmer와 기여자는 해당 권리를 처분할 권한이 있어야 합니다. 순수 기계 생성물의 취급은
관할권마다 다르며 프로젝트는 상업적 면책을 제공하지 않습니다.

## 3. Clean-room 및 금지 출처
공개 OASIS/ISO/RFC/W3C 규격, 공개 wire 형식, 재배포 가능한 fixture, 행위 비교와 독립 회귀 시험은
허용됩니다. LibreOffice C++, Java ODF Toolkit, Apache POI, NPOI, 상용 SDK 또는 역컴파일한 비공개
바이너리의 복사는 금지됩니다. JSON Collaboration은 호환 구현이지 소스 코드 포팅이 아닙니다.

## 4. 표준과 상표
OpenDocument, ODF, OpenFormula, OOXML 및 LibreOffice 호환 시험을 설명 목적으로 언급할 수 있지만
OASIS, TDF, LibreOffice 또는 Apache의 공식 인증이나 보증을 암시해서는 안 됩니다.

## 5. Developer Certificate of Origin (DCO)
기여자는 작성 또는 제출 권한, 재배포 불가 제3자 코드의 부재, clean-room 준수와 제3자 고지
갱신을 확인할 수 있어야 합니다. 필요하면 `Signed-off-by: Name <email>`을 사용하며 커밋에는 GPG 서명도 필요합니다.

## 6. 도입자 실사
라이선스와 SBOM, 현재 `0.x` 버전, 기능·자원 제한, 출처 및 지원을 검토하십시오. SLA는 없으며
중요 시스템에는 대체 및 자체 유지보수 계획이 필요합니다.

## 7. 보안 신고
현재 공개 이슈 추적기, 비공개 신고 채널 또는 처리 약속이 없습니다. 전체 악용 세부 정보를
공개하지 말고 보안 문제와 라이선스·침해 문제를 분리하십시오.

## 8. 관련 문서
[clean-room 출처 색인](https://github.com/rubujo/OdfKit/blob/main/docs/provenance/clean-room-source-index.md),
[확장 정책](https://github.com/rubujo/OdfKit/blob/main/docs/foreign-extension-policy.md),
[corpus 규칙](https://github.com/rubujo/OdfKit/blob/main/docs/corpus-manifest.md)을 참조하십시오.
