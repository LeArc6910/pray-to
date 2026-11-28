# 도트 그림 바깥 자료와 생성형 AI (2026-11-11)

사람 요청 "도트 이미지도 사운드처럼 무료 공개 소스가 있다면 활용해보고, 무료 생성형 ai를 붙여보자". 소리는 `Docs/SoundCredits.md`.

## 한 일

- **CC0 꾸러미 12개를 받아 우리 그림과 나란히 견줬다**(저장소 밖에서). 결론:
  - **아이템 아이콘**: Ninja Adventure(pixel-boy & AAA, CC0, https://pixel-boy.itch.io/ninja-adventure-asset-pack, 페이지에 "No generative AI was used")가 진한 외곽선·3~4단 명암·16×16으로 우리 화풍에 가장 가깝고, 지금 우리 10×10 아이콘보다 정교하다. **모양만 들여오고 색은 우리 재료 색 띠로 칠한다**(`ItemArt`는 한 모양을 등급 색으로 다시 칠하는 체계라서).
  - **UI 틀**: Buch "Golden UI"(CC0)와 Kenney "Fantasy UI Borders"(CC0)의 구조(어두운 판, 얇은 금빛 테, 모서리 장식)를 참고해 `UiArt`를 우리 색으로 새로 그렸다(그림 파일은 가져오지 않음). Ninja Adventure UI는 밝은 주황이라 동굴 화면에 튀어서 쓰지 않았다.
  - **동굴 타일·캐릭터·몬스터**: 바꾸지 않는다. 받은 타일은 벽돌 던전이거나 외곽선 없이 납작하고, 캐릭터·몬스터는 비율·시점이 달라 섞으면 티가 난다. 지금 코드 그림이 서로 맞는다.
- **들여온 것**: 곡괭이(`ItemArt.Pick16`)와 주괴(`ItemArt.BarOf`)를 Ninja Adventure에서. 곡괭이 머리는 등급 금속 색(구리·철·미스릴·흑요석·공허), 자루는 우리 나무 색, 주괴는 재료 색. 외곽선은 우리 외곽선 색. `ArtPreview`가 `imported.png`로 그린다.
- **더 들여온 것 (2026-11-11, 사람 요청 "다른 아이콘도 같은 방식으로")**: 재료 색으로 칠하는 틀 셋을 Ninja Adventure 모양으로 바꿨다. 물고기(`Fish16`), 보석(`Gem15`, 보스 재료·결정·정수 등), 병(`Jar`, 주스·술·우유·차). 물약(하트 병, `HeartJar`), 괭이, 물뿌리개, 귀환 두루마리는 원래 색 그대로. 요리 그릇·씨앗·동전·광석 덩어리는 그대로 두었다
- **변환 도구**: `DevTools/ArtImport/to_rows.py`가 PNG를 `PixelImage.FromRows` 격자 글자로 바꾼다. `--ramp`로 한 재료의 색을 L·M·m·d 글자로 받아 등급 색으로 칠할 수 있게 한다. 생성형 AI로 만든 그림이 생겨도 같은 길로 들어온다. `.meta`가 필요 없어 클라우드가 혼자 끝내고, 그림 검사(`ArtLint`)와 웹 데모를 그대로 거친다.

## 다음 후보 (사람이 고를 것)

- 요리 그릇(국·볶음·꼬치)은 Ninja 쪽 음식(국수·꼬치·초밥)으로 바꿀 수 있지만 우리 요리와 1:1로 맞지 않아 두었다.
- 출처 표기가 필요한 꾸러미(CC-BY: DawnLike, Kyrise 아이콘 등)는 쓰지 않았다. 쓰려면 크레딧 화면이 필요하다.

## 무료 생성형 AI (직접 해 본 결과)

- **키 없이 되는 곳은 쓸 수 없다**: Pollinations는 워터마크 찍힌 흐릿한 JPEG, AI Horde(익명)는 결과까지 약 30분에 도트가 아니거나 엉뚱한 그림. 16×16으로 줄이면 뭉개진다. Hugging Face의 도트 Space들은 모두 실행 오류였다.
- **16px 게임 그림을 제대로 만드는 곳은 Retro Diffusion과 PixelLab뿐**: 둘 다 가입과 API 키가 필요하고 무료 몫은 시험 몇 장(Retro Diffusion 새 계정 $0.50, 약 16장). 상업 이용은 약관상 된다.
- 넉넉한 무료(Cloudflare Workers AI 하루 약 230장)는 범용 모델이라 도트로는 다시 그려야 한다.
- 생성 그림을 게임에 넣으면 Steam의 AI 공개 대상이 되고, 순수 AI 결과는 저작권 보호가 약하다(`Docs/Research2026-11.md`).
- **그래서 붙이지 않았다.** 쓰고 싶으면 Retro Diffusion 키를 주면(채팅이 아니라 환경의 비밀값으로) 몇 장 만들어 위 변환 도구로 넣어 견줄 수 있다.

## 로컬 도트 생성 도구와 유료 모음 사이트 조사 (2026-11-11)

사람 요청 "로컬로 도트 스프라이트, 애니메이션 만들 만한 생성형 ai 툴, retro diffusion 같은 유료 생성형 ai를 모아 서비스하는 힉스필드 같은 사이트". 웹 조사만 했고 가입·결제·설치는 하지 않았다. 가격·조건은 2026-11-11에 공식 페이지에서 본 것이고 자주 바뀐다.

### 먼저 결론

- **16px 캐릭터의 8방향 걷기를 바로 쓸 수 있게 만드는 도구는 로컬에도 유료에도 없다.** 캐릭터 애니메이션은 유료도 32px부터다(PixelLab 8방향 32~64px, Retro Diffusion 32~256px). 16px로 바로 잘 나오는 것은 타일과 아이콘이다.
  - **바로잡음(2026-11-12)**: 처음에 "Retro Diffusion 걷기는 4방향 48px·32px뿐"이라고 적었는데, 모음 사이트(Replicate)에 올라온 모델만 본 것이었다. Retro Diffusion 직접 API(공식 예제 https://github.com/Retro-Diffusion/api-examples)에는 **8방향 보기 만들기**(`rd_advanced_animation__rotate` $0.10: 올린 스프라이트의 나머지 7방향, `rd_animation__8_dir_rotation` $0.25)와 **올린 그림을 움직이는 고급 애니메이션**(`walking`·`idle`·`attack` 등 $0.14, 32~256px, 4~16프레임)이 있다. 그래서 "8방향 보기를 만들고 → 방향마다 걷기를 만드는" 식으로 8방향 걷기를 만들 수 있다. 다만 한 번에 8방향 걷기 시트를 주는 기능은 아니고, 크기는 32px부터라 우리 캐릭터(16px대)와는 여전히 안 맞는다
- 현실적인 길은 "크게 그리기 → 격자 찾아 진짜 해상도로 줄이기 → 우리 팔레트로 맞추기 → 손질". 8방향과 걷기 프레임은 각도 바꾸기 모델과 영상 모델로 밑그림을 만들고 다듬는다.
- 상업 게임에는 Apache·MIT 라이선스 모델(Z-Image, FLUX.2 klein 4B, Qwen-Image-Edit, Wan2.2, TRELLIS.2)이 안전하다. FLUX.1-dev·FLUX.2 klein 9B(비상업), Hunyuan3D(라이선스가 한국을 적용 지역에서 뺀다), LPC 기반 LoRA는 피한다.

### 로컬에서 도는 것

| 도구 | 하는 일 | 필요한 것 | 가격·출력물 |
|---|---|---|---|
| Retro Diffusion Aseprite 확장 (astropulse.itch.io/retrodiffusion) | Aseprite 안에서 로컬 모델로 타일·아이템·아이콘, 팔레트 맞추기, 픽셀 복구 | RTX VRAM 4GB(GTX·AMD 6GB), RAM 16GB, Aseprite | 한 번 사면 $65(Lite $20). 출력물은 산 사람 것. 로컬 모델은 애니메이션을 못 하고, 애니메이션은 확장 안에서 온라인 크레딧. 곧 전용 편집기 "Pixelit"으로 넘어간다 |
| Grimoire (blackmagikstudios.itch.io/grimoire) | 도트 편집기 + 로컬 ComfyUI: 생성, 이미지→영상 애니메이션, 시트 내보내기, 팔레트 줄이기 | VRAM 6GB(SDXL 8GB), 애니메이션 12GB 이상(권장 24GB), Windows | $30. 크레딧 없음 |
| PixelAI Aseprite 확장 (red335.itch.io) | 로컬 SDXL/FLUX(Pixel Art XL), 또는 자기 Replicate 키로 Retro Diffusion | VRAM 6GB(FLUX 10GB), Windows | 무료, 상업 이용 가능 |
| PixelLab Aseprite 플러그인 | 생성은 PixelLab 클라우드가 한다(로컬 GPU 불필요) | Aseprite 1.3.7 이상 | 구독 $12~50/월, 출력물 소유·상업 가능 |
| ComfyUI + 무료 모델 | 아래 조합 | 아래 | 모델마다 라이선스 확인 |

ComfyUI에서 쓸 무료 모델:
- 16px 아이콘·타일: SDXL + pixel-icon-lora-sdxl(16×16 격자에 맞춰 그림, Apache-2.0, VRAM 8GB쯤)
- 캐릭터 밑그림: Z-Image-Turbo + 도트 LoRA(Apache-2.0, 16GB)
- 8방향: Qwen-Image-Edit-2511 + Multiple-Angles LoRA(그림 한 장에서 45° 간격 8방향 × 높이 4단계, Apache-2.0, GGUF로 12~16GB, 24GB면 편함). 공개 도구 중 8방향 밑그림에 가장 가깝다
- 걷기·공격 프레임: Wan2.2 TI2V-5B(이미지→영상, Apache-2.0, 오프로딩하면 8GB, 느림)
- 소품 여러 각도: FLUX.2 klein 4B + 스프라이트 시트 LoRA(2×2, Apache-2.0, fp8로 9GB쯤)
- 후처리(필수): proper-pixel-art(격자 찾아 줄이기, 영상의 모든 프레임에 같은 격자·팔레트, MIT, 2026-10 갱신), unfake.js, pixeldetector, ComfyUI-PixelArt-Detector(팔레트 변환)

**추천 조합**
1. 돈 안 들이기(VRAM 8~16GB): 위 ComfyUI 조합. 결과를 우리 팔레트로 맞춘 뒤 `DevTools/ArtImport/to_rows.py`로 코드 그림에 넣거나, 사람이 손질하는 밑그림으로 쓴다.
2. 쉽게(돈 조금): Retro Diffusion 확장 $65. 정지 그림(16px 타일·아이콘)은 로컬에서 무제한, 애니메이션만 온라인 크레딧.

### 유료 모델을 모아 서비스하는 곳

- **Higgsfield는 영상·광고 중심이다.** 이미지 모델(Seedream, GPT Image 등)을 35개 넘게 모았지만 도트·스프라이트 기능은 없다. 무료는 상업 이용 불가. 16px 도트에는 맞지 않는다.
- **Retro Diffusion을 모아 둔 곳**: Replicate(rd-fast·rd-plus·rd-tile·rd-animation을 쓴 만큼: 16px 한 장 $0.017~0.024, 애니메이션 $0.07, API), Scenario(RD 3종 + 자기 스타일 학습, $15/월부터, **무료로 만든 것은 상업 이용 불가**), Poe(RD 공식 봇, 처음 50회 무료로 맛보기).
- **PixelLab은 모음 사이트에서 찾지 못했다.** 직접 가입해 쓴다.
- 그 밖: Ludo.ai(3D를 거친 8방향 시트·애니메이션, $15/월부터, 16px 품질은 미확인), Layer.ai·fal.ai(후처리나 직접 짜는 파이프라인용), Krea·OpenArt·Magnific(옛 Freepik)·Leonardo(범용이라 도트에 안 맞음), Rosebud(게임 만드는 플랫폼이라 우리 Unity 프로젝트와 안 맞음).

**16px 도트 게임 에셋용 순위**: 1. Retro Diffusion 직접(웹·API, 충전식, 출력물 소유) 2. Replicate의 Retro Diffusion(쓴 만큼) 3. PixelLab 직접($12/월, 8방향 캐릭터·스켈레톤 걷기·타일셋) 4. Scenario 5. Ludo.ai 6. Poe(맛보기) 7. Layer.ai·fal.ai 8. Krea·OpenArt·Magnific·Leonardo·Higgsfield.

### 주의

- Steam: 플레이어가 보는 그림·소리·글에 생성형 AI 결과를 쓰면 스토어 설문에서 공개해야 한다(2026-01 개정으로 개발 도구로만 쓴 것은 공개하지 않아도 된다는 보도, Steamworks 원문은 확인 못 함).
- 확인 못 한 것: PixelLab 월 생성 횟수 공식 수치, Scenario의 RD 모델 단가, Replicate·Poe의 무료 몫과 상업 조건, Leonardo·Civitai·Tensor.art·SeaArt의 공식 조건, Retro Diffusion 확장 라이선스 원문.

## 그림·효과음·음악·3D를 한 곳에서: 결제할 곳 고르기 (2026-11-11)

사람 요청 "pixellab, retro diffusion 포함해서 효과음, bgm까지 한번에 해결할 수 있는 ai api 사이트, 차후 2d 이미지나 3d 모델링까지 고려해서 결제할 만한 곳". 웹 조사만 했다. 가격은 공식 페이지 기준이고 자주 바뀐다.

### 결론

- **Retro Diffusion과 PixelLab을 둘 다 주는 곳은 없다.** PixelLab은 직접 API·MCP뿐이다.
- 모음 사이트의 Retro Diffusion은 4종(Fast·Plus·Tile·Animation)뿐이라 **8방향 회전·고급 동작·RD Pro는 Retro Diffusion 직접 API에만 있다**(8방향 회전 $0.25, 업로드한 스프라이트 8방향 돌리기 $0.10, 동작 $0.14).
- 도트(RD)+효과음+음악+2D+3D를 한 결제로 덮는 곳은 **Scenario**(구독, 공식 MCP)와 **Replicate**(충전식, 효과음은 Stable Audio로 대신)다. Ludo.ai도 다섯 분야를 덮지만 도트는 자체 모델이고 소리 모델 이름을 밝히지 않는다.

### 사이트별 요점

| 곳 | 도트 | 효과음 | 음악 | 3D | 결제 |
|---|---|---|---|---|---|
| Scenario | RD Plus·Tile·Animation(4방향) | ElevenLabs SFX 2 | ElevenLabs·MiniMax·Lyria | Tripo·Rodin·Meshy·Trellis | 구독 $15/월(1,500CU)부터, CU 매달 소멸, 무료 출력은 비상업 |
| Replicate | RD 4종(16~32px 한 장 $0.017~0.025, 애니 $0.07) | Stable Audio 2.5($0.20/파일) | ElevenLabs Music·MiniMax·Lyria·ACE-Step | Rodin $0.40, TRELLIS2 | 충전식 쓴 만큼 |
| fal.ai | RD·PixelLab 없음(변환 도구만) | ElevenLabs SFX(2초 $0.004) | MiniMax $0.03/곡, Lyria 3 Pro $0.08, ElevenLabs $0.60/분 | Tripo $0.20~0.40, Rodin $0.40, Meshy $0.80 | 충전식, 크레딧 1년 만료 |
| Retro Diffusion 직접 | Fast $0.03, Plus $0.06, Pro $0.18, 8방향 회전, 동작 애니, 타일셋 | 없음 | 없음 | 도트 텍스처 로우폴리 | 충전 $5부터, 만료 없음, 공식 MCP |
| PixelLab 직접 | 8방향 캐릭터(64px 약 $0.017), 스켈레톤 애니, 타일셋 | 없음 | 없음 | 없음 | 구독 약 $12/월 |
| Ludo.ai | 자체 도트, 8방향 스프라이트 | 있음(모델 비공개) | 있음(모델 비공개) | 있음, 리깅 | 구독 $15/월(연간)부터, 상업 이용 됨 |
| Layer.ai | 변환만 | ElevenLabs SFX | ElevenLabs·MiniMax·Lyria 등 | Tripo·Trellis·Meshy·Rodin | 구독 $10/월부터 |
| ElevenLabs 직접 | 없음 | SFX $0.12/분 | Music $0.15/분 | 없음 | 구독 $6/월부터 |
| Higgsfield | 없음 | — | — | — | 영상·광고 중심 |

### 추천 조합

1. **한 곳으로: Scenario Starter $15/월**(모자라면 CU 추가, 대략 월 $15~45). RD·ElevenLabs 효과음·음악·2D·3D를 MCP 하나로. 캐릭터는 4방향 48px까지. 구독이 싫으면 Replicate(충전식, 비슷한 사용량에 월 $25~40쯤).
2. **이 게임에 더 맞는 조합: Retro Diffusion 직접 + fal.ai**(합해 월 $15~40쯤). RD 직접으로 16px 아이콘·타일·8방향 회전·동작 애니, fal로 효과음(ElevenLabs SFX)·음악(MiniMax·Lyria)·2D·3D(Tripo·Rodin). 둘 다 충전식이라 쓴 만큼만 낸다. 8방향 캐릭터를 비교하고 싶으면 PixelLab(약 $12/월)을 더한다.

### 라이선스 주의

- 쓰지 말 것: **MMAudio·MusicGen**(가중치가 비상업 CC BY-NC인데 일부 사이트는 상업 가능으로 표시), **Hunyuan3D**(한국 제외), **Suno·Udio**(공식 공개 API가 없고, 다른 사이트의 Suno는 비공식 우회).
- 조건이 있는 것: **ElevenLabs Music**(게임 누적 매출 $50만·회사 게임 매출 연 $100만 미만이면 인디로 상업 이용, 넘으면 플랜 변경), **Stable Audio**(연매출 $100만 미만만 무료 상업 이용), **Lyria**(워터마크), **TRELLIS.2**(의존 라이브러리가 비상업이라는 지적).
- Steam: 플레이어가 보고 듣는 AI 생성물(그림·소리·음악)은 스토어 설문에서 공개해야 한다.

## fal.ai·Scenario·Replicate 자세히 (2026-11-12)

사람 요청 "fal.ai에서 각 분야에 어떤 모델을 쓰는지와 상업 가능 유무, 샘플", "scenario, replicate도 자세히". 웹 조사만 했다(가입·결제·생성 없음). 상업 이용은 **플랫폼 표시와 원래 모델 약관 둘 다** 봤고, 둘이 어긋나면 쓰지 않는다. 가격은 2026-11 공식 페이지 기준.

### 분야별 추천 모델

| 분야 | 1순위 (곳, 단가) | 상업 이용 | 대안 | 쓰지 말 것 |
|---|---|---|---|---|
| 도트 정지·타일 | Retro Diffusion: Replicate `rd-plus`(16px $0.024)·`rd-tile`(타일 하나 $0.024, 타일셋 $0.10)·`rd-fast`($0.017) | RD "출력물은 당신 것" | RD 직접(Fast $0.03, Plus $0.06) | — |
| 도트 8방향·동작 | RD 직접 API: rotate $0.10, 8방향 회전 $0.25, 고급 애니메이션 $0.14 | 같음 | Replicate `rd-animation` 4방향 48px $0.07 | — |
| 도트 후처리 | fal `fal-ai/image2pixel`(격자·팔레트 고정·투명) | fal 상업 표시 | 우리 `DevTools/ArtImport/to_rows.py` | — |
| 효과음 | fal `elevenlabs/sound-effects/v3`: 초당 $0.002, 0.5~30초, **반복(loop) 옵션** | ElevenLabs 유료 조건으로 상업 가능(fal 경유 적용 조건은 미확인) | fal `stable-audio-3/small/sfx`($0.02/개, 연매출 $100만 미만 무료 상업, 출력물 소유) | MMAudio(가중치 비상업) |
| 음악·BGM | fal `stable-audio-3/medium`: 곡당 약 $0.04, 최대 6분20초 | Stability Community License: 연매출 $100만 미만 무료, 출력물 소유, 워터마크 없음 | ElevenLabs Music(fal $0.6/분, Replicate 약 $0.5/분, 게임 누적 매출 $50만 미만 인디 조건), Lyria 3.5(곡당 $0.08~0.10, SynthID 워터마크), ACE-Step(Apache-2.0, 아주 쌈, 품질 미확인) | MusicGen(비상업), Lyria 2(Scenario가 비상업 표시) |
| 2D(컨셉·UI·아이콘) | fal Ideogram v3 투명 배경($0.03~0.09) 또는 GPT Image(투명·SVG, 장당 단가 미확인) | 상업 표시 | Z-Image(Apache-2.0, 아주 쌈, 이음새 없는 타일 `/tiling`), Recraft(벡터) | — |
| 3D | fal `meshy/t2.5/image-to-3d`: 텍스처 포함 $0.60, 3만 삼각형 이하, 리깅 +$0.20 | 상업 표시(Meshy 원래 조건은 미확인) | Rodin($0.40), Tripo($0.20~0.40) | Hunyuan3D(라이선스가 한국 제외), TRELLIS(의존 라이브러리 비상업) |

- 반복 재생되는 BGM을 바로 만드는 음악 모델은 없다(효과음은 ElevenLabs loop 옵션이 있다). BGM은 이음매를 손봐야 한다.
- 받아 둔 예시 파일(신뢰할 수 없는 데이터로 다루고 열지 않았다, 저장소 밖): fal 27개, Replicate·Scenario 24개.

### 세 곳 비교

| | fal.ai | Replicate | Scenario |
|---|---|---|---|
| 결제 | 선불 충전, 365일 만료, 환불 불가, 무료 크레딧은 API에서 못 씀 | 선불 충전, 1년 만료, 자동 충전 최소 $15 | 구독 $15/$45/$75, 월 CU 이월 없음, 추가 CU 12개월 |
| Retro Diffusion | 없음 | 있음(가장 쌈, 8방향 없음) | 있음(장당 약 $0.11~0.15, 8방향 없음) |
| 효과음 | ElevenLabs SFX(가장 좋음, 쌈) | ElevenLabs SFX 없음, Stable Audio 2.5 $0.20/개 | ElevenLabs SFX 2 |
| 음악 | Stable Audio 3, ElevenLabs, Lyria, ACE-Step | ElevenLabs, Lyria, MiniMax, ACE-Step | 같은 계열, 단가 비공개(dryRun으로 확인) |
| 3D | Meshy·Tripo·Rodin | Rodin·Hunyuan 3.1 | 가장 많음 + 리깅·리토폴로지 |
| 자기 스타일 학습 | Z-Image LoRA 1000스텝 $0.85 | FLUX LoRA 약 $1.5/회 | Pro 이상, 5~15장(짧은 변 700px 이상) |
| 그 밖 | 출력물 소유를 넘겨주는 조항이 약관에 없음(모델별 약관), 결과 URL 기본 공개 | 출력 권리를 넘겨줌, 모델별 약관 책임은 사용자, API 기록 1시간 뒤 삭제 | 출력물 소유, **셀프서비스 콘텐츠를 학습에 쓸 수 있고 옵트아웃 없음**, Unity 플러그인(MIT)·MCP |

### 추천 (이 게임)

- **Replicate**(도트 정지·타일·4방향) + **RD 직접**(8방향·고급 동작) + **fal**(효과음 ElevenLabs·음악 Stable Audio 3·3D Meshy). 셋 다 충전식이라 쓴 만큼만 낸다.
- 아이콘·타일 300, 애니 30, 효과음 60, 음악 10, 3D 5를 한 달에 만든다면 대략: Replicate 약 $13, RD 직접 약 $5~10, fal 약 $5(효과음 2초×60 $0.24 + 음악 10곡 $0.4 + 3D 5개 $3) → **월 $25 안팎**. 후보를 세 배 뽑아도 $60~80쯤.
- 하나만 고른다면: 구독이 괜찮으면 Scenario Max($75/월, 같은 양에 맞는 플랜). 아니면 Replicate 하나(효과음은 Stable Audio로, 월 $40쯤).
- Scenario는 자기 그림으로 스타일을 학습시키고 싶을 때 한 달만 Pro($45)로.

## Retro Diffusion 직접 시험 (2026-11-12)

사람이 충전한 잔액으로 16px 그림을 만들어 견줬다. 쓴 돈 $1.34(잔액 $3.66). 그림은 저장소 밖(게임에 넣지 않음).

| 대상 | Fast $0.03 | Plus $0.06 | Pro $0.18 |
|---|---|---|---|
| 플레이어 | 깔끔하지만 외곽선이 없다 | 뭉개짐 | 진한 외곽선, 우리 화풍에 가장 가깝다(정면·뒷면이 섞여 나옴) |
| 슬라임 | 덩어리가 엉성함 | — | 좋음 |
| 곡괭이 | 망치처럼 나옴 | 모양이 이상함 | — |
| 돌바닥 | 쓸 만함(어두운 동굴 바닥) | — | 타일 $0.06·타일셋 $0.10은 밝고 납작함 |

- 16px에서 Fast는 `low_res`·`mc_item`·`mc_texture`, Plus는 `low_res`·`topdown_item`·`mc_*` 스타일만 받는다(나머지는 32px나 64px부터). 무료 값 확인(`check_cost`)은 크기를 걸러 주지 않으므로 `GET /v2/styles/selector`로 허용 크기를 먼저 본다.
- 판단: 캐릭터·몬스터는 Pro만 쓸 만하다. 바닥 질감은 Fast로 충분하다. 아이템 아이콘은 지금(CC0 꾸러미 모양 + 우리 색) 쪽이 낫다.

두 번째 시험($0.31, 잔액 $3.35):

- **생물군계 바닥 질감**(Fast `mc_texture` 16px, 7장): 지금 웹 데모 바닥과 나란히 깔아 견줬다. 모양(돌 결, 얼음 결정, 용암 틈)은 쓸 만하지만 색이 우리 화면과 따로 논다. 동굴은 주황 흙, 버섯은 초록, 얼음은 밝은 하늘색, 모래는 진한 주황으로 나와 지금의 어두운 바닥보다 훨씬 밝고 진하다. 버섯·모래는 16칸마다 같은 무늬가 되풀이되는 게 눈에 띈다. 결정·용암이 그나마 가깝다. 쓰려면 우리 팔레트로 색을 다시 입히거나(`ItemArt`의 `RampedOwn`처럼) 팔레트 입력을 같이 줘야 한다.
- **8방향 돌리기**(`rd_advanced_animation__rotate` $0.10): Pro로 만든 32px 플레이어 한 장을 넣으니 같은 인물의 8방향이 깔끔하게 나왔다(3×3 시트). 입력은 32px부터라 지금 16px 캐릭터는 32px로 그려야 쓸 수 있다.
- 판단: 8방향 캐릭터는 Pro 32px + 돌리기 조합이 가장 쓸 만하다(한 캐릭터 약 $0.28).

세 번째(2026-11-12, $0.28, 잔액 $3.07): **광맥 포식자 그룸**(보스, `Docs/Bosses.md`). 사람이 준 예산은 $1.5였다.

- Pro `rd_pro__topdown` 48px 한 장($0.18, "giant armored mole beast boss … iron drill claws, glowing orange ore crystals on its back … top-down three-quarter view", `remove_bg`). 배경을 지울 때 머리 뒤에 남은 어두운 갈색은 가장자리부터 그 두 색만 지우고, 떨어진 점 몇 개를 지웠다
- 그 그림으로 8방향 돌리기($0.10, 48px, 3×3 시트). 돌린 그림이 처음 그림보다 낫다(등의 광석 가시, 꼬리). 우리가 쓰는 다섯 모습(앞, 앞옆, 옆, 뒤옆, 뒤)을 40×39로 잘라 33색 팔레트와 행으로 옮겼다(`Art/VeinEaterArt.cs`). 왼쪽은 뒤집는다
- 공격·피격 같은 움직임은 그림을 더 사지 않고 코드로 만든다(기울이기·늘이기·뛰어오르기·가라앉기·광석 빛내기, 바위와 흙더미를 덧그리기). RD의 공격 움직임(`rd_advanced_animation__attack`)은 쓰지 않았다
- 이 그림은 Retro Diffusion으로 만들었고 상업 이용이 된다(유료 크레딧으로 만든 그림)

네 번째(2026-11-12, $0.28, 잔액 $2.79): **벚꽃 검귀 시구레**(사무라이 보스). Pro `rd_pro__topdown` 48px 한 장("samurai swordsman boss, ronin in dark crimson lacquered armor and a wide straw hat, long katana sheathed at the left hip …", `remove_bg`, 배경 손질 없이 깨끗했다)과 8방향 돌리기. 다섯 모습을 40×42로 잘라 21색 팔레트와 행으로 옮겼다(`Art/SamuraiArt.cs`). 뽑은 칼날, 발도·납도·돌진 자세와 이펙트는 모두 코드다 바닥은 Fast 그대로는 안 맞고 색을 다시 입혀야 한다.

다섯 번째(2026-11-12, $0.39, 잔액 $2.40): **애니메이션 시험**. 사람이 "직접 짜는 대신 RD를 쓰면 더 나을지" 물어 시구레 옆모습(8방향의 오른쪽 칸을 64px 칸 가운데에)으로 `rd_advanced_animation__custom_action`("허리에서 칼을 뽑아 수평으로 베고 털고 납도", $0.25)과 `rd_advanced_animation__attack`($0.14)을 한 장씩 뽑았다. 둘 다 64px 8프레임 시트. 자유 동작은 몸을 프레임마다 새로 그려 주지만 발도 대신 머리 위로 들어 베었고, 중간 프레임에서 몸이 뒤로 돌았으며 칼 모양이 프레임마다 달랐다. 공격은 칼을 뽑지 않고 몸 앞에 밧줄 같은 곡선만 휘둘렀다. 칼을 지운 몸 세 장을 코드 칼과 섞어 보기도 했으나(무료) 몸이 꼿꼿해 속도감이 줄었다. 사람이 "모션은 코드로" 정해 쓰지 않았다. 순서가 정해진 동작(발도)은 RD가 설명을 잘 따르지 않는다. 걷기처럼 단순한 동작에는 다시 볼 만하다

여섯 번째(2026-11-12, $0.28, 잔액 $2.12): **흑철 기사 발두인**(판금 기사 보스). Pro `rd_pro__topdown` 64px 한 장("hulking armored knight boss in heavy dark steel full plate armor, closed great helm with a narrow visor slit, broad rounded pauldrons, tattered deep blue tabard …, plated gauntlets empty at his sides, no weapon, no shield", `remove_bg`, $0.18)과 64px 8방향 돌리기($0.10). 무기를 손에 그리면 자세마다 움직이기 어려워 맨손으로 뽑았고, 전쟁망치는 코드로 그린다. 다섯 모습(정면 (1,0), 대각 앞 (0,0), 옆 (0,1), 대각 뒤 (0,2), 뒤 (1,2) 칸, 사무라이와 같은 규칙)을 64×64 그대로 25색 팔레트와 행으로 옮겼다(`Art/KnightArt.cs`). 다른 보스보다 크다(키 약 60픽셀).

일곱 번째(2026-11-12, $0.18, 잔액 $1.94): **발도 접촉 자세 시험**. 사람이 "자세마다 RD로 받으면 어떨지" 물어, 시구레 옆모습을 넣고 `rd_pro__edit`(128px)로 칼을 뽑아 앞으로 뻗은 접촉 자세를 한 장 받아 코드 자세와 비교 GIF를 만들었다. 인물은 지켰지만 큰 자세 변화는 따르지 않고 배경도 흰색으로 채워 와서 쓰지 않았다. 자세는 코드로 짠다(앞의 다섯 번째와 같은 결론)

여덟 번째(2026-11-13, $0.28, 잔액 $1.66): **실 잣는 어미 아라네**(거미 보스). Pro `rd_pro__topdown` 64px 한 장("giant cave spider boss, huge bloated dark purple-black abdomen with glowing sickly green markings, armored chitin carapace, eight small glowing red eyes, curved venom fangs, eight long jointed hairy legs spread wide", 배경 투명, $0.18)과 64px 8방향 돌리기(`rd_advanced_animation__rotate`, $0.10). 여덟 칸은 머리가 향하는 쪽으로 0 오른쪽 아래, 1 아래, 2 왼쪽 아래, 3 왼쪽, 4 왼쪽 위, 5 위, 6 오른쪽 위, 7 오른쪽이다. 다섯 모습(정면 1, 대각 앞 0, 옆 7, 대각 뒤 6, 뒤 5)에서 **배와 머리가슴만** 타원으로 잘라 25색 팔레트와 행으로 옮겼다(`Art/SpiderArt.cs`, 시점마다 배 한 장과 머리가슴 한 장). 다리 여덟 개는 그림의 것이 가늘고 몸에 붙어 있어 부위로 떼어 움직일 수 없으므로 쓰지 않고, 코드가 엉덩이에서 발까지 두 마디로 다시 그린다(색은 그림의 다리 색에서 골랐다). 송곳니, 독, 실, 거미줄과 독 웅덩이(`EffectArt.Web`·`VenomPool`·`Glob`), 새끼 거미(`CreatureArt`의 `Spiderling`)도 코드로 그린다.

아홉 번째(2026-11-19, $0.28, 잔액 $1.38): **플레이어 주인공**. 사람 요청 "지금 플레이 주인공도 rd 이용해서 뽑고 무기에 따른 다양한 모션도 추가해보자". Pro `rd_pro__topdown` 32px 한 장("young adventurer hero, brown messy hair, simple blue tunic with a brown leather belt, dark trousers, brown boots, empty hands, no weapon", 배경 투명, $0.18)과 32px 8방향 돌리기(`rd_advanced_animation__rotate`, $0.10). 여덟 칸의 순서는 보스 때와 같다(0 오른쪽 아래, 1 정면, 2 왼쪽 아래, 3 왼쪽, 4 왼쪽 위, 5 뒤, 6 오른쪽 위, 7 오른쪽).
- 다섯 모습(정면 1, 대각 앞 0, 옆 7, 대각 뒤 6, 뒤 5)에서 **머리(11줄)와 몸통(소매 끝까지, 9줄)만** 14색 팔레트의 행으로 옮겼다(뒤에 머리 8줄, 몸통 5줄로 줄여 다시 그림, 아래 크기 줄)(`Art/HeroArt.cs`). 왼쪽 세 방향은 오른쪽 그림을 뒤집는다. 바깥 외곽선은 떼고 부위마다 다시 두른다. 머리카락 가장자리의 튀는 점 몇 개와 눈 옆 흰 점은 손으로 정리했다
- 팔, 다리, 무기는 RD 그림을 쓰지 않고 코드로 그린다(리그). 그래서 무기별 모션이 여덟 방향에서 한 번에 나온다. 튜닉의 세 색(밝은 쪽·그늘·어두운 쪽)은 플레이어 색의 띠로 바꿔 칠한다
- 크기: 처음에는 RD가 준 그대로(폭 16, 키 26픽셀) 썼다가, 사람 말("캐릭터 크기가 너무 커. npc 크기 정도로")에 따라 같은 날 NPC 크기(폭 12, 키 18픽셀)로 **손으로 다시 그렸다**. RD 그림은 인물의 생김새(머리 모양, 튜닉, 허리띠, 다섯 방향의 모습)의 밑그림으로 남았고, 지금 행은 그것을 보고 줄여 그린 것이다. 추가 결제는 없다. 자세한 것은 `Docs/ArtPipeline.md`의 주인공 절
- 이 그림은 Retro Diffusion으로 만들었고 상업 이용이 된다(유료 크레딧으로 만든 그림). Steam AI 공개 대상인 것은 보스 그림과 같다

열 번째(2026-11-26, $0.28, 잔액 $1.10): **동굴 수호자**(첫 보스, 이끼 낀 바위 골렘). 사람 요청 "맨 처음 만나는 보스도 rd로 8방향 생성하고 패턴 만들어줘". Pro `rd_pro__topdown` 64px 한 장("ancient stone golem cave guardian boss, hulking body of grey-brown cave rock slabs with green moss patches, glowing amber crystal core in its chest, two small glowing amber eyes in a heavy rock head, huge boulder fists hanging at its sides, short thick stone legs, empty hands, no weapon, top-down three-quarter view", `remove_bg`, $0.18)과 64px 8방향 돌리기(`rd_advanced_animation__rotate`, $0.10, 8장짜리 GIF로 왔다). 여덟 칸의 순서는 보스 때와 같다(0 오른쪽 아래, 1 정면, 2 왼쪽 아래, 3 왼쪽, 4 왼쪽 위, 5 뒤, 6 오른쪽 위, 7 오른쪽). 다섯 모습(정면 1, 대각 앞 0, 옆 7, 대각 뒤 6, 뒤 5)을 64×64 그대로 23색 팔레트와 행으로 옮겼다(`Art/WardenArt.cs`, 배경 손질 없이 깨끗했다). 시안(다섯 시점과 자세 여섯)을 사람에게 먼저 보이고 "그대로 진행"을 받았다. 팔·다리를 떼어 움직이는 자세, 돌 위팔, 핵과 눈의 빛, 이펙트는 모두 코드다. 이 그림은 Retro Diffusion으로 만들었고 상업 이용이 된다(유료 크레딧으로 만든 그림). Steam AI 공개 대상인 것은 다른 보스 그림과 같다

열한 번째(2026-11-27, $0.28, 잔액 $0.82): **개미귀신**(둘째 보스, 모래 동굴의 개미지옥 애벌레). 사람 요청 "두번째 보스도 동일하게 작업해줘". Pro `rd_pro__topdown` 64px 한 장("giant antlion boss, huge round segmented abdomen with dark brown and sandy tan bands, small armored head with two enormous curved sickle mandibles like pincers, six short spiky legs, bristly hairs on the body, dusty desert sand insect, small dark eyes, top-down three-quarter view", `remove_bg`, $0.18)과 64px 8방향 돌리기(`rd_advanced_animation__rotate`, $0.10). 첫 요청은 응답을 기다리다 끊겼지만 `GET /v2/inferences/requests`로 결과를 되찾아 두 번 결제하지 않았다(돌리기는 v2 비동기 작업으로 받았다). `remove_bg`인데도 사막 배경(하늘·모래·선인장)이 붙어 와서 배경 색 아홉 가지를 바깥에서부터 지우고 떨어진 모래 점을 지웠다. 여덟 칸의 순서는 머리가 향하는 쪽으로 0 정면, 1 왼쪽 아래, 2 왼쪽, 3 왼쪽 위, 4 왼쪽 위, 5 뒤, 6 오른쪽 위, 7 오른쪽이었다. 다섯 모습(정면 0, 대각 앞은 1을 뒤집어서, 옆 7, 대각 뒤 6, 뒤 5)을 64×64 그대로 21색 행과 부위 지도로 옮겼다(`Art/AntlionArt.Rows.cs`). 시안을 사람에게 먼저 보이고 "그대로 진행"을 받았다. 턱을 벌리는 것, 머리·배·다리를 떼어 움직이는 자세, 모래에 가라앉는 구덩이, 뿌리는 모래는 모두 코드다. 이 그림은 Retro Diffusion으로 만들었고 상업 이용이 된다(유료 크레딧으로 만든 그림). Steam AI 공개 대상인 것은 다른 보스 그림과 같다
열두 번째(2026-11-27, $0.28, 잔액 $0.36): **수정 거미 여왕**(세 번째로 만나는 보스). 사람 요청 "세 번째로 만나는 보스도 rd로 8방향 생성하고 패턴 만들어줘". Pro `rd_pro__topdown` 64px 한 장("crystal spider queen boss, giant regal spider with a faceted body made of translucent pink and pale sky-blue crystal, glowing crystal crown of sharp pink gems on its head, eight long jointed legs of clear blue crystal shards, sparkling gem abdomen with an egg sac of small crystals, six glowing pink eyes, top-down three-quarter view", `remove_bg`, $0.18)과 64px 8방향 돌리기(`rd_advanced_animation__rotate`, $0.10, 8장짜리 GIF). 여덟 칸의 순서는 다른 보스와 같다. 다섯 시점(정면 1, 대각 앞 0, 옆 7, 대각 뒤 6)에서 **배·머리·왕관**을 잘라 29색 팔레트와 행으로 옮겼다(`Art/CrystalQueenArt.cs`). 그림은 왕관을 배 위에 그려 와서 잘라 머리에 씌운다. 뒤(5) 칸은 얼굴이 있는 앞모습처럼 나와 쓰지 않고, 뒷모습은 정면의 배와 대각 뒤의 머리로 만든다. 회색 테두리 점(128,128,128)은 지웠다. 다리 여덟은 아라네처럼 코드가 그린다(색은 그림의 다리 색). Retro Diffusion으로 만들었고 상업 이용이 된다(유료 크레딧). Steam AI 공개 대상인 것은 다른 보스 그림과 같다

열세 번째(2026-11-27, $0.28, 잔액 $0.26): **미쳐 버린 장군 모르그**(넷째로 만나는 보스, 지휘관 컨셉). Pro `rd_pro__topdown` 64px 한 장("mad demon general boss, tall lean muscular crimson-skinned demon warlord, two large curved black horns, wild long white mane, glowing orange mad eyes and a fanged grin, scarred bare chest, dark bronze spiked pauldrons, black leather war belt with a bone buckle, ragged dark red war skirt, clawed hands empty at his sides, no weapon, no shield, top-down three-quarter view", `remove_bg`, $0.18)과 64px 8방향 돌리기(`rd_advanced_animation__rotate`, $0.10, 3×3 시트). 다섯 모습(정면 (1,0), 대각 앞 (0,0), 옆 (0,1), 대각 뒤 (0,2), 뒤 (1,2) 칸)을 64×64 그대로 25색 팔레트와 행으로 옮겼다(`Art/GeneralArt.cs`, 배경 손질 없이 깨끗했다). 불꽃 대검과 등의 군기, 자세·이펙트는 모두 코드다. 같은 날 다른 보스 세션들과 잔액을 함께 썼다(남은 $0.26으로는 다음 보스 한 마리를 살 수 없다). 유료 크레딧으로 만든 그림이라 상업 이용이 되고, Steam AI 공개 대상인 것은 다른 보스 그림과 같다

## 로컬 AI로 보스 그림 (2026-11-28)

사람 요청 "rd로 도트 8방향 생성하는 것처럼 로컬 ai로". RD(Retro Diffusion API)의 "Pro 한 장 → 8방향 돌리기 → 다섯 시점을 행으로" 흐름을 이 컴퓨터(RTX 5080 16 GB, Comfy Desktop의 ComfyUI)에서 했다. 첫 그림은 **물살 뱀**(`Docs/Bosses.md` 물살 뱀 제 그림과 몸짓).

- **받은 것**(모두 Apache-2.0, Hugging Face, 2026-11-28 사람 허락): Z-Image-Turbo int8(`Comfy-Org/z_image_turbo`, 6.2 GB) + 텍스트 인코더 `qwen_3_4b_fp8_mixed`(5.6 GB) + VAE `ae`(0.3 GB), 도트 LoRA `tarn59/pixel_art_style_lora_z_image_turbo`(0.17 GB, 학습 자료 출처는 카드에 없다), Qwen-Image-Edit-2511 Q4_K_M GGUF(`unsloth`, 12.8 GB) + 여러 각도 LoRA(`fal/Qwen-Image-Edit-2511-Multiple-Angles-LoRA`, 0.3 GB) + Lightning 4단계 LoRA(`lightx2v`, 0.85 GB) + 텍스트 인코더 `qwen_2.5_vl_7b_fp8_scaled`(`Comfy-Org/Qwen-Image_ComfyUI`, 9.4 GB). 노드 ComfyUI-GGUF(city96, Apache-2.0). 모델은 `F:\Comfy-Desktop\ComfyUI-Shared\models`, 저장소에는 넣지 않는다
- **도구**(`DevTools/ArtImport/LocalAi/`): `comfy.py`(헤드리스 ComfyUI에 그리기·돌리기 요청), `to_sprite.py`(큰 그림 → 64px: 배경 지우기, 첫 그림 기준 같은 비율로 평균 내 줄이기, 공용 팔레트, 외톨이 점 정리, 외곽선), `serpent_rows.py`(다섯 시점 → 행과 부위 지도, 보스마다 `LAYOUT`만 바꿔 쓴다), `serpent_prompt.txt`
- **순서**: ① ComfyUI를 헤드리스로 켠다(포트 8190, `ComfyUI\.venv\Scripts\python.exe main.py --port 8190 --extra-model-paths-config <Comfy Desktop의 instance-model-paths yaml>`). GPU를 다른 세션과 나눠 쓰면 `F:\Comfy-Desktop\gpu-lock.json`을 먼저 본다 ② `LORA=1.0 python -I comfy.py t2i b serpent_prompt.txt 101 102 ...`(장당 약 4초, 1024×1024) ③ 사람이 고른 한 장으로 `python -I comfy.py angle out/b_106.png r_270 7 "<sks> left side view elevated shot medium shot"`(장당 약 40초). 게임은 오른쪽을 보는 모습을 쓰므로 정면 0°, 앞옆 315°(front-left quarter), 옆 270°(left side), 뒤옆 225°(back-left quarter), 뒤 180°(back) ④ `python -I to_sprite.py 60 24 64 views v_front.png v_frontside.png v_side.png v_backside.png v_back.png` ⑤ `python -I serpent_rows.py views Assets/_Project/Scripts/Client/Art/SerpentArt.Rows.cs parts.png`
- **해 보고 안 것**: 도트 LoRA 없이(또는 0.8)는 만화풍 벡터 그림이 나왔고 1.0에 "16-bit pixel art sprite, menacing, bold dark outline, detailed pixel shading"을 넣으니 도트답게 나왔다. 여러 각도 LoRA는 같은 몸을 여덟 방향으로 잘 돌렸다("elevated shot"이 우리 시점에 맞다). 1024 그림을 64px로 줄이면 비늘 결이 자글자글해서, 평균 내 줄이고 색을 줄인 쪽(부드러움)과 칸마다 가장 많은 색을 고른 쪽(또렷함) 중 사람이 부드러운 쪽을 골랐다. RD 그림보다 머리 세부(눈)가 뭉개져 눈은 부위 지도의 E 점으로 코드가 빛나게 그린다
- **공허의 군주**(2026-11-28, `Docs/Bosses.md` 공허의 군주 제 그림과 몸짓): 정면은 `void_lord_prompt.txt`(갑옷과 별 망토를 섞은 안, 사람이 열다섯 장 중 d_235 시드 235), 다섯 시점은 시드 7·8 두 장씩 돌려 고르고 `to_sprite.py 60 24 64`, 부위 지도는 `void_lord_rows.py`(그림의 구슬·반짝이는 지우고 가장 큰 덩어리만 남긴다, 어깨·손 자리도 적는다). 헤드리스 포트 8192, 세 보스 세션이 `gpu-lock.json`으로 번갈아 썼다. 새 모델은 받지 않았다
- 라이선스: 모델·LoRA·노드가 Apache-2.0이라 결과를 공개 저장소와 상업 게임에 쓸 수 있다. 플레이어가 보는 AI 생성 그림이라 Steam AI 공개 대상인 것은 RD 그림과 같다
