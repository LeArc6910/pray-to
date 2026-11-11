# 도트 그래픽 MCP

도트 그림을 만들거나 고칠 때 붙일 수 있는 MCP 서버를 조사하고, 이 프로젝트에 맞는 것과 클라우드 세션에 연결하는 법을 정리한다(2026-11-02 조사). 아직 아무것도 연결하지 않았다. 쓰기로 하면 아래 "쓰기로 하면"의 순서를 따른다.

## 결론

- **지금은 MCP가 없어도 된다.** 그림은 `Scripts/Client/Art/`에 코드로 그린 16px 도트이고, 클라우드가 ArtPreview로 PNG를 만들어 직접 보고 고친다(`Docs/ArtPipeline.md`). 캐릭터 동작, 몬스터마다 다른 모습 같은 일도 이 방식으로 한다.
- **그림을 새로 많이 만들어야 할 때** 생성형 MCP를 붙인다. 후보는 둘이다.
  1. **Retro Diffusion**: 쓴 만큼 낸다(장당 약 3~18센트). 16px 정지 그림(아이템, 몬스터 한 장)과 16~32px 타일셋이 된다. 팔레트를 고정할 수 있다. 애니메이션은 32px 이상만 된다
  2. **PixelLab**: 캐릭터를 4·8방향으로 만들고 걷기·공격·대기 같은 애니메이션을 붙이는 도구가 가장 많다. 16px부터, 탑다운 타일셋 16·32px가 된다. 월 구독제이고, 팔레트 고정은 비싼 pro 모드에서만 된다
- **에디터를 조종하는 MCP**(Aseprite, Pixelorama 등)는 클라우드 컨테이너에 맞지 않는다. GUI나 유료 실행 파일이 필요하다.
- 어느 쪽을 쓰든 만든 PNG를 그대로 게임에 넣지 않는다. 우리 팔레트로 줄인 뒤 `Art/*.cs`의 문자열 격자로 바꿔 넣는다. 그래야 "그림은 코드" 원칙(웹 데모에 바로 들어가고, ArtPreview로 검토하고, 색을 시트에서 바꾸는 것)이 유지된다. 이 변환기(`DevTools/`)는 아직 없고, 쓰기로 하면 클라우드가 만든다.

## 후보 비교

| | Retro Diffusion | PixelLab |
|---|---|---|
| 주소 | `https://mcp.retrodiffusion.ai/mcp` | `https://api.pixellab.ai/mcp` |
| 방식 | 원격 HTTP(Streamable HTTP), `Authorization: Bearer rdpk-…` | 원격 HTTP, `Authorization: Bearer <토큰>` |
| 문서 | [retro-diffusion-mcp](https://github.com/Retro-Diffusion/retro-diffusion-mcp), [api-examples](https://github.com/Retro-Diffusion/api-examples) | [pixellab.ai/mcp](https://www.pixellab.ai/mcp), [도구 문서](https://api.pixellab.ai/mcp/docs) |
| 요금 | 선불 잔액. rd_fast 약 $0.03, rd_plus 약 $0.06, rd_pro $0.18/장. 견적 도구는 무료 | 구독제. 무료 체험 뒤 월 $12~50로 알려졌다(공식 가격표는 열지 못해 확인하지 못했다) |
| 크기 | rd_pro 12~256px(16×16 정지 그림 가능). 타일셋 16~32px. 애니메이션은 32px 이상(4방향 걷기는 48px) | 캐릭터 16~256px, 4·8방향. 탑다운 Wang 타일셋 16·32px |
| 애니메이션 | 걷기·대기·점프·웅크리기·공격·부서짐·임의 동작, 8방향 돌리기. 4~16프레임 | 걷기·달리기·대기·주먹·화염구·쓰러짐 등 약 50개 틀 |
| 화풍 맞추기 | `input_palette`(팔레트 그림으로 색 고정), rd_pro에 참조 그림 9장까지, 사용자 스타일 | `palette`(색 2~256개), 스타일 그림·캐릭터 id. 팔레트는 pro 모드에서만 |
| 걸리는 시간 | 비동기 작업과 즉시 생성 둘 다 | 비동기, 보통 2~5분 |
| 생성물 권리 | 만든 사람 소유, 상업적 사용 가능. 단 무료 크레딧으로 만든 것은 공개될 수 있다 | 만든 사람이 저작권을 갖고 상업적 사용 가능. 생성물로 AI 모델 학습 금지, Open RAIL-M 사용 제한을 따른다 |

권리 내용은 조사할 때 읽은 약관([Retro Diffusion](https://retrodiffusion.ai/terms), [PixelLab](https://www.pixellab.ai/termsofservice)) 요약이다. 가입하기 전에 원문을 다시 읽는다.

**이 프로젝트에 맞추면**:

- 아이템, 타일, 몬스터 한 장처럼 16px 정지 그림이 많다. 팔레트를 고정할 수 있고 쓴 만큼 내는 **Retro Diffusion이 1순위**다. 무료 크레딧 결과는 공개될 수 있으므로 유료 잔액으로 만든다.
- 플레이어처럼 여러 방향으로 걷고 공격하는 것을 한꺼번에 만들 때는 **PixelLab**이 낫다. 다만 16px에서의 애니메이션 품질은 아직 확인하지 못했다. 우리 플레이어는 16px 칸 안의 10px 폭이라, 32px 이상으로 만든 결과를 줄이면 손이 많이 간다.
- 두 서비스 모두 우리 화풍(어두운 외곽선, 3~4단 명암)과 똑같이 나오지는 않는다. 변환기에서 팔레트로 줄이고 외곽선을 다시 두르는 정리를 거친다.

## 클라우드 세션에 연결하는 법

클라우드 세션은 매번 새 컨테이너에서 시작하므로, 내 컴퓨터에서 명령으로 넣은 MCP 설정은 넘어오지 않는다. 방법은 둘이다.

### 1. 저장소의 `.mcp.json`과 환경에 넣은 키 (추천)

저장소 맨 위에 `.mcp.json`을 두면 클라우드와 로컬 세션이 같은 서버를 쓴다. 키는 파일에 적지 않고 환경 변수에서 읽는다.

```json
{
  "mcpServers": {
    "retro-diffusion": {
      "type": "http",
      "url": "https://mcp.retrodiffusion.ai/mcp",
      "headers": { "Authorization": "Bearer ${RETRO_DIFFUSION_API_KEY:-}" }
    },
    "pixellab": {
      "type": "http",
      "url": "https://api.pixellab.ai/mcp",
      "headers": { "Authorization": "Bearer ${PIXELLAB_API_KEY:-}" }
    }
  }
}
```

- `:-` 뒤가 비어 있어 키가 없는 컴퓨터에서도 설정은 읽힌다. 그 서버만 인증에 실패한다.
- 키는 클라우드 환경 설정에 넣는다: 세션 제목 표시줄의 클라우드 환경 메뉴 → Edit → **Network secrets**(예전 앱은 "API credentials"). 그 칸이 없으면 환경 변수로 넣는다. 이름은 `RETRO_DIFFUSION_API_KEY`, `PIXELLAB_API_KEY`다.
- 키는 채팅이나 커밋에 적지 않는다(UNITY_EMAIL·UNITY_PASSWORD와 같은 규칙).
- 키를 넣은 다음 **새 세션**부터 MCP 도구가 보인다. 처음 쓸 때 저장소의 MCP 서버를 믿을지 묻는 확인이 나올 수 있다.
- 로컬에서 쓰려면 같은 이름의 환경 변수를 그 컴퓨터에도 둔다.

### 2. 대화 앱의 커넥터

대화 앱의 커넥터 설정에서 원격 MCP 주소로 사용자 지정 커넥터를 더하는 방법도 있다. 연결한 커넥터는 새로 시작하는 세션에서 읽힌다.

다만 두 서비스는 API 키를 `Authorization` 헤더로 받는다. 커넥터가 이런 고정 키를 받을 수 있는지, 두 서비스가 OAuth 로그인을 지원하는지는 확인하지 못했다. 안 되면 1번으로 한다.

### 네트워크

- 2026-11-02에 이 클라우드 환경에서 두 주소 모두 MCP 연결 요청(initialize)에 HTTP 200으로 답했다. 지금 네트워크 정책으로 닿는다.
- 나중에 막히면 환경 설정의 Network access에서 `mcp.retrodiffusion.ai`, `api.pixellab.ai`를 Allowed domains에 더한다(클라우드 환경 설정 안내 참고). 그때 "Allow package managers"는 켜 둔다.

## 쓰기로 하면

1. (사람) 서비스를 고르고 가입한다. Retro Diffusion은 잔액을 충전하고, PixelLab은 구독한다. API 키를 만든다.
2. (사람) 위의 환경 설정에 키를 넣는다. 로컬에서도 쓰려면 그 컴퓨터의 환경 변수에도 넣는다.
3. (클라우드) `.mcp.json`을 커밋한다. 생성한 PNG를 팔레트로 줄여 `Art/*.cs` 격자로 바꾸는 변환기를 `DevTools/`에 만들고, 시험으로 몬스터 한두 마리를 만들어 ArtPreview로 지금 그림과 견준다.
4. (사람) 미리보기를 보고 계속 쓸지 정한다.

## 맞지 않는 것

| 종류 | 예 | 까닭 |
|---|---|---|
| Aseprite 조종 | [diivi/aseprite-mcp](https://github.com/diivi/aseprite-mcp)(가장 활발, MIT), [willibrandon/pixel-mcp](https://github.com/willibrandon/pixel-mcp), letsagents/aseprite-mcp | 유료 Aseprite 실행 파일이 필요하다. EULA상 본인 컴퓨터에 설치하는 것이고, 매번 새로 뜨는 클라우드 컨테이너에 넣기는 애매하다. 로컬 PC에 정품 Aseprite가 있으면 로컬 세션에서만 쓸 수 있다 |
| 다른 에디터 조종 | Pixelorama MCP, Piskel MCP, LibreSprite | 에디터 창(GUI)이 떠 있어야 한다(Piskel은 확인하지 못했다) |
| 그리기 캔버스 | ssyubix-pixelart-mcp | 그림을 만들어 주지 않고 점을 찍는 도구다. 우리 C# 격자와 같은 일이다 |
| 로컬 생성 모델 | mcpbytes-pixel-sprite | NVIDIA GPU와 약 16GB 모델이 필요하다 |
| 일반 이미지 생성 | fal, Replicate, Hugging Face | 16px 격자에 맞는 진짜 도트가 나오지 않아 후처리가 크다. 모델마다 라이선스가 다르다 |
| Unity 조종 | [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp), Unity 공식 MCP(베타) | 실행 중인 Unity 에디터가 있어야 해서 로컬 전용이다. 우리는 에디터 스크립트(`ProjectSetup`)로 같은 일을 한다 |
