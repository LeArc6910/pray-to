using Game.Core;
using UnityEngine;

namespace Game.Client
{
    // Playing together across PC, phones and the web (2026-11-27, Docs/CrossPlay.md): a joiner who drops tries the same
    // room again, a web host is told its tab must stay in front, and a refusal for another version says who updates.
    public sealed partial class GameRoot
    {
        const string LostHost = "호스트와 연결이 끊어졌습니다. ";
        internal const string WebHostWarning = "웹으로 연 방: 탭을 숨기면 멈춘다 · 오래 열 방은 앱으로";
        // A dropped joiner tries the same code this many times, this long apart.
        const int RejoinTries = 3;
        const float RejoinDelay = 2;

        string _lastJoinCode;
        int _rejoinTries;

        void Rejoin()
        {
            _rejoinTries++;
            _codeInput = _lastJoinCode;
            _status = $"연결이 끊어져 같은 방에 다시 잇는 중… ({_rejoinTries}/{RejoinTries})";
            Invoke(nameof(RejoinNow), RejoinDelay);
        }

        void RejoinNow()
        {
            if (_client == null && !_connecting) StartOnlineJoin();
        }

        bool _mapNoted;

        // Once in again, a later drop has its tries anew. A map that came out other than the host's is asked for, and the
        // player told once it came.
        void NoteJoined()
        {
            if (_client == null || _client.Status != ClientStatus.Playing) return;
            _rejoinTries = 0;
            if (_client.TakeMapRequest() is byte[] ask) SendToHost(ask);
            if (_client.MapFromHost && !_mapNoted)
            {
                _mapNoted = true;
                _view?.ShowBanner("이 기기의 지도가 호스트와 달라 호스트의 지도를 받았다");
            }
        }
    }
}
