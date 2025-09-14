using System;
using UnityEngine;

namespace HotUpdate.Scripts.Collector
{
    public interface IColliderConfig
    {
        ColliderType ColliderType { get; }
        Vector3 Size { get; }
        Vector3 Center { get; }
        float Radius { get; }
        float Height { get; }
        int Direction { get; }
    }
    
    public struct BoxColliderConfig : IColliderConfig
    {
        public ColliderType ColliderType => ColliderType.Box;
        public Vector3 Size { get; set; }
        public Vector3 Center { get; set; }
        public float Radius => Size.magnitude;
        public float Height => Size.y;
        public int Direction => -1;
    }
    
    public struct SphereColliderConfig : IColliderConfig
    {
        public ColliderType ColliderType => ColliderType.Sphere;
        public Vector3 Size { get; set; }
        public Vector3 Center { get; set; }
        public float Radius { get; set; }
        public float Height { get; set; }
        public int Direction => -1;
    }
    
    public struct CapsuleColliderConfig : IColliderConfig
    {
        public ColliderType ColliderType => ColliderType.Capsule;
        public Vector3 Size { get; set; }
        public Vector3 Center { get; set; }
        public float Radius { get; set; }
        public float Height { get; set; }
        public int Direction { get; set; }
    }

    public static class GamePhysicsSystem
    {
        // 快速检测物品是否被拾取
        public static bool FastCheckItemIntersects(
            Vector3 a,
            Vector3 b,
            IColliderConfig aConfig,
            IColliderConfig bConfig)
        {
            var aBounds = GetWorldBounds(a, aConfig);
            var bBounds = GetWorldBounds(b, bConfig);

            return aBounds.Intersects(bBounds);
        }

        // 带安全距离的检测
        public static bool CheckIntersectsWithMargin(
            Vector3 a,
            Vector3 b,
            IColliderConfig aConfig,
            IColliderConfig bConfig,
            float margin = 0.5f)
        {
            var aBounds = GetWorldBounds(a, aConfig);
            var bBounds = GetWorldBounds(b, bConfig);
        
            aBounds.Expand(margin);
            var isIntersects = aBounds.Intersects(bBounds);
            return isIntersects;
        }
        private static Bounds GetWorldBounds(Vector3 position, IColliderConfig config)
        {
            if (config == null)
                return new Bounds();
            return config.ColliderType switch
            {
                ColliderType.Box => new Bounds(position, config.Size),
                ColliderType.Sphere => new Bounds(position, Vector3.one * (config.Radius * 2)),
                ColliderType.Capsule => GetCapsuleBounds(position, config),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static Bounds GetCapsuleBounds(Vector3 position, IColliderConfig config)
        {
            var axis = config.Direction switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                2 => Vector3.forward,
                _ => Vector3.up
            };

            var halfHeight = Mathf.Max(config.Height * 0.5f, config.Radius);
            var center = position + config.Center;
            var top = center + axis * halfHeight;
            var bottom = center - axis * halfHeight;

            return new Bounds
            {
                center = (top + bottom) * 0.5f,
                size = new Vector3(
                    config.Radius * 2 + (axis == Vector3.right ? config.Height : 0),
                    config.Radius * 2 + (axis == Vector3.up ? config.Height : 0),
                    config.Radius * 2 + (axis == Vector3.forward ? config.Height : 0)
                )
            };
        }
        
        public static IColliderConfig CreateColliderConfig(ColliderType colliderType, Vector3 size, Vector3 center, float radius, float height = 0, int direction = 1)
        {
            return colliderType switch
            {
                ColliderType.Box => new BoxColliderConfig
                {
                    Size = size,
                    Center = center
                },
                ColliderType.Sphere => new SphereColliderConfig
                {
                    Radius = radius,
                    Center = center
                },
                ColliderType.Capsule => new CapsuleColliderConfig
                {
                    Height = height,
                    Radius = radius,
                    Direction = direction,
                    Center = center
                },
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        public static IColliderConfig CreateColliderConfig(Collider collider)
        {
            switch (collider)
            {
                case BoxCollider box:
                    return new BoxColliderConfig
                    {
                        Size = box.size,
                        Center = box.center
                    };
            
                case SphereCollider sphere:
                    return new SphereColliderConfig
                    {
                        Radius = sphere.radius,
                        Center = sphere.center
                    };
            
                case CapsuleCollider capsule:
                    return new CapsuleColliderConfig
                    {
                        Height = capsule.height,
                        Radius = capsule.radius,
                        Direction = capsule.direction,
                        Center = capsule.center
                    };
            
                default:
                    throw new ArgumentException("Collider type not supported: " + collider.GetType());
            }
        }

        //     private static bool PreciseCheck(
        //         Transform a,
        //         Transform b,
        //         IColliderConfig aConfig,
        //         IColliderConfig bConfig)
        //     {
        //         return (aConfig.ColliderType, bConfig.ColliderType) switch
        //         {
        //             (ColliderType.Box, ColliderType.Box) => CheckBoxBox(a, b, aConfig, bConfig),
        //             (ColliderType.Box, ColliderType.Sphere) => CheckBoxSphere(a, b, aConfig, bConfig),
        //             (ColliderType.Capsule, ColliderType.Sphere) => CheckCapsuleSphere(a, b, aConfig, bConfig),
        //             // 添加其他组合...
        //             _ => Physics.ComputePenetration(
        //                 CreateTempCollider(aConfig), 
        //                 a.position, 
        //                 a.rotation,
        //                 CreateTempCollider(bConfig), 
        //                 b.position, 
        //                 b.rotation,
        //                 out _, 
        //                 out _
        //             )
        //         };
        //     }
        //     
        //     // 盒体 vs 盒体
        //     private static bool CheckBoxBox(
        //         Transform a,
        //         Transform b,
        //         BoxColliderConfig aConfig,
        //         BoxColliderConfig bConfig)
        //     {
        //         var aBox = new BoxCollider { 
        //             center = aConfig.Center,
        //             size = aConfig.Center  
        //         };
        //
        //         var bBox = new BoxCollider { 
        //             center = bConfig.Center,
        //             size = bConfig.Center 
        //         };
        //
        //         return Physics.ComputePenetration(
        //             aBox, a.GetPosition(), a.GetRotation(),
        //             bBox, b.GetPosition(), b.GetRotation(),
        //             out _, out _
        //         );
        //     }
        //
        //     // 胶囊体 vs 球体
        //     private static bool CheckCapsuleSphere(
        //         Transform capsuleProvider,
        //         Transform sphereProvider,
        //         ico capsuleConfig,
        //         ItemColliderConfig sphereConfig)
        //     {
        //         var capsuleAxis = capsuleConfig.direction switch
        //         {
        //             0 => Vector3.right,
        //             1 => Vector3.up,
        //             2 => Vector3.forward,
        //             _ => Vector3.up
        //         };
        //
        //         var capsuleTop = capsuleProvider.GetPosition() + 
        //                          capsuleAxis * (capsuleConfig.height * 0.5f);
        //         var capsuleBottom = capsuleProvider.GetPosition() - 
        //                             capsuleAxis * (capsuleConfig.height * 0.5f);
        //
        //         var sphereCenter = sphereProvider.GetPosition();
        //         var closestPoint = ClosestPointOnLineSegment(
        //             capsuleBottom, 
        //             capsuleTop, 
        //             sphereCenter
        //         );
        //
        //         return Vector3.Distance(closestPoint, sphereCenter) <= 
        //                (capsuleConfig.radius + sphereConfig.radius);
        //     }
        //
        //     private static Vector3 ClosestPointOnLineSegment(Vector3 a, Vector3 b, Vector3 point)
        //     {
        //         var ab = b - a;
        //         var t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
        //         return a + t * ab;
        //     }
        // }
    }
}