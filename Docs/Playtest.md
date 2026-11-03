# 웹 플레이 시험 (클라우드가 대신 해 보기)

사람이 로컬에서 직접 해 보며 보던 확인을 클라우드 세션이 먼저 한다(사용자 요청, 2026-11-04: "내가 테스트 요청하면 원래 로컬에서 내가 확인해야 하는 부분을 최대한 너가 테스트"). 클라우드 컨테이너의 헤드리스 Chromium으로 GitHub Pages의 웹 데모를 띄우고, 실제 키보드·마우스와 터치로 조작하고, 화면을 찍어 클라우드가 본다. Unity가 없어도 된다. WebGL은 SwiftShader(소프트웨어)로 그려서 초당 20프레임쯤 나온다.

## 돌리는 법

```
# 배포된 웹 데모로 지금 바로
node DevTools/WebPlaytest/playtest.js

# 방금 푸시한 것이 배포될 때까지(웹 데모 빌드는 15분쯤) 기다렸다가
node DevTools/WebPlaytest/playtest.js --wait

# 화면 하나만: pc, phone(휴대폰 세로 390×844), phone-wide(가로 844×390)
node DevTools/WebPlaytest/playtest.js --profiles phone
```

결과는 `DevTools/WebPlaytest/out/`에 사진(`<화면>-<장면>.png`)과 `report.md`(확인 항목마다 통과·실패, 브라우저가 본 에러)로 남는다(커밋하지 않는다). 클라우드는 사진을 열어 보고, 사람에게는 사진과 숫자로 알 수 없는 것만 맡긴다.

## 무엇을 해 보는지

- **PC 1280×720, 키보드·마우스**: 메뉴, 혼자 하기, D로 걷기, Q 스킬과 마나, 마나 물약, 슬라임에 마우스를 누르고 있기(연타 사진 넷), Tab 가방, 세트를 입은 가방, 새 기계, 빛 구멍과 채광창(아침·저녁·보름달 밤), 장날, 폭풍·안개·비, 기지 방어, 공허 균열
- **휴대폰 세로·가로, 터치**: 탭하면 터치 화면이 되는지, 왼쪽 스틱을 끌어 걷기, 스킬 단추, 공격 스틱, 미니맵 아래 [가방] 단추, 기계 장면
- 확인마다 게임의 상태(위치, 마나, 맞은 몬스터 수, 기계의 전력, 날씨·행사, 방어·균열)를 읽어 통과·실패를 적는다. 브라우저 콘솔의 에러와 예외는 모두 모은다

## 게임 쪽 원격 조종 (`Client/Playtest.cs`)

페이지가 `unityInstance.SendMessage("Game", "Playtest", "<명령; 명령>")`을 부르면 혼자 하기에서만 듣는다(다른 사람이 있는 게임에서는 듣지 않는다). 명령마다 콘솔에 `[playtest] ...`로 답한다.

| 명령 | 하는 일 |
|---|---|
| `solo` | "playtest"라는 새 월드와 캐릭터로 혼자 하기를 시작한다 |
| `scene combo\|machines\|defense\|rift\|skylight\|set` | PlayMode 장면(`FeatureTests`)처럼 둘레를 꾸민다 |
| `clock <날> <시>`, `weather clear\|rain\|storm\|fog` | 시계를 옮긴다 |
| `give <재료> <개수>`, `potion <소모품> <개수>`, `drink <소모품>`, `mana <양>` | 가방과 마나 |
| `spawn <몬스터> <dx> <dy> [생명력]` | 곁에 몬스터를 둔다 |
| `attack on\|off [x y]`, `skill <칸>`, `touch on\|off`, `bag on\|off` | 조작 |
| `report` | 상태 한 줄(JSON) |

웹 페이지(`Assets/WebGLTemplates/Fit/index.html`)는 `window.unityInstance`를 열어 두고, 웹 데모 워크플로는 배포에 `version.txt`(커밋)를 넣어 `--wait`가 기다릴 수 있게 한다.

## 사진으로 알 수 없는 것

손맛(연타의 리듬, 히트 스톱이 거슬리지 않는지), 소리, 실제 휴대폰의 손가락 감각과 성능, 오래 해 보는 재미. 이것만 사람에게 묻는다. 로컬의 Unity 에디터 시험(명령 한 줄, 감시 스크립트)은 그대로 둔다: 에디터에서만 드러나는 문제(임포트, 에셋, `.meta`)를 잡는다.
