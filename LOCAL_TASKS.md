# 로컬 작업 지시서

클라우드 세션(코드·데이터·문서 담당)이 로컬 세션(Unity 에디터 담당)에게 넘기는 작업 목록이다.
로컬 세션은 이 파일을 읽고 위에서부터 처리한 뒤, 결과를 아래 "결과 보고" 칸에 적어 커밋·푸시한다.

## 규칙

- 작업 브랜치: `main`. 시작 전에 항상 `git pull`
- 클라우드가 스크립트를 수정하므로 **로컬에서는 `.cs` 파일을 고치지 않는다**. 컴파일 에러나 버그는 고치지 말고 Console 로그를 그대로 결과 보고에 붙인다
- 로컬이 커밋하는 것: 씬, 프리팹, `.asset`, `.meta`, `Packages/`, `ProjectSettings/`, 아트·사운드 파일
- 이미지·오디오는 Git LFS로 올라간다 (`git lfs install` 한 번 필요)
- **(사람)** 표시가 붙은 항목은 로컬 세션이 대신할 수 없다. 사람에게 요청하고 결과만 적는다

## 할 일

### 1. Unity 프로젝트 열기 (최우선)
저장소에는 `Packages/manifest.json`과 `ProjectSettings/ProjectVersion.txt`만 있다. 나머지 설정은 Unity가 처음 열 때 만든다.
- [ ] `git lfs install` 후 저장소 클론
- [ ] **(사람)** Unity Hub에 6000.6.3f1이 설치돼 있는지 확인하고, 모듈에 **Web Build Support**를 추가 (웹 데모 빌드용)
- [ ] Unity Hub > Add > Add project from disk → 저장소 폴더 → 6000.6.3f1로 열기
- [ ] Active Input Handling을 묻는 창이 뜨면 **Input System Package (New)** 선택
- [ ] 패키지 설치가 끝난 뒤 Console에 에러가 없는지 확인. Netcode for GameObjects, Unity Transport, URP, Input System이 manifest의 버전대로 설치됐는지 Package Manager에서 확인해 결과 보고에 적기

### 2. URP 2D 렌더러 설정
- [ ] `Assets/_Project/Settings/`에 **URP Asset (with 2D Renderer)** 생성 (Create > Rendering > URP Asset (with 2D Renderer))
- [ ] Project Settings > Graphics의 Default Render Pipeline과 Quality의 모든 레벨 Render Pipeline Asset에 위 에셋 지정
- [ ] Project Settings > Editor > Default Behaviour Mode를 **2D**로
- [ ] Project Settings > Player > Product Name을 `pray-to`로

### 3. 컴파일·테스트 확인 후 커밋
- [ ] Console에 컴파일 에러가 없는지 확인
- [ ] Window > General > Test Runner > EditMode > Run All. 통과/실패 개수 보고 (클라우드 기준 64개 통과)
- [ ] 생성된 `.meta`, `Packages/packages-lock.json`, `ProjectSettings/`, `Assets/_Project/Settings/`를 커밋·푸시. `Library/`, `Temp/`, `Logs/`, `UserSettings/`는 커밋하지 않는다 (`.gitignore` 확인)

### 4. CI용 Unity 라이선스
GitHub Actions에서 Unity 테스트와 웹 빌드를 돌리려면 필요하다.
- [ ] **(사람)** Unity Hub > Preferences > Licenses에 Personal 라이선스가 활성화돼 있는지 확인
- [ ] **(사람)** 라이선스 파일 찾기. Windows는 `C:\ProgramData\Unity\Unity_lic.ulf`, macOS는 `/Library/Application Support/Unity/Unity_lic.ulf`
- [ ] **(사람)** GitHub 저장소 Settings > Secrets and variables > Actions에 시크릿 3개 추가: `UNITY_LICENSE`(ulf 파일 내용 전체), `UNITY_EMAIL`, `UNITY_PASSWORD`(Unity 계정). 이 값들은 이 파일이나 커밋에 절대 적지 않는다
- [ ] 결과 보고에는 시크릿을 추가했는지 여부만 적는다

## 결과 보고

(아직 없음)
