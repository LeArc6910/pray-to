# 아트·사운드 생성 계획

AI 생성 도구로 아트와 사운드를 만드는 방안을 조사한 결과다(2026-10 기준). 게임의 틀이 어느 정도 잡히고 아트 스타일을 정한 뒤에 진행한다. 그때까지는 코드로 만든 단색 도형을 임시 그래픽으로 쓴다.

## 원칙

- **스타일을 먼저 정한다.** 픽셀아트 여부, 캐릭터 크기(예: 32px), 팔레트를 정한 뒤에 도구를 고른다. 도구로 할 수 있는 일이 스타일 결정에 영향을 주므로 그 전에 짧은 시험은 해 본다.
- **본격 제작 전에 시험부터 한다.** 몬스터 1종(8방향 걷기·공격), 지형 타일셋 1개, 아이콘 10개 정도를 만들어 품질과 손볼 시간(Aseprite 등)을 잰다.
- **생성 결과는 원본 그대로 쓰지 않는다.** 팔레트 고정, 프레임 정리, 타일 이음매 확인을 거친다.

## 후보

| 용도 | 1순위 | 대안 | MCP | 메모 |
|---|---|---|---|---|
| 캐릭터·몬스터 (4·8방향, 애니메이션) | PixelLab | Retro Diffusion | 둘 다 공식 원격 MCP (Bearer 키) | PixelLab: `create_character`(4·8방향, 최대 128px), `animate_character`(걷기·대기 등 템플릿 또는 4~40프레임). Retro Diffusion: 걷기·대기·공격 사이클, 스프라이트 시트 출력 |
| 타일셋 | PixelLab | Retro Diffusion | 위와 같음 | PixelLab `create_topdown_tileset`: 16타일 Wang 세트(16·32px). Retro Diffusion: Wang·blob 세트 |
| 아이템 아이콘 | Retro Diffusion | PixelLab | 위와 같음 | 같은 스타일로 묶어서 생성 |
| 일러스트·초상화 | Scenario | Replicate(여러 모델) | Scenario 공식 MCP(OAuth 또는 API 키), Replicate 공식 MCP | Scenario는 우리 스타일로 학습시킬 수 있다 |
| 효과음 | ElevenLabs 효과음 API | Ludo.ai | ElevenLabs는 쓸 만한 MCP가 없어 작은 스크립트로 연결. Ludo는 MCP 베타 | 0.5~30초, 반복 재생용 옵션 있음 |
| BGM | 보류 | Stable Audio | 공식 MCP 없음 | 아래 라이선스 참고. Suno는 공개 API가 없다 |

## 비용과 라이선스 (확인 필요)

조사 시점의 내용이라 실제로 쓰기 전에 약관을 다시 확인한다.

- **PixelLab**: 결과물 소유권은 사용자에게 있고 상업적 이용 가능. 결과물로 다른 모델을 학습시키는 것은 금지. 월 구독제(약 $12~50, 제3자 출처).
- **Retro Diffusion**: 선불, 이미지당 약 $0.015~0.18, 애니메이션당 약 $0.07~0.25, 타일셋 $0.10. FAQ상 게임 상업 이용 가능(정식 약관은 미확인).
- **Scenario**: 결과물 소유권은 사용자에게 있음. **일반 요금제는 올린 콘텐츠를 Scenario 모델 개선에 쓸 수 있다.** 월 $15/45/75, API는 Pro 이상.
- **ElevenLabs 효과음**: 상업 이용에는 유료 요금제가 필요하고, 출처 표기는 필요 없음.
- **ElevenLabs 음악**: 일반 요금제는 "스튜디오 게임"을 제외한다. 매출 50만 달러 이상인 게임(또는 연 매출 100만 달러 이상인 개발사)은 Enterprise 계약이 필요하다. 쓰기 전에 반드시 조건을 다시 본다.
- **Stable Audio**: 생성당 약 $0.08. 매출 100만 달러 미만 사업자는 상업 이용 가능(마케팅 자료 기준, API 약관은 미확인).

## 배포 시 주의

- **Steam 공개 의무**: 게임에 들어가서 플레이어가 보고 듣는 AI 생성 콘텐츠는 Steam 콘텐츠 설문에서 밝혀야 한다. 개발 도구로 쓴 AI(코드 작성 등)는 대상이 아니다. 2026년 1월에 규칙이 바뀌었다.
- **저작권**: 사람 손을 거치지 않은 순수 AI 생성물은 미국에서 저작권 보호를 받지 못할 수 있다. 손으로 다듬는 과정이 법적으로도 의미가 있다.

## 연결 방법

1. 사람이 서비스에 가입하고 API 키를 만든다.
2. 키는 저장소에 넣지 않는다. 클라우드 환경 설정의 비밀 값(환경 변수)으로 넣고, 저장소의 `.mcp.json`은 그 환경 변수를 참조한다. OAuth만 되는 서비스는 claude.ai 커넥터로 붙인다.
3. 클라우드 환경의 네트워크 허용 목록에 서비스 주소와 결과 파일 다운로드 주소를 추가해야 할 수 있다.

## Unity로 가져오기

- 생성한 PNG는 `Assets/_Project/Art/` 아래에 이름 규칙을 지켜 넣는다(예: `monster_slime_walk.png`).
- 클라우드가 `AssetPostprocessor` 에디터 스크립트를 만들어 픽셀아트 설정(Point 필터, 압축 없음, PPU, 시트 자르기)을 자동으로 적용한다. 로컬은 Unity를 열어 `.meta`를 만들고 커밋만 한다.
- 데이터 시트의 id와 파일 이름을 맞춰서, 몬스터나 아이템에 그림이 자동으로 연결되게 한다.

## 진행 시점

1. M2~M3에서 게임 흐름이 잡힌다.
2. 아트 스타일을 정한다(`Docs/Design.md`의 열린 질문).
3. PixelLab과 Retro Diffusion으로 위의 시험을 해서 비교한다.
4. 고른 도구로 가져오기 파이프라인을 붙이고 임시 그래픽을 바꿔 나간다.
5. 사운드는 전투 손맛을 다듬을 때 효과음부터 붙인다. BGM은 라이선스를 따로 정한 뒤에 한다.

## 출처

- PixelLab: https://www.pixellab.ai/mcp, https://api.pixellab.ai/mcp/docs, https://www.pixellab.ai/termsofservice
- Retro Diffusion: https://github.com/Retro-Diffusion/retro-diffusion-mcp, https://www.retrodiffusion.ai
- Scenario: https://www.scenario.com/features/mcp, https://www.scenario.com/terms-and-conditions, https://www.scenario.com/pricing
- Replicate MCP: https://replicate.com/docs/reference/mcp
- Ludo.ai: https://ludo.ai/blog/introducing-ludo-ai-api-mcp-integration
- ElevenLabs: https://elevenlabs.io/docs/api-reference/text-to-sound-effects/convert, https://elevenlabs.io/docs/api-reference/music/compose, https://elevenlabs.io/eleven-music-model-specific-terms, https://github.com/elevenlabs/elevenlabs-mcp
- Stability: https://docs.stability.ai/pricing
- Suno: https://www.digitalmusicnews.com/2026/07/03/suno-is-opening-an-api-partner-program/
- Steam: https://partner.steamgames.com/doc/gettingstarted/contentsurvey, https://videogameschronicle.com/news/valve-has-significantly-rewritten-steams-rules-for-how-developers-much-disclose-ai-use
