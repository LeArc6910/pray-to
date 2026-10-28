# 웹 데모

`main`의 최신 상태를 브라우저(휴대폰 포함)에서 바로 해 볼 수 있게 하는 빌드와 배포다. 주소는 https://learc6910.github.io/pray-to/ 이다(배포가 켜진 뒤).

## 흐름

1. `main`에 푸시하면 `.github/workflows/web-demo.yml`이 돈다. 문서(`*.md`, `Docs/`)와 `DevTools/`만 바뀐 푸시는 건너뛴다. Actions 탭에서 손으로 돌릴 수도 있다(Run workflow).
2. 시크릿 `UNITY_EMAIL`, `UNITY_PASSWORD`가 없으면 빌드를 건너뛰고 안내만 남긴다(실패로 표시하지 않는다).
3. GameCI의 Unity 이미지(`unityci/editor:ubuntu-<ProjectVersion의 버전>-webgl-3`) 안에서 `.github/scripts/build-web.sh`가 돈다.
   - **라이선스**: 에디터에 들어 있는 Licensing Client로 Unity Personal을 활성화한다(`--activate-ulf --include-personal --username --password`, 시리얼 없음). `--showEntitlements`로 확인하고, 빌드가 어떻게 끝나든 `--return-ulf`로 돌려준다. GameCI의 `unity-builder`는 Personal도 ULF 파일에서 꺼낸 시리얼로 활성화하는데, Unity 6 Personal은 시리얼이 없고 Hub가 ULF를 남기지 않아 "serial invalid"로 실패했다(2026-10-28). 계정은 소셜 로그인이나 2단계 인증 없이 이메일·비밀번호로 로그인할 수 있어야 한다
   - **빌드**: `Game.Client.Editor.WebBuild.RunBatch`. 먼저 `Game > Set Up Project`를 해서 씬과 시트 목록이 최신인지 맞춘 뒤 WebGL로 `build/web`에 빌드한다. `Library/`는 Actions 캐시에 남겨 다음 빌드를 줄인다
4. 결과를 GitHub Pages에 올린다. 처음 빌드는 20분 안팎, `Library` 캐시가 있으면 job 전체가 12분쯤이다(이미지 받기 3분, 빌드 6분).

드물게 에디터가 시작하자마자 `Killed`(종료 코드 137)로 끝난다(2026-10-28에 한 번. 같은 캐시로 다시 돌리니 됐다). 그러면 Actions 탭에서 Re-run failed jobs를 누른다. 되풀이되면 클라우드에 알린다.

빌드 설정(`ProjectSetup.UseWebSettings`): gzip 압축에 decompression fallback을 켠다. GitHub Pages는 `.gz` 파일에 `Content-Encoding` 헤더를 붙이지 않으므로, 브라우저 대신 Unity 로더가 압축을 푼다. 데이터 캐싱도 켜서 두 번째 접속부터는 빨리 뜬다.

로컬에서 빌드만 해 보려면 에디터 메뉴 **Game > Build Web Demo**(결과는 `Builds/Web`, 커밋하지 않는다). 아무 정적 서버로 그 폴더를 띄우면 된다. 예: `python -m http.server 8000 --directory Builds/Web` 후 같은 Wi-Fi의 휴대폰에서 `http://<PC 주소>:8000`. 빌드하면 Unity가 URP 에셋 몇 개(`URP-2D.asset`의 셰이더 프리필터 설정, `UniversalRenderPipelineGlobalSettings.asset`, `DefaultVolumeProfile.asset`)를 고쳐 쓰는데, 게임에는 영향이 없으니 되돌려도 된다. Burst가 저장소 루트에 만드는 `Data/`는 무시된다.

## 웹에서 달라지는 것

- **혼자 하기만 된다.** 브라우저는 연결을 받을 수 없어 호스트가 될 수 없고, 참가하려면 WebSocket 전송이 필요하다. 메뉴에서 호스트와 참가를 숨긴다
- **글꼴**: 웹 빌드는 시스템 글꼴을 쓸 수 없어서 Noto Sans KR(SIL OFL 1.1, `Docs/Licenses/NotoSansKR-OFL.txt`)을 게임에 넣었다. 10 MB짜리 원본에서 Medium 굵기와 필요한 글자(ASCII, 기호, KS X 1001 한글 2,350자, 코드와 시트에 쓰인 모든 글자)만 남겨 약 520 KB다. 새 글자가 필요하면 `DevTools/Fonts/make-korean-font.py`로 다시 만든다. 에디터와 PC 빌드도 같은 글꼴을 쓴다
- **화면 크기**: 브라우저는 기기의 실제 픽셀 밀도로 그리므로(휴대폰은 점 하나에 3픽셀쯤) 그대로면 글자가 아주 작다. `Ui`가 `window.devicePixelRatio`(`Plugins/WebGL/Display.jslib`)만큼 IMGUI를 키운다. 세로 화면에서는 카메라를 넓혀 가로로 보이는 범위를 지킨다. 가로로 들고 하는 쪽이 편하다

## 터치 조작

| 동작 | 하는 법 |
|---|---|
| 이동 | 화면 왼쪽 절반을 누르고 끈다. 누른 곳에 스틱이 생기고, 덜 끌면 천천히 걷는다 |
| 공격 | 화면 오른쪽 절반을 누른다. 누른 곳에 붉은 스틱이 생기고, 누르고 있는 동안 계속 휘두른다. 스틱을 끄는 방향으로 휘두르고, 거의 끌지 않으면 걷는 방향(서 있으면 바라보던 방향)으로 휘두른다 |
| 스킬 | 오른쪽 아래 구석의 버튼 4개(2×2). 공격 스틱을 잡고 있으면 그 쪽으로, 아니면 바라보는 쪽으로 쓴다. 다른 손가락으로 스틱을 잡은 채 눌러도 된다. 메뉴에서 고른 클래스의 스킬이 들어간다(`Docs/Classes.md`) |
| 지도 | 오른쪽 위 미니맵을 탭하면 전체 지도가 크게 열린다. 아무 데나 다시 탭하면 닫힌다. 가 본 곳만 보인다 |
| 가방 | 미니맵 아래 [가방] 버튼. 아이템 이름을 누르고 있으면 설명이 보인다 |
| 나가기 | 미니맵 아래 [나가기] 버튼 |

버튼, 미니맵, 가방 창 위에서 시작한 터치와 지도가 크게 열려 있는 동안의 터치는 조작으로 치지 않는다. 터치를 한 번이라도 하면 HUD 안내와 버튼이 터치용으로 바뀐다. 브라우저는 탭을 마우스 클릭으로도 보내므로 마지막 터치 뒤 1초 동안은 마우스를 무시한다.

## 아직 확인하지 못한 것

클라우드에서는 실제 기기로 돌려 볼 수 없어서 다음은 첫 배포 뒤 휴대폰에서 확인한다.

- IMGUI 버튼(메뉴, 가방, 나가기)이 탭에 반응하는지. 안 되면 버튼도 터치 입력으로 직접 처리한다
- 글자 크기와 HUD·가방 창 배치가 화면에 맞는지
- 프레임이 충분히 나오는지
