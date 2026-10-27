# pray-to

코어키퍼 계열의 탑뷰 2D 액션·생존·크래프팅 게임이다. Unity 6000.6.3f1, 협동 멀티플레이. 기획과 기술 원칙은 `Docs/Design.md`에 있고, 방향이 바뀌면 그 문서를 먼저 고친다.

## 작업 분담

- **클라우드 세션**: 코드(`.cs`, `.asmdef`), 데이터, 테스트, 문서. Unity 에디터 없이 `dotnet`으로 컴파일·테스트한다.
- **로컬 세션**: Unity 에디터가 필요한 일. 씬, 프리팹, `.asset`, `.meta`, `Packages/packages-lock.json`, `ProjectSettings/`, 아트·사운드를 맡는다. `LOCAL_TASKS.md`를 위에서부터 처리하고 결과를 같은 파일에 적는다. 에디터 설정과 Unity 안의 테스트는 명령 한 줄(`DevTools/Local/setup.ps1`, `setup.sh`)이 하고 결과도 직접 적는다. 사람이 직접 해야 하는 일은 `LOCAL_TASKS.md`에 따로 모아 둔다.
- 로컬은 `.cs` 파일을 고치지 않는다. 컴파일 에러나 버그는 Console 로그를 결과 보고에 붙이고, 클라우드가 고친다.
- 클라우드는 에디터 작업이 필요하면 `LOCAL_TASKS.md`에 할 일을 추가한다. 가능하면 손 작업 대신 에디터 스크립트로 만들고 `ProjectSetup.SetUp`에서 불러서, 로컬은 명령 한 줄만 다시 실행하게 한다.
- 클라우드는 `.meta`를 만들지 않는다. 새 파일의 `.meta`는 로컬이 Unity를 열 때 생기고 로컬이 커밋한다. 단, 클라우드가 파일을 지우거나 옮길 때는 짝이 되는 `.meta`도 같이 지우거나 옮긴다.

## 코드 구조

- `Assets/_Project/Scripts/Core` (`Game.Core`): 게임 규칙과 시뮬레이션 전부. UnityEngine을 참조하지 않는다(`noEngineReferences`). netstandard2.1, C# 9.
- `Assets/_Project/Data`: 데이터 시트(CSV). Core가 직접 읽는다. 형식은 `Docs/Loot.md`(아이템), `Docs/Combat.md`(몬스터), `Docs/Bosses.md`(보스)에 있다. 시트를 새로 추가하면 `GameSheets`와 `SceneSetup`에도 넣는다.
- `Assets/Tests/EditMode` (`Game.Core.Tests`): Core 테스트(NUnit). Unity Test Runner와 `dotnet test`가 같은 파일을 쓴다.
- `Assets/Tests/PlayMode` (`Game.Client.Tests`): 실제 클라이언트를 플레이 모드로 몇 초씩 돌려 에러가 없는지 보는 테스트. Unity에서만 돌고(명령 한 줄이 실행한다), 클라우드는 컴파일만 확인한다.
- `DevTools/`: Unity 없이 Core를 빌드·테스트·시뮬레이션하는 .NET 프로젝트. 소스는 `Assets/`에서 가져다 쓰므로 여기에 게임 코드를 두지 않는다.
  - `Game.Core`: Core만 컴파일한다. 경고도 에러로 친다.
  - `Game.Core.Tests`: 테스트
  - `CoreSandbox`: 밸런스 시뮬레이션 콘솔
  - `UnityCompileCheck`: Unity 쪽 코드(`Scripts/Client`, `Tests/PlayMode`)를 Unity 실제 엔진 DLL로 컴파일만 해 본다. 패키지(Netcode, Input System, URP, Test Framework) API는 `Stubs/PackageStubs.cs`에 패키지 소스와 같은 시그니처로 적어 둔다. 새 패키지 API를 쓰면 여기에 추가한다.
  - `Local`: 로컬 컴퓨터에서 쓰는 명령 한 줄(`setup.ps1`, `setup.sh`). 둘은 같은 일을 하므로 하나를 고치면 다른 하나도 고친다. `test/run.sh`가 가짜 Unity(`test/fake-unity`)로 두 스크립트를 확인한다.
- `Assets/_Project/Scripts/Client` (`Game.Client`): 화면, 입력, 네트워크 전송만 맡는다. 게임 규칙을 두지 않는다. `Editor/`에는 프로젝트 설정과 씬·에셋을 코드로 만드는 메뉴(`Game > Set Up Project`, `Game > Set Up Main Scene`)가 있다.

## 명령

```
dotnet test DevTools/Game.Core.Tests
dotnet run --project DevTools/CoreSandbox -- --loot 25 10000
dotnet run --project DevTools/CoreSandbox -- --progression
dotnet run --project DevTools/CoreSandbox -- --item 25 7
dotnet run --project DevTools/CoreSandbox -- --fight 120 42
dotnet run --project DevTools/CoreSandbox -- --boss 300 42 --players 2

# Unity 쪽 코드 컴파일 확인. fetch는 세션마다 한 번(약 1분 반, 엔진 DLL만 .unity/에 받는다)
DevTools/UnityCompileCheck/fetch-unity.sh
dotnet build DevTools/UnityCompileCheck

# 로컬 명령 한 줄 스크립트 확인 (pwsh가 있으면 PowerShell 쪽도)
bash DevTools/Local/test/run.sh
```

GitHub Actions(`.github/workflows/core-tests.yml`)가 푸시마다 테스트, Unity 쪽 컴파일 확인, 로컬 스크립트 확인을 돌린다. Unity DLL은 Actions 캐시에 남겨 두고 저장소에는 넣지 않는다.

## 규칙

- Core에서는 `System.Random` 대신 `GameRandom`을 쓴다. 호스트, 클라이언트, 테스트가 같은 결과를 내야 한다. 시각(`DateTime.Now`, `Environment.TickCount` 등)도 Core 안에서 읽지 않고 밖에서 넘겨받는다.
- 게임 상태는 호스트의 Core 시뮬레이션만 바꾼다. 자세한 내용은 `Docs/Design.md`의 멀티플레이 절에 있다.
- 에디터에서 손으로 만드는 콘텐츠를 늘리지 않는다. 월드는 절차 생성, 아이템·몬스터·보스는 데이터, 에셋은 에디터 스크립트로 만든다.
- 커밋 메시지는 영어로 쓴다. 첫 줄은 명령형 요약, 본문에는 무엇을 왜 바꿨는지 적는다.
