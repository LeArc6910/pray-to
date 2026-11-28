# pray-to

코어키퍼 계열의 탑뷰 2D 액션·생존·크래프팅 게임이다. Unity 6000.6.3f1, 협동 멀티플레이. 기획과 기술 원칙은 `Docs/Design.md`에 있고, 방향이 바뀌면 그 문서를 먼저 고친다.

## 작업 분담

- **클라우드 세션**: 코드(`.cs`, `.asmdef`), 데이터, 테스트, 문서. Unity 에디터 없이 `dotnet`으로 컴파일·테스트한다. 푸시할 때마다 `CLOUD_LOG.md` 맨 위에 항목을 같은 커밋으로 추가한다(무엇을 왜 바꿨는지, 요청 대조, 로컬에서 할 일, 비어 있는 로컬 확인 칸). 요청 대조는 사람이 요청한 항목마다 코드·저장·안내 목표·Playtest 장면·문서까지 됐는지 적고, 빠진 것은 빠졌다고 적는다(AI가 말없이 빠뜨리는 것을 잡는다, `Docs/Research2026-11.md` 후보 5).
- **로컬 세션**: Unity 에디터가 필요한 일. 씬, 프리팹, `.asset`, `.meta`, `Packages/packages-lock.json`, `ProjectSettings/`, 아트·사운드를 맡는다. 시작할 때 `git pull` 후 `CLOUD_LOG.md`에서 로컬 확인이 비어 있는 항목을 읽고 체크한다. `LOCAL_TASKS.md`를 위에서부터 처리하고 결과를 같은 파일에 적는다. 에디터 설정과 Unity 안의 테스트는 명령 한 줄(`DevTools/Local/setup.ps1`, `setup.sh`)이 하고 결과도 직접 적는다. 사람이 직접 해야 하는 일은 `LOCAL_TASKS.md`에 따로 모아 둔다.
- **사람 손 없이**: 로컬 컴퓨터에서 감시 스크립트(`DevTools/Local/watch.ps1 -Install`, `watch.sh --install`)를 켜 두면 클라우드가 main에 올릴 때마다 명령 한 줄이 저절로 돌고 결과 커밋을 푸시한다. 화면 캡처(PlayMode `Screens` 분류)는 `local-screens` 브랜치에 덮어써 올라온다. 클라우드는 `git fetch origin local-screens` 후 `git show origin/local-screens:<이름>.jpg`로 사진을 꺼내 화면을 확인하고, 사람에게는 사진으로 볼 수 없는 것(손맛, 소리, 실제 휴대폰 터치)만 맡긴다.
- **클라우드가 대신 해 보기**: 사람이 "테스트해 줘"라고 하면, 사람이 로컬에서 직접 보던 확인을 클라우드가 먼저 한다. 웹 데모 배포를 기다려 `node DevTools/WebPlaytest/playtest.js --wait`로 헤드리스 Chromium에서 PC(키보드·마우스)와 휴대폰(터치) 화면으로 플레이하고, 사진과 `report.md`를 열어 본 뒤 결과를 알린다(`Docs/Playtest.md`). 새 기능을 만들면 `Client/Playtest.cs`의 장면과 `playtest.js`·`sweep.js`의 확인도 더한다. 단추는 `Ui.Button`·`Ui.LayoutButton`으로 그려야 시험이 진짜 클릭·탭으로 누를 수 있다. 사람에게는 사진으로 알 수 없는 것(손맛, 소리, 실제 휴대폰 감각)만 묻는다. 전체 점검(`sweep.js`를 구역 없이)은 사람이 요청할 때만 돌린다. 평소 배포 뒤에는 새로 넣거나 바꾼 기능의 구역만(`--only`) 돌리고 그 사진만 본다(토큰을 아끼려고, 사용자 결정, 2026-11-09).
- 로컬은 `.cs` 파일을 고치지 않는다. 컴파일 에러나 버그는 Console 로그를 결과 보고에 붙이고, 클라우드가 고친다.
- 클라우드는 에디터 작업이 필요하면 `LOCAL_TASKS.md`에 할 일을 추가한다. 가능하면 손 작업 대신 에디터 스크립트로 만들고 `ProjectSetup.SetUp`에서 불러서, 로컬은 명령 한 줄만 다시 실행하게 한다.
- 클라우드 세션은 씬·프리팹·`.asset`·`.meta`·`ProjectSettings/`·`Packages/packages-lock.json`을 Edit/Write로 고치지 못한다(`.claude/hooks/guard-unity-files.sh`가 막는다. 로컬 세션은 막지 않는다).
- 클라우드는 `.meta`를 만들지 않는다. 새 파일의 `.meta`는 로컬이 Unity를 열 때 생기고 로컬이 커밋한다. 단, 클라우드가 파일을 지우거나 옮길 때는 짝이 되는 `.meta`도 같이 지우거나 옮긴다.

## 코드 구조

자세한 설명은 `Docs/Codebase.md`. 지킬 것만 여기에 둔다.

- `Assets/_Project/Scripts/Core` (`Game.Core`): 게임 규칙과 시뮬레이션 전부. UnityEngine을 참조하지 않는다(`noEngineReferences`). netstandard2.1, C# 9
- `Assets/_Project/Data`: 데이터 시트(CSV). Core가 직접 읽는다. 시트마다 형식 문서가 있다(`Docs/Codebase.md`의 목록). 시트를 새로 추가하면 `GameSheets`와 `SceneSetup`에도 넣는다
- `Assets/Tests/EditMode`(Core 테스트, `dotnet test`와 Unity가 같은 파일), `Assets/Tests/PlayMode`(Unity에서만 도는 화면 시험. 새 화면을 만들면 장면과 사진을 더한다)
- `DevTools/`: Unity 없이 빌드·테스트·시뮬레이션하는 .NET 프로젝트(`Game.Core`, `Game.Core.Tests`, `CoreSandbox`, `ArtPreview`, `UnityCompileCheck`, `Local`, `WebPlaytest`). 소스는 `Assets/`에서 가져다 쓰므로 여기에 게임 코드를 두지 않는다. 새 패키지 API는 `UnityCompileCheck/Stubs/`에 패키지와 같은 시그니처로 적는다. `Local`의 짝 스크립트(ps1·sh)는 함께 고친다
- `Assets/_Project/Scripts/Client` (`Game.Client`): 화면, 입력, 네트워크 전송만. 게임 규칙을 두지 않는다. 그림·소리는 `Art/`의 코드로 그린다. UI는 IMGUI, 크기는 `Ui`의 점 단위(`Ui.Width`), 줄바꿈은 `Ui.Wrap`, 입력은 `Controls`(키보드·마우스와 터치)
- 웹 데모: `.github/workflows/web-demo.yml`이 GitHub Pages에 올린다(`Docs/WebDemo.md`)

## 명령

전체 목록(시뮬레이션, 던전·보스 재기, 그림 미리보기 `--only` 종류, 웹 점검 구역 등)은 `Docs/Commands.md`. 필요한 줄만 찾아 읽는다. 새 명령은 그 문서에 더한다.

```
export PATH=$PATH:/root/.dotnet
dotnet test DevTools/Game.Core.Tests
# Unity 쪽 컴파일 확인. fetch는 세션마다 한 번(.unity/가 비어 있으면)
DevTools/UnityCompileCheck/fetch-unity.sh
dotnet build DevTools/UnityCompileCheck
dotnet build DevTools/UnityCompileCheck/Assemblies/Game.Client -p:DefineConstants=UNITY_WEBGL -p:UnityAssemblies=$PWD/DevTools/UnityCompileCheck/.unity/WebGL -o /tmp/webgl-check
# 시뮬레이션 대표: 첫 한 시간, 한 방의 몫(난이도), 던전 한 판
dotnet run --project DevTools/CoreSandbox -- --hour 60 1,2,3,4,5,6,7,8
dotnet run --project DevTools/CoreSandbox -- --hits
# 그림은 바뀐 부분만(--only 종류는 Docs/Commands.md)
dotnet run --project DevTools/ArtPreview -- --only <종류>
# 웹 점검: 배포를 기다려 바뀐 구역만
node DevTools/WebPlaytest/sweep.js --wait <커밋> --only <구역>
bash DevTools/Local/test/run.sh
```

GitHub Actions(`.github/workflows/core-tests.yml`)가 푸시마다 테스트, 도트 그림 그리기(`art-preview` 아티팩트), Unity 쪽 컴파일 확인, 로컬 스크립트 확인을 돌린다. Unity DLL은 Actions 캐시에 남겨 두고 저장소에는 넣지 않는다.

## 토큰 아끼기 (사용자 결정, 2026-11-26)

- 큰 작업은 새 세션에서 한다. 이어 할 내용은 저장소 문서(`CLOUD_LOG.md`, `LOCAL_TASKS.md`, `Docs/NextTasks.md`)에 남긴다
- 에이전트는 적게, 범위를 좁혀 띄운다. 작은 고침은 직접 한다. 큰 문서·스크립트는 필요한 절만 찾아 읽는다(grep, 줄 범위)
- 웹 점검은 바뀐 구역만 돌리고, 사진은 실패했거나 새로 바뀐 화면만 본다. 배포 뒤 다시 고치기는 두 번까지
- 큰 그림 작업은 작은 시안을 먼저 사람에게 보이고 방향을 확인받은 뒤 만든다
- 보고와 문서는 짧게: 설계 문서 하나에 계속 덧붙이지 않고 작업마다 짧은 문서로 나눈다
- **도트 그림·사운드 작업은 Fable 모델로**(사용자 결정, 2026-11-28, 토큰 절약과 작업 효율): 도트 그림(스프라이트·타일·소품·이펙트·UI 그림·Retro Diffusion 그림 옮기기)과 사운드(효과음·배경음악 고르기·만들기·가공·연결)는 Fable(`claude-fable-5-2`)이 한다. 지금 세션이 다른 모델이면 그 일만 Fable 하위 작업으로 맡기고, 끝나면 닫는다
  - 클라우드: `create_session`에 `model: claude-fable-5-2`로 하위 세션을 만들어 맡기고, 결과를 받은 뒤 `archive_session`으로 닫는다. 처음부터 그림·소리 작업인 세션은 Fable로 만든다
  - 로컬: `Agent` 도구에 `model: "fable"`로 하위 에이전트를 띄워 맡긴다(끝나면 저절로 닫힌다). 로컬 세션 자체가 그림·소리 작업이면 Fable로 연다
  - 그림·소리와 코드가 섞인 일은 코드(규칙·시험·화면 배치)는 지금 세션이, 그림을 그리거나 소리를 고르는 부분만 Fable이 맡는다

## 규칙

- Core에서는 `System.Random` 대신 `GameRandom`을 쓴다. 호스트, 클라이언트, 테스트가 같은 결과를 내야 한다. 시각(`DateTime.Now`, `Environment.TickCount` 등)도 Core 안에서 읽지 않고 밖에서 넘겨받는다.
- 게임 상태는 호스트의 Core 시뮬레이션만 바꾼다. 자세한 내용은 `Docs/Design.md`의 멀티플레이 절에 있다.
- PC와 모바일 크로스 플랫폼으로 만든다(사용자 결정, 2026-10-29). 새 기능은 키보드·마우스와 터치 둘 다로 쓸 수 있어야 하고, UI는 휴대폰 가로·세로와 작은 PC 창에 들어가야 한다. `Docs/Design.md` 기술 원칙 7.
- 에디터에서 손으로 만드는 콘텐츠를 늘리지 않는다. 월드는 절차 생성, 아이템·몬스터·보스는 데이터, 에셋은 에디터 스크립트로 만든다.
- 작업 중 사용량을 살핀다: 5시간 사용량이 80%를 넘거나 주간 사용량이 90%를 넘으면 새 작업을 시작하지 않고, 돌고 있는 에이전트를 멈추고, 사람에게 알린 뒤 사람이 다시 요청할 때까지 기다린다(사용자 결정, 2026-11-22). 퍼센트는 세션 정보에 나오지 않고 경고 상태만 보이므로, 경고가 보이면 새 작업을 멈추고 사람에게 사용량 화면의 숫자를 물어 기준을 넘었는지 정한다(경고는 기준보다 일찍 켜진다, 2026-11-22). 에이전트를 띄우기 전과 묶음을 합치기 전에 확인한다
- 사용자에게는 한국어로 말한다(사용자 결정, 2026-10-28). 작업 보고, 중간 안내, 마지막 요약까지 모든 답을 예외 없이 한국어로 쓴다(사용자가 2026-11-09에 다시 강조). 작업 사이에 남기는 짧은 안내와 명령·도구 설명(사람 화면에 보이는 설명 줄)도 한국어로 쓴다(사용자 결정, 2026-11-09). 문서도 지금처럼 한국어로 쓴다.
- 저장소에 올리는 파일, 커밋, 브랜치, PR 어디에도 AI 도구를 썼다는 표시(도구·회사 이름, 대화 앱 주소, 도구 이름이 붙은 브랜치)를 두지 않는다. 이 파일의 이름은 예외다(사용자 결정, 2026-11-11).
- 커밋 메시지는 영어로 쓴다. 첫 줄은 명령형 요약, 본문에는 무엇을 왜 바꿨는지 적는다.
- 커밋 작성자는 저장소 주인으로 한다: `git config user.name LeArc6910`, `git config user.email 270705266+LeArc6910@users.noreply.github.com`. 메시지에 `Co-Authored-By`나 세션 링크 같은 도구 표시 줄을 붙이지 않는다(사용자 결정, 2026-10-28).
- main 기록은 2026-10-28에 한 번 다시 썼다. 옛 기록의 커밋을 merge하거나 강제 푸시로 되살리지 않는다. 옛 기록 위에서 작업하던 클론은 `git fetch` 후 `git reset --hard origin/main`으로 옮긴다(푸시하지 않은 커밋은 `git rebase --onto`로).
