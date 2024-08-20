using System;
using System.Collections.Generic;
using System.Reflection;
using Data;
using UniRx;

namespace Model
{
    public abstract class GameModel : IDisposable
    {
        public ReactiveProperty<int> UID { get; } = new ReactiveProperty<int>();
        public ReactiveProperty<string> Name { get; } = new ReactiveProperty<string>();
        
        //方便外部来直接注销Model
        public void Dispose()
        {
            UID.Dispose();
            Name.Dispose();
            OnDispose();
        }
        
        protected abstract void OnDispose();

        /// <summary>
        /// 提供一种同步数据到data的方法
        /// </summary>
        /// <param name="data">数据</param>
        /// <typeparam name="T">类型</typeparam>
        //public abstract bool ConvertDataToModel<T>(T data) where T: GameData;
        public virtual bool ConvertDataToModel<T>(T data) where T: GameData
        {
            var dataFields = data.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            var modelFields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            var successfullyConverted = new List<string>();
            var failedToConvert = new List<string>();

            foreach (var df in dataFields)
            {
                bool converted = false;
                foreach (var mf in modelFields)
                {
                    if (mf.FieldType.IsGenericType && mf.FieldType.GetGenericTypeDefinition() == typeof(ReactiveProperty<>))
                    {
                        if (df.Name == mf.Name && df.FieldType == mf.FieldType.GetGenericArguments()[0])
                        {
                            var rpInstance = mf.GetValue(this);
                            var rpValueSetter = mf.FieldType.GetMethod("set_Value");
                            rpValueSetter.Invoke(rpInstance, new object[] { df.GetValue(data) });
                            successfullyConverted.Add(df.Name);
                            converted = true;
                            break;
                        }
                    }
                }
                if (!converted)
                {
                    failedToConvert.Add(df.Name);
                }
            }

            if (failedToConvert.Count > 0)
            {
                throw new InvalidOperationException($"Failed to convert data fields: {string.Join(", ", failedToConvert)}");
            }

            return failedToConvert.Count == 0;
        }
    }
}