# pray-to

코어키퍼 계열의 탑뷰 2D 액션·생존·크래프팅 게임이다. Unity 6000.6.3f1, 협동 멀티플레이. 기획과 기술 원칙은 `Docs/Design.md`에 있고, 방향이 바뀌면 그 문서를 먼저 고친다.

## 작업 분담

- **클라우드 세션**: 코드(`.cs`, `.asmdef`), 데이터, 테스트, 문서. Unity 에디터 없이 `dotnet`으로 컴파일·테스트한다. 푸시할 때마다 `CLOUD_LOG.md` 맨 위에 항목을 같은 커밋으로 추가한다(무엇을 왜 바꿨는지, 요청 대조, 로컬에서 할 일, 비어 있는 로컬 확인 칸). 요청 대조는 사람이 요청한 항목마다 코드·저장·안내 목표·Playtest 장면·문서까지 됐는지 적고, 빠진 것은 빠졌다고 적는다(AI가 말없이 빠뜨리는 것을 잡는다, `Docs/Research2026-11.md` 후보 5).
- **로컬 세션**: Unity 에디터가 필요한 일. 씬, 프리팹, `.asset`, `.meta`, `Packages/packages-lock.json`, `ProjectSettings/`, 아트·사운드를 맡는다. 시작할 때 `git pull` 후 `CLOUD_LOG.md`에서 로컬 확인이 비어 있는 항목을 읽고 체크한다. `LOCAL_TASKS.md`를 위에서부터 처리하고 결과를 같은 파일에 적는다. 에디터 설정과 Unity 안의 테스트는 명령 한 줄(`DevTools/Local/setup.ps1`, `setup.sh`)이 하고 결과도 직접 적는다. 사람이 직접 해야 하는 일은 `LOCAL_TASKS.md`에 따로 모아 둔다.
- **사람 손 없이**: 로컬 컴퓨터에서 감시 스크립트(`DevTools/Local/watch.ps1 -Install`, `watch.sh --install`)를 켜 두면 클라우드가 main에 올릴 때마다 명령 한 줄이 저절로 돌고 결과 커밋을 푸시한다. 화면 캡처(PlayMode `Screens` 분류)는 `local-screens` 브랜치에 덮어써 올라온다. 클라우드는 `git fetch origin local-screens` 후 `git show origin/local-screens:<이름>.jpg`로 사진을 꺼내 화면을 확인하고, 사람에게는 사진으로 볼 수 없는 것(손맛, 소리, 실제 휴대폰 터치)만 맡긴다.
- **클라우드가 대신 해 보기**: 사람이 "테스트해 줘"라고 하면, 사람이 로컬에서 직접 보던 확인을 클라우드가 먼저 한다. 웹 데모 배포를 기다려 `node DevTools/WebPlaytest/playtest.js --wait`로 헤드리스 Chromium에서 PC(키보드·마우스)와 휴대폰(터치) 화면으로 플레이하고, 사진과 `report.md`를 열어 본 뒤 결과를 알린다(`Docs/Playtest.md`). 새 기능을 만들면 `Client/Playtest.cs`의 장면과 `playtest.js`·`sweep.js`의 확인도 더한다. 단추는 `Ui.Button`·`Ui.LayoutButton`으로 그려야 시험이 진짜 클릭·탭으로 누를 수 있다. 사람에게는 사진으로 알 수 없는 것(손맛, 소리, 실제 휴대폰 감각)만 묻는다. 전체 점검(`sweep.js`를 구역 없이)은 사람이 요청할 때만 돌린다. 평소 배포 뒤에는 새로 넣거나 바꾼 기능의 구역만(`--only`) 돌리고 그 사진만 본다(토큰을 아끼려고, 사용자 결정, 2026-11-09).
- 로컬은 `.cs` 파일을 고치지 않는다. 컴파일 에러나 버그는 Console 로그를 결과 보고에 붙이고, 클라우드가 고친다.
- 클라우드는 에디터 작업이 필요하면 `LOCAL_TASKS.md`에 할 일을 추가한다. 가능하면 손 작업 대신 에디터 스크립트로 만들고 `ProjectSetup.SetUp`에서 불러서, 로컬은 명령 한 줄만 다시 실행하게 한다.
- 클라우드 세션은 씬·프리팹·`.asset`·`.meta`·`ProjectSettings/`·`Packages/packages-lock.json`을 Edit/Write로 고치지 못한다(`.claude/hooks/guard-unity-files.sh`가 막는다. 로컬 세션은 막지 않는다).
- 클라우드는 `.meta`를 만들지 않는다. 새 파일의 `.meta`는 로컬이 Unity를 열 때 생기고 로컬이 커밋한다. 단, 클라우드가 파일을 지우거나 옮길 때는 짝이 되는 `.meta`도 같이 지우거나 옮긴다.

## 코드 구조

자세한 설명은 `Docs/Codebase.md`. 지킬 것만 여기에 둔다.

- `Assets/_Project/Scripts/Core` (`Game.Core`): 게임 규칙과 시뮬레이션 전부. UnityEngine을 참조하지 않는다(`noEngineReferences`). netstandard2.1, C# 9
- `Assets/_Project/Data`: 데이터 시트(CSV). Core가 직접 읽는다. 시트마다 형식 문서가 있다(`Docs/Codebase.md`의 목록). 시트를 새로 추가하면 `GameSheets`와 `SceneSetup`에도 넣는다
- `Assets/Tests/EditMode`(Core 테스트, `dotnet test`와 Unity가 같은 파일), `Assets/Tests/PlayMode`(Unity에서만 도는 화면 시험. 새 화면을 만들면 장면과 사진을 더한다)
- `DevTools/`: Unity 없이 빌드·테스트·시뮬레이션하는 .NET 프로젝트(`Game.Core`, `Game.Core.Tests`, `CoreSandbox`, `ArtPreview`, `UnityCompileCheck`, `Local`, `WebPlaytest`). 소스는 `Assets/`에서 가져다 쓰므로 여기에 게임 코드를 두지 않는다. 새 패키지 API는 `UnityCompileCheck/Stubs/`에 패키지와 같은 시그니처로 적는다. `Local`의 짝 스크립트(ps1·sh)는 함께 고친다
- `Assets/_Project/Scripts/Client` (`Game.Client`): 화면, 입력, 네트워크 전송만. 게임 규칙을 두지 않는다. 그림·소리는 `Art/`의 코드로 그린다. UI는 IMGUI, 크기는 `Ui`의 점 단위(`Ui.Width`), 줄바꿈은 `Ui.Wrap`, 입력은 `Controls`(키보드·마우스와 터치)
- 웹 데모: `.github/workflows/web-demo.yml`이 GitHub Pages에 올린다(`Docs/WebDemo.md`)

## 명령

```
dotnet test DevTools/Game.Core.Tests
dotnet run --project DevTools/CoreSandbox -- --loot 25 10000
dotnet run --project DevTools/CoreSandbox -- --progression
dotnet run --project DevTools/CoreSandbox -- --item 25 7
dotnet run --project DevTools/CoreSandbox -- --fight 120 42
dotnet run --project DevTools/CoreSandbox -- --boss 300 42 --players 2
dotnet run --project DevTools/CoreSandbox -- --boss 300 42 --classes warrior,fire_mage
dotnet run --project DevTools/CoreSandbox -- --boss 400 1 --boss-id crystal_queen --gear iron_sword,iron_armor --reaction 0.5
# 그 레벨의 캐릭터처럼: 스킬 포인트, 숙련 특성, 결정을 다 쓴 봇 (Docs/Bosses.md 성장 곡선)
dotnet run --project DevTools/CoreSandbox -- --boss 600 1 --boss-id void_lord --gear obsidian_sword,obsidian_armor,obsidian_helmet,obsidian_boots,obsidian_ring,obsidian_amulet --classes warrior,fire_mage --level 34 --build
dotnet run --project DevTools/CoreSandbox -- --farm 120 mushroom
dotnet run --project DevTools/CoreSandbox -- --economy
dotnet run --project DevTools/CoreSandbox -- --factory 30
# 지도를 숫자로: 막힌 칸, 벽 덩어리, 열린 바닥, 생물군계마다, 방·통로, 걷는 걸음, 생성 시간(Docs/WorldRework.md). --gen 1|2|3|4|5|6|7로 생성기 판 하나만(--flow와 --hour에도. 3판부터 생물군계 소품과 꾸밈의 밀도도, 4판은 랜드마크 수도, 5판은 얼음·모래 동굴의 소품과 랜드마크, 중간 보스의 굴도, 6판은 수정 동굴·지하 호수의 소품과 랜드마크, 물 위의 섬, 굴과 얕은 물도, 7판은 용암 지대·공허의 심연의 소품과 랜드마크, 용암 속 바위, 김 뿜는 틈·금 간 판과 굴도. 새 월드는 7판, Docs/BiomeRework.md B1a·B1c·B2a·B2b·B3a·B3b·B4a·B4b)
dotnet run --project DevTools/CoreSandbox -- --mapstats 1,2,3
# 어느 시스템이 어느 시스템에 재료를 주는지, 고립된 재료(Docs/Systems.md)
dotnet run --project DevTools/CoreSandbox -- --supply
dotnet run --project DevTools/CoreSandbox -- --defense 6 42 --gear iron_sword,iron_helmet,iron_armor,iron_boots --classes warrior --turrets 3
dotnet run --project DevTools/CoreSandbox -- --rift 7 42 --players 2 --gear void_sword,void_helmet,void_armor,void_boots --classes warrior,fire_mage
dotnet run --project DevTools/CoreSandbox -- --skills 60 --gear iron_sword
dotnet run --project DevTools/CoreSandbox -- --supports 60 --gear iron_sword
# 새 캐릭터의 첫 한 시간을 안내대로(상태와 길찾기로 움직이는 봇, Docs/Playtest.md). 시드마다 지표 표와 막힘 보고서
dotnet run --project DevTools/CoreSandbox -- --hour 60 1,2,3,4,5,6,7,8
# 완급 감독의 긴장 곡선(쌓기·절정·풀기·쉬기, 보낸 것, 싸움 사이 시간. Docs/WorldRework.md 묶음 4b). --no-director로 감독 끄기
dotnet run --project DevTools/CoreSandbox -- --pace 30 1,2,3
# 인스턴스 던전 한 판(입구에서 주인과 끝 상자까지 봇이 돈다: 완주 시간, 분당 경험치·금화값, 쓰러짐. Docs/WorldRework.md 묶음 5b)
dotnet run --project DevTools/CoreSandbox -- --dungeon abandoned_mine 1,2,3 --gear copper_sword,copper_armor --classes warrior --level 5
dotnet run --project DevTools/CoreSandbox -- --dungeon spore_depths 1,2,3 --gear iron_sword,iron_armor --classes warrior --level 8
dotnet run --project DevTools/CoreSandbox -- --dungeon frozen_crypt 1,2,3 --gear iron_sword,iron_armor,iron_helmet,iron_boots --classes warrior --level 10
dotnet run --project DevTools/CoreSandbox -- --dungeon buried_temple 1,2,3 --gear iron_sword,iron_armor,iron_helmet,iron_boots --classes warrior --level 10
dotnet run --project DevTools/CoreSandbox -- --dungeon geode_heart 1,2,3 --gear iron_sword,iron_armor,iron_helmet,iron_boots --classes warrior --level 16
dotnet run --project DevTools/CoreSandbox -- --dungeon sunken_aqueduct 1,2,3 --gear mithril_sword,mithril_armor,mithril_helmet,mithril_boots --classes warrior --level 20
dotnet run --project DevTools/CoreSandbox -- --dungeon ember_forge 1,2,3 --gear mithril_sword,mithril_armor,mithril_helmet,mithril_boots --classes warrior --level 25
dotnet run --project DevTools/CoreSandbox -- --dungeon broken_gate 1,2,3 --gear obsidian_sword,obsidian_armor,obsidian_helmet,obsidian_boots --classes warrior --level 32
# 둘이서(--players 2, 봇마다 같은 장비·클래스, 줄 끝에 사람마다 분당 경험치)와 사람처럼 늦게 피하기(--reaction 0.5, --roll-miss 0.3). 들판은 --fight 300 <시드> --rooms <생물군계>(--players 2도). Docs/BiomeRework.md B5a
dotnet run --project DevTools/CoreSandbox -- --dungeon abandoned_mine 1,2,3 --gear copper_sword,copper_armor --classes warrior --level 5 --players 2
dotnet run --project DevTools/CoreSandbox -- --dungeon abandoned_mine 1,2,3 --gear copper_sword,copper_armor --classes warrior --level 5 --reaction 0.5
dotnet run --project DevTools/CoreSandbox -- --fight 300 1 --rooms cave --gear copper_sword,copper_armor --classes warrior --level 5 --players 2
# 함정 방마다 창살이 몇 칸 떨어지나(거리로 자른 것과 가장 좁은 곳, 2·3판 월드. Docs/BiomeRework.md B1c)
dotnet run --project DevTools/CoreSandbox -- --traps 1,2,3

# 도트 그림과 효과음·음악을 DevTools/ArtPreview/out/에 PNG와 WAV로 쓴다(sheet.png, player.png, bosses.png, anim/*.gif, cave.png, cave_unlit.png, arena.png, effects.png, sounds/, music/)
dotnet run --project DevTools/ArtPreview
# 보스 하나의 움직임만 다시(anim/의 GIF, 프레임표, 연속성 보고서. spider, knight, samurai, vein_eater). hero는 주인공의 무기별 모션(hero_*), entrances는 네 보스의 입장만, arenas는 아레나 그림만(arenas.png, arenas_unlit.png, arenas_world.png, arenas/), skillfx는 스킬 이펙트만(skillfx/의 클래스별 프레임표·GIF, Docs/SkillEffects.md), monsterfx는 일반 몬스터의 예고와 이펙트만(monsterfx/의 종별 프레임표·GIF, Docs/Combat.md 몬스터의 공격 시트)
dotnet run --project DevTools/ArtPreview -- --only spider
# 생성된 지도 그림만(out/map/: 생성기 1판·2판, 시드 1~3의 온 지도와 시작 지점, 시드 1의 생물군계마다와 방 그래프, 역할마다의 방(v2_seed1_roles.png, rooms_art.png, rooms_art_lit.png), 새 칸(room_tiles.png), 길과 샛길(v2_seed1~3_roads.png))
dotnet run --project DevTools/ArtPreview -- --only map
# UI 개편 시안(out/uimock/: 양식 A·B·C와 추천 조합의 가방·대화·상점·성장 화면, 휴대폰 세로 가방·대화, compare_abc.png. Docs/UiRework.md 4-4)
dotnet run --project DevTools/ArtPreview -- --only uimock
# 게임의 UI 스킨만(out/ui/: skin.png 조각 전부, windows.png 지금 화면에 입힌 모습, pixels.png 정수 배 틀, encounters.png·encounters_phone.png 상점·상자·작업대·모루·대화 창, growth.png·growth_phone.png 스킬·특성 트리·캐릭터 창, records.png·records_phone.png 일지·지도, bag_hud.png·bag_hud_phone.png 가방·HUD·쓰러짐 카드·메뉴. Docs/UiRework.md U1~U7)
dotnet run --project DevTools/ArtPreview -- --only ui
# 생물군계 개편 시안(out/biomemock/: 이어지는 울타리 fences.png, 입체 규칙 depth.png, 생물군계마다 biome_<id>.png, 색 비교 compare_palettes.png, 크기 sizes.png, 한눈 비교 overview.png. Docs/BiomeRework.md)
dotnet run --project DevTools/ArtPreview -- --only biomemock
# 서 있는 층·이어지는 울타리만(out/depth/: joined.png 다섯 종류 16모양과 문, pen.png·pen_nofade.png 방목장과 흐려짐, place_*.png 장소의 정렬·흐려짐, biomes.png 생물군계 꾸밈. Docs/BiomeRework.md B0)
dotnet run --project DevTools/ArtPreview -- --only depth
# 생물군계의 모습만(out/biomes/: 생물군계마다(cave, mushroom, ice, sand, crystal, lake, lava, abyss) 소품·꾸밈·작은 생물 표 props_<id>.png, 새 월드(생성기 7판)의 방 하나 room_<id>_unlit.png·room_<id>.png(빛 켠 것), 흙 동굴 마을 place_dawn_village.png, 오두막 울타리 place_hero_hut.png, 장소의 건물·꾸밈을 생물군계마다 place_props.png·place_props_ice_sand.png·place_props_crystal_lake.png·place_props_lava_abyss.png·place_<장소>_<cave|mushroom|ice|sand|crystal|lake|lava|abyss>[_lit].png, 랜드마크 landmarks.png·landmarks_ice_sand.png·landmarks_crystal_lake.png·landmarks_lava_abyss.png(_lit도)·landmark_<종류>[_lit].png. Docs/BiomeRework.md B1a·B1c·B2a·B3a·B4a)
dotnet run --project DevTools/ArtPreview -- --only biomes
# 몬스터 크기 등급만(out/sizes/: lineup.png 사람 옆 S·M·L·XL, poses.png 큰 몸의 자세, leaders.png 정예 금빛 테, clearance.png 여유 지도와 길 너비, icesand.png 얼음·모래 동굴의 새 종(자세·뒷모습·잠든 모습), crystallake.png 수정 동굴·지하 호수의 종(흉내쟁이의 결정 모습 포함), lavavoid.png 용암 지대·공허의 심연의 종(잠든 대장장이·문지기 포함), report.txt 종마다 그림 폭. Docs/BiomeRework.md B0b·B2b·B3b·B4b)
dotnet run --project DevTools/ArtPreview -- --only sizes
# 칸 그림 점검(out/flat/: objects.png 물건 층에 그리는 칸 전부를 지금 그리는 대로, 서 있는지(STANDS)·이웃에 맞추는지(JOINED)·벨트(BELT)·납작한지(FLAT), objects.txt 목록, 게임이 그리는 대로의 장면 scene_factory·scene_home·scene_farm·scene_rooms.png, 변형 표 laid_variants.png·stood_variants.png, rails.png·rail_variants.png 이어지는 철도. Docs/BiomeRework.md 납작한 칸 점검)
dotnet run --project DevTools/ArtPreview -- --only flat
# 인스턴스 던전의 깔린 모습만(out/dungeons.png, out/dungeons/, 문 방의 불 색과 야영지의 뚜껑 문 out/dungeons/gates.png)
dotnet run --project DevTools/ArtPreview -- --only dungeons
# 대화 초상만(out/portraits/: 인물마다 여섯 표정의 sheet.png, 4배 close_*.png, 대화 상자 시안 dialogue.png. Docs/UiRework.md 4-2)
dotnet run --project DevTools/ArtPreview -- --only portraits
# 시작화면 개편의 그림만(out/title/: looks.png 주인공 외형(머리 모양·색·피부·눈·옷, 기본 외형과 옛 그림 비교), looks_moves.png 무작위 외형의 모션, logo.png·logo_glint.png 로고와 반짝임, buttons.png 글자 단추·판·목록 줄, biome_icons.png, loading_<생물군계>.png 로딩 장면, T3 화면 시안 screens_pc.png·screens_phone.png·screens_phonewide.png와 화면마다 screen_<크기>_<title|characters|make|worlds|loading|hub>.png. Docs/TitleRework.md T2·T3)
dotnet run --project DevTools/ArtPreview -- --only title

# Unity 쪽 코드 컴파일 확인. fetch는 세션마다 한 번(몇 분, 에디터와 웹 플레이어의 엔진 DLL만 .unity/에 받는다)
DevTools/UnityCompileCheck/fetch-unity.sh
dotnet build DevTools/UnityCompileCheck
# 웹 빌드처럼: 웹 플레이어의 DLL로 게임 어셈블리만. 에디터에만 있는 API를 쓰면 여기서 걸린다
dotnet build DevTools/UnityCompileCheck/Assemblies/Game.Client -p:DefineConstants=UNITY_WEBGL -p:UnityAssemblies=$PWD/DevTools/UnityCompileCheck/.unity/WebGL -o /tmp/webgl-check

# 웹 데모를 헤드리스 Chromium으로 플레이해 보기(사진과 report.md가 DevTools/WebPlaytest/out/에). --wait면 배포를 기다린다
node DevTools/WebPlaytest/playtest.js --wait
# LOCAL_TASKS.md의 사람 확인을 차례로 해 보는 전체 점검(30~40분, 결과는 out/sweep/). --only로 일부만
node DevTools/WebPlaytest/sweep.js --wait

# 로컬 명령 한 줄 스크립트 확인 (pwsh가 있으면 PowerShell 쪽도)
bash DevTools/Local/test/run.sh
```

GitHub Actions(`.github/workflows/core-tests.yml`)가 푸시마다 테스트, 도트 그림 그리기(결과는 실행의 `art-preview` 아티팩트), Unity 쪽 컴파일 확인, 로컬 스크립트 확인을 돌린다. Unity DLL은 Actions 캐시에 남겨 두고 저장소에는 넣지 않는다.

## 규칙

- Core에서는 `System.Random` 대신 `GameRandom`을 쓴다. 호스트, 클라이언트, 테스트가 같은 결과를 내야 한다. 시각(`DateTime.Now`, `Environment.TickCount` 등)도 Core 안에서 읽지 않고 밖에서 넘겨받는다.
- 게임 상태는 호스트의 Core 시뮬레이션만 바꾼다. 자세한 내용은 `Docs/Design.md`의 멀티플레이 절에 있다.
- PC와 모바일 크로스 플랫폼으로 만든다(사용자 결정, 2026-10-29). 새 기능은 키보드·마우스와 터치 둘 다로 쓸 수 있어야 하고, UI는 휴대폰 가로·세로와 작은 PC 창에 들어가야 한다. `Docs/Design.md` 기술 원칙 7.
- 에디터에서 손으로 만드는 콘텐츠를 늘리지 않는다. 월드는 절차 생성, 아이템·몬스터·보스는 데이터, 에셋은 에디터 스크립트로 만든다.
- 작업 중 사용량을 살핀다: 5시간 사용량이 80%를 넘거나 주간 사용량이 90%를 넘으면 새 작업을 시작하지 않고, 돌고 있는 에이전트를 멈추고, 사람에게 알린 뒤 사람이 다시 요청할 때까지 기다린다(사용자 결정, 2026-11-22). 퍼센트는 세션 정보에 나오지 않고 경고 상태만 보이므로, 경고가 보이면 새 작업을 멈추고 사람에게 사용량 화면의 숫자를 물어 기준을 넘었는지 정한다(경고는 기준보다 일찍 켜진다, 2026-11-22). 에이전트를 띄우기 전과 묶음을 합치기 전에 확인한다
- 사용자에게는 한국어로 말한다(사용자 결정, 2026-10-28). 작업 보고, 중간 안내, 마지막 요약까지 모든 답을 예외 없이 한국어로 쓴다(사용자가 2026-11-09에 다시 강조). 작업 사이에 남기는 짧은 안내와 명령·도구 설명(사람 화면에 보이는 설명 줄)도 한국어로 쓴다(사용자 결정, 2026-11-09). 문서도 지금처럼 한국어로 쓴다.
- 저장소에 올리는 파일, 커밋, 브랜치, PR 어디에도 AI 도구를 썼다는 표시(도구·회사 이름, 대화 앱 주소, 도구 이름이 붙은 브랜치)를 두지 않는다. 이 파일의 이름은 예외다(사용자 결정, 2026-11-11).
- 커밋 메시지는 영어로 쓴다. 첫 줄은 명령형 요약, 본문에는 무엇을 왜 바꿨는지 적는다.
- 커밋 작성자는 저장소 주인으로 한다: `git config user.name LeArc6910`, `git config user.email 270705266+LeArc6910@users.noreply.github.com`. 메시지에 `Co-Authored-By`나 세션 링크 같은 도구 표시 줄을 붙이지 않는다(사용자 결정, 2026-10-28).
- main 기록은 2026-10-28에 한 번 다시 썼다. 옛 기록의 커밋을 merge하거나 강제 푸시로 되살리지 않는다. 옛 기록 위에서 작업하던 클론은 `git fetch` 후 `git reset --hard origin/main`으로 옮긴다(푸시하지 않은 커밋은 `git rebase --onto`로).
