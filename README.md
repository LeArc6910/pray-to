# pray-to

코어키퍼 계열의 탑뷰 2D 액션·생존·크래프팅 게임. Unity 6000.6.3f1, 협동 멀티플레이.

- 기획과 기술 원칙: [`Docs/Design.md`](Docs/Design.md)
- 작업 방식과 명령: [`CLAUDE.md`](CLAUDE.md)
- Unity 에디터 작업 목록: [`LOCAL_TASKS.md`](LOCAL_TASKS.md)

Unity 없이 게임 로직을 테스트하려면 .NET 8 SDK를 설치한 뒤:

```
dotnet test DevTools/Game.Core.Tests
```
