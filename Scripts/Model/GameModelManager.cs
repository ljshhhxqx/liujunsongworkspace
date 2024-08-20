using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Data;

namespace Model
{
    public interface IGameModelManager
    {
        void Register<T>() where T: GameModel, new();
        void Unregister<T>() where T: GameModel;
        void Clear();
        T GetModel<T>() where T: GameModel, new();
        void ConvertAllData(IEnumerable<GameData> gameDatas);
    }

    class GameModelManager : IGameModelManager
    {
        private readonly Dictionary<Type, GameModel> gameModels = new Dictionary<Type, GameModel>();
        private readonly Dictionary<Type, Type> dataModelMap = new Dictionary<Type, Type>();

        public GameModelManager()
        {
            //在这里建立所有GameData和GameModel的映射关系
            dataModelMap.Add(typeof(PlayerInGameData), typeof(PlayerInGameModel));
        }

        public bool InitAllModels(IEnumerable<GameModel> models)
        {
            if (gameModels.Count == 0)
            {
                foreach (var model in models)
                {
                    gameModels.AddOrUpdate(model.GetType(), model);
                }
                return true;
            }
            return false;
        }

        public void Register<T>() where T : GameModel, new()
        {
            var type = typeof(T);
            gameModels.TryAdd(type, new T());
        }

        public void Unregister<T>() where T : GameModel
        {
            var type = typeof(T);
            if (gameModels.ContainsKey(type))
            {
                gameModels[type].Dispose();
                gameModels.Remove(type);
            }
        }

        public void Clear()
        {
            foreach (var model in gameModels.Values)
            {
                model?.Dispose();
            }
            gameModels.Clear();
        }

        public T GetModel<T>() where T : GameModel, new()
        {
            var type = typeof(T);
            if (gameModels.TryGetValue(type, out var model))
            {
                return (T)model;
            }

            return null;
        }
        
        //此处把所有的data转化为Model
        public void ConvertAllData(IEnumerable<GameData> gameDatas)
        {
            foreach (var data in gameDatas)
            {
                if (dataModelMap.TryGetValue(data.GetType(), out var modelType))
                {
                    var model = GetModelAsGameModel(modelType);
                    model.ConvertDataToModel(data);
                }
            }
        }

        private GameModel GetModelAsGameModel(Type modelType)
        {
            if (!gameModels.TryGetValue(modelType, out var model))
            {
                model = (GameModel)Activator.CreateInstance(modelType);
                gameModels[modelType] = model;
            }
            return model;
        }
    }
    
    /// <summary>
    /// 用这个类来存储玩家的所有游戏数据
    /// </summary>
    public class PlayerGameModel : IGameModelManager
    {
        //玩家的唯一id
        private int uid { get; set; }
        private GameModelManager gameModelManager { get; set; }

        public PlayerGameModel(int uid, IEnumerable<GameModel> models)
        {
            this.uid = uid;
            this.gameModelManager = new GameModelManager();
            this.gameModelManager.InitAllModels(models);
        }
        
        public override bool Equals(object obj)
        {
            return obj is PlayerGameModel data &&
                   uid == data.uid;
        }
    
        public override int GetHashCode()
        {
            return uid.GetHashCode();
        }

        public void Register<T>() where T : GameModel, new()
        {
            gameModelManager.Register<T>();
        }

        public void Unregister<T>() where T : GameModel
        {
            gameModelManager.Unregister<T>();
        }

        public void Clear()
        {
            gameModelManager.Clear();
        }

        public T GetModel<T>() where T : GameModel, new()
        {
            return gameModelManager.GetModel<T>();
        }

        public void ConvertAllData(IEnumerable<GameData> gameDatas)
        {
            gameModelManager.ConvertAllData(gameDatas);
        }
    }

    public class PlayersGameModelManager
    {
        private readonly Dictionary<int, PlayerGameModel> players = new Dictionary<int, PlayerGameModel>();
        private static IEnumerable<GameModel> models;

        private IEnumerable<GameModel> Models
        {
            get
            {
                return models ??= GetPlayerModels();
            }
        }

        private IEnumerable<GameModel> GetPlayerModels()
        {
            var modelTypes = Assembly.GetAssembly(typeof(GameModel)).GetTypes()
                .Where(t => t.IsSubclassOf(typeof(GameModel)) && !t.IsAbstract);
            foreach (var modelType in modelTypes)
            {
                if (modelType != typeof(GameModel))
                {
                    yield return (GameModel)Activator.CreateInstance(modelType);
                }
            }
        }

        public void AddPlayer(int uid)
        {
            if (!players.ContainsKey(uid))
            {
                players.Add(uid, new PlayerGameModel(uid, Models));
            }
            else
            {
                throw new ArgumentException($"Player {uid} already exists");
            }
        }
        
        public void RemovePlayer(int uid)
        {
            if (players.TryGetValue(uid, out var playerGameModel))
            {
                playerGameModel.Clear();
                players.Remove(uid);
            }
        }

        public void Clear()
        {
            foreach (var player in players.Values)
            {
                player.Clear();
            }
            players.Clear();
        }

        public PlayerGameModel GetPlayerModel(int uid)
        {
            if (players.TryGetValue(uid, out var playerGameModel))

            {
                return playerGameModel;
            }
            return null;
        }
    }
}