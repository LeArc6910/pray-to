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
# 무기 두 벌(Docs/Weapons.md): --gear에 무기를 둘 적으면 둘째가 보조 무기
dotnet run --project DevTools/CoreSandbox -- --boss 300 1 --boss-id antlion --gear iron_bow,iron_sword,iron_armor,iron_helmet --classes warrior,archer --level 10 --build
# 같은 싸움에서 봇이 손에 든 무기의 고유 기술도 쓴다(--arts, Docs/Weapons.md 묶음 C 밸런스)
dotnet run --project DevTools/CoreSandbox -- --boss 300 1 --boss-id antlion --gear iron_hammer,iron_armor,iron_helmet --classes warrior,archer --level 10 --build --arts
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
# 보스 하나의 움직임만 다시(anim/의 GIF, 프레임표, 연속성 보고서. general, warden, antlion, serpent, titan, voidlord, wraith, crystal_queen, spider, knight, samurai, vein_eater. serpent·wraith는 전체 패턴 serpent_patterns.gif·crown_wraith_patterns.gif도. voidlord는 공허의 군주, 전체 패턴 void_lord_patterns.gif도). hero는 주인공의 무기별 모션(hero_*), entrances는 여섯 보스의 입장만, arenas는 아레나 그림만(arenas.png, arenas_unlit.png, arenas_world.png, arenas/), skillfx는 스킬 이펙트만(skillfx/의 클래스별 프레임표·GIF, Docs/SkillEffects.md), monsterfx는 일반 몬스터의 예고와 이펙트만(monsterfx/의 종별 프레임표·GIF, Docs/Combat.md 몬스터의 공격 시트)
dotnet run --project DevTools/ArtPreview -- --only spider
# 보스 평타의 전조 세기: 쉬는 자세와 전조의 실루엣 변화(out/anim/tells.txt, Docs/BossRedesign.md 9절)
dotnet run --project DevTools/ArtPreview -- --only tells
# 몸의 틈 점검: 모든 보스·몬스터의 모든 프레임에서 몸 안에 생긴 투명한 틈(anim/gaps.txt, gaps_<이름>.png, Docs/Bosses.md 몸의 틈)
dotnet run --project DevTools/ArtPreview -- --only gaps
# 몇 프레임만 틈을 빨갛게 나란히(anim/gaps_frames.png, 고치기 전후 보기)
dotnet run --project DevTools/ArtPreview -- --only gapframes cave_warden:Side:Raise:15 black_knight:FrontSide:Sweep:4
# 생성된 지도 그림만(out/map/: 생성기 1판·2판, 시드 1~3의 온 지도와 시작 지점, 시드 1의 생물군계마다와 방 그래프, 역할마다의 방(v2_seed1_roles.png, rooms_art.png, rooms_art_lit.png), 새 칸(room_tiles.png), 길과 샛길(v2_seed1~3_roads.png))
dotnet run --project DevTools/ArtPreview -- --only map
# UI 개편 시안(out/uimock/: 양식 A·B·C와 추천 조합의 가방·대화·상점·성장 화면, 휴대폰 세로 가방·대화, compare_abc.png. Docs/UiRework.md 4-4)
dotnet run --project DevTools/ArtPreview -- --only uimock
# 이펙트 개편 시안(out/fxmock/: 이펙트 15개의 지금·개선안 프레임표와 GIF, all.png. Docs/EffectsRework.md)
dotnet run --project DevTools/ArtPreview -- --only fxmock
# 보스 예고 도형과 공용 착지(out/hazard/: warn.png 차오름·깜빡임, blast.png 타격·바닥 흔적, 각각 흑백 _gray, pieces.png. Docs/EffectsRework.md 6절)
dotnet run --project DevTools/ArtPreview -- --only hazard
# 스킬 이펙트(E3 공용 폭발·층 조각, E4 스킬 30종): out/skillfx/<클래스>.png·_gray·report.txt(조각 수), pieces.png. 웹 점검은 sweep.js --only blast(폭발), layers(범위 스킬 여섯 나란히·유성 낙하), skillfx(서른 가지 하나씩)
dotnet run --project DevTools/ArtPreview -- --only skillfx
# F2: monsterfx의 pieces.png에 띠 장판 네 줄과 도형 착지 열한 줄, bossfx의 muzzle.png(쏘는 순간의 섬광), shot의 맨 아래 줄에 조준선 두 안(전·지금)
# 일반 몬스터의 예고·탄·장판(E5): out/monsterfx/<종>.png·_gray, pieces.png(장판 다섯, 예고, 탄 열 줄, 돌진, 원소 터짐). 한 이펙트 24조각·빛 3개를 넘으면 실패. 웹 점검은 sweep.js --only mobwarn
dotnet run --project DevTools/ArtPreview -- --only monsterfx
# 적의 탄(out/shot/: shots.png 원소 열넷(보스 아홉 + 몬스터 다섯)의 맥동·각도·큰 것·멈춘 것·소멸 5프레임과 옛 공·플레이어 탄·조준선, 흑백 _gray, report.txt. Docs/EffectsRework.md 7절)
dotnet run --project DevTools/ArtPreview -- --only shot
# 보스 고유 이펙트(E6): out/bossfx/<보스>.png·_gray(줄마다 이펙트 하나, 밑에 타격 도형), report.txt(조각·빛 수). 한 이펙트 72조각·빛 3개를 넘으면 실패. 웹 점검은 sweep.js --only bossfx
dotnet run --project DevTools/ArtPreview -- --only bossfx
# R1b 그림 한 장(out/r1b/sheet.png·_gray): 시구레 납도 자세 여덟 방향과 벚꽃 표지, 발두인 투구 벗은 다섯 방향·자세와 떨어진 투구, 붉은·푸른 색 표지
dotnet run --project DevTools/ArtPreview -- --only r1b
# 보스의 땅 그림 한 장(out/ground/sheet.png·_gray, R2a): 용암 원·띠, 식은 바위, 얕은 물, 균열 띠·원, 용암 가장자리와 섬, 분출구 각 4장, 함께 놓은 장면. 소리 lavabubble.wav·overheat.wav도 같은 폴더에
dotnet run --project DevTools/ArtPreview -- --only ground
# 타격 이펙트(E7): out/hitfx/hits.png·hits_gray.png(줄마다 원소의 보통·강한 타격, 왼쪽에서 맞음), report.txt. 12조각·빛 1개·0.4초를 넘으면 실패. 웹 점검은 sweep.js --only hits
dotnet run --project DevTools/ArtPreview -- --only hitfx
# 게임의 UI 스킨만(out/ui/: skin.png 조각 전부, windows.png 지금 화면에 입힌 모습, pixels.png 정수 배 틀, encounters.png·encounters_phone.png 상점·상자·작업대·모루·대화 창, growth.png·growth_phone.png 스킬·특성 트리·캐릭터 창, records.png·records_phone.png 일지·지도, bag_hud.png·bag_hud_phone.png 가방·HUD·쓰러짐 카드·메뉴. Docs/UiRework.md U1~U7)
dotnet run --project DevTools/ArtPreview -- --only ui
# 사냥 세트·정예 고유·보스 세트 아이콘과 사냥 재료(out/gear.png, Docs/Gear.md)
dotnet run --project DevTools/ArtPreview -- --only gear
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
# 무기 종류를 주인공이 든 모습(out/weapons_held.png), 종류마다 연타 1·2·3단 다섯 장씩(out/weapons_combo.png, 시간도 출력), 종류마다 탄(out/weapons_shots.png). Docs/Weapons.md 묶음 B. 아이콘 줄은 --only gear의 out/weapons.png
dotnet run --project DevTools/ArtPreview -- --only weapons
# 무기 점검: 무기마다 연타 1~3단의 호·탄 그림, 휘두르기·맞힘 소리, 손에 든 그림, 종류의 동작·느낌(out/weapon_audit.md, 빠지면 실패). Docs/WeaponAudit.md
dotnet run --project DevTools/ArtPreview -- --only weaponaudit
# 무기 고유 기술: 종류마다 실제 이펙트 프로그램 네 순간(out/weapon_arts.png). Docs/Weapons.md 묶음 C
dotnet run --project DevTools/ArtPreview -- --only weaponarts
# 그림 린트만(몇 초): 외딴 픽셀·색 수·흔들림·쌍둥이를 세고 out/lint.md에 적는다. 기준(lint-baseline.txt)보다 늘면 실패
dotnet run --project DevTools/ArtPreview -- --only lint

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

```
# 길만 걷는 봇의 분당 만남(7판·새 판)과 사냥터(생물군계마다 수, 서서 3분 싸우기). Docs/HuntingGrounds.md
dotnet run --project DevTools/CoreSandbox -- --roadwalk 10 1,2,3,4
# 웹 점검 구역 hunt: 새 월드의 사냥터, 몬스터 무리, 지도 표시
node DevTools/WebPlaytest/sweep.js --wait <커밋> --only hunt
```


```
# 보스의 땅·물체·분노(R2a, Docs/Bosses.md 지형·물체·분노): 용암 거인 봇 전투(알맞음 조건, 시드를 바꿔 1~5)와 웹 점검 구역 ground(PC·휴대폰)
dotnet run --project DevTools/CoreSandbox -- --boss 600 1 --boss-id lava_titan --gear mithril_sword,mithril_armor,mithril_helmet,mithril_boots,mithril_ring,mithril_amulet --classes warrior,fire_mage --reaction 0.5 --level 25 --build
node DevTools/WebPlaytest/sweep.js --wait <커밋> --only ground
# 기둥·광맥·결계(R2b, Docs/Bosses.md 기둥·광맥·벽 돌기): 동굴 수호자·그룸 봇 전투(알맞음, 위 명령의 boss-id·장비·레벨만 바꿈: cave_warden 구리 5, vein_eater 미스릴 29)와 웹 점검 구역 props(PC·휴대폰)
dotnet run --project DevTools/CoreSandbox -- --boss 600 1 --boss-id vein_eater --gear mithril_sword,mithril_armor,mithril_helmet,mithril_boots,mithril_ring,mithril_amulet --classes warrior,fire_mage --reaction 0.5 --level 29 --build
node DevTools/WebPlaytest/sweep.js --wait <커밋> --only props
# 끌기·숨기·벽(R3, Docs/Bosses.md 끌기·숨기·벽): 개미귀신(철 12)·물살 뱀(미스릴 22)·아라네(공허 45) 봇 전투(알맞음, 위 명령의 boss-id·장비·레벨만 바꿈, --roll-miss 0.4로 놓치는 봇)와 웹 점검 구역 forces(PC·휴대폰)
dotnet run --project DevTools/CoreSandbox -- --boss 600 1 --boss-id antlion --gear iron_sword,iron_armor,iron_helmet,iron_boots,iron_ring,iron_amulet --classes warrior,fire_mage --reaction 0.5 --level 12 --build
node DevTools/WebPlaytest/sweep.js --wait <커밋> --only forces
```

```
# 새 월드의 새벽 마을·왕도를 글자로(Docs/Towns.md)
dotnet run --project DevTools/CoreSandbox -- --town 1
```
