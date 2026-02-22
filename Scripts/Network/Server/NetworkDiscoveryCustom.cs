using System.Net;
using AOTScripts.Data;
using Mirror.Discovery;
using UnityEngine;

namespace HotUpdate.Scripts.Network.Server
{
    public class NetworkDiscoveryCustom : NetworkDiscoveryBase<DiscoveryRequest, DiscoveryResponse>
    {
        private bool _connected;
        private string _targetRoomId;
        private NetworkManagerCustom _networkManagerCustom;

        private void Awake()
        {
            _networkManagerCustom = GetComponent<NetworkManagerCustom>();
        }

        public void StartBroadcast(string roomId)
        {
            _targetRoomId = roomId;
            AdvertiseServer();
        }

        public void StartFindServer(string roomId)
        {
            _connected = false;
            _targetRoomId = roomId;
            StartDiscovery();
        }

        protected override DiscoveryRequest GetRequest()
        {
            return new DiscoveryRequest
            {
                roomId = _targetRoomId
            };
        }

        protected override DiscoveryResponse ProcessRequest(
            DiscoveryRequest request,
            IPEndPoint endpoint)
        {
            if (request.roomId != _targetRoomId)
                return default;

            var uri = transport.ServerUri();

            return new DiscoveryResponse
            {
                roomId = _targetRoomId,
                address = uri.Host,
                port = (ushort)uri.Port
            };
        }

        protected override void ProcessResponse(
            DiscoveryResponse response,
            IPEndPoint endpoint)
        {
            if (_connected)
                return;

            if (response.roomId != _targetRoomId)
                return;

            _connected = true;

            _networkManagerCustom.networkAddress = endpoint.Address.ToString();
            _networkManagerCustom.StartClient();

            StopDiscovery();

            Debug.Log($"发现匹配房间服务器: {_networkManagerCustom.networkAddress}");
        }
    }
}