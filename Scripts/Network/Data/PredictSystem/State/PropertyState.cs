using System;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.State
{
    public interface IPropertyState
    {
        bool IsEqual(IPropertyState other, float tolerance = 0.01f);
    }
}