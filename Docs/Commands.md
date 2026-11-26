# 명령 모음

클라우드·로컬 세션이 쓰는 명령 전부(2026-11-26, `CLAUDE.md`에서 옮김: 매 작업마다 읽는 `CLAUDE.md`를 짧게 두어 토큰을 아끼려고. 사람 결정). 자주 쓰는 몇 줄만 `CLAUDE.md`에 남겼다. 새 명령은 여기에 더한다.

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
# 한 방이 최대 생명력에서 가져가는 몫: 모든 몬스터·정예·보스 공격을 맨몸 / 앞 단계 장비 / 제 단계 장비로, 목표와 견준 ✓·✗(Docs/Difficulty.md, 시험 DifficultyTests가 같은 표를 잡는다). 단계 하나만은 --hits iron처럼
dotnet run --project DevTools/CoreSandbox -- --hits
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

# Windows·Android 앱 빌드는 로컬 Unity에서(모듈과 명령은 Docs/NativeBuild.md): -executeMethod Game.Client.Editor.NativeBuild.RunBatchWindows / RunBatchAndroid

# 로컬 명령 한 줄 스크립트 확인 (pwsh가 있으면 PowerShell 쪽도)
bash DevTools/Local/test/run.sh
```

GitHub Actions(`.github/workflows/core-tests.yml`)가 푸시마다 테스트, 도트 그림 그리기(결과는 실행의 `art-preview` 아티팩트), Unity 쪽 컴파일 확인, 로컬 스크립트 확인을 돌린다. Unity DLL은 Actions 캐시에 남겨 두고 저장소에는 넣지 않는다.

