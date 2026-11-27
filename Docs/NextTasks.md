# 다음 작업 (2026-11-26 정리, 사람 검토 중)

새 세션이 이어받을 때 이 문서와 해당 절만 읽는다. 사람이 "진행해"라고 한 묶음만 한다. 끝난 묶음은 이 문서에서 지우고 `CLOUD_LOG.md`에 남긴다.

## 남은 일

- 마을·왕도의 새 건물을 생물군계마다 다른 모습으로(지금은 한 모습)
- 생성기의 실수 계산을 정수로(크로스 플레이 지도 일치의 근본 해결, `Docs/CrossPlay.md`)

## 이펙트 개편 적용 (2026-11-27 사람 확인: "시안 전체 다 마음에 들어")

`Docs/EffectsRework.md`의 3절(개선안)·4절(묶음과 정한 것)만 읽고, 시안 코드(`DevTools/ArtPreview/FxMockKit.cs`·`FxMockPreview.cs`, `--only fxmock`)를 본보기로 삼는다. 묶음 하나가 한 세션이다.

- E1 보스 예고 도형과 공용 착지는 끝났다(2026-11-27, `Docs/EffectsRework.md` 6절). 본보기: `Client/Art/HazardArt.cs`, `WorldView.Hazards.cs`, `--only hazard`, `sweep.js --only hazards`
- E2 적의 탄은 끝났다(2026-11-27, `Docs/EffectsRework.md` 7절). 본보기: `Client/Art/ShotArt.cs`, `WorldView.Shots.cs`, `--only shot`, `sweep.js --only shots`. 남은 것: 사방 사격·나선의 조준선(호스트가 쏠 때 각도를 정해 Core를 바꿔야 한다), 월광참·독액의 소멸(E6)
- **E3 공용 층 조각과 폭발**(다음 차례): 바닥 빛(빛 지도의 짧은 광원), 그을음·균열, 연기, 꼬리 불티, 폭발(흰 핵 → 노랑 → 주황 → 빨강, 속부터 연기로 비워짐). E4~E6의 바탕
- E5 일반 몬스터 → E4 플레이어 스킬(클래스마다) → E6 보스 고유 → E7 타격·숫자
- 묶음마다: 한도(이펙트 72조각, 바닥 빛 3장, 흔적 24개) 확인, 흑백 표, Playtest 장면과 `sweep.js` 구역, PlayMode 사진

## 정해진 것 (2026-11-27, 추천대로)

- 사냥터 생물군계마다 2곳, 길에서 만나는 빈도 분당 1~2회, 다시 자라는 시간 게임 속 하루, 새 월드에만(8·9판, 2a는 10판), 크로스 플레이 준비는 이번에 함
