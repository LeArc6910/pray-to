# 웹 데모

`main`의 최신 상태를 브라우저(휴대폰 포함)에서 바로 해 볼 수 있게 하는 빌드와 배포다. 주소는 https://learc6910.github.io/pray-to/ 이다(배포가 켜진 뒤).

## 흐름

1. `main`에 푸시하면 `.github/workflows/web-demo.yml`이 돈다. 문서(`*.md`, `Docs/`)와 `DevTools/`만 바뀐 푸시는 건너뛴다. Actions 탭에서 손으로 돌릴 수도 있다(Run workflow).
2. Unity 라이선스 시크릿이 없으면 빌드를 건너뛰고 안내만 남긴다(실패로 표시하지 않는다).
3. GameCI(`game-ci/unity-builder`)가 `unityci/editor:ubuntu-6000.6.3f1-webgl-3` 이미지로 `Game.Client.Editor.WebBuild.RunBatch`를 실행한다. 먼저 `Game > Set Up Project`를 해서 씬과 시트 목록이 최신인지 맞춘 뒤 WebGL로 빌드한다. `Library/`는 Actions 캐시에 남겨 다음 빌드를 줄인다.
4. 결과를 GitHub Pages에 올린다. 처음 빌드는 20분 안팎, 캐시가 있으면 그보다 짧다.

빌드 설정: gzip 압축에 decompression fallback을 켠다. GitHub Pages는 `.gz` 파일에 `Content-Encoding` 헤더를 붙이지 않으므로, 브라우저 대신 Unity 로더가 압축을 푼다. 데이터 캐싱도 켜서 두 번째 접속부터는 빨리 뜬다.

로컬에서 빌드만 해 보려면 에디터 메뉴 **Game > Build Web Demo**(결과는 `Builds/Web`, 커밋하지 않는다). 아무 정적 서버로 그 폴더를 띄우면 된다. 예: `python -m http.server 8000 --directory Builds/Web` 후 같은 Wi-Fi의 휴대폰에서 `http://<PC 주소>:8000`.

## 웹에서 달라지는 것

- **혼자 하기만 된다.** 브라우저는 연결을 받을 수 없어 호스트가 될 수 없고, 참가하려면 WebSocket 전송이 필요하다. 메뉴에서 호스트와 참가를 숨긴다
- **글꼴**: 웹 빌드는 시스템 글꼴을 쓸 수 없어서 Noto Sans KR(SIL OFL 1.1, `Docs/Licenses/NotoSansKR-OFL.txt`)을 게임에 넣었다. 10 MB짜리 원본에서 Medium 굵기와 필요한 글자(ASCII, 기호, KS X 1001 한글 2,350자, 코드와 시트에 쓰인 모든 글자)만 남겨 약 520 KB다. 새 글자가 필요하면 `DevTools/Fonts/make-korean-font.py`로 다시 만든다. 에디터와 PC 빌드도 같은 글꼴을 쓴다
- **화면 크기**: 브라우저는 기기의 실제 픽셀 밀도로 그리므로(휴대폰은 점 하나에 3픽셀쯤) 그대로면 글자가 아주 작다. `Ui`가 `window.devicePixelRatio`(`Plugins/WebGL/Display.jslib`)만큼 IMGUI를 키운다. 세로 화면에서는 카메라를 넓혀 가로로 보이는 범위를 지킨다. 가로로 들고 하는 쪽이 편하다

## 터치 조작

| 동작 | 하는 법 |
|---|---|
| 이동 | 화면 왼쪽 절반을 누르고 끈다. 누른 곳에 조이스틱이 생기고, 덜 끌면 천천히 걷는다 |
| 공격 | 화면 오른쪽 절반을 누르고 있는다. 누른 쪽을 바라보고 계속 휘두른다 |
| 가방 | 오른쪽 위 [가방] 버튼. 아이템 이름을 누르고 있으면 설명이 보인다 |
| 나가기 | 오른쪽 위 [나가기] 버튼 |

버튼과 가방 창 위에서 시작한 터치는 조작으로 치지 않는다. 터치를 한 번이라도 하면 HUD 안내와 버튼이 터치용으로 바뀐다. 브라우저는 탭을 마우스 클릭으로도 보내므로 마지막 터치 뒤 1초 동안은 마우스를 무시한다.

## 아직 확인하지 못한 것

클라우드에서는 실제 기기로 돌려 볼 수 없어서 다음은 첫 배포 뒤 휴대폰에서 확인한다.

- IMGUI 버튼(메뉴, 가방, 나가기)이 탭에 반응하는지. 안 되면 버튼도 터치 입력으로 직접 처리한다
- 글자 크기와 HUD·가방 창 배치가 화면에 맞는지
- 프레임이 충분히 나오는지
