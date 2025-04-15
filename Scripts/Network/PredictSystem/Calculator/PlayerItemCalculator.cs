using System;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerItemCalculator: IPlayerStateCalculator
    {
        public Random Random { get; } = new Random();
        public static PlayerItemConstant Constant { get; private set; }
        public static void SetConstant(PlayerItemConstant constant)
        {
            Constant = constant;
        }
    }
    

    public class PlayerItemComponent
    {
    }

    public struct PlayerItemConstant
    {
        public ItemConfig ItemConfig;
        public WeaponConfig WeaponConfig;
        public ArmorConfig ArmorConfig;
        public PropertyConfig PropertyConfig;
        public BattleEffectConditionConfig ConditionConfig;
    }
}