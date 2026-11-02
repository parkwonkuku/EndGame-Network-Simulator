# Evidence and Validation Scope

## 확인 결과

| 주장 | 코드 근거 | 이번 작업의 확인 수준 | 말할 수 없는 범위 |
|---|---|---|---|
| TTL 만료와 패킷 회수 시점을 분리 | `ExpireTraffic`, `TryFinalizeTraffic`, `PacketLifecycleService.Release` | 생명주기와 분기 정적 확인 | 장시간 실행에서 미해결 패킷이 없는지 |
| 패킷 생성 중 실패 시 이전 생성분을 반환 | `TryStartTraffic`의 생성 실패 분기 | 복원 순서 정적 확인 | 실제 풀 고갈을 주입한 Play Mode 확인 |
| 이전 업로드 응답이 최신 pending 상태를 바꾸지 않음 | `SaveLocal`, `SaveCloudJsonAsync`의 버전 비교 | 비동기 응답 분기 정적 확인 | 서버 쓰기 순서, 기기 간 충돌, ETag 동시성 제어 |
| Tick 뒤 PostTick 실행 | `Simulation.Tick` | 호출 순서 정적 확인 | 프레임 드롭 시 모든 누락 틱을 즉시 따라잡음 |

## 검증 단계 구분

- 완료: 실제 코드 확인, 문서와 코드의 책임·호출 흐름 대조, 공개용 발췌본 대조, Markdown 상대 링크 검사
- 미실행: Unity 컴파일, Edit Mode/Play Mode 테스트, 장시간 시뮬레이션
- 미실행: UGS 실계정 저장·복구, Steam 인증, 실기기 검증
- 미측정: 성능 개선율, 처리량 향상, 프레임 시간 변화

따라서 문서의 결과는 “코드에서 확인한 상태 전이와 실패 처리” 수준으로 제한합니다. 실행 검증이 추가되기 전에는 “버그가 완전히 재발하지 않는다”, “성능이 향상됐다” 같은 표현을 사용하지 않습니다.
