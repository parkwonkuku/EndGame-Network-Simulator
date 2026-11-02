# 공개 코드 가이드

[게임 소개](../README.md) · [문제 해결 과정](case-studies.md) · [아키텍처](architecture.md)

## 선정 기준

기능 수보다 **틱 안의 처리 순서, 결과 통지와 자원 회수 조건, 비동기 응답이 최신 상태인지 판단하는 방법**이 보이는 파일을 골랐습니다. 긴 시간축 설명은 상세 문서로 옮기고 코드에는 역할과 복원 책임 중심의 주석만 남겼습니다.

## 권장 읽기 순서

1. [SimulationTick.Excerpt.cs](../src/simulation/SimulationTick.Excerpt.cs): 0.1초 정책 틱과 `Tick → PostTick` 순서
2. [Traffic.Excerpt.cs](../src/traffic/Traffic.Excerpt.cs): 트래픽 상태, 결과 통지 여부와 모든 패킷 해결 조건
3. [TrafficManager.Excerpt.cs](../src/traffic/TrafficManager.Excerpt.cs): 생성 실패 복원, TTL 만료, 도착·드롭과 최종 반환
4. [PacketLifecycleService.Excerpt.cs](../src/traffic/PacketLifecycleService.Excerpt.cs): 패킷 생성, 현재 소유권과 풀 반환
5. [SaveManager.CloudSync.Excerpt.cs](../src/save/SaveManager.CloudSync.Excerpt.cs): 로컬 저장 버전과 비동기 응답 처리

## 함께 읽어야 하는 계약

| 자료형·인터페이스 | 계약과 역할 |
|---|---|
| `Traffic` | 전체 패킷 수, 도착·드롭 수, TTL과 결과 통지 상태 보관 |
| `Packet` | 소속 트래픽, 데이터 크기와 현재 네트워크 소유 위치 보관 |
| `TrafficPoolManager` | `Traffic`과 `Packet` 풀 제공 |
| `SimulationEventBus` | `Tick` 뒤 `PostTick`을 구독자에게 전달 |
| `ISaveStorage` | 로컬 동기 저장 계약 |
| `IAsyncSaveStorage` | 클라우드 비동기 저장 계약 |

## 공개 코드 형태

- 직접 발췌본은 원본의 선택한 멤버와 메서드 본문을 그대로 가져왔습니다.
- 검토용 재구성본은 핵심 분기와 호출 순서를 유지하되 클래스 이름을 바꾸고 로그·오디오·Unity 생명주기 같은 주변 코드를 생략했습니다.
- 요청한 공개 형식에 맞춰 `using`과 `namespace` 감싸기를 제외했습니다.
- 발췌본은 독립 실행이나 단독 컴파일을 목적으로 하지 않습니다.

| 공개용 코드 | 형태 | 원본 프로젝트 경로 |
|---|---|---|
| `src/simulation/SimulationTick.Excerpt.cs` | 검토용 재구성 | `Assets/Project/Scripts/Core/Simulation/Simulation.cs` |
| `src/traffic/Traffic.Excerpt.cs` | 직접 발췌 | `Assets/Project/Scripts/Core/NetworkGraph/Traffic.cs` |
| `src/traffic/TrafficManager.Excerpt.cs` | 검토용 재구성 | `Assets/Project/Scripts/Core/Managers/TrafficManager.cs` |
| `src/traffic/PacketLifecycleService.Excerpt.cs` | 직접 발췌 | `Assets/Project/Scripts/Core/Managers/PacketLifecycleService.cs` |
| `src/save/SaveManager.CloudSync.Excerpt.cs` | 검토용 재구성 | `Assets/Project/Scripts/Managers/SaveManager.cs` |

## 공개 범위

Unity 씬·프리팹·아트, 전체 게임 흐름, SDK 어댑터, 서비스 설정과 인증 정보는 포함하지 않습니다. 독립 실행용 프로젝트나 전체 소스 재구성을 목적으로 하지 않습니다.
