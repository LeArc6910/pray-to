# 다음 작업 (2026-11-26 정리, 사람 검토 중)

새 세션이 이어받을 때 이 문서와 해당 절만 읽는다. 사람이 "진행해"라고 한 묶음만 한다. 끝난 묶음은 이 문서에서 지우고 `CLOUD_LOG.md`에 남긴다.

## 남은 일

- **보스 패턴 재설계**(`Docs/BossRedesign.md`, 사람이 정함 8절): R1a·R1b·R2a·R2b·R3 끝(R3: 개미귀신 모래 구덩이·둔덕 습격·끌고 물기·개미지옥의 입, 물살 뱀 잠수·파도 벽·조이는 물살·해일·물 가장자리, 아라네 실 잇기, `Docs/Bosses.md` "끌기·숨기·벽"), 다음은 R4(새 세션: 분신·순서·막간, 모르그 대열은 R3의 `wave`로). R3 그림·소리는 Fable 하위 세션(구덩이·둔덕·V자 물결·파도 벽·실·끌림 흐름). R3에서 안 한 것: 모래 분수, 조이는 물살 뒤 물줄기, 물살 뱀 2페이즈 웅덩이 둘. 그룸은 R2b 뒤에도 늘 구르는 봇 3(2)/5, 놓치는 봇 0이다(전 1(1)): 사람이 해 보고 정한다. 용암 거인이 3페이즈에 늘 구르는 봇에게도 한두 판 쓰러진다(5(4) → 5(3)): 사람이 해 보고 정한다. R1b에서 시구레가 늘 구르는 봇에게 쉬워졌다(시드 1~10 4(1) → 8(6), `Docs/Bosses.md` "늦은 공격·납도 반격·색 표지"): 사람이 해 보고 조정할지 정한다. 평타 표시 없애기는 보류(9절)
- **무기 확장 B·C**(`Docs/Weapons.md`): A는 끝, B 시안은 사람이 봤다. 지금 할 일은 `Docs/Weapons.md`의 "묶음 B 진행"(종류마다 제 연타 동작, 종류마다 다른 타격감). 그다음 C는 무기 고유 기술(마우스 오른쪽·V, 휴대폰은 공격 단추 위, 설계안 표대로)

- 마을·왕도의 새 건물을 생물군계마다 다른 모습으로(지금은 한 모습)
- 생성기의 실수 계산을 정수로(크로스 플레이 지도 일치의 근본 해결, `Docs/CrossPlay.md`)

## 이펙트 개편 (끝남, 2026-11-27)

E1~E7과 F1(14절)·F2(15절)·F3(16절: 파고들기·도약 바위의 조준선, 남은 사각 점, 덜 된 확인)까지 했다(`Docs/EffectsRework.md`). 정말 남은 것은 13절: 일부러 하지 않은 것(왕관의 망령의 탄 등), 로컬 감시가 찍을 사진과 `.meta`(`LOCAL_TASKS.md` 199단계), 사람 확인(`Docs/HumanChecklist.md` 7절).

발두인 0조각은 끝(점검이 화면 멈춤 중의 프레임을 읽었다, `Docs/EffectsRework.md` 17절). 로컬 PlayMode 실패 19개 중 열둘는 시험 쪽 원인(마을 안, 맨몸 1레벨, 방 고르기)을 고쳤고 다시 돌리기는 `LOCAL_TASKS.md` 200단계. 웹 점검 `roll`의 "다시 쓰기 시간 안의 둘째 누름" 실패 둘(휴대폰)은 아직 안 봤다.

## PlayMode 실패 중 남은 일곱 (2026-11-27, 이펙트 무관)

지난 보고는 메시지 첫 줄만 있어 원인을 끝까지 못 봤다. 200단계 결과(전체 메시지·Console 로그)를 받고 본다.

- `FeatureTests.RumoursAndSlowRest` "The goal list says only which way:" — 메시지 뒤 `HudLines`가 비어 있다. `_hudLines`는 HUD가 그려질 때마다 "Lv …"부터 채우므로 HUD가 아예 안 그려졌을 수 있다(`RestoreJourney(…, 4, …)` 뒤 장 카드나 대화 상자가 덮는지, `DrawHud(compact)`의 `compact`/`side`). 원인 미확인
- `FeatureTests.TheBagAndHudDraw` "The food's buff has no icon." — `BuffNames`는 `Buffs()`가 그릴 때(`playing && !compact && !squeezed`)만 채워진다. 어느 화면 크기(`tag`)에서인지, `UseConsumable(mushroom_soup)`가 거절됐는지(`_client.Food`) 볼 것. 원인 미확인
- `FeatureTests.TheEncounterWindowsDraw` "Her portrait is in the talk box." — `TalkMerchant`는 첫 줄의 이름(`상인 도라`)으로 `SpeakerOf`를 찾아 `merchant:…`가 돼야 한다. 이야기 인물에 같은 이름은 없다. 실제 `_root.TalkPortrait`·`TalkText`를 메시지에 넣어 볼 것(시작 자리가 마을 안인 것도 의심). 원인 미확인
- `LayoutTests.TheBagAndHudFit` 667×375 "배너가 [메뉴] 판에 겹친다 (0,301,667,64) / (353,186,304,160)": `GameRoot.BannerRoom`이 양옆 기둥 사이 폭이 260보다 좁으면 가로지르는 것들 아래로 내리는데, 메뉴 판 아래(354)는 화면 밖이라 바닥(301)으로 잘려 메뉴 판에 겹친다. 고칠 방향: 아래로 못 내려가면 메뉴 판 옆(왼쪽) 자리를 쓰기. 휴대폰 가로에서 기둥 크기를 보고 정할 것(배치 고침, 화면 확인 필요)
- `LayoutTests.TheFixedPartsKeepToTheSafeArea` 667×375 안전 영역 32 0 32 16, 스킬 1: "시간·장소 줄 / 구르기" 겹침. 오른쪽 기둥의 시간·장소 줄이 낮게 내려왔거나 구르기 단추가 올라왔다. 두 자리의 값을 보고 정할 것
- `LayoutTests.TheGrowthScreensFit` 360×640 특성: "고른 점과 서랍이 하늘을 거의 다 덮는다 (84)"(기준 100). 휴대폰 세로의 특성 화면 서랍 높이
- `TitleScreenTests.TheTestGroundsPanelAndHitLogDraw` "The hit log is on the screen." — `TestGround.DrawOverlay`가 머리줄만 그리는 경우(`bottom - top < headHeight`: 목표 줄·HUD가 길 때)나 창이 덮어 `Hide`된 경우. 로컬 Game 창 크기와 함께 볼 것

## 정해진 것 (2026-11-27, 추천대로)

- 사냥터 생물군계마다 2곳, 길에서 만나는 빈도 분당 1~2회, 다시 자라는 시간 게임 속 하루, 새 월드에만(8·9판, 2a는 10판), 크로스 플레이 준비는 이번에 함
