# 녹음 효과음 출처 (2026-11-11)

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
| footstep_dirt_1.wav | footstep00.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| footstep_dirt_2.wav | footstep01.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| footstep_dirt_3.wav | footstep02.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| footstep_dirt_4.wav | footstep03.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| footstep_dirt_5.wav | footstep06.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| footstep_dirt_6.wav | footstep07.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
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
| footstep_metal_1.wav | metal_steps_01.wav | Metal footsteps on concrete | Thimras | https://opengameart.org/content/metal-footsteps-on-concrete | CC0 1.0 |
| footstep_metal_2.wav | metal_steps_02.wav | Metal footsteps on concrete | Thimras | https://opengameart.org/content/metal-footsteps-on-concrete | CC0 1.0 |
| footstep_metal_3.wav | metal_steps_04.wav | Metal footsteps on concrete | Thimras | https://opengameart.org/content/metal-footsteps-on-concrete | CC0 1.0 |
| footstep_metal_4.wav | metal_steps_05.wav | Metal footsteps on concrete | Thimras | https://opengameart.org/content/metal-footsteps-on-concrete | CC0 1.0 |
| footstep_water_1.wav | sfx100v2_footstep_wet_01.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| footstep_water_2.wav | sfx100v2_footstep_wet_02.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| footstep_water_3.wav | sfx100v2_footstep_wet_03.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| footstep_carpet_1.wav | footstep_carpet_000.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_carpet_2.wav | footstep_carpet_001.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| footstep_carpet_3.wav | footstep_carpet_003.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_pickaxe_1.wav | impactMining_000.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_pickaxe_2.wav | impactMining_001.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_pickaxe_3.wav | impactMining_003.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_rockbreak_1.wav | bfh1_rock_breaking_02.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_rockbreak_2.wav | bfh1_rock_breaking_01.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_rockbreak_3.wav | bfh1_rock_breaking_03.ogg | 75 CC0 breaking / falling / hit sfx | rubberduck | https://opengameart.org/content/75-cc0-breaking-falling-hit-sfx | CC0 1.0 |
| sfx_swing_1.wav | swish-3.wav | Swishes Sound Pack | artisticdude | https://opengameart.org/content/swishes-sound-pack | CC0 1.0 |
| sfx_swing_2.wav | swish-5.wav | Swishes Sound Pack | artisticdude | https://opengameart.org/content/swishes-sound-pack | CC0 1.0 |
| sfx_swing_3.wav | swish-9.wav | Swishes Sound Pack | artisticdude | https://opengameart.org/content/swishes-sound-pack | CC0 1.0 |
| sfx_hit_1.wav | impactPunch_medium_000.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_hit_2.wav | impactPunch_medium_002.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_hit_3.wav | impactPunch_medium_004.ogg | Impact Sounds (1.0) | Kenney | https://kenney.nl/assets/impact-sounds | CC0 1.0 |
| sfx_pickup_1.wav | drop_002.ogg | Interface Sounds (1.0) | Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 |
| sfx_pickup_2.wav | drop_003.ogg | Interface Sounds (1.0) | Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 |
| sfx_ui_click_1.wav | click_001.ogg | Interface Sounds (1.0) | Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 |
| sfx_ui_click_2.wav | select_001.ogg | Interface Sounds (1.0) | Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 |
| sfx_ui_hover_1.wav | tick_001.ogg | Interface Sounds (1.0) | Kenney | https://kenney.nl/assets/interface-sounds | CC0 1.0 |
| sfx_door_open_1.wav | doorOpen_1.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| sfx_door_close_1.wav | doorClose_1.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| sfx_chest_open_1.wav | open chest_0.wav | open chest SFX | Oiboo | https://opengameart.org/content/open-chest-sfx | CC0 1.0 |
| sfx_chest_open_2.wav | creak1.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| sfx_splash_1.wav | splash_03.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| sfx_splash_2.wav | splash_09.ogg | 40 CC0 water / splash / slime SFX | rubberduck | https://opengameart.org/content/40-cc0-water-splash-slime-sfx | CC0 1.0 |
| sfx_coin_1.wav | handleCoins2.ogg | RPG Audio (1.0) | Kenney | https://kenney.nl/assets/rpg-audio | CC0 1.0 |
| sfx_coin_2.wav | item_coins_01.ogg | 80 CC0 RPG SFX | rubberduck | https://opengameart.org/content/80-cc0-rpg-sfx | CC0 1.0 |
| footstep_stone_alt_1.wav | step1.wav | Sound Effects Pack (Owlish Media Sound Effects.zip) | OwlishMedia | https://opengameart.org/content/sound-effects-pack | CC0 1.0 |
| footstep_stone_alt_2.wav | step2.wav | Sound Effects Pack (Owlish Media Sound Effects.zip) | OwlishMedia | https://opengameart.org/content/sound-effects-pack | CC0 1.0 |
| footstep_stone_alt_3.wav | step3.wav | Sound Effects Pack (Owlish Media Sound Effects.zip) | OwlishMedia | https://opengameart.org/content/sound-effects-pack | CC0 1.0 |
| footstep_stone_alt_4.wav | step4.wav | Sound Effects Pack (Owlish Media Sound Effects.zip) | OwlishMedia | https://opengameart.org/content/sound-effects-pack | CC0 1.0 |
| footstep_stone_alt_5.wav | stone01.ogg | Different steps on wood, stone, leaves, gravel and mud | TinyWorlds | https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud | CC0 1.0 |
| footstep_wood_alt_1.wav | sfx100v2_footstep_wood_01.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| footstep_wood_alt_2.wav | sfx100v2_footstep_wood_03.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| footstep_wood_alt_3.wav | sfx100v2_footstep_wood_04.ogg | 100 CC0 SFX #2 | rubberduck | https://opengameart.org/content/100-cc0-sfx-2 | CC0 1.0 |
| footstep_wood_alt_4.wav | wood01.ogg | Different steps on wood, stone, leaves, gravel and mud | TinyWorlds | https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud | CC0 1.0 |
| footstep_wood_alt_5.wav | wood03.ogg | Different steps on wood, stone, leaves, gravel and mud | TinyWorlds | https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud | CC0 1.0 |
| footstep_dirt_alt_1.wav | mud02.ogg | Different steps on wood, stone, leaves, gravel and mud | TinyWorlds | https://opengameart.org/content/different-steps-on-wood-stone-leaves-gravel-and-mud | CC0 1.0 |
| footstep_grass_alt_1.wav | grassy-footstep2.wav | Sound Effects Pack (Owlish Media Sound Effects.zip) | OwlishMedia | https://opengameart.org/content/sound-effects-pack | CC0 1.0 |
| footstep_grass_alt_2.wav | grassy-footstep3.wav | Sound Effects Pack (Owlish Media Sound Effects.zip) | OwlishMedia | https://opengameart.org/content/sound-effects-pack | CC0 1.0 |
