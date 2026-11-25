using UnityEngine;

namespace Game.Client
{
    // The loading screen's one line of help (Docs/TitleRework.md 4-4, 묶음 T3): short, about how to play, never about a
    // place, monster or boss not met (so none names one). Each is a row of the string sheet ("tip.*", Korean and
    // English, Docs/Strings.md); the Korean is kept here too. The journal's goals page lists them all again ("도움말").
    static class Tips
    {
        static readonly (string Id, string Korean)[] All =
        {
            ("tip.screens", "가방은 Tab, 스킬은 K, 일지는 J, 지도는 M. 휴대폰은 [메뉴]에서 연다"),
            ("tip.class", "클래스는 [스킬] 화면에서 고른다. 레벨이 오르면 둘째 칸이 열린다"),
            ("tip.roll", "구르는 동안에는 맞지 않는다. 바닥의 붉은 예고를 보면 굴러서 피한다"),
            ("tip.rest", "가만히 쉬면 천천히 낫는다. 야영지·제단·물약은 더 빨리 낫게 한다"),
            ("tip.fall", "쓰러지면 잠시 뒤 집에서 일어난다. 던전 안에서는 입구에서"),
            ("tip.pickaxe", "곡괭이로 벽과 광맥을 캔다. 퀵슬롯에 두면 바로 바꿔 든다"),
            ("tip.workbench", "작업대에서 도구와 장비를, 화로에서 주괴를 만든다"),
            ("tip.rumors", "일지의 [소문]은 갈 곳을 방향과 생물군계로 알려 준다"),
            ("tip.pin", "지도에서 곳을 골라 [여기로 길 안내]를 누르면 목표 목록이 따라간다"),
            ("tip.waystones", "한 번 연 길잡이 돌 사이는 빠르게 오갈 수 있다"),
            ("tip.rails", "끊긴 광차 선로를 고치면 광차를 타고 멀리 간다"),
            ("tip.packs", "무리는 함께 알아챈다. 하나씩 끌어내 싸우면 쉽다"),
            ("tip.elites", "금빛 테를 두른 몬스터는 정예다. 세지만 더 많이 떨군다"),
            ("tip.skills", "스킬 점수로 스킬을 배우고 단계를 올린다"),
            ("tip.passives", "특성 점수는 [특성] 화면의 나무에 쓴다"),
            ("tip.bestiary", "잡은 몬스터는 일지의 [도감]에 남는다"),
            ("tip.merchant", "상인에게 쓰지 않는 것을 팔고 모자란 것을 산다"),
            ("tip.anvil", "모루에서 장비를 강화한다"),
            ("tip.chest", "상자에 넣어 두면 가방이 가벼워진다"),
            ("tip.torch", "횃불을 세우면 그 둘레가 밝아진다"),
            ("tip.fish", "물가에서 낚시를 한다. 구운 물고기는 든든한 먹을거리다"),
            ("tip.seeds", "씨앗은 흙에 심어 기른다"),
            ("tip.dungeon", "던전 문 너머는 들어갈 때마다 새로 지어진다. 끝 상자를 노리자"),
            ("tip.together", "함께 하기: 방을 만들면 코드가 나온다. 친구는 그 코드로 참가한다"),
            ("tip.export", "저장본은 이 기기에만 있다. [저장본]의 [전부 내보내기]로 파일을 남겨 두자"),
            ("tip.seed", "같은 시드를 적으면 같은 월드가 만들어진다"),
            ("tip.characters", "캐릭터는 어느 월드에나 들어간다. 가방과 레벨은 캐릭터를 따라간다"),
            ("tip.worlds", "지은 것과 상자는 월드에 남는다"),
            ("tip.guide", "[목표 안내]를 따라가면 처음 할 일을 알려 준다"),
            ("tip.motion", "설정의 [움직임: 줄이기]는 흔들림과 흐르는 배경을 멈춘다"),
            ("tip.daylength", "하루 길이는 새 월드를 만들 때와 설정에서 바꾼다"),
        };

        public static int Count => All.Length;

        public static string Text(int index) => All[(index % All.Length + All.Length) % All.Length] is var (id, korean) ? Strings.T(id, korean) : "";

        // Another at random, never the one shown.
        public static int Next(int shown)
        {
            if (All.Length < 2) return 0;
            int next = Random.Range(0, All.Length - 1);
            return next >= shown && shown >= 0 ? next + 1 : next;
        }
    }
}
