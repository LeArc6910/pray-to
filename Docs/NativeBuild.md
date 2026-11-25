# 네이티브 빌드: PC(Windows)와 휴대폰 앱 (2026-11-26)

지금은 웹 데모(`Docs/WebDemo.md`)로만 내보내지만, 나중에 Windows 실행 파일과 휴대폰 앱(Android, iOS)도 낸다(`Docs/Design.md` 기술 원칙 7). 웹 브라우저가 대신 해 주던 일 가운데 앱이 스스로 해야 하는 것을 미리 해 두었다. 웹 데모의 동작은 그대로다(UI 크기 설정만 새로 생겼고 100%면 전과 같다).

## 웹과 앱이 다른 것

| | 웹 데모 | Windows 앱 | 휴대폰 앱 |
|---|---|---|---|
| 프레임 | 브라우저가 그릴 때(`requestAnimationFrame`) | 화면 주사율에 맞춤(vSync) | 초당 60, 설정 [프레임: 30]이면 30(배터리) |
| 점 하나의 픽셀(UI 배율) | 브라우저의 `devicePixelRatio` | 시스템 배율(96 dpi가 1, 150%면 1.5) | 화면 dpi ÷ 160(400 dpi 폰이 2.5), dpi를 모르면 짧은 변 ÷ 360 |
| 안전 영역(노치·둥근 모서리) | 없음 | 없음 | `Screen.safeArea` 안에 화면을 배치 |
| 나갈 때 저장 | 탭을 숨기거나 페이지를 떠날 때 | 창이 포커스를 잃거나 닫힐 때 | 홈으로 나가거나(일시 정지) 포커스를 잃을 때 |

### 프레임 (`Client/FrameRate.cs`)

Unity는 휴대폰에서 따로 말하지 않으면 초당 30프레임으로 돈다. 시작할 때(`GameRoot.Awake`) 휴대폰 앱은 `Application.targetFrameRate = 60`(vSync 끔), 설정 [프레임: 30]이면 30. PC 앱은 vSync 1(화면 주사율, 그 이상은 그리지 않는다). 웹과 에디터는 건드리지 않는다. [프레임] 줄은 휴대폰 앱의 설정에만 보인다. 개발 정보 첫 줄 끝에 지금 목표("60", "화면 주사율", "브라우저")가 나온다.

### UI 배율 (`Ui.PointScale`)

화면은 점 단위로 배치하고 GUI 행렬이 점을 픽셀로 키운다. 웹은 전부터 `devicePixelRatio`를 썼고, 앱은 1이었다(휴대폰 앱이면 글자가 아주 작았다). 이제:

- 휴대폰 앱: `Screen.dpi / 160`을 0.25 단위로 반올림, 1~4. dpi가 0이면 짧은 변 ÷ 360. 짧은 변이 320점보다 작아지지 않게 줄인다(배치 시험이 360×640까지 본다)
- PC 앱: `Screen.dpi`가 120 이상이면 `dpi / 96`(150% 배율 화면이 1.5), 아니면 1. 짧은 변 480점 이상
- 웹: 전처럼 `devicePixelRatio`(1~4)
- 에디터 게임 뷰는 1(PlayMode 사진이 그대로). 기기 시뮬레이터에서 휴대폰을 고르면 휴대폰 규칙
- 설정 [UI 크기: 100%]가 90·100·115·130%로 돈다(모든 판에서). 키우는 쪽은 짧은 변이 320점(PC 앱 480점) 아래로 내려가지 않는 데까지만

웹 글 상자(`WebText`)는 페이지 픽셀을 `devicePixelRatio`로 따로 계산해(`Ui.PageRatio`) UI 크기를 바꿔도 칸 위에 뜬다.

### 안전 영역 (`Ui.Inset`, `Ui.FullScreen`)

노치·카메라 구멍·둥근 모서리가 있는 휴대폰은 화면 가장자리 일부를 가린다. OnGUI가 시작할 때 `Screen.safeArea`를 점으로 바꿔(`Ui.Inset`) GUI 행렬을 그만큼 옮기고, `Ui.Width`·`Ui.Height`는 안전 영역의 크기가 된다. 그래서 HUD, 미니맵, 스틱, 스킬 단추, 창, 시작화면이 모두 따로 고치지 않고 안전 영역 안에 놓인다. 게임 세계(카메라)는 화면 전체에 그리고, 화면 전체를 덮어야 하는 것(어둡게 하기, 시작화면 배경, 로딩, 붉은 가장자리, 번쩍임, 허브 바깥 누르기)은 `Ui.FullScreen`(안전 영역 밖까지)에 그린다. `Ui.FromScreen`도 옮긴 만큼 뺀다. Android는 `renderOutsideSafeArea`를 켜서 노치 옆까지 그린다(`NativeBuild`).

시험: PlayMode `LayoutTests.TheFixedPartsKeepToTheSafeArea`(노치 휴대폰 넷의 안전 영역을 `Ui.InsetOverride`로 세우고 고정 부품이 모두 그 안, 서로 안 겹침), 사진 `combo-touch-safearea`(가로 휴대폰에 44·16·44·20점). 웹 점검은 `layout` 구역에서 Playtest 명령 `inset 44 0 44 20`으로 세우고 HUD·가방 단추가 안전 영역 안인지 본다(사진 `game-safearea`, `bag-safearea`). 웹 데모 자체는 안전 영역을 두지 않는다(브라우저가 페이지를 노치 밖에 둔다).

### 나갈 때 저장 (`GameRoot.SaveOnLeaving`)

휴대폰은 홈으로 나간 앱을 말없이 닫을 수 있다. `OnApplicationPause(true)`·`OnApplicationFocus(false)`·`OnApplicationQuit`에서 캐릭터, 이 월드의 지도(바뀌었으면), 호스트면 월드를 정해진 저장과 같은 길로 쓴다. 게임은 끝내지 않는다. 포커스가 빨리 오가면(알트탭, 웹의 글 상자) 5초에 한 번만 쓰고, 끌 때는 늘 쓴다. 웹은 `index.html`이 탭이 숨거나(`visibilitychange`) 페이지를 떠날 때(`pagehide`) `GameRoot.PageHidden`을 부른다(IndexedDB 동기화는 그대로 Unity가 한다). 시험: PlayMode `SessionTests.LeavingTheAppSavesAndPlaysOn`, 웹 점검 `save` 구역의 탭 숨기기.

## 빌드하기 (`Client/Editor/NativeBuild.cs`)

메뉴 Game > Build Windows / Build Android, 또는 명령줄. 빌드에 필요한 Player Settings는 빌드를 시작할 때 스크립트가 정한다(`ProjectSettings/`를 손으로 고치지 않는다). 빌드 전에 `ProjectSetup.SetUp`을 돌린다(웹 빌드와 같이).

- **Windows**: x64, **Mono**. Windows용 Unity 에디터에는 Mono 빌드 지원이 함께 깔려 따로 설치할 것이 없다. IL2CPP는 "Windows Build Support (IL2CPP)" 모듈과 Visual Studio의 C++ 도구가 더 필요해서 지금은 쓰지 않는다(성능이 모자라면 그때 바꾼다). 창 모드 전체 화면, 창 크기 바꾸기 허용. 결과 `Builds/Windows/pray-to.exe`
- **Android**: APK, **IL2CPP ARM64**(Google Play는 64비트를 요구하고 Mono는 32비트 ARM만 만든다), 최소 API 26(Android 8.0), 대상 API는 설치된 가장 높은 것, 세로·가로 자동 회전(거꾸로 세로는 빼고), 인터넷 권한(온라인 방·클라우드 저장), 앱 아이디 `com.prayto.game`(스토어에 올리기 전에 정한다). 에디터의 디버그 키로 서명한다(내 휴대폰에 깔기에는 충분, 스토어에는 따로 키를 만든다). 결과 `Builds/Android/pray-to.apk`
- **iOS**: 아직 없다. Mac과 Xcode가 있어야 하고, 같은 방식으로 `BuildIos`를 더하면 된다

### 로컬에 깔아 둘 것

Unity Hub > 설치 > 6000.6.3f1 > 모듈 추가:

- Windows에서 Windows 빌드: 따로 없음(Mac·Linux 에디터라면 "Windows Build Support (Mono)")
- Android: "Android Build Support"와 그 아래 "OpenJDK", "Android SDK & NDK Tools"를 모두 체크

없으면 배치 빌드가 종료 코드 2로 끝나고 로그에 어느 모듈이 없는지 적는다.

### 명령

Windows PowerShell(저장소 맨 위에서):

```
& "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -projectPath . -buildTarget Win64 -executeMethod Game.Client.Editor.NativeBuild.RunBatchWindows -logFile Logs/build-windows.log
& "C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -projectPath . -buildTarget Android -executeMethod Game.Client.Editor.NativeBuild.RunBatchAndroid -logFile Logs/build-android.log
```

macOS는 `/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity`, Linux는 `~/Unity/Hub/Editor/6000.6.3f1/Editor/Unity`. `-customBuildPath <파일>`로 다른 곳에 쓴다. 종료 코드 0 성공, 1 빌드 실패(로그의 `Native build` 줄과 그 위 오류), 2 모듈 없음. 에디터가 열려 있으면 같은 프로젝트로 배치 모드를 돌릴 수 없으니 닫고 돌리거나 메뉴로 빌드한다.

APK를 휴대폰에 깔기: 휴대폰의 개발자 옵션에서 USB 디버깅을 켜고 `adb install -r Builds/Android/pray-to.apk`(adb는 Unity가 받은 SDK의 `platform-tools`에 있다), 또는 파일을 휴대폰에 옮겨 연다(출처를 알 수 없는 앱 허용).

## 사람이 확인할 것

사진으로 알 수 없는 것만: 실제 휴대폰에서 글자 크기가 알맞은지(UI 크기 90~130%), 노치·둥근 모서리에 HUD가 가리지 않는지(세로·가로), 60프레임의 부드러움과 30으로 바꿨을 때의 배터리, 홈으로 나갔다가 앱을 지우고(최근 앱에서 밀기) 다시 열었을 때 이어지는지, Windows 앱에서 150% 배율 화면의 글자 크기와 창을 닫았다 열었을 때 이어지는지. `Docs/HumanChecklist.md`.
