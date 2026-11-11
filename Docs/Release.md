# 출시 계획 초안

조사 보고서 후보 18·19·13(`Docs/Research2026-11.md`, 2026-11-11). 계정을 만들고 돈을 내고 공개하는 일은 사람이 정하고 한다. 이 문서는 그 결정을 돕는 초안이고, 클라우드가 미리 해 둘 수 있는 것(문구 초안, 의견 단추, CI 자동 수정)은 해 두었다.

## 순서

조사 결과(How To Market A Game의 Next Fest 설문, GameDiscoverCo)를 따른 순서다. 위시리스트의 68~88%는 데모를 하지 않고 스토어 페이지만 보고 누른다. 그래서 페이지가 먼저다.

1. **한 줄 훅을 정한다**(사람): 아래 후보 가운데 하나를 고르거나 고친다. 캡슐 이미지와 트레일러 첫 5초가 이 한 줄을 보여 줘야 한다
2. **Steam 페이지(한·영)**(사람: Steamworks 계정, 앱 등록비 100달러): 아래 소개 문구 초안, 스크린숏 6장 이상(웹 점검 사진에서 고를 수 있다), 캡슐 이미지, AI 사용 공개
3. **웹 데모를 앞단 깔때기로**: 웹 데모는 Steam 데모를 대신하지 않는다. 메뉴와 끝 화면에 "Steam에서 찜하기" 링크를 둔다(페이지 주소가 생기면 클라우드가 넣는다)
4. **문자열 표**(후보 17): 영어 페이지와 영어 데모 전에 화면 문구를 표로 뺀다(`Docs/Strings.md`)
5. **작은 축제와 국내 창구**(사람): BIC(지난해 4월 접수·8월 행사), 스토브인디, 스트리머 협업. 데모를 몇 달 다듬는다
6. **Steam 데모**(사람): Next Fest보다 먼저 낸다. "데모를 Next Fest와 함께 처음 내지 말라"
7. **Next Fest는 위시리스트 약 2,000개 뒤**: 1,000개 미만이면 축제 동안 중앙값 462개뿐이었다

## 한 줄 훅 후보

- "링크 하나로 폰과 PC가 같은 동굴에" — 웹·휴대폰·PC 협동을 앞세운다(방 코드, `Docs/Multiplayer.md`)
- "전쟁이 끝난 뒤, 동굴 깊은 곳에서 무언가 다시 깨어났다" — 이야기와 장소를 앞세운다(`Docs/Story.md`)
- "캐고, 짓고, 기르고, 친구와 보스를 쓰러뜨리는 동굴 생활" — 장르를 그대로 말한다(가장 무난하지만 "코어키퍼 아류"로 보일 위험)

조사 보고서는 첫째나 둘째를 권한다. 2025년 10월 Next Fest에서 돌파한 게임은 한눈에 이해되는 훅을 가졌고, 공통점은 협동과 생존이었다.

## 소개 문구 초안

**한국어(짧게)**: 마왕과의 전쟁이 끝난 지 일 년, 동굴 깊은 곳에서 괴물이 다시 늘고 있다. 곡괭이 하나로 시작해 광석을 캐고, 기지를 짓고, 밭과 공장을 돌리고, 친구와 함께 깨어난 수호자들을 쓰러뜨리자. 링크 하나면 폰과 PC가 같은 동굴에서 만난다.

**English (short)**: A year after the war against the Demon King, monsters are stirring again deep in the caves. Start with a pickaxe, mine ore, build a base, run farms and factories, and take down the reawakened guardians with friends. One link puts phones and PCs in the same cave.

**특징(한·영 나란히 쓸 것)**
- 협동: 웹·휴대폰·PC가 방 코드 하나로 함께(Unity Relay)
- 큰 이야기 4장과 곁이야기, 고른 말에 따라 바뀌는 대사
- 보스 여덟, 어느 보스든 같은 전조 말(범위·돌진·모이기·탄막·부르기)
- 농사·낚시·동물·요리, 자동화 공장, 기지 방어, 공허의 균열
- 하루 길이를 고르는 느긋한 생활(20~60분), 놓쳐도 잃지 않는 날

## AI 사용 공개 문구 초안

Steam은 2026년 1월부터 **플레이어가 보거나 듣는 AI 생성물**을 중심으로 공개를 받는다. pray-to의 그림과 소리는 사람이 정한 규칙을 코드로 그리고 합성한 것이다(`Docs/ArtPipeline.md`). 다만 그 코드와 글의 많은 부분을 AI 코딩 도구가 썼으므로 숨기지 않고 쓰는 편이 낫다.

> 이 게임의 코드와 글 일부는 AI 코딩 도구(Claude Code)의 도움을 받아 썼습니다. 그림과 효과음·음악은 생성형 이미지·음악 모델을 쓰지 않고, 도트와 소리를 코드로 그리고 합성했습니다. 모든 내용은 개발자가 정하고 검토했습니다.

> Parts of this game's code and text were written with the help of an AI coding tool (Claude Code). The art, sound effects and music use no generative image or music model: the pixels and sounds are drawn and synthesized by code. The developer decided and reviewed everything.

## 의견 단추와 단계 기록(후보 13)

외부 분석 서비스를 고르는 것은 사람의 결정(개인정보 고지, 비용)이라 서비스는 붙이지 않았다. 대신 서비스 없이 되는 만큼 넣었다(`Client/Feedback.cs`).

- **단계 기록은 이 기기에만**: 로드 → 첫 이동 → 첫 채굴 → 첫 제작 → 첫 전투 → 첫 보스 전조 → 첫 보스 처치에 처음 닿은 플레이 시간을 이 기기에 적는다. 어디에도 보내지 않는다
- **[의견 보내기]**(메뉴): 버전, 기기(웹·PC·휴대폰), 플레이 시간, 위 단계 기록을 채운 GitHub 이슈 쓰기 화면을 연다. 사람이 읽고 직접 [Submit]을 눌러야 보내진다(GitHub 계정 필요)
- 나중에 서비스를 고르면(예: PostHog, Plausible, 직접 만든 작은 수집 서버) 단계 기록을 그쪽으로도 보내게 할 수 있다. 그때는 메뉴에 고지와 끄기 단추를 함께 둔다

## CI 자동 수정(후보 19)

`.github/workflows/ci-autofix.yml`: main에서 Core 테스트 워크플로가 실패하면 Claude가 실패 로그를 읽고 고친 브랜치(`claude/autofix-<커밋>`)로 풀 리퀘스트를 연다. main에 직접 올리지 않고, 같은 커밋에는 한 번만, 자기가 만든 브랜치의 실패에는 돌지 않는다. 확실한 원인을 못 찾으면 이슈를 연다. 테스트를 끄거나 약하게 하지 말라고 지시해 두었다.

- **켜려면**(사람): 저장소 Settings → Secrets and variables → Actions에 `ANTHROPIC_API_KEY`를 넣는다. 없으면 알림만 남기고 건너뛴다(`LOCAL_TASKS.md` 121단계)
- 쓰는 액션: `anthropics/claude-code-action@v1`. API 사용료는 키 주인의 계정에 든다
