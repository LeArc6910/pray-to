# 도트 그림 바깥 자료와 생성형 AI (2026-11-11)

사람 요청 "도트 이미지도 사운드처럼 무료 공개 소스가 있다면 활용해보고, 무료 생성형 ai를 붙여보자". 소리는 `Docs/SoundCredits.md`.

## 한 일

- **CC0 꾸러미 12개를 받아 우리 그림과 나란히 견줬다**(저장소 밖에서). 결론:
  - **아이템 아이콘**: Ninja Adventure(pixel-boy & AAA, CC0, https://pixel-boy.itch.io/ninja-adventure-asset-pack, 페이지에 "No generative AI was used")가 진한 외곽선·3~4단 명암·16×16으로 우리 화풍에 가장 가깝고, 지금 우리 10×10 아이콘보다 정교하다. **모양만 들여오고 색은 우리 재료 색 띠로 칠한다**(`ItemArt`는 한 모양을 등급 색으로 다시 칠하는 체계라서).
  - **UI 틀**: Buch "Golden UI"(CC0)와 Kenney "Fantasy UI Borders"(CC0)의 구조(어두운 판, 얇은 금빛 테, 모서리 장식)를 참고해 `UiArt`를 우리 색으로 새로 그렸다(그림 파일은 가져오지 않음). Ninja Adventure UI는 밝은 주황이라 동굴 화면에 튀어서 쓰지 않았다.
  - **동굴 타일·캐릭터·몬스터**: 바꾸지 않는다. 받은 타일은 벽돌 던전이거나 외곽선 없이 납작하고, 캐릭터·몬스터는 비율·시점이 달라 섞으면 티가 난다. 지금 코드 그림이 서로 맞는다.
- **들여온 것**: 곡괭이(`ItemArt.Pick16`)와 주괴(`ItemArt.BarOf`)를 Ninja Adventure에서. 곡괭이 머리는 등급 금속 색(구리·철·미스릴·흑요석·공허), 자루는 우리 나무 색, 주괴는 재료 색. 외곽선은 우리 외곽선 색. `ArtPreview`가 `imported.png`로 그린다.
- **변환 도구**: `DevTools/ArtImport/to_rows.py`가 PNG를 `PixelImage.FromRows` 격자 글자로 바꾼다. `--ramp`로 한 재료의 색을 L·M·m·d 글자로 받아 등급 색으로 칠할 수 있게 한다. 생성형 AI로 만든 그림이 생겨도 같은 길로 들어온다. `.meta`가 필요 없어 클라우드가 혼자 끝내고, 그림 검사(`ArtLint`)와 웹 데모를 그대로 거친다.

## 다음 후보 (사람이 고를 것)

- Ninja Adventure의 음식·물약·물고기·상자·도구(괭이·물뿌리개·망치) 모양을 같은 방법으로(재료별 색 띠는 우리 것). 한 번에 하나씩 `imported.png`로 견준 뒤 넣는다.
- 출처 표기가 필요한 꾸러미(CC-BY: DawnLike, Kyrise 아이콘 등)는 쓰지 않았다. 쓰려면 크레딧 화면이 필요하다.

## 무료 생성형 AI (직접 해 본 결과)

- **키 없이 되는 곳은 쓸 수 없다**: Pollinations는 워터마크 찍힌 흐릿한 JPEG, AI Horde(익명)는 결과까지 약 30분에 도트가 아니거나 엉뚱한 그림. 16×16으로 줄이면 뭉개진다. Hugging Face의 도트 Space들은 모두 실행 오류였다.
- **16px 게임 그림을 제대로 만드는 곳은 Retro Diffusion과 PixelLab뿐**: 둘 다 가입과 API 키가 필요하고 무료 몫은 시험 몇 장(Retro Diffusion 새 계정 $0.50, 약 16장). 상업 이용은 약관상 된다.
- 넉넉한 무료(Cloudflare Workers AI 하루 약 230장)는 범용 모델이라 도트로는 다시 그려야 한다.
- 생성 그림을 게임에 넣으면 Steam의 AI 공개 대상이 되고, 순수 AI 결과는 저작권 보호가 약하다(`Docs/Research2026-11.md`).
- **그래서 붙이지 않았다.** 쓰고 싶으면 Retro Diffusion 키를 주면(채팅이 아니라 환경의 비밀값으로) 몇 장 만들어 위 변환 도구로 넣어 견줄 수 있다.
