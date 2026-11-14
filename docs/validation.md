# 게임 로직 자동 검증과 저장 경합 수정

## 목표와 작업 분담

게임 로직과 비주얼이 분리된 구조를 활용해 TTL·패킷 회수·저장 응답 순서를 외부 .NET 환경에서 자동 검사했습니다. 화면 없는 검증 방향을 정하고, AI를 활용해 제품 코드 분석, 시나리오 구성, 검사 도구 작성, 실행 결과 분석과 수정까지 진행했습니다.

## 문제를 실행 가능한 단위로 나눈 과정

| 영역 | 검증 기준 | 실제 호출하는 제품 코드 |
|---|---|---|
| TTL | 만료 직전에는 유지하고 만료 시 실패를 한 번 통지 | TrafficManager.HandlePostTick → ExpireTraffic |
| 회수 | 만료 뒤 소유 정보·대여 수 유지, 전체 해결 뒤 최종 회수 | ReportPacketArrived/ReportPacketDropped → PacketLifecycleService.Release → TryFinalizeTraffic |
| 중복·예외 | 중복 보고 차단, 구독자 예외 후 나머지 처리 진행 | PacketLifecycleService.IsActive, NetworkEventBus.InvokeSafely |
| 로컬 우선 저장 | 클라우드 응답 전 로컬 데이터와 pending 기록 | SaveManager.SaveAsync → SaveLocal |
| 일반 저장 | 과거 성공·실패 응답으로부터 최신 pending 보호 | SaveCloudJsonAsync의 localSaveVersion 비교 |
| 재전송 | 재전송 중 새 저장이 발생해도 최신 상태 보호 | SyncPendingLocalSaveToCloudAsync의 버전 비교 |

## 검사 도구 구성

- [LogicChecks.csproj](../tools/LogicChecks/LogicChecks.csproj): 실제 제품 소스 14개를 직접 연결하는 외부 .NET 프로젝트
- [Program.cs](../tools/LogicChecks/Program.cs): 정상·경계·중복·예외·비동기 응답 순서의 15개 시나리오와 44개 단언
- [BoundaryDoubles.cs](../tools/LogicChecks/BoundaryDoubles.cs): Unity API 대역, 수동 시계, 메모리 저장소, 응답 순서를 제어하는 가짜 클라우드
- [run.ps1](../tools/LogicChecks/run.ps1): 실행 환경·결과·종료 코드·제품 소스 해시 기록

## 발견한 문제와 수정

초기 검사에서 일반 업로드의 버전 보호는 통과했지만, 재전송 중 새 저장이 발생하는 조건에서 pending이 조기에 해제됐습니다.

1. 이전 업로드 실패로 pending=true인 로컬 데이터를 읽고 재전송 A를 시작합니다.
2. A의 완료 전에 진행도를 변경하고 저장 B를 시작합니다.
3. B가 대기 중인 상태에서 A를 성공시키면, 기존 구현은 pending=false를 기록했습니다.
4. 재전송 시작 시 localSaveVersion을 보관하고, 완료 시 현재 버전과 일치할 때만 pending과 로컬 기록을 갱신하도록 수정했습니다.
5. 반대 순서도 검사했습니다. B가 성공한 뒤 A가 실패해도 최신 완료 상태를 유지합니다.

AI의 코드 분석으로 일반 업로드와 재전송의 상태 갱신 차이를 찾고, 성공·실패 두 분기에 같은 버전 보호를 적용했습니다. 실패를 재현한 검사를 유지하면서 역순 실패 검사도 추가해 수정 효과를 확인했습니다.

## 실행 결과

**15개 시나리오 통과, 실패 0개, 44개 단언 통과, 종료 코드 0.**

TTL 경계·만료 후 소유 정보와 대여 수 유지·최종 회수·중복 보고·구독자 예외와 저장의 로컬 우선 처리·응답 역전·실패 유지·재전송 경합을 확인했습니다.

환경은 Windows, .NET SDK 11.0.100, 런타임 11.0.0, Release입니다. Unity API와 시계는 대역, 로컬 저장은 메모리, 클라우드 응답은 TaskCompletionSource로 제어합니다. 검사 대상은 제품 코드의 상태 전이·소유 정보·객체 대여 수·저장 응답 처리입니다. 외부 빌드의 Unity 직렬화·추적 필드 경고 4개는 실행 로그에 기록돼 있습니다.

[실행 로그](../tools/LogicChecks/results/latest.txt) · [대상 소스 SHA-256](../tools/LogicChecks/results/sources.json)
