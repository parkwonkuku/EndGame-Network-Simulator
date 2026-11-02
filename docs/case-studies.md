# 핵심 구현 시스템과 트러블슈팅

[게임 소개](../README.md) · [코드 가이드](code-guide.md) · [아키텍처](architecture.md)

각 사례는 `docs/troubleshooting/`의 개별 문서에 상황·과제·대안·선택 이유·실패 처리와 확인 범위를 정리했습니다.

| 문서 | 주제 |
|---|---|
| [01. TTL 만료와 자원 회수](troubleshooting/traffic-expiration-lifecycle.md) | 실패 결과 통지, 패킷 점유 유지, 풀 할당 실패 복원 |
| [02. 비동기 저장 응답 버전](troubleshooting/async-save-versioning.md) | 로컬 우선 저장, pending 상태, 이전 응답 제한과 재시도 |

검증 수준과 미실행 항목은 [검증 근거](evidence.md)에 별도로 기록했습니다.
