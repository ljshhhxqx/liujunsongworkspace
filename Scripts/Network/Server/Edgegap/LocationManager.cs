using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Network.Server.Edgegap
{
    public class LocationManager
    {
        private const string IpifyUrl = "https://api.ipify.org?format=json";

        public async UniTaskVoid GetPlayerLocationOrIP()
        {
            string playerIP = await GetPlayerIPAsync();
            if (!string.IsNullOrEmpty(playerIP))
            {
                Debug.Log($"Player IP: {playerIP}");
                // 处理IP地址，例如存储或发送给Edgegap
            }
            else
            {
                Debug.LogError("Failed to get player IP");
            }
        }

        private async UniTask<string> GetPlayerIPAsync()
        {
            using var request = UnityWebRequest.Get(IpifyUrl);
            var operation = await request.SendWebRequest();

            if (operation.result is UnityWebRequest.Result.ConnectionError or UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error: {operation.error}");
                return null;
            }

            var response = JsonUtility.FromJson<IPResponse>(operation.downloadHandler.text);
            return response.ip;
        }

        [System.Serializable]
        private class IPResponse
        {
            public string ip;
        }
    }
}
