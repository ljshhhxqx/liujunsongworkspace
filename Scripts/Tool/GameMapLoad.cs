using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotUpdate.Scripts.Tool
{
    public class GameMapLoad : MonoBehaviour
    {
        [Header("截图设置")]
        public string screenshotFolder = "MapSnapshots";
        public int resolution = 1024;
        public LayerMask includeLayers = -1;
    
        private Camera snapshotCamera;
        private string sceneName;
    
        [Button]
        void SnapshotMap()
        {
            sceneName = SceneManager.GetActiveScene().name;
            // 自动生成地图快照（如果不存在）
            if (!MapSnapshotExists())
            {
                GenerateMapSnapshot();
            }
            else
            {
                LoadExistingSnapshot();
            }
        }
    
        public void GenerateMapSnapshot()
        {
            StartCoroutine(GenerateSnapshotCoroutine());
        }
    
        private IEnumerator GenerateSnapshotCoroutine()
        {
            // 创建临时摄像机
            GameObject cameraGO = new GameObject("SnapshotCamera");
            snapshotCamera = cameraGO.AddComponent<Camera>();
        
            // 设置摄像机参数
            SetupSnapshotCamera();
        
            // 等待一帧确保摄像机设置完成
            yield return null;
        
            // 创建RenderTexture
            RenderTexture renderTexture = new RenderTexture(resolution, resolution, 24);
            snapshotCamera.targetTexture = renderTexture;
        
            // 渲染
            snapshotCamera.Render();
        
            // 保存为Texture2D
            Texture2D snapshot = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
            RenderTexture.active = renderTexture;
            snapshot.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            snapshot.Apply();
        
            // 保存到文件
            SaveTextureToFile(snapshot, $"{sceneName}_MiniMap.png");
        
            // 清理
            RenderTexture.active = null;
            snapshotCamera.targetTexture = null;
            DestroyImmediate(renderTexture);
            DestroyImmediate(cameraGO);
        
            Debug.Log("地图快照生成完成！");
        }
    
        private void SetupSnapshotCamera()
        {
            // 找到地图的边界
            Bounds mapBounds = CalculateMapBounds();
        
            // 设置摄像机位置和参数
            Vector3 cameraPos = mapBounds.center + Vector3.up * mapBounds.size.y * 2;
            snapshotCamera.transform.position = cameraPos;
            snapshotCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            snapshotCamera.orthographic = true;
            snapshotCamera.orthographicSize = Mathf.Max(mapBounds.size.x, mapBounds.size.z) * 0.5f;
            snapshotCamera.cullingMask = includeLayers;
            snapshotCamera.clearFlags = CameraClearFlags.SolidColor;
            snapshotCamera.backgroundColor = Color.black;
        }
    
        private Bounds CalculateMapBounds()
        {
            Renderer[] renderers = FindObjectsOfType<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                return bounds;
            }
        
            // 默认边界
            return new Bounds(Vector3.zero, new Vector3(100, 10, 100));
        }
    
        private void SaveTextureToFile(Texture2D texture, string filename)
        {
            byte[] bytes = texture.EncodeToPNG();
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, screenshotFolder, filename);
        
            // 确保目录存在
            string directory = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }
        
            System.IO.File.WriteAllBytes(path, bytes);
        
            // 保存纹理引用供小地图使用
            SaveTextureReference(texture, path);
        }
    
        private bool MapSnapshotExists()
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, screenshotFolder, "map_snapshot.png");
            return System.IO.File.Exists(path);
        }
    
        private void LoadExistingSnapshot()
        {
            string path = System.IO.Path.Combine(Application.streamingAssetsPath, screenshotFolder, "map_snapshot.png");
            if (System.IO.File.Exists(path))
            {
                byte[] fileData = System.IO.File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(fileData);
            
                // 应用到小地图
                ApplyTextureToMinimap(texture);
            }
        }
    
        private void SaveTextureReference(Texture2D texture, string path) { /* 保存引用 */ }
        private void ApplyTextureToMinimap(Texture2D texture) { /* 应用到小地图 */ }
    }
}