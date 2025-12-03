using HotUpdate.Scripts.Config.ArrayConfig;
using UnityEngine;

namespace HotUpdate.Scripts.Tool
{
    [RequireComponent(typeof(LineRenderer))]
    public class AttackSectorLine : MonoBehaviour
    {
        [SerializeField]
        private LineRenderer lineRenderer;
        [Header("攻击范围参数")] 
        public float radius = 5f;
        public float angle = 60f;
        public float height = 2f;
        public int segments = 20;

        [Header("线渲染设置")] public Color lineColor = Color.red;
        public float lineWidth = 0.1f;
        public bool init;

        public void SetParams(AttackConfigData data) 
        {
            this.radius = data.AttackRadius;
            this.angle = data.AttackRange;
            this.height = data.AttackHeight;
            SetupLineRenderer();
            init = true;
        }

        void Update()
        {
            // 实时更新绘制
            DrawSector();
        }

        void SetupLineRenderer()
        {
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = false;
            lineRenderer.positionCount = segments * 3 + 4;
        }

        void DrawSector()
        {
            if (!init)
            {
                return;
            }
            Vector3 origin = Vector3.zero;
            float halfAngle = angle * 0.5f * Mathf.Deg2Rad;
            int index = 0;

            // 1. 绘制底面扇形
            lineRenderer.SetPosition(index++, origin);
            for (int i = 0; i < segments; i++)
            {
                float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, (float)i / (segments - 1));
                Vector3 point = new Vector3(
                    Mathf.Sin(currentAngle) * radius,
                    0,
                    Mathf.Cos(currentAngle) * radius
                );
                lineRenderer.SetPosition(index++, point);
            }

            lineRenderer.SetPosition(index++, origin);

            // 2. 绘制顶面扇形
            lineRenderer.SetPosition(index++, origin + Vector3.up * height);
            for (int i = 0; i < segments; i++)
            {
                float currentAngle = Mathf.Lerp(-halfAngle, halfAngle, (float)i / (segments - 1));
                Vector3 point = new Vector3(
                    Mathf.Sin(currentAngle) * radius,
                    height,
                    Mathf.Cos(currentAngle) * radius
                );
                lineRenderer.SetPosition(index++, point);
            }

            lineRenderer.SetPosition(index++, origin + Vector3.up * height);

            // 3. 绘制连接线（侧面）
            Vector3 leftDir = new Vector3(Mathf.Sin(-halfAngle), 0, Mathf.Cos(-halfAngle));
            Vector3 rightDir = new Vector3(Mathf.Sin(halfAngle), 0, Mathf.Cos(halfAngle));

            lineRenderer.SetPosition(index++, origin);
            lineRenderer.SetPosition(index++, origin + Vector3.up * height);

            lineRenderer.SetPosition(index++, origin + leftDir * radius);
            lineRenderer.SetPosition(index++, origin + Vector3.up * height + leftDir * radius);

            lineRenderer.SetPosition(index++, origin + rightDir * radius);
            lineRenderer.SetPosition(index++, origin + Vector3.up * height + rightDir * radius);
        }
    }
}