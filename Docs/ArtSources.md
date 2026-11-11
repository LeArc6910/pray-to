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

- **16px 캐릭터의 8방향 걷기를 바로 쓸 수 있게 만드는 도구는 로컬에도 유료에도 없다.** 캐릭터 애니메이션은 유료도 32~48px부터다(Retro Diffusion 걷기 48px·32px, PixelLab 8방향 32~64px). 16px로 바로 잘 나오는 것은 타일과 아이콘이다.
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
