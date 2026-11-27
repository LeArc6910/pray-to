# 녹음 효과음 출처 (2026-11-11)

2차(2026-11-26, 효과음 더 많이와 배경음악·환경음)와 3차(2026-11-27, 빈칸 채우기)와 4차(2026-11-27, 장면을 보며 다시 고르기)는 이 문서 아래 "2차"·"3차"·"4차" 절에 있다.

사람 요청 "걷는 사운드가 너무 시끄럽고 걷는 것 같지가 않아. 바닥 타일에 따라 소리가 달라졌으면 좋겠어. 무료로 공개되어 있는 효과음을 다운로드 받아서 사용해도 좋고 (다른 사운드도 이런 식으로)".

- 파일: `Assets/_Project/Resources/Sfx/*.wav`(모노, 22050 Hz). 이름이 `세트_번호`라 같은 세트 가운데 하나를 고른다(바로 앞에 낸 것은 피한다).
- 코드: `Client/Recordings.cs`(어떤 소리를 어떤 세트로, 바닥 고르기, 세트마다 크기 맞추기), `Sounds.Step`. 녹음이 없는 소리는 코드로 만든 소리(`Art/SoundArt.cs`) 그대로다. 메뉴 [설정]의 [소리 종류: 녹음/합성]으로 견줄 수 있다.
- 발소리: 걸음 간격을 0.6칸에서 1.15칸으로 늘렸다(걸으면 1초에 두 번쯤, 전에는 여덟 번). 크기도 줄이고, 걸을 때마다 높낮이 ±5%, 크기 0.8~1배.
- 바닥: 깔개 → 카펫, 부두 → 나무, 레일·벨트 → 쇠, 길·웨이포인트·깐 바닥 → 돌(뒤꿈치·발끝 두 번 치는 대안 세트), 흙 → 흙. 맨바닥은 생물군계로: 버섯 숲 → 낙엽, 얼음 → 눈, 모래 → 모래, 수정·용암·심연 → 거친 바위, 호수 → 젖은 발, 그 밖 → 흙.
- 녹음으로 바꾼 효과음: 캐기(곡괭이), 부서짐(바위), 휘두르기, 맞히기, 줍기, 클릭, 문 열기·닫기, 물 튀김(낚싯줄 던지기 포함).
- 받기만 하고 아직 안 쓰는 것: 상자 열기, 동전, 마우스오버, 풀 발소리(풀 바닥이 아직 없다).
- 찾지 못한 것: 얼음(눈 발소리를 조금 높여 쓴다), 금속 격자, 벽돌, 철벅이는 물.
- 사람이 들어 볼 것: `LOCAL_TASKS.md` 133단계.

모두 CC0이라 출처 표기 의무는 없지만 아래에 남긴다. 크레딧: Kenney.nl, rubberduck, Fantozzi, TinyWorlds, Thimras, OwlishMedia, artisticdude, Oiboo.


모든 파일은 출처 페이지에서 **CC0 (Creative Commons Zero 1.0, public domain dedication)** 로 표시된 것을 2026-11-11에 직접 확인한 뒤 받았다.
CC0이므로 출처 표기는 의무가 아니지만, 아래 기록을 남겨 둔다(Kenney는 'crediting is not mandatory'라고 적어 둠).
OpenGameArt 페이지에 CC0 외의 라이선스가 함께 붙은 팩(예: OGA-BY + CC0 이중 라이선스)은 쓰지 않았다.

가공: ffmpeg로 모노·22050 Hz 디코딩, 50 Hz 고역 통과(직류·웅웅거림 제거), 앞 무음 자르기(3 ms 여유), 뒤 꼬리 자르기와 짧은 페이드아웃, 길이 상한, 피크 정규화(발소리 -14 dBFS, 효과음 -6 dBFS, UI -8 dBFS, 마우스오버 -12 dBFS), 16-bit PCM WAV.

## 출처 팩

- **Impact Sounds (1.0)** — Kenney (Kenney Vleugels, www.kenney.nl)
  - 페이지: https://kenney.nl/assets/impact-sounds
  - 라이선스(페이지 표기): Creative Commons CC0 (page: 'License: Creative Commons CC0' -> creativecommons.org/publicdomain/zero/1.0/; License.txt: 'License: (Creative Commons Zero, CC0)')
- **RPG Audio (1.0)** — Kenney (Kenney Vleugels, www.kenney.nl)
  - 페이지: https://kenney.nl/assets/rpg-audio
  - 라이선스(페이지 표기): Creative Commons CC0 (page: 'License: Creative Commons CC0'; License.txt: 'License (Creative Commons Zero, CC0)')
- **Interface Sounds (1.0)** — Kenney (Kenney Vleugels, www.kenney.nl)
  - 페이지: https://kenney.nl/assets/interface-sounds
  - 라이선스(페이지 표기): Creative Commons CC0 (page: 'License: Creative Commons CC0'; License.txt: 'License: (Creative Commons Zero, CC0)')
- **Fantozzi's Footsteps (Grass/Sand & Stone)** — Fantozzi (submitted by qubodup)
  - 페이지: https://opengameart.org/content/fantozzis-footsteps-grasssand-stone
  - 라이선스(페이지 표기): CC0 (OpenGameArt 'License(s): CC0')
- **Different steps on wood, stone, leaves, gravel and mud** — TinyWorlds
  - 페이지: https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud
  - 라이선스(페이지 표기): CC0 (OpenGameArt 'License(s): CC0')
- **Metal footsteps on concrete** — Thimras
  - 페이지: https://opengameart.org/content/metal-footsteps-on-concrete
  - 라이선스(페이지 표기): CC0 (OpenGameArt 'License(s): CC0')
- **100 CC0 SFX #2** — rubberduck
  - 페이지: https://opengameart.org/content/100-cc0-sfx-2
  - 라이선스(페이지 표기): CC0 (OpenGameArt 'License(s): CC0')
- **40 CC0 water / splash / slime SFX** — rubberduck
  - 페이지: https://opengameart.org/content/40-cc0-water-splash-slime-sfx
  - 라이선스(페이지 표기): CC0 (OpenGameArt 'License(s): CC0')
- **75 CC0 breaking / falling / hit sfx** — rubberduck
  - 페이지: https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx
  - 라이선스(페이지 표기): CC0 (OpenGameArt 'License(s): CC0')
- **80 CC0 RPG SFX** — rubberduck
  - 페이지: https://opengameart.org/content/80-cc0-rpg-sfx
  - 라이선스(페이지 표기): CC0 (OpenGameArt 'License(s): CC0')
- **Swishes Sound Pack** — artisticdude
  - 페이지: https://opengameart.org/content/swishes-sound-pack
  - 라이선스(페이지 표기): CC0 (OpenGameArt 'License(s): CC0')
- **Sound Effects Pack (Owlish Media Sound Effects.zip)** — OwlishMedia
  - 페이지: https://opengameart.org/content/sound-effects-pack
  - 라이선스(페이지 표기): CC0 (OpenGameArt 'License(s): CC0')
- **open chest SFX** — Oiboo
  - 페이지: https://opengameart.org/content/open-chest-sfx
  - 라이선스(페이지 표기): CC0 (OpenGameArt 'License(s): CC0')

## 파일별 기록

| 파일 | 원본 파일 이름 | 팩 | 저자 | 출처 페이지 | 라이선스 |
|---|---|---|---|---|---|
| footstep_stone_1.wav | footstep_concrete_000.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_stone_2.wav | footstep_concrete_001.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_stone_3.wav | footstep_concrete_002.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_stone_4.wav | footstep_concrete_003.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_stone_5.wav | footstep_concrete_004.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_rock_1.wav | Fantozzi-StoneL1.flac | Fantozzi's Footsteps (Grass/Sand & Stone) | Fantozzi | https://opengameart.org/content/fantozzis-footsteps-grasssand-stone | CC0 1.0 |
| footstep_rock_2.wav | Fantozzi-StoneR1.flac | Fantozzi's Footsteps (Grass/Sand & Stone) | Fantozzi | https://opengameart.org/content/fantozzis-footsteps-grasssand-stone | CC0 1.0 |
| footstep_rock_3.wav | Fantozzi-StoneL2.flac | Fantozzi's Footsteps (Grass/Sand & Stone) | Fantozzi | https://opengameart.org/content/fantozzis-footsteps-grasssand-stone | CC0 1.0 |
| footstep_rock_4.wav | Fantozzi-StoneR2.flac | Fantozzi's Footsteps (Grass/Sand & Stone) | Fantozzi | https://opengameart.org/content/fantozzis-footsteps-grasssand-stone | CC0 1.0 |
| footstep_dirt_1.wav (4차에서 바꿈) | footstep00.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| footstep_dirt_2.wav (4차에서 바꿈) | footstep01.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| footstep_dirt_3.wav (4차에서 바꿈) | footstep02.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| footstep_dirt_4.wav (4차에서 바꿈) | footstep03.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| footstep_dirt_5.wav (4차에서 바꿈) | footstep06.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| footstep_dirt_6.wav (4차에서 바꿈) | footstep07.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| footstep_wood_1.wav | footstep_wood_000.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_wood_2.wav | footstep_wood_001.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_wood_3.wav | footstep_wood_002.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_wood_4.wav | footstep_wood_003.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_wood_5.wav | footstep_wood_004.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_sand_1.wav | Fantozzi-SandL1.flac | Fantozzi's Footsteps (Grass/Sand & Stone) | Fantozzi | https://opengameart.org/content/fantozzis-footsteps-grasssand-stone | CC0 1.0 |
| footstep_sand_2.wav | Fantozzi-SandR1.flac | Fantozzi's Footsteps (Grass/Sand & Stone) | Fantozzi | https://opengameart.org/content/fantozzis-footsteps-grasssand-stone | CC0 1.0 |
| footstep_sand_3.wav | Fantozzi-SandL2.flac | Fantozzi's Footsteps (Grass/Sand & Stone) | Fantozzi | https://opengameart.org/content/fantozzis-footsteps-grasssand-stone | CC0 1.0 |
| footstep_sand_4.wav | Fantozzi-SandR2.flac | Fantozzi's Footsteps (Grass/Sand & Stone) | Fantozzi | https://opengameart.org/content/fantozzis-footsteps-grasssand-stone | CC0 1.0 |
| footstep_sand_5.wav | Fantozzi-SandL3.flac | Fantozzi's Footsteps (Grass/Sand & Stone) | Fantozzi | https://opengameart.org/content/fantozzis-footsteps-grasssand-stone | CC0 1.0 |
| footstep_sand_6.wav | Fantozzi-SandR3.flac | Fantozzi's Footsteps (Grass/Sand & Stone) | Fantozzi | https://opengameart.org/content/fantozzis-footsteps-grasssand-stone | CC0 1.0 |
| footstep_snow_1.wav | footstep_snow_000.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_snow_2.wav | footstep_snow_001.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_snow_3.wav | footstep_snow_002.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_snow_4.wav | footstep_snow_003.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_snow_5.wav | footstep_snow_004.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_grass_1.wav | footstep_grass_000.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_grass_2.wav | footstep_grass_001.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_grass_3.wav | footstep_grass_002.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_grass_4.wav | footstep_grass_003.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_grass_5.wav | footstep_grass_004.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_leaves_1.wav | leaves01.ogg | Different steps on wood, stone, leaves, gravel and mud | TinyWorlds | https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud | CC0 1.0 |
| footstep_leaves_2.wav | leaves02.ogg | Different steps on wood, stone, leaves, gravel and mud | TinyWorlds | https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud | CC0 1.0 |
| footstep_metal_1.wav (4차에서 바꿈) | metal_steps_01.wav | Metal footsteps on concrete | Thimras | https://opengameart.org/content/metal-footsteps-on-concrete | CC0 1.0 |
| footstep_metal_2.wav (4차에서 바꿈) | metal_steps_02.wav | Metal footsteps on concrete | Thimras | https://opengameart.org/content/metal-footsteps-on-concrete | CC0 1.0 |
| footstep_metal_3.wav (4차에서 바꿈) | metal_steps_04.wav | Metal footsteps on concrete | Thimras | https://opengameart.org/content/metal-footsteps-on-concrete | CC0 1.0 |
| footstep_metal_4.wav (4차에서 바꿈) | metal_steps_05.wav | Metal footsteps on concrete | Thimras | https://opengameart.org/content/metal-footsteps-on-concrete | CC0 1.0 |
| footstep_water_1.wav | sfx100v2_footstep_wet_01.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| footstep_water_2.wav | sfx100v2_footstep_wet_02.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| footstep_water_3.wav | sfx100v2_footstep_wet_03.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| footstep_carpet_1.wav (4차에서 바꿈) | footstep_carpet_000.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_carpet_2.wav (4차에서 바꿈) | footstep_carpet_001.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_carpet_3.wav (4차에서 바꿈) | footstep_carpet_003.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_pickaxe_1.wav (4차에서 바꿈) | impactMining_000.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_pickaxe_2.wav (4차에서 바꿈) | impactMining_001.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_pickaxe_3.wav (4차에서 바꿈) | impactMining_003.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_rockbreak_1.wav | bfh1_rock_breaking_02.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_rockbreak_2.wav | bfh1_rock_breaking_01.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_rockbreak_3.wav | bfh1_rock_breaking_03.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_swing_1.wav | swish-3.wav | Swishes Sound Pack | artisticdude | https://opengameart.org/content/swishes-sound-pack | CC0 1.0 |
| sfx_swing_2.wav | swish-5.wav | Swishes Sound Pack | artisticdude | https://opengameart.org/content/swishes-sound-pack | CC0 1.0 |
| sfx_swing_3.wav | swish-9.wav | Swishes Sound Pack | artisticdude | https://opengameart.org/content/swishes-sound-pack | CC0 1.0 |
| sfx_hit_1.wav | impactPunch_medium_000.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_hit_2.wav | impactPunch_medium_002.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_hit_3.wav | impactPunch_medium_004.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_pickup_1.wav (4차에서 바꿈) | drop_002.ogg | Interface Sounds (1.0) | Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 |
| sfx_pickup_2.wav (4차에서 바꿈) | drop_003.ogg | Interface Sounds (1.0) | Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 |
| sfx_ui_click_1.wav (4차에서 바꿈) | click_001.ogg | Interface Sounds (1.0) | Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 |
| sfx_ui_click_2.wav (4차에서 바꿈) | select_001.ogg | Interface Sounds (1.0) | Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 |
| sfx_ui_hover_1.wav (4차에서 바꿈) | tick_001.ogg | Interface Sounds (1.0) | Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 |
| sfx_door_open_1.wav | doorOpen_1.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| sfx_door_close_1.wav | doorClose_1.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| sfx_chest_open_1.wav (4차에서 바꿈) | open chest_0.wav | open chest SFX | Oiboo | https://opengameart.org/content/open-chest-sfx | CC0 1.0 |
| sfx_chest_open_2.wav (4차에서 바꿈) | creak1.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| sfx_splash_1.wav | splash_03.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| sfx_splash_2.wav | splash_09.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| sfx_coin_1.wav | handleCoins2.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| sfx_coin_2.wav | item_coins_01.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| footstep_stone_alt_1.wav (4차에서 바꿈) | step1.wav | Sound Effects Pack (Owlish Media Sound Effects.zip) | OwlishMedia | https://opengameart.org/content/sound-effects-pack | CC0 1.0 |
| footstep_stone_alt_2.wav (4차에서 바꿈) | step2.wav | Sound Effects Pack (Owlish Media Sound Effects.zip) | OwlishMedia | https://opengameart.org/content/sound-effects-pack | CC0 1.0 |
| footstep_stone_alt_3.wav (4차에서 바꿈) | step3.wav | Sound Effects Pack (Owlish Media Sound Effects.zip) | OwlishMedia | https://opengameart.org/content/sound-effects-pack | CC0 1.0 |
| footstep_stone_alt_4.wav (4차에서 바꿈) | step4.wav | Sound Effects Pack (Owlish Media Sound Effects.zip) | OwlishMedia | https://opengameart.org/content/sound-effects-pack | CC0 1.0 |
| footstep_stone_alt_5.wav (4차에서 바꿈) | stone01.ogg | Different steps on wood, stone, leaves, gravel and mud | TinyWorlds | https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud | CC0 1.0 |
| footstep_wood_alt_1.wav (4차에서 바꿈) | sfx100v2_footstep_wood_01.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| footstep_wood_alt_2.wav (4차에서 바꿈) | sfx100v2_footstep_wood_03.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| footstep_wood_alt_3.wav (4차에서 바꿈) | sfx100v2_footstep_wood_04.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| footstep_wood_alt_4.wav (4차에서 바꿈) | wood01.ogg | Different steps on wood, stone, leaves, gravel and mud | TinyWorlds | https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud | CC0 1.0 |
| footstep_wood_alt_5.wav (4차에서 바꿈) | wood03.ogg | Different steps on wood, stone, leaves, gravel and mud | TinyWorlds | https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud | CC0 1.0 |
| footstep_dirt_alt_1.wav | mud02.ogg | Different steps on wood, stone, leaves, gravel and mud | TinyWorlds | https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud | CC0 1.0 |
| footstep_grass_alt_1.wav | grassy-footstep2.wav | Sound Effects Pack (Owlish Media Sound Effects.zip) | OwlishMedia | https://opengameart.org/content/sound-effects-pack | CC0 1.0 |
| footstep_grass_alt_2.wav | grassy-footstep3.wav | Sound Effects Pack (Owlish Media Sound Effects.zip) | OwlishMedia | https://opengameart.org/content/sound-effects-pack | CC0 1.0 |


## 2차: 효과음·배경음악 (2026-11-26)

사람 요청 "Sonniss랑 Kenney 무료 효과음부터 받아서 넣어줘. bgm도 있다면 넣어주고".

- **Sonniss GDC 번들은 쓰지 않았다.** sonniss.com이 자동 접속을 사이트 쪽에서 403으로 막아(네트워크 정책이 아님) 공식 라이선스 페이지를 직접 열어 확인할 수 없었고, 번들은 수십 GB 압축 파일이라 이 환경에서 받기도 어렵다. 라이선스를 직접 확인하지 못한 것은 쓰지 않는다는 원칙대로 뺐다. 사람이 받아 오면(로컬) 같은 방식으로 가공해 넣을 수 있다.
- **Kenney**: kenney.nl도 자동 접속을 403으로 막아, Kenney가 OpenGameArt에 직접 올린 같은 팩(CC0)에서 받았다.
- 모든 파일은 2026-11-26에 OpenGameArt 페이지의 'License(s)'가 **CC0 하나뿐**인 것만 받았다(CC0와 다른 라이선스가 함께 붙은 것, CC-BY·CC-BY-SA·GPL은 뺐다). Kenney 팩은 안의 License.txt도 CC0. 출처 표기 의무는 없지만 아래에 남긴다.
- 받은 원본은 저장소에 넣지 않았다. 쓰는 것만 가공해 넣었다.

### 가공

- 효과음(`Resources/Sfx`, WAV): 1차와 같다. 모노 22050 Hz, 50 Hz 고역 통과, 앞 무음 자르기(3 ms 여유), 뒤 꼬리 자르기와 페이드아웃, 길이 상한, 피크 정규화(효과음 -5~-7 dBFS, UI -8, 징글 -6~-7), 16-bit PCM.
- 음악(`Resources/Music`, Ogg Vorbis q2, 모노 22050 Hz): 곡을 잘라 반복 길이를 정하고, 반복 끝 뒤의 몇 초를 처음 몇 초에 등전력 크로스페이드로 겹쳐 이음매를 없앤다. 크기는 RMS -19 dBFS(코드로 만든 곡의 크기와 같다), 피크 -1 dBFS 아래.
- 환경음(`Resources/Ambience`): 반복용 원본 그대로, RMS -22 dBFS.
- 가져오기 설정은 에디터 스크립트 `Client/Editor/SoundImport.cs`가 정한다(모노, 22050 Hz, Vorbis; 효과음은 불러올 때 풀기, 음악·환경음은 압축한 채로 품질 0.35).

### 어디에 무엇 (코드: `Client/Recordings.cs`)

- 전투: 치명타 `sfx_crit`, 내가 맞음 `sfx_hurt`, 내려찍기 `sfx_thump`, 보스 예고·필살기 경고 `sfx_gong`, 보스 포효 `sfx_roar`, 몬스터 부르기·수정 울림 `sfx_bell`
- 구르기 `sfx_roll`, 피하기·돌진 `sfx_whoosh`
- 원소: 불 `sfx_fire`(Boom과 불 기술 7개), 얼음 `sfx_ice`(Ice와 얼음 기술 3개), 번개 `sfx_zap`(Zap과 번개 기술 3개), 깨짐 `sfx_shatter`, 마법 폭발 `sfx_spell`(Nova)
- 몬스터 울음 `voice_<종류>_<cry|attack|hurt>`: 슬라임, 벌레, 정령(wisp), 지렁이·악마, 짐승(새로: 쥐·늑대·예티·매머드·도롱뇽), 골렘(새로: 골렘·수호자·문지기·거북·미믹·대장간지기). 버섯·박쥐·게와 녹음이 없는 울음은 코드 소리 그대로
- UI: 열기 `sfx_ui_open`, 닫기 `sfx_ui_close`, 마우스오버 `sfx_ui_hover`(1차에 받아 둔 것), 누름 `sfx_ui_confirm`, 뒤로 `sfx_ui_cancel`, 실패 `sfx_ui_fail`
- 사기·팔기 `sfx_coin_buy`(새 소리 종류 Buy), 상자 열기 `sfx_chest_open`(1차에 받아 둔 것, 새 소리 종류 Chest)
- 물 `sfx_bubble`, 퐁 `sfx_plop`
- 수확 `sfx_harvest`, 먹기 `sfx_eat`, 놓기 `sfx_place`
- 제작 `sfx_craft`, 망치·모루와 껍질에 막힘 `sfx_anvil`
- 징글(Kenney NES): 승리 `jingle_victory`, 레벨업 `jingle_levelup`, 기술 배움 `jingle_learn`, 이야기 단계 `jingle_story`, 쓰러짐 `jingle_down`. 음높이가 올라가는 것을 골랐다(내려가는 것은 쓰러짐)
- 배경음악: 시작 화면, 마을, 생물군계 8곳, 던전 안, 보스전. 왕국·오두막·둥지·항구·유적·광산은 코드로 만든 곡 그대로. 지역 곡의 전투판과 밤판은 같은 녹음이 끊기지 않고 이어진다(녹음에는 전투판이 따로 없다)
- 환경음: 호숫가 `amb_lakeside`, 항구 `amb_waves`. 나머지는 코드 소리 그대로
- 설정의 [소리 종류: 합성]이면 모두 전처럼 코드 소리·곡. 음악 크기 설정은 녹음 곡에도 그대로 걸린다

### 아직 찾지 못한 것

얼음·벽돌·금속 격자 발소리, 철벅이는 물 발소리, 용암·김(Sizzle) 녹음, 버섯·박쥐·게 울음, 왕국·오두막·둥지·항구·유적·광산 곡.

### 사람이 들어 볼 것

`Docs/HumanChecklist.md`의 2026-11-26 소리 항목.

### 배경음악 파일

| 파일 | 원본 파일 | 곡·팩 | 작가 | 출처 페이지 | 쓰는 곳 | 반복 | 라이선스 |
|---|---|---|---|---|---|---|---|
| music_title.ogg | Calm1 - A Place I Call Home.ogg | JRPG Music Pack #4 [Calm] | Juhani Junkala (SubspaceAudio) | https://opengameart.org/content/jrpg-pack-4-calm | 시작 화면 | 0–45.9초, 끝 0.5초를 처음에 겹침 | CC0 1.0 |
| music_village.ogg | Calm3 - Peaceful Days.ogg | JRPG Music Pack #4 [Calm] | Juhani Junkala (SubspaceAudio) | https://opengameart.org/content/jrpg-pack-4-calm | 마을 | 0–58초, 4초 겹침(끝 잔향) | CC0 1.0 |
| music_cave.ogg | dark_cavern_ambient_001.ogg | Dark Cavern Ambient | Paul Wortmann | https://opengameart.org/content/dark-cavern-ambient | 흙 동굴 | 5–65초, 4초 겹침 | CC0 1.0 |
| music_mushroom.ogg | GameMusic_ForestTheme_24_0.mp3 | Dark Forest Theme | cynicmusic | https://opengameart.org/content/dark-forest-theme | 버섯 숲 | 0–88초, 3초 겹침 | CC0 1.0 |
| music_ice.ogg | Snowfall (Looped ver.)_0.ogg | Snowfall | Kistol | https://opengameart.org/content/snowfall | 얼음 동굴 | 0–47.5초, 1초 겹침 | CC0 1.0 |
| music_crystal.ogg | song18_0.mp3 | Crystal Cave (song18) | cynicmusic | https://opengameart.org/content/crystal-cave-song18 | 수정 동굴 | 0–47.5초, 1.5초 겹침 | CC0 1.0 |
| music_lava.ogg | Danish Mega Pony v. 4 OST - Track 02 (Fire Level)_0.mp3 | Fire Level | Spring Spring | https://opengameart.org/content/fire-level | 용암 지대 | 0–101초, 2.5초 겹침 | CC0 1.0 |
| music_abyss.ogg | 012_Sirens_in_Darkness_0.mp3 | Sirens in Darkness | cynicmusic | https://opengameart.org/content/sirens-in-darkness | 심연 | 20–95초, 4초 겹침 | CC0 1.0 |
| music_lake.ogg | Cleyton RX - Underwater_0.mp3 | Underwater theme | Cleyton Kauffman | https://opengameart.org/content/underwater-theme | 호수 | 0–75초, 3초 겹침 | CC0 1.0 |
| music_sand.ogg | headinthesand_0.ogg | Head in the Sand (seamless loop) | congusbongus | https://opengameart.org/content/head-in-the-sand-seamless-loop | 모래 동굴 | 전체(원래 이음매 없는 반복) | CC0 1.0 |
| music_dungeon.ogg | dungeon002_0.ogg | Dungeon Ambience | yd | https://opengameart.org/content/dungeon-ambience | 던전 안 | 10–85초, 4초 겹침 | CC0 1.0 |
| music_boss.ogg | 3. Boss Fight.ogg | 4 Chiptunes (Adventure) | Juhani Junkala (SubspaceAudio) | https://opengameart.org/content/4-chiptunes-adventure | 보스전(모든 페이즈) | 0–70초, 1.5초 겹침 | CC0 1.0 |

### 효과음·환경음 파일

| 파일 | 원본 파일 이름 | 팩 | 저자 | 출처 페이지 | 라이선스 |
|---|---|---|---|---|---|
| sfx_ui_open_1.wav (4차에서 바꿈) | open_001.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_ui_open_2.wav (4차에서 바꿈) | open_002.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_ui_close_1.wav (4차에서 바꿈) | close_001.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_ui_close_2.wav (4차에서 바꿈) | close_002.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_ui_confirm_1.wav (4차에서 바꿈) | select_001.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_ui_confirm_2.wav (4차에서 바꿈) | select_002.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_ui_cancel_1.wav | back_001.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_ui_cancel_2.wav | back_002.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_ui_fail_1.wav (4차에서 바꿈) | error_001.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_ui_fail_2.wav (4차에서 바꿈) | error_002.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_roll_1.wav (4차에서 바꿈) | cloth1.ogg | 50 RPG sound effects | Kenney | https://opengameart.org/content/50-rpg-sound-effects | CC0 1.0 |
| sfx_roll_2.wav (4차에서 바꿈) | cloth3.ogg | 50 RPG sound effects | Kenney | https://opengameart.org/content/50-rpg-sound-effects | CC0 1.0 |
| sfx_roll_3.wav (4차에서 바꿈) | cloth4.ogg | 50 RPG sound effects | Kenney | https://opengameart.org/content/50-rpg-sound-effects | CC0 1.0 |
| sfx_whoosh_1.wav (4차에서 바꿈) | sfx100v2_air_01.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| sfx_whoosh_2.wav (4차에서 바꿈) | sfx100v2_air_02.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| sfx_whoosh_3.wav (4차에서 바꿈) | sfx100v2_air_03.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| sfx_crit_1.wav (4차에서 바꿈) | slam_01.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_crit_2.wav (4차에서 바꿈) | slam_02.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_crit_3.wav (4차에서 바꿈) | slam_03.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_hurt_1.wav | bfh1_hit_01.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_hurt_2.wav | bfh1_hit_02.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_hurt_3.wav | bfh1_hit_03.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_thump_1.wav | slam_04.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_thump_2.wav | slam_05.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_thump_3.wav | slam_06.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_gong_1.wav | gong_01.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_gong_2.wav | gong_02.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_bell_1.wav | bell_01.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_bell_2.wav | bell_02.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_bell_3.wav | bell_03.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_roar_1.wav | roar_01.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| sfx_roar_2.wav | roar_02.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| sfx_roar_3.wav | creature_roar_01.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| sfx_fire_1.wav | spell_fire_01.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| sfx_fire_2.wav | spell_fire_02.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| sfx_fire_3.wav | spell_fire_03.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| sfx_fire_4.wav | spell_fire_04.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| sfx_ice_1.wav | glass_001.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_ice_2.wav | glass_002.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_ice_3.wav | glass_003.ogg | Interface Sounds | Kenney | https://opengameart.org/content/interface-sounds | CC0 1.0 |
| sfx_shatter_1.wav | bfh1_glass_breaking_01.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_shatter_2.wav | bfh1_glass_breaking_02.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_shatter_3.wav | bfh1_glass_breaking_03.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_zap_1.wav | zap1.mp3 | 63 Digital sound effects | Kenney | https://opengameart.org/content/63-digital-sound-effects-lasers-phasers-space-etc | CC0 1.0 |
| sfx_zap_2.wav | zap2.mp3 | 63 Digital sound effects | Kenney | https://opengameart.org/content/63-digital-sound-effects-lasers-phasers-space-etc | CC0 1.0 |
| sfx_zap_3.wav | zapTwoTone.mp3 | 63 Digital sound effects | Kenney | https://opengameart.org/content/63-digital-sound-effects-lasers-phasers-space-etc | CC0 1.0 |
| sfx_spell_1.wav | spell_01.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| sfx_spell_2.wav | spell_02.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| sfx_plop_1.wav | plop_01.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_plop_2.wav | plop_02.ogg | 100 CC0 SFX | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 1.0 |
| sfx_bubble_1.wav | bubble_01.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| sfx_bubble_2.wav | bubble_02.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| sfx_bubble_3.wav | bubble_03.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| sfx_anvil_1.wav | bfh1_metal_hit_01.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_anvil_2.wav | bfh1_metal_hit_02.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_anvil_3.wav | bfh1_metal_hit_03.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_craft_1.wav (4차에서 바꿈) | metalLatch.ogg | 50 RPG sound effects | Kenney | https://opengameart.org/content/50-rpg-sound-effects | CC0 1.0 |
| sfx_craft_2.wav (4차에서 바꿈) | metalClick.ogg | 50 RPG sound effects | Kenney | https://opengameart.org/content/50-rpg-sound-effects | CC0 1.0 |
| sfx_place_1.wav (4차에서 바꿈) | item_stone_01.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| sfx_place_2.wav (4차에서 바꿈) | item_stone_02.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| sfx_place_3.wav (4차에서 바꿈) | item_stone_03.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| sfx_place_4.wav (4차에서 바꿈) | item_stone_04.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| sfx_harvest_1.wav | chop.ogg | 50 RPG sound effects | Kenney | https://opengameart.org/content/50-rpg-sound-effects | CC0 1.0 |
| sfx_harvest_2.wav | handleSmallLeather.ogg | 50 RPG sound effects | Kenney | https://opengameart.org/content/50-rpg-sound-effects | CC0 1.0 |
| sfx_eat_1.wav | eat_01.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| sfx_eat_2.wav | eat_02.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| sfx_eat_3.wav | eat_03.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| sfx_eat_4.wav | eat_04.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| sfx_coin_buy_1.wav (4차에서 바꿈) | chips-stack-1.ogg | 54 casino sound effects | Kenney | https://opengameart.org/content/54-casino-sound-effects-cards-dice-chips | CC0 1.0 |
| sfx_coin_buy_2.wav (4차에서 바꿈) | handleCoins.ogg | 50 RPG sound effects | Kenney | https://opengameart.org/content/50-rpg-sound-effects | CC0 1.0 |
| jingle_victory_1.wav | jingles_NES12.ogg | 85 short music jingles | Kenney | https://opengameart.org/content/85-short-music-jingles | CC0 1.0 |
| jingle_levelup_1.wav | jingles_NES03.ogg | 85 short music jingles | Kenney | https://opengameart.org/content/85-short-music-jingles | CC0 1.0 |
| jingle_learn_1.wav | jingles_NES14.ogg | 85 short music jingles | Kenney | https://opengameart.org/content/85-short-music-jingles | CC0 1.0 |
| jingle_story_1.wav | jingles_NES08.ogg | 85 short music jingles | Kenney | https://opengameart.org/content/85-short-music-jingles | CC0 1.0 |
| jingle_down_1.wav | jingles_NES11.ogg | 85 short music jingles | Kenney | https://opengameart.org/content/85-short-music-jingles | CC0 1.0 |
| voice_slime_cry_1.wav | slime_01.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| voice_slime_cry_3.wav | slime_03.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| voice_slime_cry_5.wav | slime_05.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| voice_slime_attack_7.wav | slime_07.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| voice_slime_attack_9.wav | slime_09.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| voice_slime_hurt_11.wav | slime_11.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| voice_slime_hurt_13.wav | slime_13.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| voice_bug_cry_1.wav | bug_01.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_bug_cry_2.wav | bug_02.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_bug_attack_3.wav | bug_03.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_bug_attack_4.wav | bug_04.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_wisp_cry_1.wav | weird_01.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_wisp_cry_2.wav | weird_02.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_wisp_cry_3.wav | weird_03.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_wisp_attack_4.wav | weird_04.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_wisp_attack_5.wav | weird_05.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_worm_cry_1.wav | monster_01.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_worm_cry_2.wav | monster_02.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_worm_cry_3.wav | monster_03.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_worm_attack_4.wav | monster_04.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_worm_attack_5.wav | monster_05.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_beast_cry_1.wav | barking_01.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_beast_cry_2.wav | howl.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_beast_cry_3.wav | grunt_01.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_beast_attack_2.wav | grunt_02.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_beast_attack_3.wav | grunt_03.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_beast_attack_4.wav | grunt_04.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_beast_hurt_1.wav | hurt_01.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_beast_hurt_2.wav | hurt_02.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_beast_hurt_3.wav | hurt_03.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_golem_cry_1.wav | troll_01.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_golem_cry_2.wav | troll_02.ogg | 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | CC0 1.0 |
| voice_golem_attack_1.wav | stones_01.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| voice_golem_attack_2.wav | stones_02.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| voice_golem_attack_3.wav | stones_03.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| voice_golem_hurt_1.wav | item_stone_01.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| voice_golem_hurt_2.wav | item_stone_02.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| Ambience/amb_lakeside.ogg | sfx100v2_loop_water_03.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| Ambience/amb_waves.ogg | sfx100v2_loop_water_02.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |

## 3차: 빈칸 채우기 (2026-11-27)

사람 요청 "못 찾은 소리들도 다른 CC0 사이트에서 찾아서 넣어줘".

- 효과음은 **Freesound**에서 라이선스를 'Creative Commons 0'으로 걸러 찾고, 소리 페이지마다 라이선스 링크가 publicdomain/zero/1.0인 것을 2026-11-27에 다시 확인했다. 원본 대신 Freesound가 공개로 내주는 미리듣기(HQ mp3, 박쥐 울음만 LQ)를 받아 가공했다(로그인 없이 받을 수 있는 것은 미리듣기뿐이다. 22050 Hz 모노로 줄이므로 차이가 거의 없다).
- 곡은 1·2차처럼 OpenGameArt에서 라이선스가 CC0 하나뿐인 것만 받았다.
- 가공: 긴 녹음(걸음 여러 번)은 소리가 시작하는 곳마다 잘라 큰 것부터 몇 개를 골랐다(앞 5 ms 여유, 길이 상한, 끝 페이드, 발소리 -14 dBFS, 그 밖 -8 dBFS). 곡은 2차와 같은 방법(60초 안쪽, 반복판은 그대로).
- 코드: 바닥 종류에 벽돌(`BrickFloor`), 쇠 격자(`Grate`), 철벅이는 물(얕은 물 `ShallowWater`; 호수 맨바닥은 젖은 발 그대로)을 새로 나눴다. 얼음 바닥은 눈 발소리를 높여 쓰던 것을 얼음 녹음으로. 김(`Sizzle`), 용암 지대·용암 환경음(`Forge`, `Lava`), 박쥐·게·버섯 울음, 모든 장소 곡.
- 그래도 못 넣은 것: 버섯 공격·맞음 울음, 게 맞음 울음(합성 그대로). Freesound의 으깸 소리 하나는 미리듣기 받기가 막혀(403) 뺐다.

### 3차 배경음악 파일

| 파일 | 원본 파일 | 곡·팩 | 작가 | 출처 페이지 | 쓰는 곳 | 반복 | 라이선스 |
|---|---|---|---|---|---|---|---|
| music_kingdom.ogg | CHIPTUNE_Loop_The_Bards_Tale.wav | Chiptune Medieval: The Bard's Tale (loop) | RandomMind | https://opengameart.org/content/chiptune-medieval-the-bards-tale | 왕국 | 전체(반복판) | CC0 1.0 |
| music_hut.ogg | CHIPTUNE_Loop_The_Old_Tower_Inn.wav | Chiptune Medieval: The Old Tower Inn (loop) | RandomMind | https://opengameart.org/content/chiptune-medieval-the-old-tower-inn | 오두막 | 전체(반복판) | CC0 1.0 |
| music_harbor.ogg | port_town_in_game.ogg | Port Town Loop | beardalaxy | https://opengameart.org/content/port-town-loop | 항구 | 전체(반복판) | CC0 1.0 |
| music_ruins.ogg | Ruined City Theme.wav | Ruined City Theme | Cleyton Kauffman | https://opengameart.org/content/ruined-city-theme | 유적 | 0–60초, 3초 겹침 | CC0 1.0 |
| music_nest.ogg | Evil4 - Witch's Lair.ogg | JRPG Music Pack #3 [Evil] | Juhani Junkala (SubspaceAudio) | https://opengameart.org/content/jrpg-pack-3-evil | 마족 둥지 | 0–60초, 2초 겹침 | CC0 1.0 |
| music_mine.ogg | Evil2 - Catacombs.ogg | JRPG Music Pack #3 [Evil] | Juhani Junkala (SubspaceAudio) | https://opengameart.org/content/jrpg-pack-3-evil | 광산 | 0–60초, 3초 겹침 | CC0 1.0 |

### 3차 효과음·환경음 파일 (Freesound)

| 파일 | 원본 이름 | 저자 | 출처 페이지 | 쓰는 곳 | 라이선스 |
|---|---|---|---|---|---|
| footstep_ice_1~5 | footsteps - ice 09.wav | Anthousai | https://freesound.org/people/Anthousai/sounds/539683/ | 얼음 바닥 발소리 | CC0 1.0 |
| footstep_brick_1~4 (4차에서 바꿈) | Footsteps on concrete | florianreichelt | https://freesound.org/people/florianreichelt/sounds/459964/ | 벽돌 바닥 발소리 | CC0 1.0 |
| footstep_grate_1~5 (4차에서 바꿈) | Metal Grate Running | Oilcan03 | https://freesound.org/people/Oilcan03/sounds/786977/ | 쇠 격자 발소리 | CC0 1.0 |
| footstep_splash_1~4 | Splashing Footsteps Shallow Water | ChristopherJngs | https://freesound.org/people/ChristopherJngs/sounds/861369/ | 얕은 물(철벅) 발소리 | CC0 1.0 |
| sfx_sizzle_1~2 | G39-24-Steam Hiss.wav | craigsmith | https://freesound.org/people/craigsmith/sounds/438641/ | 김·불과 물(Sizzle) | CC0 1.0 |
| sfx_sizzle_3 | Water Poured onto a Burning Fireplace – Hissing Steam and Sizzling Embers | bassimat | https://freesound.org/people/bassimat/sounds/870180/ | 김·불과 물(Sizzle) | CC0 1.0 |
| Ambience/amb_lava.ogg | Lava Loop 1 | Fission9 | https://freesound.org/people/Fission9/sounds/474849/ | 용암 지대·용암 환경음(20초 반복) | CC0 1.0 |
| voice_bat_cry_1~2 | Bat Screech | Yoyodaman234 | https://freesound.org/people/Yoyodaman234/sounds/667579/ | 박쥐 울음 | CC0 1.0 |
| voice_bat_hurt_1 | Cartoon - Bat / Mouse Squeak | Breviceps | https://freesound.org/people/Breviceps/sounds/445958/ | 박쥐 맞음 | CC0 1.0 |
| voice_crab_attack_1~3 | crab walking | stuniverso | https://freesound.org/people/stuniverso/sounds/761559/ | 게 공격(집게 딸깍) | CC0 1.0 |
| voice_crab_cry_1~3 | Crab - Legs | Vrymaa | https://freesound.org/people/Vrymaa/sounds/770031/ | 게 알아챔(다리 소리) | CC0 1.0 |
| voice_mushroom_cry_1~3 | puff.wav | Melilaura | https://freesound.org/people/Melilaura/sounds/531522/ | 버섯 알아챔(퍽) | CC0 1.0 |

## 4차: 장면을 보며 다시 고르기 (2026-11-27, 로컬)

사람 요청 "효과음이 어울리지 않는 게 많은데 Sonniss, Kenney는 로컬에서 Claude in Chrome으로 진행하면 접근이 될 것 같다", 이어서 "발소리, 배경음악 포함한 모든 사운드 다 다시 골라줘", "사운드만 보고 판단하기는 힘든데 예시 이미지 gif나 다른 샘플을 보여주고 선택하는게 나을 것 같아".

- **Kenney**: kenney.nl에서 오디오 팩 8개(Impact Sounds, RPG Audio, Interface Sounds, UI Audio, Sci-fi Sounds, Digital Audio, Casino Audio, Music Jingles)를 사람의 브라우저로 받았다. 페이지마다 'License: Creative Commons CC0'(creativecommons.org/publicdomain/zero/1.0/), 팩 안의 License.txt도 'License: (Creative Commons Zero, CC0)'와 'This content is free to use in personal, educational and commercial projects'를 2026-11-27에 직접 확인했다. 출처 표기는 의무가 아니다(Kenney: 'this is not mandatory').
- **Sonniss GDC 번들은 이번에도 쓰지 않았다.** 공식 라이선스(https://sonniss.com/gdc-bundle-license/, Version 2.0, 2026-08-27 시행)를 직접 열어 봤다. 상업용·출처 표기 없이·평생 무제한 프로젝트에 쓸 수 있지만, RESTRICTIONS c)가 효과음을 가공했든 아니든 파일로 다른 사람에게 배포·게시하는 것을 서면 허락 없이 금한다('Re-designed and manipulated sounds remain licensed material'). 이 저장소는 공개라 `Resources/Sfx`의 wav를 넣으면 누구나 그 파일을 받을 수 있게 되므로 애매하다고 보고 뺐다. 저장소를 비공개로 하거나 Sonniss에 서면 허락을 받으면 다시 볼 수 있다. AI 학습에 쓰는 것도 금지다.
- **고른 방법**: 세트마다 지금 소리와 Kenney 후보 2~3개를 1차와 같은 방식으로 가공하고(모노 22050 Hz, 50 Hz 고역 통과, 앞 4 ms 여유로 무음 자르기, 꼬리 자르기와 30 ms 페이드, 길이 상한, 피크 정규화: 효과음 -6, UI -8, 마우스오버 -12, 발소리 -14 dBFS, 16-bit PCM), 웹 데모를 로컬에서 띄워 시험 명령으로 장면 약 60개(때리기, 맞기, 구르기, 캐기, 상자, 먹기, 바닥 14종 걷기, 몬스터 9종, 보스, 가방 창 등)를 녹화했다. 녹화 중 게임이 낸 녹음 소리는 파형으로 어느 세트인지 알아내 끄고 시각만 적어, 듣기 페이지에서 장면을 틀며 그 순간에 후보 소리를 넣었다. 사람이 장면을 보며 98세트 가운데 94세트를 골랐다.
- **바꾼 것**: 아래 표 19세트(Kenney 후보). 그 밖에 치명타 `sfx_crit`는 지금 맞힘(`sfx_hit`) 소리를 +3 반음 높여 만들었고(사람: "원래 타격음이 살짝 더 높은 톤으로"), 제작 `sfx_craft`는 모루 소리(`sfx_anvil`, 2차 rubberduck "75 CC0 breaking / falling / hit sfx")를 그대로 썼고(사람: "모루를 내리치는 소리로"), 피하기·돌진 `sfx_whoosh`는 지금 소리를 0.28초로 줄였다(사람: "더 짧았으면").
- **지금 소리 그대로**(사람이 "지금 소리"를 고름): 휘두르기, 깨짐, 보스 포효, 바위 부서짐, 모루, 문, 먹기, 물(보글·튀김), 얼음 바닥·얕은 물 발소리, 몬스터 울음 20세트 전부, 배경음악 15곡, 환경음 3개.

### 4차에서 아직 못 한 것 (다음에)

사람이 "다른 곳에서 새로 찾기"를 고른 것. Kenney에 맞는 것이 없어 다른 CC0 출처나 생성 도구로 후보를 만들어 다시 장면과 함께 들려준다.

- 뒤로·취소 `sfx_ui_cancel`
- 내려찍기 `sfx_thump`: "땅을 쿵 찍는 소리"
- 몬스터 부르기·수정 울림 `sfx_bell`: "좀 더 마법을 쓰는 느낌"
- 불 `sfx_fire`: "불이 타오르는 듯한 소리에 폭발음은 너무 크지 않게 깔리게"(지금 폭발음이 너무 큼), 김 `sfx_sizzle`도 "불이 타오르는 듯한 소리로"
- 얼음 `sfx_ice`: "쩌적 갈라지는 청량한 소리", 번개 `sfx_zap`: "지지직거리는 소리"
- 징글: 승리 "더 신나게", 레벨업 "더 높은 음으로 기쁘게", 기술 배움, 쓰러짐 "슬픈 음악으로"
- 발소리: 눈 "뽀드득", 낙엽 "바스락", 젖은 발(호수) "더 끈적거리는 듯", 거친 바위, 모래
- 배경음악: 용암 지대 "용암이 흐르는 듯한", 모래 동굴 "더 이집트 느낌", 보스전·오두막·왕국 "비프음 같으니 기조는 두고 소리를 다듬어"

코드가 필요한 것(클라우드에 부탁, `CLOUD_LOG.md` 2026-11-27 로컬 항목): 마지막 휘두르기가 너무 큼, 내가 맞음을 캐릭터 성별에 따른 짧은 신음으로(새 세트), 보스 경고음 빼기, 버섯 소환 때 칼 휘두르는 이펙트가 나오는 것, 마법 폭발(Nova)을 원소별 소리로, 수확을 수확 물건마다 다르게(새 세트), 보스 포효를 보스마다 다르게(나중).

### 4차 효과음 파일 (Kenney, kenney.nl에서 받음)

| 파일 | 원본 파일 이름 | 팩 | 저자 | 출처 페이지 | 라이선스 | 받은 날 |
|---|---|---|---|---|---|---|
| sfx_ui_click_1.wav | click1.ogg | UI Audio | Kenney | https://kenney.nl/assets/ui-audio | CC0 1.0 | 2026-11-27 |
| sfx_ui_click_2.wav | click3.ogg | UI Audio | Kenney | https://kenney.nl/assets/ui-audio | CC0 1.0 | 2026-11-27 |
| sfx_ui_hover_1.wav | rollover4.ogg | UI Audio | Kenney | https://kenney.nl/assets/ui-audio | CC0 1.0 | 2026-11-27 |
| sfx_ui_hover_2.wav | rollover5.ogg | UI Audio | Kenney | https://kenney.nl/assets/ui-audio | CC0 1.0 | 2026-11-27 |
| sfx_ui_open_1.wav | bookFlip3.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| sfx_ui_open_2.wav | bookOpen.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| sfx_ui_close_1.wav | bookClose.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| sfx_ui_close_2.wav | bookPlace3.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| sfx_ui_confirm_1.wav | switch3.ogg | UI Audio | Kenney | https://kenney.nl/assets/ui-audio | CC0 1.0 | 2026-11-27 |
| sfx_ui_confirm_2.wav | switch4.ogg | UI Audio | Kenney | https://kenney.nl/assets/ui-audio | CC0 1.0 | 2026-11-27 |
| sfx_ui_fail_1.wav | error_004.ogg | Interface Sounds | Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 | 2026-11-27 |
| sfx_ui_fail_2.wav | error_008.ogg | Interface Sounds | Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 | 2026-11-27 |
| sfx_roll_1.wav | cloth1.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| sfx_roll_2.wav | cloth2.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| sfx_roll_3.wav | cloth3.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| sfx_pickaxe_1.wav | impactMining_002.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| sfx_pickaxe_2.wav | impactMining_004.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| sfx_pickup_1.wav | handleSmallLeather.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| sfx_pickup_2.wav | handleSmallLeather2.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| sfx_place_1.wav | impactWood_medium_000.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| sfx_place_2.wav | impactWood_medium_001.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| sfx_place_3.wav | impactWood_medium_002.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| sfx_chest_open_1.wav | metalClick.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| sfx_chest_open_2.wav | metalLatch.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| sfx_coin_buy_1.wav | handleCoins.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| sfx_coin_buy_2.wav | handleCoins2.ogg | RPG Audio | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 | 2026-11-27 |
| footstep_dirt_1.wav | footstep_grass_000.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_dirt_2.wav | footstep_grass_001.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_dirt_3.wav | footstep_grass_002.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_dirt_4.wav | footstep_grass_004.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_stone_alt_1.wav | footstep_concrete_000.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_stone_alt_2.wav | footstep_concrete_001.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_stone_alt_3.wav | footstep_concrete_002.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_stone_alt_4.wav | footstep_concrete_003.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_stone_alt_5.wav | footstep_concrete_004.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_wood_alt_1.wav | footstep_wood_000.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_wood_alt_2.wav | footstep_wood_001.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_wood_alt_3.wav | footstep_wood_002.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_wood_alt_4.wav | footstep_wood_003.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_wood_alt_5.wav | footstep_wood_004.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_carpet_1.wav | footstep_carpet_000.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_carpet_2.wav | footstep_carpet_001.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_carpet_3.wav | footstep_carpet_002.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_carpet_4.wav | footstep_carpet_003.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_carpet_5.wav | footstep_carpet_004.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_brick_1.wav | footstep_concrete_000.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_brick_2.wav | footstep_concrete_002.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_brick_3.wav | footstep_concrete_004.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_grate_1.wav | impactMetal_light_000.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_grate_2.wav | impactMetal_light_001.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_grate_3.wav | impactMetal_light_002.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_grate_4.wav | impactMetal_light_004.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_metal_1.wav | impactMetal_light_001.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_metal_2.wav | impactMetal_light_002.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| footstep_metal_3.wav | impactMetal_light_004.ogg | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| sfx_crit_1~3.wav | (sfx_hit_1~3 = impactPunch_medium_000·002·004를 +3 반음) | Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 | 2026-11-27 |
| sfx_craft_1~3.wav | (sfx_anvil_1~3 그대로) | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 | 2026-11-27 |
| sfx_whoosh_1~3.wav | (2차 sfx_whoosh를 0.28초로) | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 | 2026-11-27 |
