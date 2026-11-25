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

## T2 만든 것 (2026-11-25)

그림만 만들었고 화면에는 아무것도 걸지 않았다(T3가 쓴다). `dotnet run --project DevTools/ArtPreview -- --only title`이 `out/title/`에 시안을 쓴다.

**주인공 외형** (`Client/Art/HeroArt.Looks.cs`, `HeroArt.cs`)

- `HeroArt.Draw(Look, HeroMove, frame, HeroGear, CharacterLook, blink)`가 외형대로 그린다. 서기·걷기·세 공격·시전·찌르기·펼치기·마시기·맞기·쓰러짐·일어남·구르기, 다섯 무기 모두 같은 리그를 쓰므로 따로 손댄 모션은 없다
- 머리 모양 8: 짧은 머리(지금 그림) · 긴 머리 · 묶은 머리 · 뾰족 머리 · 깎은 머리 · 양갈래 · 단발 · 올림머리. 모양마다 머리 그림을 다섯 방향으로 다시 그렸다. 긴 머리와 꼬리는 머리 여덟 줄 아래로 내려와 몸통 위에 덮이고, 뾰족 머리와 올림머리는 위로 두 줄 올라간다(머리와 함께 기울고 숙인다)
- 머리색 9(갈색·검정·금빛·붉은빛·은빛·파랑·초록·분홍·보라), 피부 5, 눈 6(검정·갈색·파랑·초록·잿빛·붉은빛), 윗옷 12, 아랫옷 12. 윗옷은 `Ramp.Of`의 밝음·그늘·어둠 세 단계, 피부는 세 단계, 머리는 두 단계, 아랫옷은 먼 다리가 한 단계 어둡다
- `CharacterLook.Default`는 지금 주인공과 픽셀까지 같다. 미리보기가 모든 모션·무기·방향의 5,545프레임을 옛 그림(`Rgba tunic` 오버로드, `#3F7FD8`)과 견주고 하나라도 다르면 실패한다. 그래서 아랫옷 0번(지금의 짙은 갈색)만 먼 다리를 어둡게 하지 않는다
- 옛 `Draw(..., Rgba tunic, blink)`는 그대로 남아 "기본 외형 + 그 색 윗옷"을 그린다(외형을 모르는 다른 플레이어, 마을 사람 대신 그리는 자리)
- 이름표: `HeroArt.HairNames`·`HairColorNames`·`SkinNames`·`EyeNames`·`ShirtNames`·`PantsNames`(한국어), 색 견본: `HairColor(i)`·`SkinColor(i)`·`EyeColor(i)`·`ShirtColor(i)`·`PantsColor(i)`. 스프라이트 키에 넣을 이름은 `HeroArt.Key(look)`. 물감(팔레트)은 외형마다 한 번 만들어 두고, 그린 그림은 지금처럼 부르는 쪽이 `Sprites`에 키로 묶어 둔다
- 모자람: 머리가 위로 올라가는 두 모양에서 `Joints.Top`(이름표 자리)은 두 픽셀 낮게 나온다. 눈 색은 1×2픽셀이라 가까이 봐야 구별된다(만들기 화면의 미리보기는 3배 이상을 권한다)

**제목 그림** (`Client/Art/TitleArt.cs`)

- 로고 `TitleArt.Logo`(69×23, `LogoWidth`×`LogoHeight`): 두꺼운 금빛 글자 "pray◆to", 하이픈은 깎은 보석, 검은 테와 돌빛 그림자. 2배·3배에서 읽힌다. 반짝임은 `LogoGlint(frame)`(빛만 있는 덧그림, `GlintFrames` 12걸음)을 로고 위에 겹친다. 한글 부제는 글자로 쓴다
- 글자 단추: 상자 없음. 글자색 `Ink(state)`(쉬는·올린·고른·누른)와 그림자 `InkShadow`, 못 고르는 글자 `InkOff`, 커지는 비율 `GrowHover`·`GrowSelected`. 양옆 표지 `Mark(state)`(◆ 7×7, 쉬면 null), 아래 빛 띠 `Strip(width, state)`(높이 5, 양끝이 사라진다), 값 줄의 화살표 `Arrow(left, state)`, 새긴 꺾쇠 `Bracket(left)`
- 판 `Panel`(아홉 조각, 테 `PanelBorder` 6): 밤빛 반투명(`PanelAlpha` 178) 위에 얇은 금빛 테와 모서리 보석. 화면 이름 아래 줄 `Divider(width)`
- 목록 줄 `Row(state)`(아홉 조각, 테 `RowBorder` 4): 쉬는 줄은 옅은 띠, 올리면 왼쪽 금빛 막대, 고르면 금빛 테. 새로 만들기 줄 `NewRow(width, height, state)`(점선 테, 크기대로 그린다)와 `Plus(state)`
- 생물군계 아이콘 `BiomeIcon(biome)` 16×16 여덟(`BiomeOrder`: 흙 동굴·버섯 숲·얼음·모래·수정·호수·용암·심연)

**로딩 장면** (`Client/Art/LoadingArt.cs`)

- `LoadingArt.Scene(biome, seed, monsters, hero, width, height)`: 그 생물군계의 작은 굴을 지어(바닥·벽, 생물군계 소품 다섯까지, 꾸밈, 호수·용암은 웅덩이, 작은 생물) 게임 그림(`TileArt`·`StandArt`·`CreatureArt`·`CritterArt`)과 게임의 빛(`LightMap`, 생물군계 빛 색)으로 그리고 160×90으로 자른다. 같은 생물군계와 시드는 늘 같은 그림
- `monsters`는 보여 줄 몬스터의 id와 색(셋까지). null이면 그 생물군계의 둘(`MonstersOf`), 빈 목록이면 없음. **만난 몬스터만 보이기는 화면이 정한다**: 이 함수는 받은 것만 그린다. `hero`를 주면 그 외형의 주인공이 서 있다
- 부를 때마다 새로 그리니(빛 지도까지 계산한다, 걸리는 시간은 재지 않았다) 로딩에 들어갈 때 한 번 그려 스프라이트로 둔다

**시안** (`out/title/`): `looks.png`(머리 모양 × 다섯 방향 × 세 색 조합, 선택지마다 색, 기본 외형과 옛 그림 비교), `looks_moves.png`(무작위 외형 여섯의 걷기·검·활·지팡이 공격·구르기·쓰러짐), `logo.png`·`logo_glint.png`, `buttons.png`(글자 단추 네 상태, 화살표·꺾쇠, 판·줄·새로 만들기), `biome_icons.png`, `loading_<생물군계>.png` 여덟(왼쪽 몬스터만, 오른쪽 주인공과 함께).

## T3 만든 것 (2026-11-25)

T1의 목록·로딩과 T2의 그림을 화면에 걸었다. 코드는 `Client/GameRoot.Title.cs`(시작화면과 쪽들), `Client/GameRoot.Loading.cs`(로딩), `Client/TitleDraw.cs`(그리는 도우미), `Client/TitleBackdrop.cs`(흐르는 배경), `Client/Tips.cs`(도움말), `Core/Save/NewWorld.cs`(월드 이름 주사위와 시드). `GameRoot`는 `partial`이 되어 옛 메뉴 코드(약 360줄)가 빠졌다.

### 화면

- **시작화면**: 가 본 생물군계마다 `LoadingArt`의 굴(256×144)을 이어 붙인 띠를 한 번 텍스처로 그리고(처음 기기는 흙 동굴 둘), 화면 높이를 채우는 정수 배로 창만 초당 5픽셀 옮긴다. 불빛은 따뜻한 빛이 천천히 숨 쉬고, 위·아래는 어둡게 덮는다. "움직임: 줄이기"면 한 장면에 멈추고 로고도 서 있다. 생물군계는 이 기기의 모든 캐릭터 저장본의 `Visited`를 합친 것. 로고는 `TitleArt.Logo`를 화면 폭에 맞는 정수 배로, 2점 남짓 오르내리고 6초마다(누르면 곧) 반짝임이 지나간다. 그 아래 부제, 다섯 글자 단추(+웹 밖 [끝내기]) 뒤에는 옅은 어둠 구름(`TitleDraw.Halo`, 시안에서 밝은 소품 위 글이 흐려 더함). 가로 휴대폰은 로고 왼쪽, 단추 오른쪽. 버전은 오른쪽 아래, 내보내기 알림은 왼쪽 아래(닿으면 단추 아래)
- **글자 단추** `Ui.Look.Text`: `Ui.Button`으로 그려 시험이 그대로 누른다. 상태마다 `TitleArt.Ink`, 그림자, 커지기(`GrowHover`·`GrowSelected`, 움직임 줄이기와 시험 중에는 안 커진다), 양옆 `Mark`, 아래 `Strip`, 올리면 틱 소리(`UiMotion.Hover`). PC는 마우스가 지나간 단어가 골라지고 ↑↓로 옮기고 Enter로 누른다(목록 줄도). 휴대폰은 줄마다 44점
- **캐릭터 목록**(`캐릭터 고르기`): 최근 한 순. 줄마다 걷는 외형(움직임 줄이기면 서 있음), 이름, "Lv · 클래스", "플레이 시간 · 오늘/어제/n일 전", 가장 깊이 간 생물군계 아이콘, [지우기]. 읽지 못하는 저장본은 흐리게 까닭을 적고 [지우기]만. 맨 아래 점선의 [새 캐릭터]
- **새 캐릭터 만들기**: 미리보기(PC 5배, 휴대폰 4배, 가로 휴대폰 3배)가 여덟 방향을 돌며 걷는다. [이름 칸](16자, 처음엔 "모험가", 겹치면 "모험가 2"…), 외형 여섯 줄(머리 모양·머리색·피부·눈·윗옷·아랫옷, ◀ ▶와 이름, 색 견본), 클래스 안내(`menu.classes_new`). 아래에 [주사위]·[만들기]. 이름이 비었거나 있는 캐릭터면 알린다. 바꾼 뒤 [뒤로]·Esc는 "만들던 캐릭터를 버린다" [버리기]/[계속 만들기]
- **월드 목록**(`월드 고르기`): 위에 고른 캐릭터(외형·이름·레벨)와 "{이름}의 클래스: …"(새 캐릭터는 "클래스는 게임 안 [스킬] 화면에서 고른다"). 줄마다 가장 깊이 간 생물군계, "n일째 · 플레이 시간", "오늘 · 가 본 월드"(이 캐릭터의 지도 저장본이 그 시드로 있으면), [지우기]. 맨 아래 [새 월드], 쪽 아래에 늘 보이는 [혼자 하기](금빛 틀의 주 동작). 줄을 누르면 고르고 고른 줄을 다시 누르면 시작
- **새 월드 만들기**: [월드 이름 칸](무작위 두 낱말, [주사위]), [시드 칸](비우면 무작위, 숫자는 그대로, 글은 FNV-1a로 셈해 어느 기기에서나 같다), [하루 길이: n분], 안내 한 줄, [만들기]
- **함께 하기**: 캐릭터를 먼저 고르고(같은 목록) → [온라인 방 만들기] → 월드 목록(주 동작 [방 열기]) 또는 [코드 칸]+[코드로 참가], PC는 LAN [호스트 (포트 …)](월드 목록으로)·[호스트 주소]·[참가]
- **로딩**: 그 캐릭터가 가 본 생물군계 하나(무작위, 새 캐릭터는 흙 동굴)의 `LoadingArt.Scene`, 거기 사는 몬스터 가운데 그 캐릭터가 잡은 것 둘까지(흙 동굴은 사는 곳이 없는 몬스터도, 큰 몸은 뺀다, 못 만났으면 없음), 그 외형의 주인공. 금테 판 틀, 왼쪽 아래 생물군계 이름, 아래에 단계 문구(`LoadText`), 금빛 막대, 도움말 한 줄(7초마다, 누르면 다음). 참가하는 쪽도 지도를 만드는 동안 같다
- **도움말**: `Client/Tips.cs`의 31줄(`tip.*`, 영어도). 못 가 본 곳·보스의 이름은 없다. 일지의 [목표] 쪽 맨 아래 "도움말" 머리 밑에 전부 다시 나온다(새 구역을 더하면 휴대폰 세로의 목차 탭이 56점보다 좁아져서 쪽 안에 두었다)
- **하위 쪽**(저장본·설정·키 바꾸기, 게임 안 설정도): 흐르는 배경 위 `TitleArt.Panel`, 머리에 쪽 이름과 `Divider`, 왼쪽에 [뒤로]. 설정·키는 줄마다 왼쪽 이름, 오른쪽 값 글자 단추(시험이 읽는 "이름: 값"은 그대로), 올리면 줄이 밝아진다. [하루 길이]는 새 월드 쪽과 설정의 [플레이]에 있다(설정 값 하나라 이미 있는 월드에도 쓴다)
- **허브**(게임 안 [메뉴]·Esc): 자리와 칸(`HubLayout`, 8칸)은 그대로, 카드 대신 `TitleArt.Panel`, 칸마다 아이콘 아래 글자 단추. [나가기]는 같은 판에 "저장하고 시작 화면으로 나간다"와 [저장하고 나가기](붉은 틀)/[남기]. Esc는 물음을 먼저 닫는다. 나가면 시작화면 첫 쪽
- **다른 플레이어의 외형**: `WorldView`가 나는 `WorldClient.Look`, 남은 명단이 외형을 실어 온 뒤(`WorldClient.KnowsLookOf`, 새로 더함) `EntityView.Look`으로 그린다. 그 전에는 예전 사람마다 윗옷 색. 스프라이트 키에 `HeroArt.Key(look)`

### 시험

- EditMode(`TitleTests`): 명단이 오기 전엔 외형을 모른다, 시드 글(빈 칸·숫자·글·FNV-1a), 무작위 월드 이름이 칸에 맞고 저장 키가 된다. 1136 → 1139
- PlayMode 사진(`TitleScreenTests`): `title`, `title-touch`, `title-characters`, `title-make`, `title-worlds`, `title-newworld`, `title-settings`(각각 `-touch`도), `title-loading`, `title-hub`, `title-hub-ask`, `title-hub-ask-touch`
- 웹 점검: 새 구역 `title`(PC·휴대폰 세로·가로, `Docs/Playtest.md`). 시작 흐름이 바뀌어 고친 것: `menu`([게임 시작] 쪽에 있던 [혼자 하기]·[하루 길이]·클래스 안내를 각각 월드 목록·새 월드·새 캐릭터 쪽에서 본다, [함께 하기]는 캐릭터를 고른 뒤, 진짜 클릭 시작은 [새 캐릭터] → [이름 칸]에 "탭시험" → [만들기] → [새 월드] → [만들기] → [혼자 하기]), `save`(캐릭터 목록 줄에 레벨·시간·"오늘", savetest를 눌러 월드 목록에서 "savetest의 클래스: 전사 + 화염 마법사"), `layout`(월드 목록의 [혼자 하기]가 처음부터 보이고 옆으로 넘치지 않는다), `time`([하루 길이]를 설정에서), `ui`(주 동작 사진은 월드 목록), 메뉴로 시작하던 스무 곳과 탭 네 곳은 `lib.js`의 `startThroughMenu` 하나로, `leaveGame`은 [저장하고 나가기]까지. `playtest.js`·`h.startSolo`의 `solo` 명령은 그대로(기본 외형)
- 시안: `dotnet run --project DevTools/ArtPreview -- --only title` → `out/title/screens_pc.png`, `screens_phone.png`, `screens_phonewide.png`, 화면마다 `screen_<pc|phone|phonewide>_<title|characters|make|worlds|loading|hub>.png`. 도구에 한글 글꼴이 없어 글은 영어 시트의 말을 대문자로 쓰고, 목록이 넘칠 때 화면처럼 잘리거나 굴러가지 않는다

### 다르게 한 것

- 배경은 생성기의 작은 지도 대신 로딩 그림과 같은 `LoadingArt` 굴을 이었다. 게임의 칸·소품·빛·작은 생물을 그대로 쓰면서 생물군계를 고르기 쉽고, 시트 없이 바로 그린다
- 새로 만든 캐릭터·월드는 [만들기]에서 바로 파일이 되지 않는다(저장 형식이 플레이로 만든다). 처음 들어가 저장될 때까지 목록 맨 위에 "아직 들어가지 않았다"로 남는다(이 실행 동안만)
- [혼자 하기]·[만들기]·[방 열기]는 글자 단추가 아닌 금빛 틀(`Ui.Look.Primary`)이다. 쪽마다 하나인 주 동작이 눈에 띄게(`Docs/UiRework.md` 단추 세 가지)
- 허브는 큰 글자 세로 목록 대신 지금의 칸 배치를 지켰다(`LayoutTests.EveryScreenFits`의 8칸·40점·겹침 확인)
- 로고를 누르면 반짝임만 다시 지나간다(박쥐 같은 장치는 없다)

### 남은 것

- 외형 선택지 이름(`HeroArt.HairNames` 등)은 영어에서도 한국어다
- 키보드로 고른 줄이 목록 밖이면 목록이 따라 굴러가지 않는다
- 배경 띠를 만드는 데 드는 시간은 웹에서 재지 않았다(생물군계 수만큼 `LoadingArt.Scene`, 처음 그릴 때 한 번). 웹 점검 `title`의 사진과 사람이 본 첫 화면의 멈칫으로 본다
- 사람에게: 흐르는 빠르기와 멀미, 글자 단추의 손맛과 틱 소리, 진짜 휴대폰에서 이름 칸에 적기(화면 글쇠판), 도움말을 읽을 시간, 다른 사람과 협동할 때 서로의 외형

### 웹 점검 뒤 고친 것 (2026-11-25)

배포 `10f96b5`의 점검(`title,menu,save,layout,time,ui,touch`)에서 28개가 실패했다. 원인과 고친 곳:

- **휴대폰이 첫 탭 전까지 PC 배치였다**: `Controls.UsingTouch`가 `Application.isMobilePlatform`(브라우저의 사용자 에이전트)만 봤다. 사용자 에이전트가 PC인 터치 기기(점검의 휴대폰, 맥이라고 말하는 iPad)는 첫 탭에서야 터치가 되고, 그때 시작화면 글자 단추가 32(가로)·38(세로)점에서 44점으로 바뀌며 줄이 움직여 손가락 밑의 [게임 시작]이 빠져나갔다(가로 휴대폰의 흐름 전체와 `touch` 구역의 연쇄 실패, `layout`의 740×360). 이제 브라우저의 주 포인터가 손가락이면(`(pointer: coarse)`, `Display.jslib`의 `GetCoarsePointer`) 처음부터 터치다. 44점 줄은 그대로 화면에 들어간다(가로는 로고 왼쪽, 글자 오른쪽)
- **PC 웹에서 한글을 적을 수 없었다**: Unity 웹 플레이어는 키를 한 번 누를 때마다 글자로 읽어서, 입력기가 짜 맞추는 한글·일본어·중국어는 IMGUI 칸에 들어가지 않는다("적은 이름으로 캐릭터를 만든다"가 "플레이어"). 이제 웹의 PC에서는 칸을 누르면 페이지의 진짜 글 상자(`Display.jslib`의 `TextInput*`)가 칸 위에서 키를 받고, 게임은 그 값과 끝의 커서를 그린다(`WebText.cs`, `TitleDraw.Field`). Enter·Esc·Tab이나 다른 곳 누르기로 끝난다. Unity의 키 듣기가 상자에서 글자를 빼앗지 않게 `index.html`이 그보다 먼저 듣는다. 휴대폰 브라우저는 Unity의 화면 글쇠판 상자를 그대로 쓴다. 펫 이름 칸(`InventoryWindow.Character`)과 클라우드 아이디 칸은 아직 예전 칸이다
- **칸의 글이 위에 붙었다**(휴대폰의 높은 칸): `TitleDraw.Field`의 글을 칸 높이 가운데에
- **허브 판 사이로 게임이 비쳤다**: 사람 이름과 시계가 아이콘 사이에 겹쳐 읽혔다. 판 밑을 78% 검게 덮는다(`ScreenStack.DrawHub`, 50%로는 여전히 비쳤다)
- **점검 도구**: 긴 응답(`buttons`)이 여러 줄로 오는데 바쁜 기계에서 마지막 조각이 늦으면 앞부분만 읽혀 설정의 [움직임]이 "없음"이었다. `h.ask`가 JSON이 다 될 때까지 기다린다. `h.press`는 누른 뒤 게임의 프레임을 기다리고(`h.frames`), 휠로 굴려 찾은 단추는 멈춘 것을 본 뒤 누른다([하루 길이]의 첫 누름이 빠졌다). `h.typeInto`도 칸이 상자를 연 프레임 뒤에 적는다

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
