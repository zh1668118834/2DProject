using System;
using UnityEngine;
using Game.Protocol.Battle;
using Game.Infrastructure.Network.Client;
using Game.Client.Bussiness.BattleBussiness.Generic;
using System.Collections.Generic;
using ZeroFrame.Protocol;

namespace Game.Client.Bussiness.BattleBussiness.Network
{

    public class BulletReqAndRes
    {
        NetworkClient battleClient;
        List<Action> actionList;

        public BulletReqAndRes()
        {
            actionList = new List<Action>();
        }

        public void Inject(NetworkClient client)
        {
            battleClient = client;
        }

        public void TickAllRegistAction()
        {
            for (int i = 0; i < actionList.Count; i++)
            {
                var action = actionList[i];
                action.Invoke();
            }
            actionList.Clear();
        }

        #region [Regist]

        public void RegistRes_BulletSpawn(Action<FrameBulletSpawnResMsg> action)
        {
            AddRegister(action);
        }

        public void RegistRes_BulletTearDown(Action<FrameBulletLifeOverResMsg> action)
        {
            AddRegister(action);
        }

        public void RegistRes_BulletHitRole(Action<FrameBulletHitRoleResMsg> action)
        {
            AddRegister(action);
        }

        public void RegistRes_BulletHitWall(Action<FrameBulletHitWallResMsg> action)
        {
            AddRegister(action);
        }

        #endregion

        // Private Func
        void AddRegister<T>(Action<T> action) where T : IZeroMessage<T>, new()
        {
            lock (actionList)
            {
                battleClient.RegistMsg<T>((msg) =>
                {
                    actionList.Add(() =>
                    {
                        action.Invoke(msg);
                    });
                });
            }
        }

    }

}