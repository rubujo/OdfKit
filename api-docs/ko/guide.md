---
title: 사용, 규정 준수, 보안 및 증거 안내
_lang: ko
---

# 사용, 규정 준수, 보안 및 증거

## API 문서 범위

API 참조는 `net10.0` 공개 어셈블리와 XML 문서에서 생성됩니다. 직접 작성된 핵심 API와 공개 확장은 개별 페이지로 제공됩니다. 스키마에서 생성된 대규모 `OdfKit.DOM` 표면은 두 TFM의 Public API baseline과 Typed DOM coverage로 계속 관리됩니다. 멤버 요약은 현재 영어와 정체 중국어로 제공되며, 이 한국어 항목은 모든 API 멤버가 번역되었다고 주장하지 않습니다.

## 라이선스 및 AI 제작

OdfKit의 독창적인 코드와 사이트 문서는 CC0 1.0 Universal을 사용합니다. 제3자 패키지, 스키마, 도구 및 fixture에는 각자의 라이선스가 유지됩니다. 공개 프로젝트 콘텐츠는 AI 도구를 사용하여 작성, 정리 또는 제작되었습니다. 이 사이트는 법률 자문이 아니며 SLA 또는 상업적 면책을 제공하지 않습니다. OdfKit은 OASIS, The Document Foundation, LibreOffice 또는 Apache의 공식 프로젝트나 승인받은 프로젝트가 아닙니다.

## 보안 및 상호 운용성 범위

신뢰할 수 없는 파일에는 reader와 package 리소스 제한을 유지하고 적절한 검증 또는 정화를 수행하십시오. 이러한 통제는 위험을 줄이지만 악성 문서에 대한 절대적인 안전을 보장하지 않습니다. 스키마 유효성, round-trip 또는 특정 LibreOffice 버전 테스트가 모든 오피스 제품군에서 픽셀 단위로 동일한 결과를 의미하지는 않습니다.

## 기능 및 증거

주장은 `PackageFidelity`, `SemanticApiDepth`, `InteropEvidence`로 분리되며 한 차원이 다른 차원을 증명하지 않습니다. 공개 성능 결과에는 commit, runtime, 환경 및 재현 가능한 방법이 포함되어야 합니다. 성능 예산은 아직 고정 샘플 수집 단계입니다.

- [API 참조 열기 [en + zh-TW]](xref:OdfKit)
- [주장 및 증거 색인](project-docs/evidence-index.md)
- [보안 제한](project-docs/security-limits.md)
- [지식재산권 및 규정 준수](project-docs/ip-compliance.md)
- [라이선스](articles/license.md)
- [제3자 고지](project-docs/THIRD-PARTY-NOTICES.md)
