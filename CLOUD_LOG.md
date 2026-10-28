# 클라우드 작업 기록

클라우드 세션(코드·데이터·문서 담당)이 한 일을 최신순으로 적는다. 로컬 세션은 작업을 시작할 때 `git pull` 후 이 파일을 열고, **로컬 확인**이 비어 있는 항목을 읽는다. 해야 할 일이 있으면 `LOCAL_TASKS.md`에 있고, 다 읽으면 체크하고 커밋한다.

시각은 한국 시간이다. 커밋은 `git log --oneline`의 제목이나 해시로 찾는다. 2026-10-28 기록을 다시 쓴 뒤의 해시다.

항목 형식:

- **바뀐 것**: 무엇을 왜 바꿨는지
- **로컬에서 할 일**: 명령 한 줄을 다시 돌려야 하는지, 플레이로 확인할 것이 있는지. 없으면 "없음"
- **로컬 확인**: 로컬 세션이 읽고 체크한다

## 2026-10-28 20:19 클래스 두 개와 태그 조합 (M4)

- **바뀐 것**
  - 사람이 고른 방향(2026-10-28): 클래스는 **자유 조합**, 지금은 최대 2개(`HostConfig.MaxClasses`). 첫 클래스는 **전사**와 **화염 마법사**. 나중에 2개보다 많이 고르게 바꿀 수 있는지도 점검해 `Docs/Classes.md` "2개보다 많이 고르게 할 때"에 적었다. 데이터와 통신은 그대로 두고, 스킬 칸 고르기 창과 패시브 균형만 정하면 된다
  - 데이터: `Assets/_Project/Data/Classes/classes.csv`(패시브), `skills.csv`(클래스마다 스킬 2개). 전사는 회전베기(둘레 전체)와 전투 함성(근접 피해·공격 속도 버프), 화염 마법사는 화염구(날아가 터지는 투사체)와 불꽃 부여(모든 공격에 화염 태그). 형식과 규칙은 `Docs/Classes.md`
  - Core: `ClassSheet`(시트 읽기, 틀린 칸은 행 번호와 함께 알려 줌), `World.Skills.cs`(스킬 쓰기, 플레이어 투사체는 적과 보스에만 맞음), 플레이어마다 재사용 대기와 버프, 능력치는 장비 + 패시브 + 버프. 원소는 태그로만 다룬다(`Docs/Loot.md`에서 미뤄 둔 것). 불꽃 부여를 켜면 회전베기가 근접 + 화염이 되어 전사·화염 마법사 패시브와 화염·근접 접사가 모두 붙는다
  - 통신: 프로토콜 0.5.0. Join에 고른 클래스, Welcome·Roster에 받아들인 클래스, 새 메시지 SkillCommand, 스냅숏에 칸별 재사용 대기·버프와 투사체를 쏜 스킬, 사건 SkillUsed·Burst. 가장 큰 스냅숏은 4인이 스킬을 모두 쓰는 보스전에서 754바이트(한도 1200). 옛 0.4.0 클라이언트도 "버전이 다릅니다"를 받는다
  - 화면: 메뉴에 클래스 고르기(누른 순서가 스킬 칸 순서. 처음엔 둘 다 골라져 있다), PC는 아래 가운데 Q E R F 칸(마우스를 올리면 설명), 휴대폰은 오른쪽 아래 2×2 버튼(손가락을 직접 읽어서 스틱을 잡은 채 눌러도 된다). 회전베기 고리, 화염구 불덩이·폭발, 버프 고리. 색은 태그를 따른다(화염 주황, 마법 보라, 근접 은색). 가방 창에 클래스와 스킬별 지금 피해
  - 균형(`CoreSandbox --classes`, 구리 검): 혼자 보스 처치가 클래스 없이 82~133초(다섯 번 중 두 번 실패), 하나면 48~79초, 둘이면 38~55초. 화염 마법사가 처음 숫자로는 전사보다 한참 세서 화염구를 140%·1.2초에서 120%·1.5초로 낮췄다. 보스 체력은 그대로 두었다. 플레이해 보고 너무 쉬우면 `bosses.csv` 체력부터 올린다
  - 테스트 157개(새로 17개), Unity 쪽 컴파일 확인(웹 포함), 로컬 스크립트 확인 통과. 새 글자가 모두 게임 글꼴에 있는 것도 확인했다
  - Core tests와 Web demo가 모두 성공했고, 20:33에 배포되어 사이트 파일이 바뀐 것까지 확인했다(https://github.com/LeArc6910/pray-to/actions/runs/40679167188 )
- **로컬에서 할 일**: `LOCAL_TASKS.md` 6번(명령 한 줄로 새 파일의 `.meta`와 `GameSheets.asset` 커밋, 사람이 플레이 확인)
- **로컬 확인**: [x] 2026-10-29 로컬 세션

## 2026-10-28 19:18 미니맵과 공격 스틱 (19:10 휴대폰 요청)

- **바뀐 것**
  - 미니맵(`MapView.cs`): 오른쪽 위에 내 주변 40×40칸. 탭·클릭하면 동굴 전체 지도가 크게 열리고, 아무 데나 다시 탭·클릭하면 닫힌다. PC는 M으로 열고 닫고 Esc로도 닫는다(지도가 열려 있으면 Esc는 나가기 대신 지도를 닫는다)
  - 보여 주는 것(`Docs/Design.md` "화면과 조작"): 가 본 곳(지나간 자리에서 8칸)만 바닥·벽이 채워진다. 나, 다른 플레이어, 보스 아레나(가 보지 않아도 표시)를 점과 원으로 찍는다. 몬스터와 바닥 아이템은 넣지 않았다. 가 본 곳 기록은 세션이 끝나면 사라진다
  - 지도가 크게 열려 있는 동안 공격은 막고, 터치는 스틱으로 넘기지 않고, 그 아래 버튼(가방 창, [나가기] 등)은 눌리지 않게 했다. IMGUI는 먼저 그린 버튼에 클릭을 주기 때문에, 그대로 두면 지도를 닫으려는 탭이 [나가기]를 누를 수 있었다
  - 공격 스틱(`Controls.cs`): 오른쪽 절반을 누르면 누른 자리에 붉은 스틱이 생기고, 누르고 있는 동안 계속 휘두른다. 끄는 방향을 바라보고, 거의 끌지 않으면 걷는 방향으로, 서 있으면 바라보던 방향 그대로 휘두른다. PC의 마우스 조준은 그대로다
  - 배치: [가방]·[나가기]를 미니맵 아래로 옮겼다. 가방 창은 그 열 왼쪽에 화면 높이만큼(좁은 세로 화면에서는 그 열 아래에) 열린다. 보스 체력바는 HUD와 미니맵 사이에 놓인다. 세로 화면에서는 HUD 폭을 줄여 미니맵과 겹치지 않게 했다. 휴대폰 가로·세로, 600×375, 1280×720에서 칸 배치가 겹치지 않는지 그림으로 확인했다(실제 화면은 Unity가 없어 못 봤다)
  - 가방 설명 상자를 글 길이에 맞춰 넓혀서, 로컬이 적은 "다."가 줄바꿈되는 문제를 고쳤다
  - `Docs/WebDemo.md` 터치 조작 표를 고쳤다
  - 배포: 이 푸시의 Web demo 첫 시도는 에디터가 시작 1초 만에 `Killed`(종료 코드 137)로 끝났다. 라이선스는 활성화·반납 모두 정상이었다. 실패한 job만 한 번 다시 돌렸더니 같은 `Library` 캐시로 성공했다(빌드 6분, https://github.com/LeArc6910/pray-to/actions/runs/40671141847 ). 19:37에 배포됐고 사이트의 파일이 새것으로 바뀐 것까지 확인했다. 원인은 러너 쪽 일시적인 것으로 보고 따로 고치지 않았다. 또 나오면 실패한 job을 다시 돌리고, 되풀이되면 클라우드에 알린다
- **로컬에서 할 일**: `LOCAL_TASKS.md` 5번(명령 한 줄로 `MapView.cs.meta` 커밋, 사람이 휴대폰으로 확인. Web demo 결과는 클라우드가 확인했다)
- **로컬 확인**: [x] 2026-10-28 로컬 세션

## 2026-10-28 18:18 웹 데모 라이선스 활성화 수정, 기록 정리 반영

- **바뀐 것**
  - 로컬의 두 번째 빌드 실패("serial invalid")를 고쳤다. GameCI(`unity-builder`)는 Personal 라이선스도 ULF 파일에서 꺼낸 시리얼로 활성화하는데, Unity 6 Personal은 시리얼이 없고 Hub가 ULF도 남기지 않는다. Unity 문서도 Personal은 명령줄 활성화를 지원하지 않는다고 적고 있다
  - 그래서 `unity-builder`를 빼고, 같은 GameCI 이미지(`unityci/editor:ubuntu-6000.6.3f1-webgl-3`) 안에서 `.github/scripts/build-web.sh`를 돌린다. 에디터에 들어 있는 Licensing Client로 `--activate-ulf --include-personal --username --password`(시리얼 없이) 활성화하고, `--showEntitlements`로 Unity Personal인지 확인한 뒤 빌드하고, 끝나면 성공·실패와 상관없이 `--return-ulf`로 돌려준다. 6000.6.3f1의 Licensing Client(1.18.3)를 직접 받아 이 옵션들이 있는 것을 확인했다. 실제 활성화는 비밀번호가 필요해 여기서는 못 해 봤다
  - 이제 시크릿은 `UNITY_EMAIL`, `UNITY_PASSWORD` 둘만 쓴다. `UNITY_LICENSE`는 필요 없다(지워도 된다)
  - 뒤이은 커밋: 이 푸시로 돈 Web demo에서 활성화는 성공했다(`Product: Unity Personal, Type: ULF`, 끝나고 반납도 됐다). 그 다음 에디터가 공유 메모리 부족(`Insufficient shared memory ... --shm-size=1025M`)으로 멈춰서 컨테이너에 `--shm-size=2g`를 줬다
  - 그 뒤 Web demo가 처음으로 성공했다(18:47, https://github.com/LeArc6910/pray-to/actions/runs/40663963893): 활성화 → 빌드 15분(`Web build Succeeded ... 15.2 MB, 0 errors`) → 라이선스 반납 → Pages 배포. https://learc6910.github.io/pray-to/ 에서 `index.html`과 `Build/` 파일 4개(loader, framework, data 5.4 MB, wasm 10.4 MB)가 내려오는 것까지 확인했다
  - 커밋 기록 정리를 반영했다: 이 클라우드 세션도 새 기록으로 옮겼고(`reset --hard origin/main`, 파일 내용은 옛 기록과 같았다), 앞으로 커밋은 `LeArc6910` noreply 주소로 만들고 도구 표시 줄을 붙이지 않는다. `CLAUDE.md` 규칙 절에 적었다. 이 파일의 커밋 해시도 새 기록의 해시로 고쳤다
  - 로컬 웹 빌드 확인에서 나온 것들
    - 웹 빌드 설정(gzip, decompression fallback, 데이터 캐싱)을 `ProjectSetup.SetUp`에서 정한다. 다음 setup 커밋에 `ProjectSettings`로 들어가고, 그 뒤로는 빌드가 `ProjectSettings.asset`을 바꾸지 않는다
    - `Main.unity`는 없을 때만 만든다. setup 때마다 씬을 다시 저장해서 fileID가 바뀌던 것을 없앴다. 씬을 다시 만들려면 파일을 지우고 setup을 돌린다
    - `.gitignore`에 `/Data/`(Burst가 웹 빌드 때 저장소 루트에 만드는 파일)
    - 가방 창의 설명 상자가 HUD를 가리지 않게 HUD 아래로 내렸고, 가방 창 배경을 불투명하게 해서 뒤의 이름표가 비치지 않는다
- **로컬에서 할 일**
  - Web demo는 클라우드가 확인했다(위). 사람이 휴대폰으로 `LOCAL_TASKS.md` 4번을 한다
  - `LOCAL_TASKS.md` 3번: 명령 한 줄을 다시 실행해 웹 설정이 들어간 `ProjectSettings`를 커밋한다
- **로컬 확인**: [x] 2026-10-28 로컬 세션

## 2026-10-28 01:05 웹 데모: 빌드·배포 워크플로, 터치 조작, 한글 글꼴

- **바뀐 것**
  - `.github/workflows/web-demo.yml`: 푸시마다 GameCI로 WebGL 빌드를 만들어 GitHub Pages(https://learc6910.github.io/pray-to/)에 올린다. Unity 라이선스 시크릿이 없으면 건너뛰고 안내만 남긴다
  - `Editor/WebBuild.cs`: `Game > Build Web Demo` 메뉴와 CI용 `RunBatch`. gzip + decompression fallback이라 Pages에서 그대로 돈다
  - 터치 조작(`Controls.cs`): 왼쪽 드래그 이동, 오른쪽 누르고 있기 공격, 오른쪽 위 [가방]·[나가기] 버튼. 키보드·마우스는 그대로
  - 화면 크기(`Ui.cs`, `Plugins/WebGL/Display.jslib`): 휴대폰의 픽셀 밀도만큼 UI를 키운다. 세로 화면은 카메라를 넓힌다. 가방 창은 화면 폭에 맞추고 통째로 스크롤한다
  - 한글 글꼴: `Resources/Fonts/NotoSansKR-Game.ttf`(Noto Sans KR Medium 부분집합, 약 520 KB, Git LFS). 웹 빌드는 시스템 글꼴을 못 쓰기 때문이다. 에디터와 PC 빌드도 이 글꼴을 쓴다. 라이선스는 `Docs/Licenses/NotoSansKR-OFL.txt`
  - 웹 메뉴에서는 호스트·참가를 숨긴다(브라우저는 호스트가 될 수 없다)
  - CI가 `UNITY_WEBGL`을 정의한 채로도 Unity 쪽 코드를 컴파일해 본다
  - 공개 전환 전에 기록을 확인했다: 커밋 이메일은 noreply 주소이고 키·비밀번호·라이선스 파일은 없다
  - 뒤이은 커밋: CI의 로컬 스크립트 확인이 저장소를 복제할 때 LFS 글꼴을 받으려다 실패해서, 그 확인에서는 LFS 다운로드를 건너뛰게 했다. 로컬에는 영향 없다
- **로컬에서 할 일**: `LOCAL_TASKS.md` 3번(명령 한 줄 다시 실행해 새 파일의 `.meta` 커밋). 사람은 "웹 데모용"(공개 전환, Pages 설정, 시크릿)과 4번(휴대폰 확인)
- **로컬 확인**: [x] 2026-10-28 로컬 세션

## 2026-10-28 01:00 로컬 결과 확인, 이 기록 시작

- **바뀐 것**
  - 명령 한 줄 세 번째 실행(00:42) 결과를 확인했다. 프로젝트 설정 성공, EditMode 140/140, PlayMode 2/2 통과
  - 커밋된 설정도 맞다: Active Input Handling = Input System(1), Product Name `pray-to`, 2D 모드, URP 2D 렌더러(`URP-2D.asset`)가 Graphics와 모든 Quality 레벨에 지정, Main 씬이 빌드 목록에 있다. 첫 실행 때 생긴 `.meta`가 이번에 다시 쓰였지만 GUID는 그대로다
  - 이 파일을 만들었다. 앞으로 클라우드는 푸시할 때마다 여기에 적는다(`CLAUDE.md`의 작업 분담)
  - 로컬 세션이 알려 준 대로 저장소가 private이다. GitHub Pages를 쓰려면 유료 플랜이 필요하므로 웹 데모 배포처는 사람이 정해야 한다. `LOCAL_TASKS.md`의 "웹 데모용"에 선택지를 적었다
- **로컬에서 할 일**: 없음. 다음은 `LOCAL_TASKS.md` 2번 플레이 확인(사람)
- **로컬 확인**: [x] 2026-10-28 로컬 세션

## 2026-10-27 23:45 Light2D 컴파일 에러 수정

커밋: `bb08eab` Reference URP's 2D assembly and compile-check each asmdef on its own

- **바뀐 것**
  - URP 17.6은 `Light2D`, `Renderer2DData`를 별도 어셈블리 `Unity.RenderPipelines.Universal.2D.Runtime`에 둔다. `Game.Client`, `Game.Client.Editor` asmdef에 참조를 넣었다
  - `DevTools/UnityCompileCheck`가 asmdef마다 따로, 참조를 asmdef에서 읽어 컴파일하도록 바꿨다. 이번 같은 참조 누락을 CI가 잡는다
  - 명령 한 줄이 라이선스 에러 줄을 보고에 적고 Hub 재로그인 안내를 붙인다
- **로컬에서 할 일**: 명령 한 줄 다시 실행 (00:42에 했고 성공)
- **로컬 확인**: [x]

## 2026-10-27 20:50 명령 한 줄

커밋: `114369b` Add a one-command local setup that configures Unity and runs its tests, `6fff389` Fix the setup script check on the CI runner

- **바뀐 것**
  - `Game > Set Up Project`(`ProjectSetup.cs`): 입력 방식, Product Name, 2D 모드, URP 2D 렌더러, Main 씬과 시트 에셋을 코드로 만든다
  - `DevTools/Local/setup.ps1`, `setup.sh`: 배치 모드로 위 메뉴와 EditMode·PlayMode 테스트를 돌리고 결과를 `LOCAL_TASKS.md`에 적고 커밋한다
  - PlayMode 테스트(`Assets/Tests/PlayMode`): 혼자 하기와 호스트를 5초씩 돌려 에러가 없는지 본다
  - `LOCAL_TASKS.md`를 명령 한 줄과 사람이 직접 하는 일로 나눴다
- **로컬에서 할 일**: 명령 한 줄 실행 (했고, 세 번째에 성공)
- **로컬 확인**: [x]

## 2026-10-27 20:24 M3b 드랍과 가방

커밋: `3d2dad5` Add drops, ground loot and a bag with equip, rune and discard commands

- **바뀐 것**: 몬스터·보스 드랍, 바닥 아이템 자동 줍기, 가방(24칸, 룬은 종류별로 쌓임), 장착·벗기·룬 끼우기·버리기 명령, 보스 처치 뒤 진행 레벨 상승. 프로토콜 0.4. 자세한 규칙은 `Docs/Loot.md`의 드랍 절
- **로컬에서 할 일**: `LOCAL_TASKS.md` 2번의 M3b 플레이 확인
- **로컬 확인**: [x] (기록을 시작하기 전 작업)

## 2026-10-27 02:22 M3 첫 보스

커밋: `cc6eb80` Add the first boss: an arena, telegraphed attacks, phases and the client side

- **바뀐 것**: 보스 아레나 생성, 동굴 수호자(3페이즈, 예고 장판, 투사체, 돌진, 소환), 인원수에 따른 체력, 초기화와 재등장. `Docs/Bosses.md`
- **로컬에서 할 일**: `LOCAL_TASKS.md` 2번의 M3 플레이 확인
- **로컬 확인**: [x] (기록을 시작하기 전 작업)

## 2026-10-27 01:58 M2 월드, 전투, 협동 접속

커밋: `718573a` Add the cave world, combat simulation and host/client netcode in Core, `0889c24` Add the Unity client for M2 and compile-check it against Unity's assemblies

- **바뀐 것**: 절차 생성 동굴, 이동·충돌, 근접 전투, 슬라임, 호스트 권한 멀티플레이(Core 메시지 + Netcode 전송), Unity 클라이언트. `Docs/Combat.md`, `Docs/Multiplayer.md`
- **로컬에서 할 일**: `LOCAL_TASKS.md` 2번의 M2 플레이 확인
- **로컬 확인**: [x] (기록을 시작하기 전 작업)

## 2026-10-26 19:05 M0 뼈대, M1 아이템

커밋: `5ab7ea8` Set up the repository skeleton with a Unity-free core and CI, `d1e3a47` Add item generation with affixes, runes and runewords

- **바뀐 것**: 저장소 구조, Unity 없이 도는 Core와 테스트, CI. 아이템 베이스·접사·룬·룬워드 시트와 생성기. `Docs/Design.md`, `Docs/Loot.md`
- **로컬에서 할 일**: 없음
- **로컬 확인**: [x] (기록을 시작하기 전 작업)
