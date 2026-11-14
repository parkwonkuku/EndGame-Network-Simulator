# 화면 없는 제품 로직 검사

Unity 플레이 모드 대신 실제 제품 C# 소스 14개를 링크해 외부 .NET에서 실행합니다. 소스 복사본에 로직을 다시 구현하지 않습니다. 외부 테스트 패키지를 사용하지 않는 콘솔 검사이며, 실패가 하나라도 있으면 종료 코드 1을 반환합니다.

## 실행

프로젝트 루트에서 PowerShell로 실행합니다. .NET 11 SDK가 필요합니다.

```powershell
pwsh -File tools/LogicChecks/run.ps1
```

NuGet.Config는 패키지 소스를 비워 외부 패키지 다운로드를 하지 않도록 합니다. SDK의 사용자 설정 읽기가 실행 환경에서 제한되면 해당 읽기를 허용한 환경에서 실행해야 합니다.

결과는 `results/latest.txt`, 대상 제품 소스의 SHA-256은 `results/sources.json`에 기록합니다. 현재 결과는 15개 시나리오·44개 단언 통과, 종료 코드 0입니다.

## 실제 실행 대상

- TrafficManager, Traffic, Packet, PacketLifecycleService
- TrafficPoolManager, ObjectPool, IDGenerator
- NetworkEventBus, SimulationEventBus, NetworkDropMonitor, NetworkStatsManager
- SaveManager, SaveStorage, GameData

정확한 경로는 LogicChecks.csproj의 Compile 항목에 있습니다. 원본 Unity 프로젝트의 Assets 경로를 기준으로 실행합니다. 제출 파일은 소스·설정·검사 결과로 구성합니다.

## 대체한 경계

- MonoBehaviour·속성·로그·오디오 등 Unity API: 최소 대역. Awake/OnEnable/OnDisable/OnDestroy는 검사에서 명시적으로 호출합니다.
- Simulation: 수동 시계와 실제 PostTick 이벤트 버스 호출.
- 클라우드: ControlledCloud가 TaskCompletionSource로 성공·실패·응답 순서를 제어합니다.
- 로컬 저장: 메모리 저장소.
- JsonUtility: System.Text.Json 기반 직렬화 대역.
- Difficulty·StarRating 상수, Node·Edge·Server 타입: 의존성 충족용 대역.

## 검사 범위

TTL 경계와 중복 통지, 만료 뒤 패킷 소유 정보·대여 수 유지, 정상·혼합 완료, 중복 반환 방지, 이벤트 구독자 예외, TTL 비활성, 잘못된 요청을 검사합니다. 저장은 로컬 우선 기록, 일반 업로드의 응답 역전, 실패·재전송·로드 실패, 재전송 중 새 저장 발생을 검사합니다.

검사 환경은 외부 .NET이며, 대상은 제품 코드의 상태 전이·패킷 소유 정보·풀 대여 수·저장 응답 처리입니다. ObjectPool은 비어 있을 때 새 객체를 생성하는 제품 구현을 그대로 사용합니다.

AI 활용 과정과 발견한 문제는 [검증 사례](../../docs/validation.md)에 기록합니다.
