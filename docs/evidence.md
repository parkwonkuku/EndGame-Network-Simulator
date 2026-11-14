# 검증 결과

## 제품 로직 실행 검사

**15개 시나리오·44개 단언 통과, 실패 0개, 종료 코드 0.**

| 대상 | 근거 | 결과 |
|---|---|---|
| TTL 만료와 회수 | 만료 경계, 통지 횟수, 소유 정보, 풀 대여 수 | 통과 |
| 중복 보고와 구독자 예외 | 정상·혼합 완료, 중복 회수 방지, 예외 후 처리 | 통과 |
| 일반 저장 | 로컬 우선 기록, 과거 성공·실패 응답 순서 제어 | 통과 |
| 재전송 경합 | 재전송 중 새 저장, 역순 성공·실패 | 버전 보호 수정 후 통과 |
| 제품 소스 추적 | Compile 항목 14개, SHA-256 기록 | 실행 대상 확인 |

환경은 Windows / .NET SDK 11.0.100 / 런타임 11.0.0 / Release입니다. Unity API·시계·저장소 경계에 대역을 사용해 제품 코드의 상태 전이·소유 정보·대여 수·저장 응답을 검사합니다. 외부 빌드 경고 4개는 로그에 기록돼 있습니다.

[실행 결과](../tools/LogicChecks/results/latest.txt) · [제품 소스 SHA-256](../tools/LogicChecks/results/sources.json) · [검사 구성과 수정 과정](validation.md) · [실행 방법](../tools/LogicChecks/README.md)

## 코드 대조와 컴파일

TTL 생성 실패 복원과 Tick → PostTick 호출 순서는 코드 대조로 확인했습니다. Unity가 생성한 Assembly-CSharp.csproj의 C# 컴파일 기록은 [컴파일 결과](../tests/results/csharp-build.txt)에 있습니다.

검사 프로젝트는 원본 Unity 프로젝트의 Assets 경로를 참조합니다. 실행에 필요한 원본 소스와 경계 대역은 검사 프로젝트 README에 정리했습니다.
