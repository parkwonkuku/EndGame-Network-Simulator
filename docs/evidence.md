# Evidence and Validation Scope

## 확인 결과

| 주장 | 코드 근거 | 이번 작업의 확인 수준 |
|---|---|---|
| TTL 만료와 패킷 회수 시점을 분리 | `ExpireTraffic`, `TryFinalizeTraffic`, `PacketLifecycleService.Release` | 생명주기와 분기 정적 확인 |
| 패킷 생성 중 실패 시 이전 생성분을 반환 | `TryStartTraffic`의 생성 실패 분기 | 복원 순서 정적 확인 |
| 이전 업로드 응답이 최신 pending 상태를 바꾸지 않음 | `SaveLocal`, `SaveCloudJsonAsync`의 버전 비교 | 비동기 응답 분기 정적 확인 |
| Tick 뒤 PostTick 실행 | `Simulation.Tick` | 호출 순서 정적 확인 |

## 검증 단계 구분

- 완료: 실제 코드 확인, 문서와 코드의 책임·호출 흐름 대조, 공개용 발췌본 대조, Markdown 상대 링크 검사
- 완료: Unity가 생성한 `Assembly-CSharp.csproj` 복원 및 C# 컴파일(경고 0, 오류 0). 실행 조건과 결과는 [`tests/results/csharp-build-2026-11-02.txt`](../tests/results/csharp-build-2026-11-02.txt)에 기록

C# 컴파일 결과는 생성된 C# 프로젝트의 스크립트 참조와 문법 오류를 확인한 기록입니다.
