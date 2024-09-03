using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Network.Client.Player;
using UniRx;

public class BuffManager
{
    private List<IBuff> _activeBuffs = new List<IBuff>();
    private BuffDatabase _buffDatabase;
    private IDisposable _disposable;

    public void StartBuffManager(BuffDatabase buffDatabase)
    {
        _buffDatabase = buffDatabase;
        _disposable = Observable.EveryUpdate().Subscribe(_ => Update());
    }
    
    public void StopBuffManager()
    {
        _disposable.Dispose();
    }

    public void AddBuff(PlayerPropertyComponent targetStats, BuffType buffType)
    {
        var buffData = _buffDatabase.GetBuffData(buffType);
        if (buffData.HasValue)
        {
            var newBuff = new Buff(buffData.Value.buffType, buffData.Value.propertyTypeEnum, buffData.Value.duration, buffData.Value.effectStrength);
            ApplyBuff(newBuff, targetStats);
            _activeBuffs.Add(newBuff);
        }
        else
        {
            var randomBuffData = _buffDatabase.GetRandomBuffData(buffType);
            if (randomBuffData.HasValue)
            {
                var newRandomBuff = new RandomBuff(randomBuffData.Value.buffType, randomBuffData.Value.propertyTypeEnum, randomBuffData.Value.durationRange, randomBuffData.Value.effectStrengthRange);
                ApplyBuff(newRandomBuff, targetStats);
                _activeBuffs.Add(newRandomBuff);
            }
        }
    }

    private void ApplyBuff(IBuff buff, PlayerPropertyComponent targetStats)
    {
        targetStats.ModifyProperty(buff.PropertyTypeEnum, buff.EffectStrength);
    }

    private void RemoveBuff(IBuff buff, PlayerPropertyComponent targetStats)
    { 
        targetStats.RevertProperty(buff.PropertyTypeEnum, buff.EffectStrength);
    }

    void Update()
    {
        // for (int i = activeBuffs.Count - 1; i >= 0; i--)
        // {
        //     IBuff buff = activeBuffs[i];
        //     buff.Update(Time.deltaTime);
        //     if (buff.IsExpired())
        //     {
        //         RemoveBuff(buff, targetStats);
        //         activeBuffs.RemoveAt(i);
        //     }
        // }
    }
}