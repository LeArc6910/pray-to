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
4. EditMode 테스트(Core 규칙)와 PlayMode 테스트(실제 클라이언트로 혼자 하기와 호스트를 5초씩 돌려 에러가 없는지)를 돌린다
5. 결과를 이 파일의 "결과 보고"에 적고, 생성된 파일과 함께 커밋·푸시한다

옵션: `-InstallEditor`(`--install-editor`)는 Unity가 없으면 Unity Hub로 설치한다(Web Build Support 포함). `-SkipTests`(`--skip-tests`), `-NoPull`(`--no-pull`). `-Commit`을 빼면 결과만 적고 커밋하지 않는다. Unity가 Hub 기본 위치가 아닌 곳에 있으면 환경 변수 `UNITY_EXE`에 실행 파일 경로를 넣는다. 로그는 `Logs/`에 남는다(커밋하지 않는다).

처음 실행은 패키지를 받고 임포트하느라 10분쯤 걸린다. 같은 메뉴는 에디터 안에서도 **Game > Set Up Project**로 실행할 수 있다.

## 사람이 직접 해야 하는 일

계정, 라이선스, 저장소 설정, 그리고 직접 해 보는 느낌은 Claude가 대신할 수 없다.

### 처음 한 번

- [ ] **Unity Hub** 설치(https://unity.com/download), Unity 계정으로 로그인, Preferences > Licenses에서 **Personal 라이선스** 활성화. 명령 한 줄의 배치 모드도 이 라이선스로 돈다. Hub 로그인이 풀리면 배치 모드가 라이선스 에러(종료 코드 198)로 멈추니, 그때는 Hub에 다시 로그인한다
- [ ] **Unity 6000.6.3f1**과 **Web Build Support** 설치. Hub에서 직접 하거나, 명령 한 줄에 `-InstallEditor`를 붙인다
- [ ] **Git** 설치(Windows는 Git for Windows. Git LFS가 같이 들어 있다) 후 저장소 클론
- [ ] 명령 한 줄 실행. 처음에 Windows 방화벽 창이 뜨면 Unity Editor를 **허용**한다(PlayMode 테스트가 호스트를 열기 때문이고, 같은 네트워크의 친구가 참가할 때도 필요하다)

### 웹 데모(모바일 브라우저 테스트)용

배포처는 공개 저장소 + GitHub Pages로 정했다(2026-10-28). 빌드·배포 워크플로(`web-demo.yml`)는 클라우드가 붙였고, 아래 셋을 하면 푸시마다 https://learc6910.github.io/pray-to/ 가 갱신된다. 자세한 건 `Docs/WebDemo.md`.

- [ ] **저장소를 public으로.** GitHub 저장소 Settings > General 맨 아래 Danger Zone > Change repository visibility > Make public. 클라우드 세션에는 이 권한이 없다. 로컬 세션에 맡긴다면 사람이 허락한 뒤 `gh repo edit LeArc6910/pray-to --visibility public --accept-visibility-change-consequences`. 공개 전에 기록을 확인했다: 커밋 이메일은 GitHub·Anthropic의 noreply 주소이고, 라이선스 파일·키·비밀번호는 없다
- [ ] Settings > Pages > Build and deployment > Source를 **GitHub Actions**로 (public이 된 뒤에 고를 수 있다)
- [ ] 라이선스 파일 찾기. Windows는 `C:\ProgramData\Unity\Unity_lic.ulf`, macOS는 `/Library/Application Support/Unity/Unity_lic.ulf`
- [ ] Settings > Secrets and variables > Actions에 시크릿 3개 추가: `UNITY_LICENSE`(ulf 파일 내용 전체), `UNITY_EMAIL`, `UNITY_PASSWORD`(Unity 계정). **이 값들은 이 파일이나 커밋, 채팅에 절대 적지 않는다.** 공개 저장소여도 시크릿은 보이지 않는다
- [ ] Actions 탭 > Web demo > Run workflow로 첫 빌드를 돌린다(이후에는 푸시마다 돈다). 결과 보고에는 위를 했는지와 빌드 성공 여부만 적는다

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

- [ ] `git pull` 후 명령 한 줄을 `-Commit`과 함께 실행
- [ ] (선택) 에디터 메뉴 **Game > Build Web Demo**로 웹 빌드가 되는지 본다. 결과 `Builds/Web`은 커밋하지 않는다. 같은 Wi-Fi의 휴대폰으로 보려면 `python -m http.server 8000 --directory Builds/Web` 후 `http://<PC 주소>:8000`

### 4. 웹 데모 확인 (사람, 휴대폰)

위 "웹 데모용"을 마치고 첫 배포가 끝난 뒤, 휴대폰 브라우저로 https://learc6910.github.io/pray-to/ 를 가로로 연다. 조작은 `Docs/WebDemo.md`의 터치 조작 표.

- [ ] 로딩이 끝나고 메뉴가 보이는지, 한글이 깨지지 않는지, 글자 크기가 읽을 만한지
- [ ] [혼자 하기], [가방], [나가기] 버튼이 탭에 반응하는지
- [ ] 왼쪽 드래그로 움직이고, 오른쪽을 누르고 있으면 그쪽으로 휘두르는지
- [ ] 가방 창이 화면에 들어오는지, 장착·버리기 버튼이 눌리는지
- [ ] 끊기지 않고 부드럽게 도는지
- [ ] 결과 보고: 휴대폰 기종과 브라우저, 안 된 항목, 불편한 점. 스크린샷 `Docs/Screenshots/web_phone.png`

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
