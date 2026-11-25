# 시작화면·캐릭터와 월드 고르기·메뉴·로딩 개편

2026-11-25 사람 요청: "게임 시작화면을 좀 더 멋지게 바꾸고 싶어. 뒤에는 배경이 흘러가고 버튼도 지금처럼 박스형이 아니라 자연스럽게 나오도록 (테라리아, 코어키퍼 참조). 그리고 맵을 하나만 두는게 아니라 캐릭과 맵을 분리해서 캐릭 리스트에서 선택, 맵 리스트에서 선택해서 이전에 사용하던 캐릭터나 맵을 이어서 할 수 있게 해줘. 시작화면에서 접근해서 들어갔을 때의 메뉴들도 좀 더 멋지게 바꾸고, 게임 로딩시에도 툴팁이나 그림 등으로 심심하지 않게 만들어줘"

## 1. 지금

- **시작화면**: `GameRoot.DrawMainMenu`. 고정된 동굴빛 바탕(`UiSkin.Backdrop`) 위에 글자 로고 "pray-to"와 가죽 틀 단추 여섯이 한 줄로 쌓여 있다. 뒤에서 움직이는 것은 없다(`Docs/UiRework.md` 남은 일 "메뉴 첫 화면의 게임 그림").
- **저장**: 이미 캐릭터(`character:<이름>`)와 월드(`world:<이름>`)가 따로 저장되고, 어느 캐릭터든 어느 월드에나 들어간다(`Docs/Saving.md`, 2026-10-30 결정, 코어키퍼처럼). 하지만 [게임 시작]에는 이름 칸 둘만 있어 **목록에서 고를 수 없고**, 저장본마다 레벨·플레이 시간·날짜를 보여 주지 않는다. 캐릭터의 모습은 정해져 있다(내 옷은 파랑, 남은 번호마다 색).
- **로딩**: 없다. 단추를 누르는 OnGUI 한 번 안에서 월드를 만든다(데스크톱 1.4~1.8초, 웹은 몇 배). 혼자 하기는 호스트와 내 클라이언트가 같은 지도를 두 번 만든다. 그동안 마지막 메뉴 그림이 멈춰 있다. `Docs/WorldRework.md`가 "로딩 화면에 단계 이름"을 계획했지만 만들지 않았다.
- **메뉴**: 하위 화면(함께 하기·저장본·설정)은 돌 창 하나에 단추를 쌓은 것이고, 게임 안 메뉴(허브)는 카드 위 4열 단추다.

## 2. 레퍼런스 (2026-11-25 조사)

| | 테라리아 | 코어키퍼 | 기타 |
|---|---|---|---|
| 시작화면 | 하루가 지날 때마다 바뀌는 배경, 낮밤과 달의 위상[1][2]. 해·달을 끌면 옛 로고[3]. 메뉴 위에 커서가 오면 틱 소리, 열고 닫는 소리[4]. 단추는 상자 없이 글자만, 올리면 커지고 밝아짐(관찰, 문서 없음) | 계절 로고와 계절 테마 옵션[12] | 스타듀: 구름과 새가 흐르고 메뉴가 내려옴, 로고를 누르면 숨은 장치[21]. 배경을 정지 그림이나 없음으로[22] |
| 캐릭터 | `.plr` 파일. 목록에 장비를 입은 모습, 이름, HP·마나, 난이도, 플레이 시간[5]. 즐겨찾기는 맨 앞, 지울 수 없음. 1.4.5부터 최근 쓴 순[2]. 만들다 Back이면 "버리겠습니까?"[5][2] | 슬롯 30. 만들기: 유형 → 외형(체형 2, 피부 4, 머리 9, 머리색 9, 눈 7, 상·하의 12색, 주사위로 무작위) → 배경(직업). 외형은 뒤에 바꾸지 못함[14]. 지우기 단추를 숨은 설정에서 슬롯 옆으로 옮김[13][15] | 스타바운드: 캐릭터 수 제한 없음, 캐릭터 먼저 고르고 참가[24] |
| 월드 | `.wld`. 이름·아이콘·난이도·크기·만든 날짜, 고른 캐릭터로 가 본 월드에 "Played"[6]. 만들기: 이름(무작위 기본), 시드(비우면 무작위), 크기, 난이도[6] | 캐릭터를 고른 뒤 월드 목록. 슬롯마다 이름·아이콘·모드·만든 날짜·보스 석상, 오른쪽에 지우기·정보. 빈 슬롯을 누르면 만들기[16]. 1.0에 시드·생성 설정[17][18] | 돈스타브: 월드를 만드는 손 그림, "Reticulating pines…" 같은 장난 문구, 팁을 끌 수 있음[23] |
| 로딩 | 단계마다 문구와 진행 막대, 결과를 미리 드러내지 않게 중립색[9]. 1.4.0.1부터 무작위 팁 한 문장[1][10] | (찾지 못함) | |

일반 지침: 로딩에는 진행 표시를 두고, 아직 못 본 장소·몬스터를 보이지 않으며, 팁은 일지에서도 다시 볼 수 있게[25]. 한국어 읽기 초당 12자[26] → 40자 팁은 4~5초, 6~8초마다 바꾸고 누르면 다음으로(추론). 터치 대상 44pt[28]·48dp[29], 지우기 확인은 대상 이름과 결과를 단추에 적는다("지우기"/"남기기")[32]. 배경 움직임은 끌 수 있게[31][33][34].

## 3. 사람이 정한 것 (2026-11-25)

1. **배경**: 동굴 위를 천천히 흐르기. 게임 그림(칸·소품·빛·작은 생물)으로 그린 작은 동굴 지도 위를 카메라가 천천히 흐르고 불빛이 깜박인다. 그 기기에서 아직 가 본 적 없는 생물군계는 나오지 않는다. 설정 "움직임: 줄이기"면 멈춘 한 장면
2. **캐릭터 만들기**: 이름 + 외형. 머리 모양·머리색·피부·눈·윗옷·아랫옷 색과 주사위. 협동에서 서로의 외형이 보인다
3. **메뉴 범위**: 시작화면에서 들어가는 화면들과 게임 안 메뉴(허브)까지 같은 새 모양으로
4. **로딩 그림**: 그 캐릭터가 가 본 곳의 장면(가 본 생물군계·만난 몬스터를 게임 그림으로), 아래에 팁 한 줄과 진행 막대. 안 가 본 곳은 보이지 않는다

## 4. 설계

### 4-1. 시작화면

- 뒤: 생성기로 만든 작은 지도(시드 고정, 64×40칸쯤)를 `TileArt`·`PropArt`·빛 지도로 그려, 카메라가 초당 몇 픽셀로 천천히 흐른다. 생물군계 띠는 그 기기의 캐릭터가 가 본 것들 가운데서 고른다(처음엔 흙 동굴만). 화면 위·아래를 어둡게 덮어 글자가 읽히게
- 로고: 도트 글자 로고(그림)가 천천히 위아래로 떠 있고 반짝임이 지나간다. 누르면 작은 장치(박쥐가 날아간다 등)
- 단추: **상자 없이 글자만**. 올리거나 고르면 글자가 조금 커지고 금빛으로 밝아지며 양옆에 작은 표지(◆)가 붙고 틱 소리. 휴대폰은 글자 줄 전체가 44pt 높이의 누르는 자리. 단추는 `Ui.Button`의 새 모양(`Look.Plain` 같은 것)으로 그려 시험이 그대로 누른다
- 단추 순서: [게임 시작] [함께 하기] [저장본] [설정] [의견 보내기] (+[끝내기] 웹 밖). 웹 점검이 이 이름에 기대므로 이름은 그대로
- 아래 구석: 버전, 내보내기 알림

### 4-2. 캐릭터 고르기 → 월드 고르기

테라리아·코어키퍼처럼 **캐릭터 먼저, 그다음 월드**.

- [게임 시작] → **캐릭터 목록**. 칸마다: 외형(걷는 모습), 이름, 레벨·클래스, 플레이 시간, 마지막으로 한 날. 최근 쓴 순. 맨 아래 [+ 새 캐릭터]. 칸 옆에 [지우기] → "「이름」을 지운다(되돌릴 수 없다)" [지우기]/[남기기]
- **새 캐릭터 만들기**: 이름 칸, 외형 고르기(항목마다 ◀ ▶, 미리보기가 돌며 걷는다), [주사위], [만들기]/[뒤로]. 만들다 뒤로면 "만들던 캐릭터를 버린다" 확인
- 캐릭터를 고르면 → **월드 목록**. 칸마다: 월드 이름, 생물군계 아이콘(가장 깊이 간 곳), 날짜(게임 속 날), 플레이 시간, 마지막으로 한 날, 이 캐릭터로 가 본 월드 표시. 맨 아래 [+ 새 월드]. 칸 옆 [지우기]
- **새 월드 만들기**: 이름(무작위 기본, 주사위), 시드(비우면 무작위), 하루 길이. [만들기]
- 월드를 고르면 → 로딩 → 게임. [혼자 하기] 단추는 월드 목록 화면 아래에 남겨 웹 점검이 누를 수 있게(고른 월드로 시작)
- [함께 하기]: 캐릭터를 고른 뒤 [온라인 방 만들기](월드 고르기로) 또는 [코드로 참가]
- 저장본에 메타 정보를 더한다: 캐릭터는 외형, 플레이 시간, 만든 때·마지막으로 한 때(밖에서 넘겨받은 유닉스 초), 가 본 생물군계. 월드는 만든 때, 플레이 시간, 마지막으로 한 때(이미 있는 `SavedAt`). 목록은 저장본 머리만 읽어 빠르게

### 4-3. 메뉴

- 하위 화면(함께 하기·저장본·설정·키)과 게임 안 허브를 같은 모양으로: 흐르는 배경(게임 안에서는 어둡게 깐 게임 화면) 위에 얇은 금빛 테의 반투명 판, 머리에 화면 이름, 항목은 글자형 단추와 "이름: 값" 줄. 고른 줄이 밝아진다. 설정의 "이름: 값" 형식과 단추 이름은 그대로(시험)
- 허브: 가운데 큰 글자 목록(가방·스킬·특성·캐릭터·일지·지도·설정·나가기) + 아이콘. [나가기]는 확인 하나("저장하고 나간다")

### 4-4. 로딩

- 월드 만들기를 **여러 프레임에 나눈다**(생성기 단계마다 넘겨주는 길, 결과는 지금과 똑같아야 한다 — 시험으로 묶는다). 혼자 하기·호스트는 만든 지도를 내 클라이언트도 같이 써서 두 번 만들지 않는다
- 화면: 가 본 곳의 장면 그림(그 캐릭터가 가 본 생물군계 가운데 하나의 방을 게임 그림으로, 만난 몬스터 한둘), 단계 문구("동굴을 파는 중", "광맥을 묻는 중", "마을을 세우는 중"…), 진행 막대, 팁 한 줄(6~8초마다, 누르면 다음). 팁은 시트(`Data/Strings` 또는 새 `tips.csv`)에, 일지의 "도움말"에서도 다시 본다
- 참가하는 쪽도 Welcome 뒤 지도를 만드는 동안 같은 화면

## 5. 묶음

- **T1 (Core·저장)**: 저장 형식(캐릭터 외형·시간·가 본 생물군계, 월드 만든 때·시간), 저장본 머리 읽기, 외형을 통신으로(협동에서 보임), 생성기 단계 나누기와 한 번만 만들기, 시험
- **T2 (그림)**: 주인공 외형 바꾸기(머리 모양 8, 머리색 9, 피부 5, 눈 6, 윗옷·아랫옷 12색, 모든 무기 모션), 로고, 글자형 단추·표지, 로딩 장면, `ArtPreview` 시안
- **T3 (화면)**: 시작화면·고르기·만들기·로딩·메뉴·허브, `Playtest.cs`·`sweep.js`·PlayMode 사진

외형 고르기의 값은 `Core/Save/CharacterLook.cs`(이 문서와 함께 만듦)에 둔다. 색과 모양은 클라이언트 그림이 정한다.

## T1 만든 것 (2026-11-25)

T1(Core·저장·통신·로딩)이 T3 화면에 넘길 바탕. 화면은 아직 그대로이고, 로딩 동안만 임시로 메뉴 바탕 위에 단계 문구와 막대를 그린다(`GameRoot.DrawLoading`, T3가 바꾼다).

### 저장본의 머리

- **캐릭터 형식 20**: 외형(`CharacterLook`), 플레이한 초, 만든 때·마지막으로 한 때(유닉스 초, 클라이언트가 `DateTimeOffset`으로 넘긴다), 가 본 생물군계(`BiomeVisits` 비트). 옛 저장본은 기본 외형·0초·모름(0)·없음으로 읽는다. 만난 몬스터는 새로 적지 않고 도감의 처치 기록(`RecordKind.Kill`)에서 꺼낸다(많이 잡은 순 12종, 보스는 따로)
- **월드 형식 17**: 머리의 `SavedAt`(= 마지막으로 한 때) 뒤에 만든 때, 호스트가 플레이한 초, 누구든 선 생물군계. 그래서 "가장 깊이 간 곳"을 보여 줄 수 있다(깊이 순서 `BiomeVisits.Order`: 흙 동굴 → 버섯 숲 → 얼음 → 모래 → 수정 → 지하 호수 → 용암 → 공허, 던전 레벨 순). 옛 월드는 다음 저장 때 그때를 만든 때로 적는다
- 세는 곳: 캐릭터는 그 사람의 기기(`WorldClient`: `NotePlayed`, 걸음마다 `Visited`, `SaveCharacter(now)`), 월드는 호스트(`WorldHost`: `NotePlayed`, 틱마다 `NoteVisits`, `SaveWorld(now)`). `GameRoot`가 플레이 중 프레임마다 `Time.unscaledDeltaTime`을 넘긴다. 던전 주머니 안은 생물군계로 세지 않는다

### 목록 (T3가 쓸 것)

- `CharacterSave.ReadSummary(byte[] data, string name = "")` → `CharacterSummary`: `Name`, `Version`, `Look`, `Experience`, `Level`, `Classes`(id), `PlayedSeconds`, `CreatedAt`, `LastPlayedAt`, `Visited`, `VisitedBiomes`, `Monsters`(id, 12까지), `Bosses`(id), `JourneyStep`
- `WorldSave.ReadSummary(byte[] data, string name = "")` → `WorldSummary`: `Name`, `Version`, `Seed`, `Width`, `Height`, `Generator`, `Clock`, `Day`, `CreatedAt`, `PlayedSeconds`, `LastPlayedAt`, `Visited`, `Deepest`(`Biome?`), `ProgressLevel`
- 둘 다 깨진 저장본은 `InvalidDataException`. 시트 없이 읽고 월드를 만들지 않는다
- `Client/SaveList.cs`: `SaveList.Characters()`·`SaveList.Worlds()` → `List<SaveList.Entry<T>>`(`Name`, `Key`, `Summary`, `Broken`, `IsBroken`, `LastPlayedAt`). 최근에 한 것부터, 모르는 옛 것은 그 뒤 이름순, 깨진 것은 맨 뒤(던지지 않는다). `SaveList.DeleteCharacter(name)`(캐릭터, 그 캐릭터의 모든 지도, `hotbar.`·`locks.`·`skillorder.`·`skillorder3.`·`guide.`·`guidepassed.` 설정), `SaveList.DeleteWorld(name)`, `SaveList.CharacterPrefs(name)`
- `BiomeVisits`: `Order`, `Bit`, `Mark`, `Has`, `In(visited)`, `Deepest(visited)`. 시작화면 배경과 로딩 그림이 "가 본 곳만" 고를 때 쓴다
- 고르기: 지금처럼 `GameRoot.PlayerName`·`WorldNameField`에 이름을 넣고 `StartSolo()`/`StartHost()`. 새 캐릭터의 외형은 시작 전에 `GameRoot.NewLook`(저장된 캐릭터는 제 외형을 쓴다)

### 협동의 외형

- 통신 0.108.0: `JoinMessage.Look`(참가하는 사람의 `CharacterLook.Packed`) → 호스트의 `Player.Look` → `RosterMessage.Looks`(이름과 같은 길)
- 화면: `EntityView.Look`(다른 플레이어), `WorldClient.LookOf(playerId)`(나와 남 모두), `WorldClient.Look`(나). 명단이 오기 전에는 기본 외형. `WorldView`의 그리기는 그대로다(T2가 `HeroArt`로 외형을 그린 뒤 여기서 읽는다)

### 로딩과 한 번만 만들기

- `WorldGenerator.Steps(seed, width, height, story, arenas, version, terrain)` → `IEnumerable<GenerationStage>`(`Key`, `Fraction`, 마지막 `Done`에만 `World`). `Generate`는 이것을 끝까지 돌 뿐이라 결과가 같다(시험: 판 1~7, 시드 1~3, 256칸에서 지도 해시·아레나·장소·방·볼거리·랜드마크가 같다). 단계 열쇠는 `GenerationStage`의 상수(`caves`, `biomes`, `rooms`, `floors`, `ways`, `features`, `ore`, `pools`, `places`, `arenas`, `furnish`, `pois`, `rails`, `join`, `props`, `cellars`, `water`, `pocket`, `done`), 문구는 `GameRoot.LoadText(stage)`(`strings.csv`의 `load.*`, 한국어·영어)
- `WorldGeneration`(한 단계씩: `Step()`, `Stage`, `Fraction`, `World`, `Done`), `Client/WorldLoader.cs`(프레임마다 12ms까지 단계를 돈다. 웹은 스레드가 없다)
- 혼자·호스트: `StartHosting`은 저장본을 읽고 로더를 시작만 한다. 다 만들면 `FinishHosting`이 `WorldHost(..., generated: 만든 것)`을 세우고, 내 클라이언트는 만든 직후의 사본(`GeneratedWorld.Copy`, 지도만 복사)을 `WorldClient.Made`로 받아 다시 만들지 않는다(`MapReused`). 호스트를 만드는 동안 온 참가자의 메시지는 들고 있다가 넘긴다
- 참가: `WorldClient.DeferMap = true`면 Welcome 뒤 지도를 `MapGeneration`으로 한 단계씩 만들고(`StepMap`), 그 사이 온 메시지(칸 바뀜, 명단, 스냅숏)를 들고 있다가 차례로 받는다. 같은 로딩 화면이 나온다
- 화면이 읽을 것: `GameRoot.Loading`, `LoadStage`, `LoadFraction`. 플레이 시험 보고(`report`)에 `loading`, `loadStage`, `loadFraction`. 만드는 중에 `solo`를 다시 보내면 `error solo: still making the world`
- PlayMode 시험은 시작 뒤 고정 1초 대신 `TestSaves.UntilPlaying(root, then, timeout = 15)`로 기다린다

### 잰 값 (데스크톱 .NET, `CoreSandbox --loadtime 1,2,3,4,5`, 1024칸, 7판)

| | 전 | 뒤 |
|---|---|---|
| 혼자 하기 시작(생성 + 호스트 + 내 클라이언트) | 1220 ms, 한 프레임 안에서 지도를 두 번 | 528 ms, 지도는 한 번 |
| 가장 긴 한 프레임 | 1.2~1.8초 전부 | 데워진 뒤 단계 하나 약 0.1초(방 잇기 80~150ms, 장소 50~110ms, 선로 40~120ms). 처음 한 번(JIT)은 300~400ms까지 |
| 다 만든 뒤 호스트와 클라이언트를 세우는 프레임 | — | 15 ms(가장 긴 21 ms) |

1판 동굴(옛 월드)은 동굴 만들기가 한 단계라 350~500ms가 한 프레임이다(`--loadtime --gen 1`). 웹은 이 값의 몇 배다. 단계 무게(`GenerationStage.WeightOf`)는 위 값으로 정해서 막대가 고르게 간다.

### T2·T3에 넘길 것

- T2: 외형을 그릴 때 `EntityView.Look`·`WorldClient.LookOf`를 읽으면 된다. `CharacterLook.cs`의 상수(가짓수)는 T1이 바꾸지 않았다
- T3: 로딩 화면은 `DrawLoading`을 바꾸면 된다(`DrawScreen`이 `Loading`일 때 부른다). 로딩 그림은 고른 캐릭터의 `CharacterSummary.VisitedBiomes`·`Monsters`로. 캐릭터·월드 목록은 `SaveList`, 지우기 확인 뒤 `SaveList.DeleteCharacter`/`DeleteWorld`. 지금의 [저장본] 화면(`SaveMenu`)의 지우기는 손대지 않았다(설정은 남긴다)

## 6. 자료 목록

[1] Terraria Wiki, 1.4.0.1 — https://terraria.wiki.gg/wiki/1.4.0.1
[2] Terraria Wiki, 1.4.5 — https://terraria.wiki.gg/wiki/1.4.5
[3] Terraria Wiki, Splash screen — https://terraria.wiki.gg/wiki/Splash_screen
[4] Terraria Wiki, Sound IDs — https://terraria.wiki.gg/wiki/Sound_IDs
[5] Terraria Wiki, Characters — https://terraria.wiki.gg/wiki/Characters
[6] Terraria Wiki, World — https://terraria.wiki.gg/wiki/World
[9] Terraria Wiki, World generation — https://terraria.wiki.gg/wiki/World_generation
[10] Terraria Wiki, Tips — https://terraria.wiki.gg/wiki/Tips
[12] Core Keeper Quality of Love 업데이트 — https://www.gamedeveloper.com/press-release/core-keepers-quality-of-love-update-introduces-visual-improvements-musical-instruments-seasonal-valentines-content
[13] Core Keeper 지우기 방법 — https://nanaon.tamura-shippo.com/en/entertainment/game/core-keeper/delete/
[14] Core Keeper 캐릭터 만들기 — https://nanaon.tamura-shippo.com/en/entertainment/game/core-keeper/character-making/
[15] Core Keeper Paws & Claws 패치 노트 — https://bisecthosting.com/blog/core-keeper-paws-claws-update-patch-notes-bug-fixes
[16] Core Keeper Wiki, World — https://core-keeper.fandom.com/wiki/World (검색 요약)
[17] Core Keeper 1.0 — https://gamespress.com/Core-Keeper-10-Update-Introduces-Revamped-World-Generation-PvP-A-New-B
[18] Core Keeper 월드 생성 옵션 — https://siliconera.com/all-core-keeper-world-generation-options-explained (검색 요약)
[21] Stardew Valley Wiki, Secrets — https://stardewvalleywiki.com/Secrets
[22] Stardew Valley Wiki, Options — https://stardewvalleywiki.com/Options
[23] Don't Starve Wiki, World Generation Screen — https://dontstarve.wiki.gg/wiki/World_Generation_Screen
[24] Starbounder, Getting Started — https://starbounder.org/Guide:Getting_Started/1.0
[25] McBride-Charpentier, Game Design Rules: Loading Screens — https://www.gamedeveloper.com/design/game-design-rules-loading-screens
[26] Netflix Korean Timed Text Style Guide — https://partnerhelp.netflixstudios.com/hc/en-us/articles/216001127
[28] Apple UI Design Dos and Don'ts — https://developer.apple.com/design/tips/
[29] Android 접근성(터치 대상) — https://support.google.com/accessibility/android/answer/7101858
[31] Game Accessibility Guidelines — https://gameaccessibilityguidelines.com/full-list/
[32] Nielsen, Confirmation Dialogs — https://www.nngroup.com/articles/confirmation-dialog/
[33] GAG, 배경 움직임 끄기 — https://gameaccessibilityguidelines.com/provide-an-option-to-turn-off-hide-background-movement/
[34] W3C SC 2.3.3 — https://www.w3.org/WAI/WCAG21/Understanding/animation-from-interactions.html
