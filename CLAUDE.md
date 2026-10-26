# pray-to

코어키퍼 계열의 탑뷰 2D 액션·생존·크래프팅 게임이다. Unity 6000.6.3f1, 협동 멀티플레이. 기획과 기술 원칙은 `Docs/Design.md`에 있고, 방향이 바뀌면 그 문서를 먼저 고친다.

## 작업 분담

- **클라우드 세션**: 코드(`.cs`, `.asmdef`), 데이터, 테스트, 문서. Unity 에디터 없이 `dotnet`으로 컴파일·테스트한다.
- **로컬 세션**: Unity 에디터가 필요한 일. 씬, 프리팹, `.asset`, `.meta`, `Packages/packages-lock.json`, `ProjectSettings/`, 아트·사운드를 맡는다. `LOCAL_TASKS.md`를 위에서부터 처리하고 결과를 같은 파일에 적는다.
- 로컬은 `.cs` 파일을 고치지 않는다. 컴파일 에러나 버그는 Console 로그를 결과 보고에 붙이고, 클라우드가 고친다.
- 클라우드는 에디터 작업이 필요하면 `LOCAL_TASKS.md`에 할 일을 추가한다. 가능하면 손 작업 대신 에디터 메뉴 스크립트를 만들어 로컬은 메뉴만 실행하게 한다.
- 클라우드는 `.meta`를 만들지 않는다. 새 파일의 `.meta`는 로컬이 Unity를 열 때 생기고 로컬이 커밋한다. 단, 클라우드가 파일을 지우거나 옮길 때는 짝이 되는 `.meta`도 같이 지우거나 옮긴다.

## 코드 구조

- `Assets/_Project/Scripts/Core` (`Game.Core`): 게임 규칙과 시뮬레이션 전부. UnityEngine을 참조하지 않는다(`noEngineReferences`). netstandard2.1, C# 9.
- `Assets/Tests/EditMode` (`Game.Core.Tests`): Core 테스트(NUnit). Unity Test Runner와 `dotnet test`가 같은 파일을 쓴다.
- `DevTools/`: Unity 없이 Core를 빌드·테스트·시뮬레이션하는 .NET 프로젝트. 소스는 `Assets/`에서 가져다 쓰므로 여기에 게임 코드를 두지 않는다.
  - `Game.Core`: Core만 컴파일한다. 경고도 에러로 친다.
  - `Game.Core.Tests`: 테스트
  - `CoreSandbox`: 밸런스 시뮬레이션 콘솔
- Unity 쪽 코드(`Game.Client` 등)가 생기면 `DevTools/UnityCompileCheck`에 UnityEngine 스텁을 두고 컴파일만 확인한다.

## 명령

```
dotnet test DevTools/Game.Core.Tests
dotnet run --project DevTools/CoreSandbox -- --dice 42
```

GitHub Actions(`.github/workflows/core-tests.yml`)가 푸시마다 같은 테스트를 돌린다.

## 규칙

- Core에서는 `System.Random` 대신 `GameRandom`을 쓴다. 호스트, 클라이언트, 테스트가 같은 결과를 내야 한다. 시각(`DateTime.Now`, `Environment.TickCount` 등)도 Core 안에서 읽지 않고 밖에서 넘겨받는다.
- 게임 상태는 호스트의 Core 시뮬레이션만 바꾼다. 자세한 내용은 `Docs/Design.md`의 멀티플레이 절에 있다.
- 에디터에서 손으로 만드는 콘텐츠를 늘리지 않는다. 월드는 절차 생성, 아이템·몬스터·보스는 데이터, 에셋은 에디터 스크립트로 만든다.
- 커밋 메시지는 영어로 쓴다. 첫 줄은 명령형 요약, 본문에는 무엇을 왜 바꿨는지 적는다.
