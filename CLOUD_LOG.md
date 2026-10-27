# 클라우드 작업 기록

클라우드 세션(코드·데이터·문서 담당)이 한 일을 최신순으로 적는다. 로컬 세션은 작업을 시작할 때 `git pull` 후 이 파일을 열고, **로컬 확인**이 비어 있는 항목을 읽는다. 해야 할 일이 있으면 `LOCAL_TASKS.md`에 있고, 다 읽으면 체크하고 커밋한다.

시각은 한국 시간이다. 커밋은 `git log --oneline`의 제목이나 해시로 찾는다.

항목 형식:

- **바뀐 것**: 무엇을 왜 바꿨는지
- **로컬에서 할 일**: 명령 한 줄을 다시 돌려야 하는지, 플레이로 확인할 것이 있는지. 없으면 "없음"
- **로컬 확인**: 로컬 세션이 읽고 체크한다

## 2026-10-28 01:00 로컬 결과 확인, 이 기록 시작

- **바뀐 것**
  - 명령 한 줄 세 번째 실행(00:42) 결과를 확인했다. 프로젝트 설정 성공, EditMode 140/140, PlayMode 2/2 통과
  - 커밋된 설정도 맞다: Active Input Handling = Input System(1), Product Name `pray-to`, 2D 모드, URP 2D 렌더러(`URP-2D.asset`)가 Graphics와 모든 Quality 레벨에 지정, Main 씬이 빌드 목록에 있다. 첫 실행 때 생긴 `.meta`가 이번에 다시 쓰였지만 GUID는 그대로다
  - 이 파일을 만들었다. 앞으로 클라우드는 푸시할 때마다 여기에 적는다(`CLAUDE.md`의 작업 분담)
  - 로컬 세션이 알려 준 대로 저장소가 private이다. GitHub Pages를 쓰려면 유료 플랜이 필요하므로 웹 데모 배포처는 사람이 정해야 한다. `LOCAL_TASKS.md`의 "웹 데모용"에 선택지를 적었다
- **로컬에서 할 일**: 없음. 다음은 `LOCAL_TASKS.md` 2번 플레이 확인(사람)
- **로컬 확인**: [ ]

## 2026-10-27 23:45 Light2D 컴파일 에러 수정

커밋: `27c68c2` Reference URP's 2D assembly and compile-check each asmdef on its own

- **바뀐 것**
  - URP 17.6은 `Light2D`, `Renderer2DData`를 별도 어셈블리 `Unity.RenderPipelines.Universal.2D.Runtime`에 둔다. `Game.Client`, `Game.Client.Editor` asmdef에 참조를 넣었다
  - `DevTools/UnityCompileCheck`가 asmdef마다 따로, 참조를 asmdef에서 읽어 컴파일하도록 바꿨다. 이번 같은 참조 누락을 CI가 잡는다
  - 명령 한 줄이 라이선스 에러 줄을 보고에 적고 Hub 재로그인 안내를 붙인다
- **로컬에서 할 일**: 명령 한 줄 다시 실행 (00:42에 했고 성공)
- **로컬 확인**: [x]

## 2026-10-27 20:50 명령 한 줄

커밋: `5248174` Add a one-command local setup that configures Unity and runs its tests, `99caee8` Fix the setup script check on the CI runner

- **바뀐 것**
  - `Game > Set Up Project`(`ProjectSetup.cs`): 입력 방식, Product Name, 2D 모드, URP 2D 렌더러, Main 씬과 시트 에셋을 코드로 만든다
  - `DevTools/Local/setup.ps1`, `setup.sh`: 배치 모드로 위 메뉴와 EditMode·PlayMode 테스트를 돌리고 결과를 `LOCAL_TASKS.md`에 적고 커밋한다
  - PlayMode 테스트(`Assets/Tests/PlayMode`): 혼자 하기와 호스트를 5초씩 돌려 에러가 없는지 본다
  - `LOCAL_TASKS.md`를 명령 한 줄과 사람이 직접 하는 일로 나눴다
- **로컬에서 할 일**: 명령 한 줄 실행 (했고, 세 번째에 성공)
- **로컬 확인**: [x]

## 2026-10-27 20:24 M3b 드랍과 가방

커밋: `f873c58` Add drops, ground loot and a bag with equip, rune and discard commands

- **바뀐 것**: 몬스터·보스 드랍, 바닥 아이템 자동 줍기, 가방(24칸, 룬은 종류별로 쌓임), 장착·벗기·룬 끼우기·버리기 명령, 보스 처치 뒤 진행 레벨 상승. 프로토콜 0.4. 자세한 규칙은 `Docs/Loot.md`의 드랍 절
- **로컬에서 할 일**: `LOCAL_TASKS.md` 2번의 M3b 플레이 확인
- **로컬 확인**: [x] (기록을 시작하기 전 작업)

## 2026-10-27 02:22 M3 첫 보스

커밋: `eea473e` Add the first boss: an arena, telegraphed attacks, phases and the client side

- **바뀐 것**: 보스 아레나 생성, 동굴 수호자(3페이즈, 예고 장판, 투사체, 돌진, 소환), 인원수에 따른 체력, 초기화와 재등장. `Docs/Bosses.md`
- **로컬에서 할 일**: `LOCAL_TASKS.md` 2번의 M3 플레이 확인
- **로컬 확인**: [x] (기록을 시작하기 전 작업)

## 2026-10-27 01:58 M2 월드, 전투, 협동 접속

커밋: `2ad218f` Add the cave world, combat simulation and host/client netcode in Core, `53fe281` Add the Unity client for M2 and compile-check it against Unity's assemblies

- **바뀐 것**: 절차 생성 동굴, 이동·충돌, 근접 전투, 슬라임, 호스트 권한 멀티플레이(Core 메시지 + Netcode 전송), Unity 클라이언트. `Docs/Combat.md`, `Docs/Multiplayer.md`
- **로컬에서 할 일**: `LOCAL_TASKS.md` 2번의 M2 플레이 확인
- **로컬 확인**: [x] (기록을 시작하기 전 작업)

## 2026-10-26 19:05 M0 뼈대, M1 아이템

커밋: `5398fbd` Set up the repository skeleton with a Unity-free core and CI, `f641e79` Add item generation with affixes, runes and runewords

- **바뀐 것**: 저장소 구조, Unity 없이 도는 Core와 테스트, CI. 아이템 베이스·접사·룬·룬워드 시트와 생성기. `Docs/Design.md`, `Docs/Loot.md`
- **로컬에서 할 일**: 없음
- **로컬 확인**: [x] (기록을 시작하기 전 작업)
