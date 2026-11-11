# 코드 구조

`CLAUDE.md`를 짧게 두려고(조사 보고서 후보 20, 2026-11-11) 코드 구조의 자세한 설명을 여기로 옮겼다. 규칙은 `CLAUDE.md`에 남아 있다. 이 문서는 어디에 무엇이 있는지 찾을 때 읽는다.


- `Assets/_Project/Scripts/Core` (`Game.Core`): 게임 규칙과 시뮬레이션 전부. UnityEngine을 참조하지 않는다(`noEngineReferences`). netstandard2.1, C# 9.
- `Assets/_Project/Data`: 데이터 시트(CSV). Core가 직접 읽는다. 형식은 `Docs/Loot.md`(아이템, 소모품), `Docs/Combat.md`(몬스터), `Docs/Bosses.md`(보스), `Docs/Classes.md`(클래스·스킬·패시브 트리), `Docs/Crafting.md`(칸, 재료, 조합법), `Docs/Farming.md`(작물, 하루와 잠은 `Docs/Time.md`), `Docs/Fishing.md`(물고기: `materials.csv`의 `fish` 열), `Docs/Automation.md`(기계), `Docs/Guide.md`(목표 안내), `Docs/Npcs.md`(상인·대장장이·어부, 상점, 부탁, 호감도), `Docs/Story.md`(이야기, 장소, 인물: `Data/Story/`)에 있다. 생활 기술은 `Docs/LifeSkills.md`, 닭장은 `Docs/Animals.md`, 통발은 `Docs/Fishing.md`, 기지 방어와 포탑은 `Docs/Defense.md`. 생물군계는 시트가 아니라 코드다(`Docs/Biomes.md`). 시트를 새로 추가하면 `GameSheets`와 `SceneSetup`에도 넣는다.
- `Assets/Tests/EditMode` (`Game.Core.Tests`): Core 테스트(NUnit). Unity Test Runner와 `dotnet test`가 같은 파일을 쓴다.
- `Assets/Tests/PlayMode` (`Game.Client.Tests`): 실제 클라이언트를 플레이 모드로 몇 초씩 돌려 에러가 없는지 보는 테스트. Unity에서만 돌고(명령 한 줄이 실행한다), 클라우드는 컴파일만 확인한다. `FeatureTests`는 호스트 월드를 직접 꾸며 새 기능의 장면을 그리고, `[Category(Screens.Category)]`가 붙은 장면은 창이 보이는 Unity에서 한 번 더 돌며 `Screens.Take`로 화면을 찍는다. 새 화면을 만들면 장면과 사진을 더한다. `LayoutTests`는 휴대폰·PC 크기마다 화면 고정 요소가 겹치지 않는지 숫자로 본다(`Ui.SizeOverride`, `Controls.ForceTouch`).
- `DevTools/`: Unity 없이 Core를 빌드·테스트·시뮬레이션하는 .NET 프로젝트. 소스는 `Assets/`에서 가져다 쓰므로 여기에 게임 코드를 두지 않는다.
  - `Game.Core`: Core만 컴파일한다. 경고도 에러로 친다.
  - `Game.Core.Tests`: 테스트
  - `CoreSandbox`: 밸런스 시뮬레이션 콘솔
  - `ArtPreview`: 게임의 도트 그림과 효과음(`Scripts/Client/Art`, UnityEngine을 쓰지 않는 C#)을 PNG와 WAV로 만든다. 움직임은 `WorldView`처럼 재생한 GIF(`out/anim/`)로 본다. Unity 없이 보고 듣고 고칠 때 쓴다. 그림이나 소리가 잘못되면 실패한다
  - `UnityCompileCheck`: Unity 쪽 코드(`Scripts/Client`, `Tests/PlayMode`)를 Unity 실제 엔진 DLL로 컴파일만 해 본다. asmdef마다 `Assemblies/`에 프로젝트가 하나씩 있고, 참조는 asmdef의 `references`를 읽어 정한다(`Directory.Build.targets`, Unity처럼 전이되지 않는다). 패키지(Netcode, Transport, Input System, URP, Test Framework, Multiplayer Services·Authentication·Core) API는 `Stubs/<어셈블리 이름>/`에 패키지 소스와 같은 시그니처로 적어 둔다. 타입은 패키지에서 실제로 들어 있는 어셈블리(패키지의 asmdef로 확인)의 스텁에 넣는다. 예: `Light2D`는 `Unity.RenderPipelines.Universal.2D.Runtime`. 새 패키지 API를 쓰면 여기에 추가한다. 에디터 DLL에는 플레이어에 없는 API도 있어서(예: `GUIStyleState.scaledBackgrounds`), 웹 빌드용 확인은 웹 플레이어의 DLL(`.unity/WebGL`)로 한다.
  - `Local`: 로컬 컴퓨터에서 쓰는 명령 한 줄(`setup.ps1`, `setup.sh`)과 감시 스크립트(`watch.ps1`, `watch.sh`). 짝마다 같은 일을 하므로 하나를 고치면 다른 하나도 고친다. `test/run.sh`가 가짜 Unity(`test/fake-unity`)로 네 스크립트를 확인한다(PowerShell 쪽은 `pwsh`가 있을 때. 클라우드에서는 `dotnet tool install --global PowerShell` 후 `DOTNET_ROOT=/root/.dotnet`로 돌린다).
- `Assets/_Project/Scripts/Client` (`Game.Client`): 화면, 입력, 네트워크 전송만 맡는다. 게임 규칙을 두지 않는다. 그림은 `Art/`에 코드로 그린 16px 도트이고 `Sprites`가 Unity 스프라이트로 바꾼다. 효과음도 `Art/SoundArt.cs`가 합성하고 `Sounds`가 튼다. 배경 음악과 환경음은 `Art/MusicArt.cs`가 합성하고 `Music`이 튼다(`Docs/ArtPipeline.md`). UI는 IMGUI이고 크기는 `Ui`의 점 단위로 잡는다(`Screen.width` 대신 `Ui.Width`). 줄이 바뀔 수 있는 한글은 `Ui.Wrap`으로 띄어쓰기에서 바꾼다(Unity는 낱말 가운데서도 바꾼다). 입력은 `Controls`가 키보드·마우스와 터치를 함께 읽는다. `Editor/`에는 프로젝트 설정과 씬·에셋을 코드로 만드는 메뉴(`Game > Set Up Project`, `Game > Set Up Main Scene`)와 웹 빌드(`Game > Build Web Demo`)가 있다.
- 웹 데모: `.github/workflows/web-demo.yml`이 GameCI로 빌드해 GitHub Pages에 올린다. 협동은 Unity Relay의 방 코드로 하고(웹과 PC가 함께, `Docs/Multiplayer.md`), 글꼴은 `Resources/Fonts`의 도트 글꼴 Galmuri11 Bold 부분집합(`DevTools/Fonts/make-pixel-font.py`)이 기본이고, 메뉴에서 Noto Sans KR 부분집합(`make-korean-font.py`)으로 바꿀 수 있다. 글자 크기는 `Ui.Text`로 정한다(도트 글꼴은 12px의 정수배). `Docs/WebDemo.md`.

