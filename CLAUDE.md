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

```
dotnet test DevTools/Game.Core.Tests
dotnet run --project DevTools/CoreSandbox -- --loot 25 10000
dotnet run --project DevTools/CoreSandbox -- --progression
dotnet run --project DevTools/CoreSandbox -- --item 25 7
dotnet run --project DevTools/CoreSandbox -- --fight 120 42
dotnet run --project DevTools/CoreSandbox -- --boss 300 42 --players 2
dotnet run --project DevTools/CoreSandbox -- --boss 300 42 --classes warrior,fire_mage
dotnet run --project DevTools/CoreSandbox -- --boss 400 1 --boss-id crystal_queen --gear iron_sword,iron_armor --reaction 0.5
# 그 레벨의 캐릭터처럼: 스킬 포인트, 숙련 특성, 결정을 다 쓴 봇 (Docs/Bosses.md 성장 곡선)
dotnet run --project DevTools/CoreSandbox -- --boss 600 1 --boss-id void_lord --gear obsidian_sword,obsidian_armor,obsidian_helmet,obsidian_boots,obsidian_ring,obsidian_amulet --classes warrior,fire_mage --level 34 --build
dotnet run --project DevTools/CoreSandbox -- --farm 120 mushroom
dotnet run --project DevTools/CoreSandbox -- --economy
dotnet run --project DevTools/CoreSandbox -- --factory 30
# 어느 시스템이 어느 시스템에 재료를 주는지, 고립된 재료(Docs/Systems.md)
dotnet run --project DevTools/CoreSandbox -- --supply
dotnet run --project DevTools/CoreSandbox -- --defense 6 42 --gear iron_sword,iron_helmet,iron_armor,iron_boots --classes warrior --turrets 3
dotnet run --project DevTools/CoreSandbox -- --rift 7 42 --players 2 --gear void_sword,void_helmet,void_armor,void_boots --classes warrior,fire_mage
dotnet run --project DevTools/CoreSandbox -- --skills 60 --gear iron_sword
dotnet run --project DevTools/CoreSandbox -- --supports 60 --gear iron_sword
# 새 캐릭터의 첫 한 시간을 안내대로(상태와 길찾기로 움직이는 봇, Docs/Playtest.md). 시드마다 지표 표와 막힘 보고서
dotnet run --project DevTools/CoreSandbox -- --hour 60 1,2,3,4,5,6,7,8

# 도트 그림과 효과음·음악을 DevTools/ArtPreview/out/에 PNG와 WAV로 쓴다(sheet.png, player.png, bosses.png, anim/*.gif, cave.png, cave_unlit.png, arena.png, effects.png, sounds/, music/)
dotnet run --project DevTools/ArtPreview

# Unity 쪽 코드 컴파일 확인. fetch는 세션마다 한 번(몇 분, 에디터와 웹 플레이어의 엔진 DLL만 .unity/에 받는다)
DevTools/UnityCompileCheck/fetch-unity.sh
dotnet build DevTools/UnityCompileCheck
# 웹 빌드처럼: 웹 플레이어의 DLL로 게임 어셈블리만. 에디터에만 있는 API를 쓰면 여기서 걸린다
dotnet build DevTools/UnityCompileCheck/Assemblies/Game.Client -p:DefineConstants=UNITY_WEBGL -p:UnityAssemblies=$PWD/DevTools/UnityCompileCheck/.unity/WebGL -o /tmp/webgl-check

# 웹 데모를 헤드리스 Chromium으로 플레이해 보기(사진과 report.md가 DevTools/WebPlaytest/out/에). --wait면 배포를 기다린다
node DevTools/WebPlaytest/playtest.js --wait
# LOCAL_TASKS.md의 사람 확인을 차례로 해 보는 전체 점검(30~40분, 결과는 out/sweep/). --only로 일부만
node DevTools/WebPlaytest/sweep.js --wait

# 로컬 명령 한 줄 스크립트 확인 (pwsh가 있으면 PowerShell 쪽도)
bash DevTools/Local/test/run.sh
```

GitHub Actions(`.github/workflows/core-tests.yml`)가 푸시마다 테스트, 도트 그림 그리기(결과는 실행의 `art-preview` 아티팩트), Unity 쪽 컴파일 확인, 로컬 스크립트 확인을 돌린다. Unity DLL은 Actions 캐시에 남겨 두고 저장소에는 넣지 않는다.

## 규칙

- Core에서는 `System.Random` 대신 `GameRandom`을 쓴다. 호스트, 클라이언트, 테스트가 같은 결과를 내야 한다. 시각(`DateTime.Now`, `Environment.TickCount` 등)도 Core 안에서 읽지 않고 밖에서 넘겨받는다.
- 게임 상태는 호스트의 Core 시뮬레이션만 바꾼다. 자세한 내용은 `Docs/Design.md`의 멀티플레이 절에 있다.
- PC와 모바일 크로스 플랫폼으로 만든다(사용자 결정, 2026-10-29). 새 기능은 키보드·마우스와 터치 둘 다로 쓸 수 있어야 하고, UI는 휴대폰 가로·세로와 작은 PC 창에 들어가야 한다. `Docs/Design.md` 기술 원칙 7.
- 에디터에서 손으로 만드는 콘텐츠를 늘리지 않는다. 월드는 절차 생성, 아이템·몬스터·보스는 데이터, 에셋은 에디터 스크립트로 만든다.
- 사용자에게는 한국어로 말한다(사용자 결정, 2026-10-28). 작업 보고, 중간 안내, 마지막 요약까지 모든 답을 예외 없이 한국어로 쓴다(사용자가 2026-11-09에 다시 강조). 작업 사이에 남기는 짧은 안내와 명령·도구 설명(사람 화면에 보이는 설명 줄)도 한국어로 쓴다(사용자 결정, 2026-11-09). 문서도 지금처럼 한국어로 쓴다.
- 저장소에 올리는 파일, 커밋, 브랜치, PR 어디에도 AI 도구를 썼다는 표시(도구·회사 이름, 대화 앱 주소, 도구 이름이 붙은 브랜치)를 두지 않는다. 이 파일의 이름은 예외다(사용자 결정, 2026-11-11).
- 커밋 메시지는 영어로 쓴다. 첫 줄은 명령형 요약, 본문에는 무엇을 왜 바꿨는지 적는다.
- 커밋 작성자는 저장소 주인으로 한다: `git config user.name LeArc6910`, `git config user.email 270705266+LeArc6910@users.noreply.github.com`. 메시지에 `Co-Authored-By`나 세션 링크 같은 도구 표시 줄을 붙이지 않는다(사용자 결정, 2026-10-28).
- main 기록은 2026-10-28에 한 번 다시 썼다. 옛 기록의 커밋을 merge하거나 강제 푸시로 되살리지 않는다. 옛 기록 위에서 작업하던 클론은 `git fetch` 후 `git reset --hard origin/main`으로 옮긴다(푸시하지 않은 커밋은 `git rebase --onto`로).
