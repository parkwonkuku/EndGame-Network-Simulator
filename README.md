# Traceroute

**패킷 처리와 비동기 상태의 일관성**

제한된 예산으로 네트워크를 구축하고 트래픽 병목과 장애에 대응하는 Unity 기반 전략 시뮬레이션 게임입니다.

[포트폴리오 PDF 보기](docs/portfolio.pdf) · [Steam 무료 데모](https://store.steampowered.com/app/5152640/Traceroute_Demo/) · [코드 읽는 순서](docs/code-guide.md) · [문제 해결 과정](docs/case-studies.md) · [아키텍처](docs/architecture.md)

![실제 네트워크 구축 화면](https://raw.githubusercontent.com/parkwonkuku/EndGame-Network-Simulator/main/docs/images/network-building.png)

## 프로젝트 개요

| 구분 | 내용 |
|---|---|
| 개발 기간 | 2026.02 - 2026.10 |
| 개발 인원 | 4명 / 기획 1, 아트 1, 클라이언트 2 |
| 담당 업무 | 팀장, 시뮬레이션 코어·저장·온라인 서비스 연동 |
| 출시 | Steam 무료 데모 출시 |
| 엔진·언어 | Unity, C# |
| 온라인 서비스 | UGS Authentication / Cloud Save / Leaderboards, Steamworks |
| 협업 | Git 브랜치, Pull Request 리뷰 |

사용자가 만든 트래픽은 여러 패킷으로 나뉘어 노드와 전송 경로를 이동합니다. 아래에서는 기능 수보다 틱 안의 처리 순서, 만료 뒤 자원 회수 조건, 로컬 저장과 비동기 응답의 경계를 설명합니다.

## 핵심 구현 시스템

### 1. 0.1초 정책 틱과 트래픽 생명주기

- `Simulation`이 누적 시간에 따라 0.1초 단위로 `Tick`과 `PostTick`을 순서대로 호출합니다.
- 노드·엣지의 패킷 처리 뒤 `PostTick`에서 TTL 만료와 트래픽 완료 조건을 검사합니다.
- 패킷 생성, 현재 소유 위치와 풀 반환은 `PacketLifecycleService`에 모았습니다.
- [책임과 호출 흐름](docs/architecture.md) · [틱 코드](src/simulation/SimulationTick.Excerpt.cs) · [트래픽 코드](src/traffic/TrafficManager.Excerpt.cs)

현재 구현은 한 프레임에 최대 한 번만 고정 틱을 처리합니다. 따라서 긴 프레임 뒤 모든 누락 틱을 같은 프레임에서 따라잡는 구조가 아니며, “완전한 프레임 독립 시뮬레이션”으로 표현하지 않습니다.

### 2. 로컬 우선 저장과 온라인 서비스 경계

- 진행도는 클라우드 응답을 기다리지 않고 로컬에 먼저 기록합니다.
- 클라우드 업로드가 필요한 데이터는 pending 상태로 남겨 다음 초기화에서 재시도합니다.
- `ISaveStorage`와 `IAsyncSaveStorage`로 로컬 저장과 비동기 저장의 호출 경계를 분리했습니다.
- [책임과 호출 흐름](docs/architecture.md) · [저장 코드](src/save/SaveManager.CloudSync.Excerpt.cs)

## 주요 트러블슈팅

### 1. TTL 만료 통지와 네트워크 자원 회수 분리

- TTL이 끝났다는 이유로 패킷을 즉시 제거하면 대기열 점유까지 사라져 병목이 가려졌습니다.
- 만료 시 실패 결과는 한 번만 통지하고, 패킷은 도착 또는 드롭될 때까지 현재 소유 상태를 유지합니다.
- 생성 도중 풀 할당이 실패하면 앞서 만든 패킷과 트래픽 등록을 되돌립니다.
- [문제·선택 근거·복원 과정·확인 범위](docs/troubleshooting/traffic-expiration-lifecycle.md) · [상태 모델](src/traffic/Traffic.Excerpt.cs) · [소유권 코드](src/traffic/PacketLifecycleService.Excerpt.cs)

### 2. 이전 클라우드 응답의 완료 상태 오표시 방지

- 업로드 중 다시 저장하면 이전 요청의 응답이 최신 저장까지 완료된 것처럼 표시할 수 있었습니다.
- 로컬 저장마다 버전을 증가시키고 현재 버전과 같은 요청만 pending 상태를 갱신합니다.
- 최신 업로드가 실패하면 로컬 데이터를 유지하고 다음 초기화에서 재전송합니다.
- [처리 순서와 보장 범위](docs/troubleshooting/async-save-versioning.md) · [저장 코드](src/save/SaveManager.CloudSync.Excerpt.cs)

이 버전 비교는 클라이언트의 pending 상태를 보호합니다. 서버 쓰기 순서, ETag 기반 동시성 제어와 여러 기기의 데이터 병합까지 해결하는 구조는 아닙니다.

## 코드 읽는 순서

1. [SimulationTick.Excerpt.cs](src/simulation/SimulationTick.Excerpt.cs): 정책 틱과 `Tick → PostTick` 호출 순서
2. [Traffic.Excerpt.cs](src/traffic/Traffic.Excerpt.cs): 결과 통지 여부와 모든 패킷 해결 조건
3. [TrafficManager.Excerpt.cs](src/traffic/TrafficManager.Excerpt.cs): 생성 실패 복원, 만료·도착·드롭과 최종 반환
4. [PacketLifecycleService.Excerpt.cs](src/traffic/PacketLifecycleService.Excerpt.cs): 패킷 소유권과 풀 반환
5. [SaveManager.CloudSync.Excerpt.cs](src/save/SaveManager.CloudSync.Excerpt.cs): 로컬 저장 버전과 비동기 응답 처리

직접 발췌와 검토용 재구성의 구분, 생략된 의존성은 [코드 가이드](docs/code-guide.md)에 정리했습니다.

## 저장소 구조

```text
src/
├── traffic/                   # 트래픽 상태, 만료·회수와 패킷 소유권
├── save/                      # 로컬 우선 저장과 응답 버전 확인
└── simulation/                # 0.1초 정책 틱과 단계 순서
docs/
├── troubleshooting/
│   ├── traffic-expiration-lifecycle.md
│   └── async-save-versioning.md
├── architecture.md            # 전체 책임·호출 흐름
├── code-guide.md              # 공개 코드 읽는 순서와 생략 범위
├── case-studies.md            # 상세 사례 목차
├── evidence.md                # 확인 수준과 검증 근거
└── review-notes.md            # 코드와 기존 문서의 차이·예상 질문
tests/README.md                # 실행한 검증 결과
docs/images/                   # 실제 게임 화면 이미지
README.md
```

## 공개 범위

전체 게임 소스 대신 상태 일관성, 처리 순서와 실패 복구를 검토할 수 있는 코드만 선별했습니다. 직접 발췌본과 주변 코드를 정리한 검토용 재구성본을 파일 첫 줄에 구분했습니다. Unity 씬·프리팹·아트 원본, SDK 어댑터, 서비스 설정과 인증 정보는 포함하지 않습니다.

이번 공개용 사본 작성에서는 실제 코드와 문서의 정적 대조, 발췌본 원본 대조, Markdown 상대 링크 검사와 C# 컴파일을 수행했습니다. 상세 결과는 [검증 근거](docs/evidence.md)와 [검증 결과](tests/README.md)에 기록했습니다.
