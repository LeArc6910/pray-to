# 로컬 작업 지시서

클라우드 세션(코드·데이터·문서 담당)이 로컬(Unity 에디터가 있는 컴퓨터)에 넘기는 작업 목록이다.
에디터 설정, 컴파일 확인, 테스트는 **명령 한 줄**로 끝난다. 그 밖에 사람이 직접 해야 하는 일은 아래에 따로 모아 둔다.
로컬 세션(이 컴퓨터에서 도는 Claude)이 있다면 명령 한 줄과 결과 보고는 로컬 세션이 해도 된다.

## 규칙

- 작업 브랜치: `main`. 명령 한 줄이 시작할 때 `git pull`을 한다
- 클라우드가 무엇을 바꿨는지는 `CLOUD_LOG.md`에 최신순으로 있다. 작업 전에 로컬 확인이 비어 있는 항목을 읽고 체크한다
- 클라우드가 스크립트를 수정하므로 **로컬에서는 `.cs` 파일을 고치지 않는다**. 컴파일 에러나 버그는 결과 보고에 남기면 클라우드가 고친다. Unity가 스크립트를 자동으로 바꾸면 명령 한 줄이 그 파일을 커밋에서 빼고 보고에 적는다
- 로컬이 커밋하는 것: 씬, 프리팹, `.asset`, `.meta`, `Packages/packages-lock.json`, `ProjectSettings/`, 스크린샷, 아트·사운드 파일
- 이미지·오디오는 Git LFS로 올라간다. 명령 한 줄이 이 저장소에 LFS를 연결한다

## 명령 한 줄

Unity 에디터에서 이 프로젝트를 닫고, 저장소 폴더에서 실행한다.

```
# Windows (PowerShell)
powershell -ExecutionPolicy Bypass -File DevTools\Local\setup.ps1 -Commit

# macOS, Linux
DevTools/Local/setup.sh --commit
```

하는 일:

1. `git pull`, Git LFS 연결
2. `ProjectSettings/ProjectVersion.txt`의 Unity(6000.6.3f1)를 Unity Hub 설치 위치에서 찾는다
3. 배치 모드로 프로젝트를 열어 **Game > Set Up Project**를 실행한다. Active Input Handling(Input System Package), Product Name, 2D 모드, URP 2D 렌더러 에셋과 Graphics·Quality 지정, `Main` 씬, `GameSheets` 에셋을 만든다
4. EditMode 테스트(Core 규칙)와 PlayMode 테스트(실제 클라이언트로 혼자 하기와 호스트를 5초씩 돌리고, 혼자 하기에서 스킬 넷을 모두 써 보며 에러가 없는지)를 돌린다
5. 결과를 이 파일의 "결과 보고"에 적고, 생성된 파일과 함께 커밋·푸시한다

옵션: `-InstallEditor`(`--install-editor`)는 Unity가 없으면 Unity Hub로 설치한다(Web Build Support 포함). `-SkipTests`(`--skip-tests`), `-NoPull`(`--no-pull`). `-Commit`을 빼면 결과만 적고 커밋하지 않는다. Unity가 Hub 기본 위치가 아닌 곳에 있으면 환경 변수 `UNITY_EXE`에 실행 파일 경로를 넣는다. 로그는 `Logs/`에 남는다(커밋하지 않는다).

처음 실행은 패키지를 받고 임포트하느라 10분쯤 걸린다. 같은 메뉴는 에디터 안에서도 **Game > Set Up Project**로 실행할 수 있다.

## 사람이 직접 해야 하는 일

계정, 라이선스, 저장소 설정, 그리고 직접 해 보는 느낌은 Claude가 대신할 수 없다.

### 처음 한 번

- [x] **Unity Hub** 설치(https://unity.com/download), Unity 계정으로 로그인, Preferences > Licenses에서 **Personal 라이선스** 활성화. 명령 한 줄의 배치 모드도 이 라이선스로 돈다. Hub 로그인이 풀리면 배치 모드가 라이선스 에러(종료 코드 198)로 멈추니, 그때는 Hub에 다시 로그인한다
- [x] **Unity 6000.6.3f1**과 **Web Build Support** 설치. Hub에서 직접 하거나, 명령 한 줄에 `-InstallEditor`를 붙인다
- [x] **Git** 설치(Windows는 Git for Windows. Git LFS가 같이 들어 있다) 후 저장소 클론
- [x] 명령 한 줄 실행. 처음에 Windows 방화벽 창이 뜨면 Unity Editor를 **허용**한다(PlayMode 테스트가 호스트를 열기 때문이고, 같은 네트워크의 친구가 참가할 때도 필요하다)

### 웹 데모(모바일 브라우저 테스트)용

배포처는 공개 저장소 + GitHub Pages로 정했다(2026-10-28). 빌드·배포 워크플로(`web-demo.yml`)는 클라우드가 붙였고, 아래 셋을 하면 푸시마다 https://learc6910.github.io/pray-to/ 가 갱신된다. 자세한 건 `Docs/WebDemo.md`.

- [x] (2026-10-28 로컬 세션, 사용자 승인) **저장소를 public으로.** GitHub 저장소 Settings > General 맨 아래 Danger Zone > Change repository visibility > Make public. 클라우드 세션에는 이 권한이 없다. 로컬 세션에 맡긴다면 사람이 허락한 뒤 `gh repo edit LeArc6910/pray-to --visibility public --accept-visibility-change-consequences`. 공개 전에 기록을 확인했다: 커밋 이메일은 GitHub·Anthropic의 noreply 주소이고, 라이선스 파일·키·비밀번호는 없다
- [x] (2026-10-28 로컬 세션, `gh api`로 build_type=workflow) Settings > Pages > Build and deployment > Source를 **GitHub Actions**로 (public이 된 뒤에 고를 수 있다)
- [x] ~~라이선스 파일 찾기~~ 필요 없어졌다(2026-10-28). Unity 6 Personal에는 ULF 파일이 없고, 웹 데모 빌드는 이제 이메일·비밀번호로 Personal을 활성화한다
- [x] (2026-10-28 01:38 사람이 넣음. 값은 확인하지 않음) Settings > Secrets and variables > Actions에 시크릿 추가: `UNITY_EMAIL`, `UNITY_PASSWORD`(Unity 계정. 소셜 로그인이나 2단계 인증 없이 https://id.unity.com 에 이메일·비밀번호로 들어가지는 계정). **이 값들은 이 파일이나 커밋, 채팅에 절대 적지 않는다.** 공개 저장소여도 시크릿은 보이지 않는다. 처음에 넣었던 `UNITY_LICENSE`는 이제 쓰지 않으니 지워도 된다
- [x] Actions 탭 > Web demo > Run workflow로 첫 빌드를 돌린다(이후에는 푸시마다 돈다). 결과 보고에는 위를 했는지와 빌드 성공 여부만 적는다
  - 2026-10-28 01:52, 02:12 두 번 실패(로그인 401, 이어서 serial invalid). 클라우드가 라이선스 활성화를 고쳤고(`CLOUD_LOG.md`), 18:47 첫 성공·배포를 클라우드가 확인했다: https://github.com/LeArc6910/pray-to/actions/runs/40663963893 . 이제 푸시마다 저절로 돈다

### (선택) 휴대폰에서 로컬 작업 시키기: Remote Control

이 컴퓨터에서 Claude Code를 켜 두면 휴대폰 Claude 앱에서 그 세션에 일을 시킬 수 있다. 명령 한 줄 실행과 결과 확인을 밖에서 할 수 있다. Pro, Max, Team, Enterprise 플랜이 필요하다.

- [ ] Claude Code 설치. Windows PowerShell에서 `irm https://claude.ai/install.ps1 | iex` (macOS·Linux는 https://code.claude.com/docs/en/setup 참고)
- [ ] 저장소 폴더에서 `claude`를 한 번 실행해 claude.ai 계정으로 로그인
- [ ] 같은 폴더에서 `claude remote-control --name pray-to`. 처음에 `Enable Remote Control? (y/n)`에 y
- [ ] 휴대폰 Claude 앱 > Code에서 초록 점이 붙은 컴퓨터 아이콘의 `pray-to` 세션을 연다
- 컴퓨터가 켜져 있고 터미널이 열려 있는 동안만 쓸 수 있다. 끝내려면 터미널에서 Ctrl+C
- 파일 수정이나 명령 실행 허락을 묻는 창이 휴대폰으로 온다. Windows에서는 명령이 샌드박스 없이 돌기 때문에 무엇을 하려는지 보고 허락한다

### 매번: 직접 플레이하고 느낌 적기

아래 "할 일"의 플레이 확인. 테스트는 에러가 나는지만 본다. 손맛, 난이도, 보기 좋은지는 사람만 알 수 있다.

## 할 일

### 1. 명령 한 줄 실행 (2026-10-27 갱신)

예전 1~3번(프로젝트 열기, URP 2D 설정, 컴파일·테스트 확인 후 커밋)은 명령 한 줄이 한다.

- [x] 첫 실행(2026-10-27 23:31, 23:34): 라이선스 에러, 그 뒤 `Light2D` 컴파일 에러로 멈췄다. 결과 보고 참고
- 클라우드 조치: `Game.Client`와 `Game.Client.Editor` asmdef에 `Unity.RenderPipelines.Universal.2D.Runtime` 참조를 넣었다. 컴파일 확인도 asmdef마다 따로 빌드하도록 바꿔서 같은 실수를 CI가 잡는다. 라이선스 에러도 이제 보고에 원인과 함께 적힌다
- [x] `git pull` 후 위 "명령 한 줄"을 `-Commit`과 함께 다시 실행 (2026-10-28 00:42 성공: EditMode 140/140, PlayMode 2/2. 결과 보고 참고)
- [x] 실패해도 그대로 둔다. 결과 보고에 로그에서 뽑은 에러 줄이 적혀 올라가고, 클라우드가 고친다
- 클라우드 기준: EditMode 140개 통과. PlayMode 2개는 클라우드에서 돌릴 수 없어 이번이 처음이다

### 2. 플레이 확인 (사람)

1번이 성공한 뒤 Unity Hub에서 프로젝트를 열고 `Main` 씬에서 Play. 세 묶음을 한 번에 해도 된다. 2인 확인은 Window > Multiplayer Play Mode에서 Player 2를 켜고, 메인 에디터는 "호스트", Player 2는 "참가"(주소 `127.0.0.1`).

**M2 이동과 전투**: 혼자 하기에서
- [ ] WASD로 움직이고 벽을 따라 미끄러지는지
- [ ] 마우스 쪽을 바라보고, 왼쪽 클릭(또는 스페이스)을 누르고 있으면 계속 휘두르는지
- [ ] 초록 슬라임이 다가와 멈추고 빨갛게 깜박인 뒤 공격하는지, 그 사이 물러나면 피해를 안 받는지
- [ ] 피해 숫자가 뜨고, 슬라임이 죽으면 사라지는지. 일부러 맞아 쓰러지면 5초 뒤 시작 지점에서 일어나는지
- [ ] 한글(메뉴, 이름, "쓰러짐")이 제대로 보이는지
- [ ] 2인: 서로 움직이는 게 보이는지, 한 쪽이 때린 슬라임이 양쪽에서 같이 줄어드는지, 참가한 쪽이 Esc로 나가면 호스트 화면에서 사라지는지

**M3 보스전**: HUD의 "보스 아레나: ○○쪽 ○칸"을 따라 붉은 바닥의 방으로 간다
- [ ] 들어가면 "동굴 수호자 등장!" 배너와 화면 위 체력바가 나오는지
- [ ] 바닥에 빨간 원·부채꼴이 차오르고 다 차면 맞는지, 그 전에 벗어나면 안 맞는지. 주황 돌 파편이 사방으로 날아가는지
- [ ] 체력 66%, 33%에서 "2페이즈!", "3페이즈!" 배너와 포효(빨갛게 깜박임, 그 동안 피해 안 들어감)가 나오는지
- [ ] 2페이즈부터 노란 길이 표시된 뒤 돌진하는지, 3페이즈에 슬라임을 부르는지, 큰 공격에 화면이 흔들리는지
- [ ] 아레나 밖으로 도망쳐 5초가 지나면 보스가 처음 상태로 돌아가는지
- [ ] 2인: 둘 다 들어가면 보스 체력이 1.6배가 되는지, 지진이 두 사람 발밑에 각각 떨어지는지, 예고 장판 타이밍이 양쪽에서 맞아 보이는지

**M3b 드랍과 가방**
- [ ] 슬라임이 가끔 작은 마름모(아이템, 등급 색)나 주황 점(룬)을 떨어뜨리는지, 가까이 가면 이름이 보이고 밟으면 왼쪽 아래에 "획득: …"이 뜨는지
- [ ] Tab(또는 I)으로 가방을 열어 능력치 줄, 마우스를 올리면 나오는 설명, [장착], 소켓 있는 아이템(이름 뒤 ○)에 룬 [끼우기], [버리기] 뒤 5초 지나 다시 줍기가 되는지. 가방이 열려 있는 동안 클릭해도 공격하지 않는지
- [ ] 보스를 잡으면 철 단계 희귀 아이템이 떨어지고, 그 뒤로 슬라임이 철 아이템을 떨어뜨리는지
- [ ] 2인: 한쪽이 주운 아이템이 다른 쪽 바닥에서도 사라지는지, 참가한 쪽에서도 가방 명령이 되는지

**결과 보고에 적을 것**: 항목별로 안 된 것, Console의 에러·경고, 어색한 점(움직임이 끊기는지, 반응이 늦은지, 전투가 너무 쉽거나 어려운지, 보스 처치까지 걸린 시간과 어려웠던 공격, 드랍이 너무 많거나 적은지, 가방 창이 불편한지). Game 뷰 스크린샷을 `Docs/Screenshots/m2_solo.png`, `m2_coop.png`, `m3_boss.png`, `m3b_bag.png`로 커밋한다.

### 3. 새 파일 받아서 명령 한 줄 다시 실행 (2026-10-28 추가, 로컬 세션이 바로 할 수 있다)

웹 데모 작업으로 글꼴, WebGL 플러그인, 스크립트가 늘었다. `.meta`를 만들고 PlayMode 테스트가 그대로 통과하는지 본다.

- [x] `git pull` 후 명령 한 줄을 `-Commit`과 함께 실행 (2026-10-28 01:13 성공: EditMode 140/140, PlayMode 2/2)
- [ ] (2026-10-28 18:20 추가) 다시 `git pull` 후 명령 한 줄을 `-Commit`과 함께 실행. 웹 빌드 설정이 `ProjectSetup`으로 옮겨 와서 `ProjectSettings.asset`이 한 번 바뀐다(`webGLDecompressionFallback: 1` 등). `Main.unity`는 이제 이미 있으면 다시 쓰지 않으므로 바뀌지 않아야 한다
- [x] (선택) 에디터 메뉴 **Game > Build Web Demo**로 웹 빌드가 되는지 본다. 결과 `Builds/Web`은 커밋하지 않는다. 같은 Wi-Fi의 휴대폰으로 보려면 `python -m http.server 8000 --directory Builds/Web` 후 `http://<PC 주소>:8000` (2026-10-28 로컬 세션이 배치 모드로 빌드하고 브라우저로 확인. 결과 보고 참고)

### 4. 웹 데모 확인 (사람, 휴대폰)

위 "웹 데모용"을 마치고 첫 배포가 끝난 뒤, 휴대폰 브라우저로 https://learc6910.github.io/pray-to/ 를 가로로 연다. 조작은 `Docs/WebDemo.md`의 터치 조작 표.

- [ ] 로딩이 끝나고 메뉴가 보이는지, 한글이 깨지지 않는지, 글자 크기가 읽을 만한지
- [ ] [혼자 하기], [가방], [나가기] 버튼이 탭에 반응하는지
- [x] 왼쪽 드래그로 움직이고, 오른쪽을 누르고 있으면 그쪽으로 휘두르는지 (2026-10-28 사람: 터치가 잘 된다. 다만 공격 방향이 불편해 아래 결과 보고에 요청을 적었다)
- [ ] 가방 창이 화면에 들어오는지, 장착·버리기 버튼이 눌리는지
- [ ] 끊기지 않고 부드럽게 도는지
- [ ] 결과 보고: 휴대폰 기종과 브라우저, 안 된 항목, 불편한 점. 스크린샷 `Docs/Screenshots/web_phone.png`

### 5. 미니맵과 공격 스틱 받기 (2026-10-28 추가)

19:10 요청을 클라우드가 구현했다(`CLOUD_LOG.md`). 푸시되면 웹 데모가 저절로 다시 배포된다.

- [x] (로컬 세션) `git pull` 후 명령 한 줄을 `-Commit`과 함께 실행. 새 스크립트 `MapView.cs`의 `.meta`가 생겨 커밋된다 (2026-10-28 19:41 성공, `160f732`)
- [x] (클라우드가 확인) 이번 푸시의 Web demo: 첫 시도가 에디터 `Killed`(137)로 끝나 실패한 job만 다시 돌렸고 성공, 19:37 배포
- [ ] (사람, 휴대폰) https://learc6910.github.io/pray-to/ 에서
  - 오른쪽 위 미니맵에 주변이 보이고, 걸어 다니면 가 본 곳이 채워지는지. 흰 점(나)과 빨간 원(보스 아레나 쪽)이 보이는지
  - 미니맵을 탭하면 전체 지도가 크게 열리고, 다시 탭하면 닫히는지. 열려 있는 동안 캐릭터가 공격하지 않는지
  - 오른쪽을 누르면 붉은 스틱이 생기고, 끄는 쪽으로 휘두르는지. 누르기만 하면 걷는 방향으로 휘두르는지
  - [가방]·[나가기]가 미니맵 아래로 옮겨졌고 잘 눌리는지
  - PC 브라우저에서는 M으로 지도가 열리고 닫히는지
- [ ] 결과 보고: 불편한 점(미니맵 크기, 스틱 위치·크기, 지도가 가리는 것)

### 6. 클래스와 스킬 받기 (2026-10-28 추가, M4)

전사와 화염 마법사를 자유롭게 섞어 고르고(지금은 최대 2개), 스킬 넷을 쓴다(`Docs/Classes.md`, `CLOUD_LOG.md`). 푸시되면 웹 데모가 저절로 다시 배포된다.

- [x] (로컬 세션) `git pull` 후 명령 한 줄을 `-Commit`과 함께 실행. 새 스크립트(`ClassSheet.cs`, `World.Skills.cs`, `SkillBar.cs`, `ClassTests.cs`)와 새 폴더 `Data/Classes`(`classes.csv`, `skills.csv`)의 `.meta`가 생기고, `GameSheets.asset`에 두 시트가 들어가 커밋된다. PlayMode 테스트 `EverySkillCanBeUsed`가 새로 돈다 (2026-10-29 00:04 성공, `94a4e64`: EditMode 157/157, PlayMode 3/3)
- [x] (클라우드가 확인) 이번 푸시의 Web demo: 한 번에 성공, 20:33 배포(https://github.com/LeArc6910/pray-to/actions/runs/40679167188 )
- [ ] (사람, PC나 휴대폰) https://learc6910.github.io/pray-to/ 나 에디터에서
  - 메뉴에 클래스가 [1] 전사, [2] 화염 마법사로 골라져 있는지. 눌러서 빼고 다시 넣으면 번호(스킬 칸 순서)가 바뀌는지. 화면이 넓으면 메뉴 오른쪽에, 좁으면 아래에 나오고, 휴대폰 가로 화면에서 잘리면 끌어서 내릴 수 있는지
  - PC는 화면 아래 가운데 Q E R F 칸, 휴대폰은 오른쪽 아래 2×2 버튼. 누르면 스킬이 나가고 칸이 어두워지며 남은 초가 줄어드는지. PC에서 칸에 마우스를 올리면 설명이 뜨는지
  - 회전베기는 둘레에 고리, 화염구는 주황 불덩이가 날아가 터지고, 전투 함성·불꽃 부여는 내 둘레에 색 고리가 도는지. 불꽃 부여를 켠 뒤 회전베기가 주황으로 보이는지
  - 휴대폰: 왼손으로 걸으면서 오른손으로 버튼을 누를 수 있는지. 공격 스틱과 버튼이 서로 방해하지 않는지
  - 가방 창 능력치 아래에 "클래스 전사 + 화염 마법사 · 스킬 피해 …"가 나오고, 불꽃 부여를 켜면 숫자가 오르는지
  - 보스가 클래스 없이 할 때보다 얼마나 쉬운지. 봇으로는 혼자 처치가 82~133초에서 38~55초로 줄었다. 너무 쉬우면 알려 준다
- [ ] 결과 보고: 스킬 손맛, 버튼 위치·크기, 너무 세거나 약한 스킬, 더 있었으면 하는 클래스

### 7. 쌓인 기능 플레이 확인 (2026-10-29 시작, 사람)

사람이 "기능이 어느 정도 쌓이면 테스트한다"고 했다(2026-10-29). 클라우드는 단계마다 여기에 확인할 것을 덧붙이고, 새 파일이 생긴 단계만 로컬 세션에 명령 한 줄을 부탁한다. 6번의 남은 사람 확인도 이때 같이 한다. PC와 휴대폰 둘 다에서 본다(`Docs/Design.md` 기술 원칙 7).

- [ ] 1단계: 작은 수정
  - PC: Q, E, R, F를 거의 동시에 눌러도 누른 스킬이 모두 나가는지
  - HUD 상자가 줄 수만큼 늘어나서 조작 안내가 잘리지 않는지(800×600쯤 창, 휴대폰 세로)
- [ ] 2단계: 활·지팡이 원거리 기본 공격(`Docs/Combat.md`)
  - 몬스터나 보스가 떨어뜨린 활·지팡이를 가방에서 장착하면, 공격할 때 휘두르지 않고 화살(가는 막대)·마법 탄(공)이 바라보는 쪽으로 날아가는지. 마우스 쪽, 휴대폰은 공격 스틱을 끄는 쪽
  - 화살은 다른 플레이어를 지나가고 처음 맞은 적에서 멈추는지, 벽에서 사라지는지
  - 불꽃 부여를 켜면 화살·마법 탄이 주황으로 바뀌는지
  - 가방 창 능력치 줄이 "화살 피해 …"처럼 기본 공격 이름으로 시작하는지
  - 손맛: 활과 지팡이가 검보다 쓸 만한지. 지팡이는 무기 피해를 활만큼 올리고 마법 피해 증가를 크게 한 대신 공격 속도를 15% 낮췄다(`Docs/Loot.md`)
- [ ] 3단계: 회복(`Docs/Combat.md` 회복 절)
  - (로컬 세션) 새 시트 `Data/Loot/consumables.csv`의 `.meta`와 `GameSheets.asset`의 소모품 칸 때문에 명령 한 줄을 `-Commit`과 함께 한 번 돌린다 → 2026-10-29 01:22 했다(`00b3c35`)
  - 맞은 뒤 6초쯤 지나면 생명력이 천천히 차는지
  - 시작할 때 물약 3개가 있고, PC는 Q E R F 옆 C 칸, 휴대폰은 스킬 버튼 왼쪽 아래 물약 버튼에 "회복 물약 ×3"이 보이는지. 맞은 뒤 누르면 초록 "+35"가 뜨고 2초에 걸쳐 차며 칸이 10초 동안 어두워지는지. 생명력이 가득하면 눌러도 줄지 않는지
  - 가방 창 맨 위 "소모품"에서 [마시기]로도 마실 수 있는지
  - 슬라임이 가끔 분홍 점(물약)을 떨어뜨리고 주우면 "획득: 회복 물약"이 뜨는지
- [ ] 4단계: 채굴·건설·제작(`Docs/Crafting.md`)
  - (로컬 세션) 새 파일(`Data/World` 폴더와 `tiles.csv`, `Data/Loot/materials.csv`, `recipes.csv`, `World.Mining.cs`, `TileSheet.cs`, `CraftingTests.cs`)의 `.meta`와 `GameSheets.asset`의 새 칸 때문에 명령 한 줄을 `-Commit`과 함께 한 번 돌린다(3단계와 같이 해도 된다) → 2026-10-29 01:22 했다(`00b3c35`)
  - 벽에 붙어 벽 쪽을 보고 공격을 누르고 있으면 먼지가 일고, 돌벽은 3번, 구리 광맥(주황 얼룩)은 5번 만에 부서지는지. 부서진 자리가 바닥이 되어 걸어갈 수 있고 미니맵에도 바뀌는지. "획득: 돌 ×1"이 뜨는지
  - 활·지팡이를 들고 벽에 붙어 공격하면 쏘지 않고 캐는지. 휴대폰은 공격 스틱을 벽 쪽으로 끌어 누르고 있으면 되는지
  - 가방 창의 "재료"에서 돌의 [놓기]를 누르면 바라보는 칸에 돌벽이 생기는지. 사람이나 몬스터가 있는 칸, 보스 아레나 안에는 안 생기는지
  - 가방 창의 "제작" 목록: 재료가 모이면 [만들기]가 눌리고 구리 장비·회복 물약이 만들어지는지. 철 장비는 보스의 "수호자의 핵"이 있어야 하는지
  - 철 광맥(푸른 회색 얼룩)이 시작 지점에서 먼 곳에 있는지
  - 손맛: 캐는 속도, 재료 모으는 데 걸리는 시간, 제작 목록 보기가 휴대폰에서 괜찮은지
- [ ] 5단계: 농사(`Docs/Farming.md`)
  - (로컬 세션) 새 파일(`crops.csv`, `World.Farming.cs`, `FarmingTests.cs`)의 `.meta`와 `GameSheets.asset`의 작물 칸 때문에 명령 한 줄을 `-Commit`과 함께 한 번 돌린다(3·4단계와 같이 해도 된다) → 2026-10-29 01:22 했다(`00b3c35`)
  - 시작할 때 가방 재료에 "버섯 포자 ×2"가 있고, [심기]를 누르면 바라보는 바닥 칸에 초록 점이 생기는지. 벽이나 이미 심은 칸에는 안 심어지는지
  - 3분 동안 점이 세 번 커지며 색이 바뀌고, 다 자라면 깜박이는지. 그 위를 걸어가면 "획득: 동굴 버섯 ×2"와 포자가 들어오는지
  - 제작 목록에서 동굴 버섯 2개로 "버섯 구이"를 만들고 C나 물약 버튼으로 먹을 수 있는지(물약이 남아 있으면 물약이 먼저 쓰인다. 가방 창의 [마시기]로는 골라 먹는다)
  - 돌벽을 캐다 보면 가끔 포자가 나오는지

## 결과 보고

명령 한 줄이 실행할 때마다 아래에 결과를 덧붙인다. 플레이 확인 결과는 사람이(또는 로컬 세션이) 날짜와 함께 적는다.

### 2026-10-27 23:31 setup.ps1 (Unity 6000.6.3f1)

- 프로젝트 설정: **실패** (종료 코드 198). Logs/setup.log에서 뽑은 줄:
  - (스크립트의 패턴에 걸린 줄이 없어 비어 있었다. 아래는 로컬 세션이 로그에서 직접 옮긴 줄)
  - `[Licensing::Module] Error: Access token is unavailable; failed to update`
  - `[Licensing::Client] Error: Code 404 while processing request (status: Found 0 entitlement groups and 0 free entitlements matching requested entitlement ids)`
  - `[Licensing::Module] Error: 'com.unity.editor.headless' was not found.`
  - `Exiting without the bug reporter. Application will exit with return code 198`
- 원인(로컬 판단): Unity 라이선스 문제로 프로젝트를 열기 전에 끝났다. 코드 문제가 아니다. Unity Hub 로그인이 풀려 배치 모드에 쓸 라이선스(`com.unity.editor.headless`)를 받지 못한 것으로 보인다. 사람이 Unity Hub에 로그인하고 Personal 라이선스를 확인한 뒤 다시 실행한다.
- 클라우드에 제안: `Get-LogErrors`(`setup.ps1`)와 `setup.sh`의 패턴에 `Licensing::.*Error`를 넣으면 이런 실패가 보고에 바로 잡힌다.

### 2026-10-27 23:34 setup.ps1 (Unity 6000.6.3f1)

- 프로젝트 설정: **실패** (종료 코드 1). Logs/setup.log에서 뽑은 줄:
  - `Assets\_Project\Scripts\Client\WorldView.cs(522,50): error CS0246: The type or namespace name 'Light2D' could not be found (are you missing a using directive or an assembly reference?)`
  - `Assets\_Project\Scripts\Client\WorldView.cs(523,31): error CS0103: The name 'Light2D' does not exist in the current context`
  - `Scripts have compiler errors.`
- 라이선스 문제는 Unity Hub 로그인으로 풀렸다. 이번에는 프로젝트를 열고 컴파일하다 멈췄다. 테스트는 돌지 않았다.
- 원인(로컬 판단): URP 17.6.0에서 `Light2D`, `Renderer2DData` 같은 2D 렌더러 타입은 네임스페이스는 그대로 `UnityEngine.Rendering.Universal`이지만 별도 어셈블리 `Unity.RenderPipelines.Universal.2D.Runtime`(패키지 안 `Runtime/2D/Unity.RenderPipelines.Universal.2D.Runtime.asmdef`)에 들어 있다. `Game.Client.asmdef`와 `Game.Client.Editor.asmdef`는 `Unity.RenderPipelines.Universal.Runtime`만 참조한다. `WorldView.cs`가 `Light2D`를, `Editor/ProjectSetup.cs`가 2D 렌더러 타입을 쓰므로 두 asmdef 모두에 `Unity.RenderPipelines.Universal.2D.Runtime` 참조가 필요해 보인다(Editor 쪽은 Game.Client가 먼저 실패해 아직 에러가 나오지 않았다). `UnityCompileCheck`의 스텁도 이 타입들을 같은 어셈블리로 나눠야 CI에서 잡힌다.
- 패키지: com.unity.netcode.gameobjects 2.13.3, com.unity.transport 6.6.0, com.unity.render-pipelines.universal 17.6.0, com.unity.inputsystem 1.20.0, com.unity.multiplayer.playmode 3.0.0, com.unity.test-framework 1.8.0

### 2026-10-28 00:42 setup.ps1 (Unity 6000.6.3f1)

- 프로젝트 설정: 성공
- 패키지: com.unity.netcode.gameobjects 2.13.3, com.unity.transport 6.6.0, com.unity.render-pipelines.universal 17.6.0, com.unity.inputsystem 1.20.0, com.unity.multiplayer.playmode 3.0.0, com.unity.test-framework 1.8.0
- EditMode 테스트: 140개 중 140개 통과, 0개 실패
- PlayMode 테스트: 2개 중 2개 통과, 0개 실패

### 2026-10-28 웹 데모 상태 확인 (로컬 세션)

사용자가 2번 플레이 확인을 브라우저에서 할 수 있는지 물어서 확인했다. 아직은 할 수 없다.

- 웹 빌드 워크플로가 없다. `.github/workflows`에는 `core-tests.yml`만 있다
- "웹 데모용" 사람 작업(시크릿 3개, Pages 소스 설정)은 아직 안 했다. `gh api repos/LeArc6910/pray-to/pages`가 404(Pages 꺼짐)
- 저장소가 private이다. GitHub Free 플랜에서는 private 저장소에 Pages를 쓸 수 없고 유료 플랜이 필요하다. Pages로 올린 페이지는 저장소가 private이어도 링크를 아는 누구나 볼 수 있다. 계정 플랜은 `gh`로 조회되지 않아 확인하지 못했다. 웹 데모를 붙이기 전에 사용자와 정해야 한다(유료 플랜, 저장소 공개 전환, 또는 다른 배포처)
- 이 PC의 Unity 6000.6.3f1에는 Web Build Support(`PlaybackEngines/WebGLSupport`)가 설치되어 있다. 로컬에서 웹 빌드를 만드는 에디터 메뉴가 생기면 배치 모드로 빌드해 로컬 서버로 띄워 볼 수 있다
- `Docs/Design.md` 6절대로, 웹에서는 혼자 하기만 되고(브라우저는 호스트 불가) 한글 폰트와 터치 조작이 더 필요하다. 그래서 이번 2번 확인은 에디터에서 하도록 안내했다

### 2026-10-28 01:13 setup.ps1 (Unity 6000.6.3f1)

- 프로젝트 설정: 성공
- 패키지: com.unity.netcode.gameobjects 2.13.3, com.unity.transport 6.6.0, com.unity.render-pipelines.universal 17.6.0, com.unity.inputsystem 1.20.0, com.unity.multiplayer.playmode 3.0.0, com.unity.test-framework 1.8.0
- EditMode 테스트: 140개 중 140개 통과, 0개 실패
- PlayMode 테스트: 2개 중 2개 통과, 0개 실패

### 2026-10-28 01:20 웹 빌드 로컬 확인 (로컬 세션)

3번 선택 항목. 에디터 메뉴 대신 배치 모드로 같은 메서드를 불렀다: `Unity.exe -batchmode -buildTarget WebGL -executeMethod Game.Client.Editor.WebBuild.RunBatch` (로그 `Logs/webbuild.log`, 커밋하지 않음).

- 빌드: **성공** (`Build Finished, Result: Success`, 종료 코드 0). `Builds/Web` 약 15.5 MB (`Web.wasm.unityweb` 10.4 MB, `Web.data.unityweb` 5.4 MB)
- `python -m http.server`로 띄워 데스크톱 Chromium 계열 브라우저(Claude 앱 내장 브라우저, 800×450 창)에서 열었다
  - 로딩 후 메뉴가 뜬다. 한글(제목, 이름 칸, [혼자 하기], 안내문 "웹 데모에서는 혼자 하기만 됩니다…")이 깨지지 않는다. 호스트·참가 버튼은 숨겨져 있다
  - [혼자 하기] 클릭으로 게임에 들어가고, 동굴 지형·플레이어·HUD(생명력, 보스 아레나 방향, 조작 안내)가 한글로 보인다. 스크린샷 `Docs/Screenshots/web_local_solo.jpg`
  - Tab으로 가방 창이 열린다. 능력치 줄, 착용 칸(구리 검 [벗기]), 소지품 0/24가 보인다. 스크린샷 `Docs/Screenshots/web_local_bag.jpg`
  - 브라우저 콘솔에 에러 없음. WebGL 2.0 컨텍스트로 돈다
- 눈에 띈 점(클라우드 참고)
  - 가방을 열면 위쪽 안내 상자 "아이템 이름에 마우스를 올리면 설명이 보입니다"가 왼쪽 위 HUD 상자와 겹쳐 "보스 아레나: 서쪽 38칸" 줄을 가린다
  - 가방 창 배경이 반투명이라 뒤의 플레이어 이름표("플레이어")가 착용 칸 글자("갑옷") 위로 비친다
- 확인하지 못한 것: 이동·공격. 자동 입력으로 짧게 누르는 키는 이동으로 이어지지 않았는데, 키를 누르고 있는 입력을 흉내 내지 못해서인지 게임 문제인지는 가리지 못했다. 사람 플레이(2번)와 휴대폰 확인(4번)에서 본다
- 빌드 부작용(클라우드 참고)
  - 빌드 뒤 이 파일들이 바뀌어 있었다. 커밋하지 않고 되돌렸다: `ProjectSettings/ProjectSettings.asset`(`webGLDecompressionFallback: 0 → 1`), `Assets/_Project/Settings/URP-2D.asset`(셰이더 프리필터 설정 `m_Prefiltering*`), `Assets/UniversalRenderPipelineGlobalSettings.asset`, `Assets/DefaultVolumeProfile.asset`(볼륨 컴포넌트 기본값 약 780줄 추가), `Assets/_Project/Scenes/Main.unity`. 웹 설정을 저장소에 남기려면 `ProjectSetup.SetUp`에서 정하는 편이 낫다
  - Burst가 저장소 루트에 `Data/Plugins/lib_burst_generated.{cpp,wasm}`을 만들었다. 명령 한 줄의 `git add -A`에 딸려 들어가지 않도록 지웠다. `.gitignore`에 `/Data/`를 넣어 주면 좋겠다
  - `ProjectSetup.SetUp`이 돌 때마다 `Main.unity`를 새로 써서 내용이 같아도 오브젝트 fileID가 바뀐다(이번 setup 커밋 `eeef9a0`에서도 약 100줄 바뀜). 커밋 기록이 시끄러워진다
  - 빌드 대상이 WebGL로 바뀐 채 남지 않도록 `-buildTarget Win64`로 되돌려 두었다

### 2026-10-28 01:30 공개 전 커밋 기록 정리 (로컬 세션, 사용자 요청)

- 사용자 요청으로 저장소를 public으로 바꾸기 전에 기록을 확인했다. 키·비밀번호·라이선스 파일은 없었다. 다만 클라우드가 만든 커밋 12개의 작성자가 저장소 주인이 아니었고, 본문 끝에 `Co-Authored-By`와 세션 링크 줄이 붙어 있었다. 사용자는 GitHub에 이런 표시를 남기지 않기로 했으므로, 사용자 승인을 받아 기록을 다시 썼다
- 모든 커밋의 작성자를 `LeArc6910 <270705266+LeArc6910@users.noreply.github.com>`으로 바꾸고 그 두 줄을 지웠다. 파일 내용(트리)은 그대로다. main을 강제 푸시했다(`d0814ab` → `104032c`). 커밋 해시가 전부 바뀌었으므로 위 보고와 `CLOUD_LOG.md`에 적힌 해시는 이제 맞지 않는다. 제목으로 찾는다
- **클라우드가 할 일**
  - 작업 전에 `git fetch` 후 `git reset --hard origin/main`으로 새 기록에 맞춘다. 푸시하지 않은 커밋이 있으면 `git rebase --onto origin/main <옛 origin/main> <브랜치>`로 옮긴다. 옛 기록을 merge하거나 강제 푸시해서 되살리지 않는다
  - 앞으로 커밋은 `git config user.name LeArc6910`, `git config user.email 270705266+LeArc6910@users.noreply.github.com`으로 만들고, 메시지에 `Co-Authored-By`·세션 링크 줄을 붙이지 않는다. 이 규칙을 `CLAUDE.md`의 규칙 절에 적어 두면 좋겠다

### 2026-10-28 01:40 공개 전환과 Pages 설정 (로컬 세션, 사용자 승인)

- 사람이 옛 커밋에 묶여 있던 Actions 실행 기록 13개를 지웠다. 확인 결과 남은 실행 12개 중 옛 작성자나 도구 표시 줄이 붙은 것은 없다. 원격 main의 커밋 22개도 모두 `LeArc6910` 작성이고 그런 줄이 없다
- 저장소를 public으로 바꿨다(`gh repo edit --visibility public`)
- GitHub Pages를 켜고 소스를 GitHub Actions로 정했다(`build_type: workflow`). 주소 https://learc6910.github.io/pray-to/ , HTTPS 강제
- 남은 사람 작업: Unity 라이선스 시크릿 3개, 그 뒤 Actions 탭에서 Web demo 첫 실행. 시크릿이 들어가기 전에는 워크플로가 빌드를 건너뛴다

### 2026-10-28 01:52 웹 데모 첫 빌드 실패: Unity 라이선스 활성화 (로컬 세션)

- Web demo 워크플로를 손으로 돌렸다: https://github.com/LeArc6910/pray-to/actions/runs/40543089823 . `check` 성공(시크릿 3개 있음), `build` 실패, `deploy` 건너뜀. 프로젝트를 열기 전, 라이선스 활성화 단계에서 5번 재시도 끝에 멈췄다(약 12분)
- 로그에서 뽑은 줄:
  - `UnityConnectLoginRequest: Failed to login - please check your username or password`
  - `No sufficient permissions while processing request "https://core.cloud.unity3d.com/api/login", HTTP error code 401`
  - `[Licensing::Module] Error: Access token is unavailable; failed to update`
  - `[Licensing::Module] Error: Failed to activate entitlement license`
  - `[Licensing::Module] Error: Failed to activate ULF license`
  - `##[error]There was an error while trying to activate the Unity license.`
- 원인(로컬 판단): 먼저 `UNITY_EMAIL`·`UNITY_PASSWORD`로 Unity 계정 로그인이 401로 거절됐다. 로그인 토큰이 없어서 그 뒤 라이선스 활성화도 실패한 것으로 보인다. 흔한 원인은 비밀번호 오타·앞뒤 공백, Google·Apple 같은 소셜 로그인으로 만든 계정이라 비밀번호가 없는 경우, 2단계 인증이다
- 두 번째 의심: `UNITY_LICENSE`. 이 PC에는 문서에 적힌 `C:\ProgramData\Unity\Unity_lic.ulf`가 없다. Unity 6 Hub는 `%LOCALAPPDATA%\Unity\licenses\UnityEntitlementLicense.xml`만 남기는데, 이것은 ULF 형식이 아니다. 로그인을 고친 뒤에도 `Failed to activate ULF license`가 남으면 이쪽을 본다(클라우드: GameCI가 Unity 6 Personal에 요구하는 형식 확인 부탁)
- 사람이 할 일: Unity 계정이 이메일·비밀번호로 로그인되는지 https://id.unity.com 에서 확인. 소셜 로그인 계정이면 비밀번호를 만들고, 2단계 인증이 켜져 있으면 확인한다. 그 뒤 `UNITY_EMAIL`·`UNITY_PASSWORD`를 다시 넣는다

### 2026-10-28 02:12 웹 데모 두 번째 빌드 실패: 시리얼 오류 (로컬 세션)

- 사람이 Unity 계정을 확인한 뒤 다시 돌렸다: https://github.com/LeArc6910/pray-to/actions/runs/40546236855 . 이번에도 `build`가 라이선스 활성화에서 멈췄다(5번 재시도, 약 12분). 저장소 시크릿의 수정 시각은 첫 입력(01:39) 그대로라 계정 쪽 조치로 로그인이 풀린 것으로 보인다
- 첫 번째와 달리 로그인과 entitlement 활성화는 성공했다. 그 다음 시리얼 확인에서 실패한다:
  - `[UnityConnect::TryLogin] Request Succeeded.`
  - `[Licensing::Module] Successfully activated the entitlement license`
  - `[Licensing::Client] Error: Code 20110 while processing request (status: serial invalid)`
  - `[Licensing::Module] License activation has failed. Aborting.`
- 원인(로컬 판단): GameCI는 Personal 라이선스일 때 `UNITY_LICENSE`(ULF 파일)의 `DeveloperData`에서 시리얼을 꺼내 넘긴다. 이 PC에는 ULF 파일이 없고 Unity 6 Hub가 남기는 `UnityEntitlementLicense.xml`은 ULF가 아니어서, 넘어간 시리얼이 잘못된 것으로 보인다. entitlement는 이미 활성화됐으므로 시리얼 없이 활성화하면 될 가능성이 있다
- **클라우드에 부탁**: `web-demo.yml`의 라이선스 활성화를 Unity 6 Personal(entitlement, ULF 없음)에 맞게 고쳐 주세요. 예: `unity-builder`의 `skipActivation`을 켜고 이메일·비밀번호로만 활성화하는 단계를 따로 두는 방법. GameCI가 Unity 6 Personal을 어떻게 지원하는지 확인이 필요하다
- 사용자 결정(02:20): 로컬 빌드를 gh-pages로 임시 배포하지 않고 클라우드의 워크플로 수정을 기다린다. 고쳐지면 로컬 세션이 Web demo를 다시 돌리고 배포를 확인한다

### 2026-10-28 18:59 setup.ps1 (Unity 6000.6.3f1)

- 프로젝트 설정: 성공
- 패키지: com.unity.netcode.gameobjects 2.13.3, com.unity.transport 6.6.0, com.unity.render-pipelines.universal 17.6.0, com.unity.inputsystem 1.20.0, com.unity.multiplayer.playmode 3.0.0, com.unity.test-framework 1.8.0
- EditMode 테스트: 140개 중 140개 통과, 0개 실패
- PlayMode 테스트: 2개 중 2개 통과, 0개 실패

### 2026-10-28 19:00 클라우드 18:18 작업 확인 (로컬 세션)

- `git pull`로 클라우드 커밋 3개를 받았다. 모두 `LeArc6910` noreply 주소로 작성됐고 도구 표시 줄이 없다. 새 기록 위로 fast-forward 됐다
- 명령 한 줄(위 18:59): 성공. 이번 커밋에서 `ProjectSettings.asset`은 `webGLDecompressionFallback: 0 → 1` 한 줄만 바뀌었고, `Main.unity`는 다시 쓰이지 않았다(씬 재생성 수정 확인)
- 배포된 웹 데모 https://learc6910.github.io/pray-to/ 를 데스크톱 브라우저(800×450 창)에서 열었다. 로딩, 메뉴, 한글, [혼자 하기], Tab 가방 창이 되고 콘솔 에러가 없다. 지난번에 적은 두 가지가 고쳐졌다: 설명 상자가 HUD 아래로 내려가 겹치지 않고, 가방 창 배경이 불투명하다. 스크린샷 `Docs/Screenshots/web_pages_bag.jpg`
- 사소한 점: 설명 상자 폭이 좁아 "…설명이 보입니다."의 마지막 글자 "다."가 다음 줄로 넘어간다
- `UNITY_LICENSE` 시크릿은 이제 쓰지 않는다. 지울지는 사람이 정한다

### 2026-10-28 19:10 휴대폰 웹 데모 첫 느낌과 기능 요청 (사람 → 로컬 세션이 옮겨 적음)

사람이 휴대폰으로 웹 데모를 해 봤다. 휴대폰 기종·브라우저와 나머지 4번 항목은 아직 받지 못했다.

- 터치 조작은 잘 된다
- **요청 1: 미니맵.** 화면 오른쪽 위에 미니맵을 둔다. 미니맵을 탭(또는 클릭)하면 맵을 크게 볼 수 있게 한다
- **요청 2: 오른쪽 공격 조이스틱.** 지금은 오른쪽을 누른 지점이 캐릭터 기준 어느 쪽인지로 공격 방향이 정해져서, 캐릭터 위치를 보면서 눌러야 해 불편하다. 왼쪽 이동처럼 오른쪽에도 원형 터치 스틱을 두어 끄는 방향으로 공격 방향을 정하게 해 달라

**클라우드에 부탁** (코드 작업이라 클라우드 담당. 로컬은 푸시되면 명령 한 줄과 웹 데모 확인을 한다)

- 미니맵
  - 오른쪽 위에는 지금 [가방]·[나가기] 버튼이 있으니 배치를 다시 잡아야 한다
  - 탭·클릭으로 크게 보기를 열고, 다시 탭하거나 닫기 버튼(PC는 Esc나 같은 키)으로 닫는다. 터치와 마우스 모두 된다
  - 보여 줄 것은 클라우드가 정하고 `Docs/Design.md`에 적는다(예: 벽·바닥, 내 위치, 보스 아레나. 가 본 곳만 보일지 전부 보일지)
  - 크게 보기가 열려 있는 동안 그 터치가 이동·공격으로 새지 않게 한다(가방 창과 같은 처리)
- 공격 스틱
  - 오른쪽 절반을 누르면 그 자리에 왼쪽과 같은 모양의 원형 스틱이 생기고, 스틱을 끄는 방향을 바라보며 누르고 있는 동안 계속 휘두른다
  - 누르기만 하고 거의 끌지 않았을 때 방향을 어떻게 할지(마지막 방향 유지 등)는 클라우드가 정한다
  - PC의 마우스 조준은 그대로 둔다
- `Docs/WebDemo.md`의 터치 조작 표도 같이 고친다

### 2026-10-28 19:41 setup.ps1 (Unity 6000.6.3f1)

- 프로젝트 설정: 성공
- 패키지: com.unity.netcode.gameobjects 2.13.3, com.unity.transport 6.6.0, com.unity.render-pipelines.universal 17.6.0, com.unity.inputsystem 1.20.0, com.unity.multiplayer.playmode 3.0.0, com.unity.test-framework 1.8.0
- EditMode 테스트: 140개 중 140개 통과, 0개 실패
- PlayMode 테스트: 2개 중 2개 통과, 0개 실패

### 2026-10-28 19:50 미니맵·공격 스틱 받기 확인 (로컬 세션)

- `git pull`로 클라우드 커밋 2개를 받았다. 작성자는 `LeArc6910`, 도구 표시 줄 없음, fast-forward
- 명령 한 줄(위 19:41): 성공. `MapView.cs.meta`만 새로 생겨 커밋됐다
- 배포된 웹 데모를 데스크톱 브라우저(1100×720 창, 마우스·키보드)에서 확인했다
  - 오른쪽 위 미니맵에 시작 지점 주변의 가 본 곳과 흰 점이 보인다. HUD 안내에 "M 지도"가 들어갔다. 스크린샷 `Docs/Screenshots/web_minimap.jpg`
  - M으로 전체 지도가 열린다. 제목 "지도 (클릭하거나 M·Esc로 닫기)", 아래 범례(나, 다른 플레이어, 보스 아레나), 가 보지 않은 보스 아레나의 빨간 원이 보인다. 스크린샷 `Docs/Screenshots/web_fullmap.jpg`
  - Esc로 닫힌다. 미니맵 클릭으로 열리고, 지도 아무 데나 클릭하면 닫힌다
  - 브라우저 콘솔에 에러 없음
- 확인하지 못한 것: 공격 스틱, 터치로 지도 열고 닫기, 미니맵 아래 [가방]·[나가기]. 마우스로는 터치 UI가 나오지 않아 휴대폰 확인(사람)으로 남긴다
- 브라우저 창이 좁고 길면(약 750×1200) 웹 페이지의 게임 화면이 창보다 넓어 오른쪽이 잘리고 가로 스크롤이 생겼다. 미니맵이 잘린 쪽에 있어 안 보였다. 휴대폰 세로 화면에서도 같은지는 사람이 본다
- 사람(19:55): 휴대폰에서는 화면이 잘리지 않는다. 잘림은 데스크톱의 좁고 긴 브라우저 창에서만 본 것이다

### 2026-10-29 00:04 setup.ps1 (Unity 6000.6.3f1)

- 프로젝트 설정: 성공
- 패키지: com.unity.netcode.gameobjects 2.13.3, com.unity.transport 6.6.0, com.unity.render-pipelines.universal 17.6.0, com.unity.inputsystem 1.20.0, com.unity.multiplayer.playmode 3.0.0, com.unity.test-framework 1.8.0
- EditMode 테스트: 157개 중 157개 통과, 0개 실패
- PlayMode 테스트: 3개 중 3개 통과, 0개 실패

### 2026-10-29 00:15 M4 클래스·스킬 받기 확인 (로컬 세션)

- `git pull`로 클라우드 커밋 2개를 받았다. 작성자 `LeArc6910`, 도구 표시 줄 없음, fast-forward
- 명령 한 줄(위 00:04): 성공. 새 `.meta` 7개와 `GameSheets.asset`(시트 두 개 추가)이 커밋됐다. 새 PlayMode 테스트 `EverySkillCanBeUsed`를 포함해 3개 모두 통과
- 배포된 웹 데모를 데스크톱 브라우저(마우스·키보드)에서 확인했다. 콘솔 에러 없음
  - 메뉴 제목이 "M4 프로토타입"이고 오른쪽에 클래스가 [1] 전사, [2] 화염 마법사로 골라져 있다. 전사를 눌러 빼면 "[ ] 전사, [1] 화염 마법사", 다시 넣으면 "[2] 전사, [1] 화염 마법사"로 번호가 바뀐다. 스크린샷 `Docs/Screenshots/web_m4_menu.jpg`
  - 그 순서로 들어가니 생명력 120/120(전사 패시브 +20%)이고, 아래 가운데 칸이 Q 화염구, E 불꽃 부여, R 회전베기, F 전투 함성으로 고른 순서를 따른다
  - 네 스킬 모두 나간다. 불꽃 부여는 주황 고리와 이름이 뜨고 칸에 남은 초(16)가 줄어든다. 화염구는 주황 불덩이가 날아간다. 회전베기, 전투 함성도 칸이 어두워지고 초가 줄어든다. 스크린샷 `Docs/Screenshots/web_m4_fireball.jpg`, `web_m4_skills.jpg`
- 눈에 띈 점(클라우드 참고)
  - E, F, R을 아주 빠르게 이어 누르면 E만 나가고 F, R은 나가지 않았다(재사용 대기 표시도 없음). 따로 누르면 모두 나간다. `Controls.cs` 126행 근처가 한 프레임에 눌린 키 중 첫 번째 칸만 돌려주므로 같은 프레임에 들어온 나머지는 버려지는 것으로 보인다. 자동 입력이라 한 프레임에 몰렸을 가능성이 크지만, 사람이 두 키를 거의 동시에 눌러도 생길 수 있다
  - HUD 조작 안내 둘째 줄이 상자 폭을 넘어 "… M 지도 · Esc 메"에서 잘린다(800×600 정도 창)
- 확인하지 못한 것: 휴대폰 2×2 스킬 버튼, 적·보스에게 스킬 피해가 들어가는지, 불꽃 부여 뒤 회전베기 색. 사람 확인으로 남긴다

### 2026-10-29 01:22 setup.ps1 (Unity 6000.6.3f1)

- 프로젝트 설정: 성공
- 패키지: com.unity.netcode.gameobjects 2.13.3, com.unity.transport 6.6.0, com.unity.render-pipelines.universal 17.6.0, com.unity.inputsystem 1.20.0, com.unity.multiplayer.playmode 3.0.0, com.unity.test-framework 1.8.0
- EditMode 테스트: 192개 중 192개 통과, 0개 실패
- PlayMode 테스트: 3개 중 3개 통과, 0개 실패

### 2026-10-29 01:35 클라우드 00:27~01:03 작업 받기 확인 (로컬 세션)

- `git pull`로 클라우드 커밋 7개를 받았다(스킬 키·HUD 수정, 원거리 기본 공격, 회복, 채굴·건설·제작, 웹 빌드 재시작, 농사, 배포 기록). 모두 `LeArc6910` 작성, 도구 표시 줄 없음, fast-forward. `CLOUD_LOG.md`의 로컬 확인 칸 6개를 체크했다
- 명령 한 줄(위 01:22): 성공. 3·4·5단계의 새 `.meta` 13개와 `GameSheets.asset`(소모품·재료·조합법·칸·작물 시트)이 한 번에 커밋됐다. EditMode 192/192, PlayMode 3/3
- 배포된 웹 데모를 데스크톱 브라우저(800×600쯤, 마우스·키보드)에서 확인했다. 콘솔 에러 없음
  - 1단계: Q E R F를 한 번에 몰아 눌러도 네 칸 모두 재사용 대기가 걸린다(지난번엔 하나만 나갔다). HUD 상자가 줄 수만큼 늘어 "(벽이면 캔다)", "Esc 메뉴"까지 잘리지 않는다. 스크린샷 `Docs/Screenshots/web_m5_skills_potion.jpg`
  - 3단계: Q E R F 옆 C 칸에 "회복 물약 ×3"이 있다. 가방 맨 위 "소모품"에 회복 물약 ×3과 [마시기]가 있다
  - 4단계: 시작 지점 가까이에 주황 얼룩 벽(구리 광맥)이 보인다. 가방 "제작" 목록에 구리·철 장비, 회복 물약, 버섯 구이가 있고, 재료가 없어 [만들기]가 흐리다. 철 장비는 "수호자의 핵 0/1"이 필요하다고 나온다. 스크린샷 `Docs/Screenshots/web_m5_crafting.jpg`
  - 5단계: 가방 "재료"에 "버섯 포자 ×2"와 [심기]가 있고, 누르면 캐릭터 옆 바닥 칸에 초록 점이 생긴다. 스크린샷 `Docs/Screenshots/web_m5_planted.jpg`
- 눈에 띈 점(클라우드 참고)
  - 메뉴 제목이 아직 "pray-to (M4 프로토타입)"이다. 지금 웹 데모에는 M5까지 들어 있다
  - 가방 창 아래쪽의 "아이템 이름에 마우스를 올리면 설명이 보입니다." 안내가 목록 위에 겹쳐서, 끝까지 내려도 마지막 줄(버섯 구이와 그 재료 "동굴 버섯 0/2")이 가려진다. 중간 위치에서는 "재료"와 "버섯 포자 ×2 [심기]" 줄도 가린다
  - 가방 창을 마우스 휠로 내리면 아주 느리다(휠 20칸에 몇 줄). 스크롤 막대를 끌면 된다. 목록이 길어져 PC에서 휠로 내리기 불편하다
- 확인하지 못한 것: 원거리 무기(드랍 필요), 쉬면 차는 생명력과 물약 마시기(맞아야 함), 채굴·돌벽 놓기·제작(벽까지 걸어가야 하는데 자동 입력으로는 키를 누르고 있을 수 없다), 버섯이 3분 동안 자라 거두기, 휴대폰. 7번의 사람 확인으로 남긴다
