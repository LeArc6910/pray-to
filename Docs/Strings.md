# 문자열 표 (화면의 말)

화면에 나오는 말을 언어별 시트에서 읽는다(2026-11-11, `Docs/Research2026-11.md` 후보 17). 지금은 시험 단계로, 메뉴 화면 하나만 표를 거친다. 영어 등 다른 언어를 내기 전에 남은 화면을 같은 방법으로 차례로 옮긴다.

## 형식

`Assets/_Project/Data/Strings/strings.csv`

| 열 | 뜻 |
|---|---|
| `id` | 말의 이름. 화면·묶음을 점으로 나눈다(`menu.daylength`, `common.on`). 겹치면 오류 |
| `ko` | 한국어. 코드의 `Strings.T(id, "한국어")`에 적은 말과 같아야 한다(시험이 확인) |
| `en` 등 | 다른 언어. 비어 있으면 한국어가 나온다. 열을 더하면 언어가 늘어난다 |

- `#`로 시작하는 줄은 설명이다. 쉼표가 든 말은 칸 전체를 큰따옴표로 감싼다(코드에 적힌 한국어와 글자 하나까지 같아야 해서 `·`로 바꾸지 않는다).
- `{0}` 자리는 `string.Format`으로 채운다(`menu.daylength`: "하루 길이: {0}분").

## 코드에서

- Core: `StringSheet.Parse(csv)`가 `StringData`를 만든다. `Get(id, language, korean)`은 그 언어의 말을, 없으면 넘겨받은 한국어를 준다. 시트가 없어도 게임은 한국어로 돈다.
- Client: `Strings.T("menu.name", "캐릭터 이름")`. 한국어를 코드 옆에 그대로 두어, 코드를 읽을 때 화면의 말이 보이고 시트가 빠져도 깨지지 않는다.
- 언어 고르기: 메뉴의 [언어] 단추(`Settings.Language`, 저장 이름 `settings.language`). 한국어 말고 채워진 언어가 있을 때만 보인다. 지금 `en` 열은 메뉴 말만 채웠다.
- 연결: `GameSheets.strings`, `SceneSetup`이 `Data/Strings/`를 넣는다. 로컬이 명령 한 줄을 다시 돌려야 씬에 들어간다.
- 시험: `GuideTests.TheStringSheetGivesEachLanguageAndKeepsTheCodesKorean`이 시트 형식과, 코드의 `Strings.T` 호출마다 한국어가 시트의 `ko`와 같은지 본다.

## 얼마나 남았나

`python3 DevTools/Strings/count-korean.py`가 코드의 한글 문자열과 시트의 한글 줄을 센다(2026-11-11).

- 코드의 한글 문자열 1,231개. 많은 곳: `InventoryWindow.cs` 277, `GameRoot.cs` 108, `WorldView.cs` 64, `SaveMenu.cs` 52, `StatText.cs` 50.
- 시트의 한글이 든 줄 734개(아이템·몬스터·목표·이야기 이름과 설명).
- Core의 시트 오류 문구(`...Sheet.cs`)와 시험 봇(`Bot/`)은 개발자만 보므로 옮기지 않는다. 이것을 빼면 화면에 나오는 코드 문자열은 대략 700~800개다.

## 옮기는 차례

1. 메뉴(끝남, 2026-11-11). 메뉴 안의 모든 말과 저장본 내보내기 안내까지. 메뉴에서 여는 저장본 목록·키 목록의 이름(동작 이름)과 클래스 이름은 아직 한국어다.
2. HUD·단추 줄·안내(`GameRoot`, `Hotbar`, `Guide` 표시).
3. 가방 창(`InventoryWindow`, 가장 많다), 저장 창(`SaveMenu`).
4. 데이터 시트의 이름·설명: 시트마다 `name_en`처럼 열을 더하는 방법과, 문자열 표에 `item.<id>.name`으로 모으는 방법 중 고른다. 시트가 많아 앞쪽이 관리가 쉽다고 보지만, 번역을 맡기기에는 한 파일인 뒤쪽이 낫다. 영어를 실제로 낼 때 정한다.
5. Core가 만드는 말(`StatText`, `ItemText`, `World.Life`의 알림): 문장을 Core에서 만들지 않고 id와 숫자를 넘겨 Client가 표로 만드는 쪽으로 바꾼다.

글꼴: 한국어 글꼴(Galmuri 등)은 영어를 그대로 그린다. 중국어·일본어는 글꼴이 따로 필요하다.
