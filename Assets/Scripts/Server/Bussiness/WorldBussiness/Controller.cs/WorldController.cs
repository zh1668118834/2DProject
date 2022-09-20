using System.Collections.Generic;
using UnityEngine;
using Game.Protocol.World;
using Game.Infrastructure.Generic;
using Game.Client.Bussiness.WorldBussiness;
using Game.Server.Bussiness.WorldBussiness.Facades;
using Game.Client.Bussiness.WorldBussiness.Generic;

namespace Game.Server.Bussiness.WorldBussiness
{

    public class WorldController
    {
        WorldFacades worldFacades;
        int serveFrame;
        float fixedDeltaTime = UnityEngine.Time.fixedDeltaTime;  //0.03f

        // 记录当前所有ConnId
        List<int> connIdList;

        // 记录所有操作帧
        struct FrameOptReqMsgStruct
        {
            public int connId;
            public FrameOptReqMsg msg;
        }
        Dictionary<int, List<FrameOptReqMsgStruct>> wRoleOptQueueDic;

        // 移动记录所有跳跃帧
        struct FrameJumpReqMsgStruct
        {
            public int connId;
            public FrameJumpReqMsg msg;
        }
        Dictionary<int, List<FrameJumpReqMsgStruct>> jumpOptDic;//TODO: --> Queue

        // 记录所有生成帧
        struct FrameWRoleSpawnReqMsgStruct
        {
            public int connId;
            public FrameWRoleSpawnReqMsg msg;
        }
        Dictionary<int, FrameWRoleSpawnReqMsgStruct> wRoleSpawnDic;//TODO: --> Queue

        // 记录所有子弹生成帧
        struct FrameBulletSpawnReqMsgStruct
        {
            public int connId;
            public FrameBulletSpawnReqMsg msg;
        }
        Dictionary<int, FrameBulletSpawnReqMsgStruct> bulletSpawnDic;   //TODO: --> Queue

        // 记录所有拾取物件帧
        struct FrameItemPickUpReqMsgStruct
        {
            public int connId;
            public FrameItemPickReqMsg msg;
        }
        Dictionary<int, Queue<FrameItemPickUpReqMsgStruct>> itemPickUpDic;

        // 记录所有武器射击帧
        struct FrameWeaponShootReqMsgStruct
        {
            public int connId;
            public FrameWeaponShootReqMsg msg;
        }
        Dictionary<int, Queue<FrameWeaponShootReqMsgStruct>> weaponShootDic;

        // 记录所有武器装弹帧
        struct FrameWeaponReloadReqMsgStruct
        {
            public int connId;
            public FrameWeaponReloadReqMsg msg;
        }
        Dictionary<int, Queue<FrameWeaponReloadReqMsgStruct>> weaponReloadDic;

        // 记录所有武器丢弃帧
        struct FrameWeaponDropReqMsgStruct
        {
            public int connId;
            public FrameWeaponDropReqMsg msg;
        }
        Dictionary<int, Queue<FrameWeaponDropReqMsgStruct>> weaponDropDic;

        bool sceneSpawnTrigger;
        bool isSceneSpawn;

        public WorldController()
        {
            connIdList = new List<int>();
            wRoleOptQueueDic = new Dictionary<int, List<FrameOptReqMsgStruct>>();
            jumpOptDic = new Dictionary<int, List<FrameJumpReqMsgStruct>>();
            wRoleSpawnDic = new Dictionary<int, FrameWRoleSpawnReqMsgStruct>();
            bulletSpawnDic = new Dictionary<int, FrameBulletSpawnReqMsgStruct>();
            itemPickUpDic = new Dictionary<int, Queue<FrameItemPickUpReqMsgStruct>>();
            weaponShootDic = new Dictionary<int, Queue<FrameWeaponShootReqMsgStruct>>();
            weaponReloadDic = new Dictionary<int, Queue<FrameWeaponReloadReqMsgStruct>>();
            weaponDropDic = new Dictionary<int, Queue<FrameWeaponDropReqMsgStruct>>();
        }

        public void Inject(WorldFacades worldFacades)
        {
            this.worldFacades = worldFacades;

            var roleRqs = worldFacades.Network.WorldRoleReqAndRes;
            roleRqs.RegistReq_WorldRoleOpt(OnWoldRoleOpt);
            roleRqs.RegistReq_Jump(OnWoldRoleJump);
            roleRqs.RegistReq_WolrdRoleSpawn(OnWoldRoleSpawn);

            var bulletRqs = worldFacades.Network.BulletReqAndRes;
            bulletRqs.RegistReq_BulletSpawn(OnBulletSpawn);

            var itemRqs = worldFacades.Network.ItemReqAndRes;
            itemRqs.RegistReq_ItemPickUp(OnItemPickUp);

            var weaponRqs = worldFacades.Network.WeaponReqAndRes;
            weaponRqs.RegistReq_WeaponShoot(OnWeaponShoot);
            weaponRqs.RegistReq_WeaponReload(OnWeaponReload);
            weaponRqs.RegistReq_WeaponDrop(OnWeaponDrop);

        }

        public void Tick()
        {
            // Tick的过滤条件
            if (sceneSpawnTrigger && !isSceneSpawn)
            {
                SpawWorldChooseScene();
                sceneSpawnTrigger = false;
            }
            if (!isSceneSpawn) return;
            int nextFrame = serveFrame + 1;

            // Physics Simulation
            Tick_Physics_Movement_Bullet(fixedDeltaTime);
            if (!wRoleOptQueueDic.TryGetValue(nextFrame, out var optList) || optList.Count == 0)
            {
                Tick_Physics_Movement_Role(fixedDeltaTime);
                var physicsScene = worldFacades.ClientWorldFacades.Repo.FiledRepo.CurPhysicsScene;
                physicsScene.Simulate(fixedDeltaTime);
            }

            // ====== Life
            Tick_BulletLife(nextFrame);
            Tick_ActiveHookersBehaviour(nextFrame);

            // Client Request
            Tick_WRoleSpawn(nextFrame);
            Tick_BulletSpawn(nextFrame);

            Tick_ItemPickUp(nextFrame);
            Tick_WeaponShoot(nextFrame);
            Tick_WeaponReload(nextFrame);
            Tick_WeaponDrop(nextFrame);

            Tick_AllOpt(nextFrame); // Include Physics Simulation

            // Physcis Col;lision
            Tick_Physics_Collision_Role(nextFrame);
            Tick_Physics_Collision_Bullet(nextFrame);
        }

        #region [Client Requst]

        #region [Role]
        void Tick_WRoleSpawn(int nextFrameIndex)
        {
            if (wRoleSpawnDic.TryGetValue(nextFrameIndex, out var spawn))
            {
                serveFrame = nextFrameIndex;

                var msg = spawn.msg;
                var connId = spawn.connId;

                var clientFacades = worldFacades.ClientWorldFacades;
                var repo = clientFacades.Repo;
                var fieldEntity = repo.FiledRepo.Get(1);
                var rqs = worldFacades.Network.WorldRoleReqAndRes;
                var roleRepo = repo.RoleRepo;
                var wrid = roleRepo.Size;

                // 服务器逻辑
                var roleEntity = clientFacades.Domain.WorldRoleDomain.SpawnWorldRoleLogic(fieldEntity.transform);
                roleEntity.Ctor();
                roleEntity.SetEntityId(wrid);
                roleEntity.SetConnId(connId);
                Debug.Log($"服务器逻辑[生成角色] serveFrame:{serveFrame} wRid:{wrid} 位置:{roleEntity.MoveComponent.CurPos}");

                // ====== 发送其他角色的状态同步帧给请求者
                var allEntity = roleRepo.GetAll();
                for (int i = 0; i < allEntity.Length; i++)
                {
                    var otherRole = allEntity[i];
                    rqs.SendUpdate_WRoleState(connId, nextFrameIndex, otherRole);
                }

                // ====== 广播请求者创建的角色给其他人
                connIdList.ForEach((otherConnId) =>
                {
                    if (otherConnId != connId)
                    {
                        rqs.SendUpdate_WRoleState(otherConnId, nextFrameIndex, roleEntity);
                    }
                });

                // ====== 回复请求者创建的角色
                rqs.SendUpdate_WRoleState(connId, nextFrameIndex, roleEntity);

                roleRepo.Add(roleEntity);
            }
        }

        void Tick_RoleStateIdle(int nextFrame)
        {
            //人物静止和运动 2个状态
            bool isNextFrame = false;
            var WorldRoleRepo = worldFacades.ClientWorldFacades.Repo.RoleRepo;
            WorldRoleRepo.Foreach((roleEntity) =>
            {
                if (roleEntity.IsIdle() && roleEntity.RoleState != RoleState.Normal)
                {
                    isNextFrame = true;
                    roleEntity.SetRoleState(RoleState.Normal);

                    var rqs = worldFacades.Network.WorldRoleReqAndRes;
                    connIdList.ForEach((connId) =>
                    {
                        rqs.SendUpdate_WRoleState(connId, nextFrame, roleEntity);
                    });
                }
            });

            if (isNextFrame)
            {
                serveFrame = nextFrame;
            }
        }

        void Tick_AllOpt(int nextFrame)
        {
            Tick_JumpOpt(nextFrame);
            Tick_MoveAndRotateOpt(nextFrame);
        }

        void Tick_MoveAndRotateOpt(int nextFrame)
        {
            if (!wRoleOptQueueDic.TryGetValue(nextFrame, out var optList)) return;

            serveFrame = nextFrame;

            while (optList.Count != 0)
            {
                var lastIndex = optList.Count - 1;
                var opt = optList[lastIndex];

                var msg = opt.msg;
                var realMsg = msg.msg;
                var connId = opt.connId;

                var rid = (byte)(realMsg >> 48);
                var roleRepo = worldFacades.ClientWorldFacades.Repo.RoleRepo;
                var role = roleRepo.GetByEntityId(rid);
                var optTypeId = opt.msg.optTypeId;
                var rqs = worldFacades.Network.WorldRoleReqAndRes;

                // ------------移动
                if (optTypeId == 1)
                {
                    Vector3 dir = new Vector3((short)(realMsg >> 32) / 100f, (short)(realMsg >> 16) / 100f, (short)realMsg / 100f);

                    //服务器逻辑Move + 物理模拟
                    var physicsDomain = worldFacades.ClientWorldFacades.Domain.PhysicsDomain;

                    var curPhysicsScene = worldFacades.ClientWorldFacades.Repo.FiledRepo.CurPhysicsScene;

                    role.MoveComponent.ActivateMoveVelocity(dir);
                    physicsDomain.Tick_RoleMoveHitErase(role);

                    role.MoveComponent.Tick_Friction(fixedDeltaTime);
                    role.MoveComponent.Tick_GravityVelocity(fixedDeltaTime);
                    role.MoveComponent.Tick_Rigidbody(fixedDeltaTime);
                    curPhysicsScene.Simulate(fixedDeltaTime);

                    // 人物状态同步
                    if (role.RoleState != RoleState.Hooking) role.SetRoleState(RoleState.Move);
                    //发送状态同步帧
                    connIdList.ForEach((otherConnId) =>
                    {
                        rqs.SendUpdate_WRoleState(otherConnId, nextFrame, role);
                    });

                }

                // ------------转向（基于客户端鉴权的同步）
                if (optTypeId == 2)
                {
                    Vector3 eulerAngle = new Vector3((short)(realMsg >> 32), (short)(realMsg >> 16), (short)realMsg);
                    role.MoveComponent.SetEulerAngle(eulerAngle);
                    // Debug.Log($"转向（基于客户端鉴权的同步）eulerAngle:{eulerAngle}");
                    //发送状态同步帧
                    connIdList.ForEach((otherConnId) =>
                    {
                        if (otherConnId != connId)
                        {
                            //只广播给非本人
                            rqs.SendUpdate_WRoleState(otherConnId, nextFrame, role);
                        }
                    });
                }

                optList.RemoveAt(lastIndex);
            }
        }

        void Tick_JumpOpt(int nextFrame)
        {
            if (!jumpOptDic.TryGetValue(nextFrame, out var jumpOptList)) return;
            serveFrame = nextFrame;

            while (jumpOptList.Count != 0)
            {
                var lastIndex = jumpOptList.Count - 1;
                var jumpOpt = jumpOptList[lastIndex];

                var wRid = jumpOpt.msg.wRid;
                var roleRepo = worldFacades.ClientWorldFacades.Repo.RoleRepo;
                var roleEntity = roleRepo.GetByEntityId(wRid);
                var rqs = worldFacades.Network.WorldRoleReqAndRes;

                //服务器逻辑Jump
                if (roleEntity.MoveComponent.TryJump())
                {
                    if (roleEntity.RoleState != RoleState.Hooking) roleEntity.SetRoleState(RoleState.Jump);
                    //发送状态同步帧
                    connIdList.ForEach((connId) =>
                    {
                        rqs.SendUpdate_WRoleState(connId, nextFrame, roleEntity);
                    });
                }

                jumpOptList.RemoveAt(lastIndex);
            }
        }
        #endregion

        #region [Bullet]
        void Tick_BulletSpawn(int nextFrame)
        {
            if (bulletSpawnDic.TryGetValue(nextFrame, out var bulletSpawn))
            {
                serveFrame = nextFrame;

                int connId = bulletSpawn.connId;
                var msg = bulletSpawn.msg;

                var bulletTypeByte = msg.bulletType;
                byte wRid = msg.wRid;
                float targetPosX = msg.targetPosX / 10000f;
                float targetPosY = msg.targetPosY / 10000f;
                float targetPosZ = msg.targetPosZ / 10000f;
                Vector3 targetPos = new Vector3(targetPosX, targetPosY, targetPosZ);
                var roleEntity = worldFacades.ClientWorldFacades.Repo.RoleRepo.GetByEntityId(msg.wRid);
                var moveComponent = roleEntity.MoveComponent;
                var shootStartPoint = roleEntity.ShootPointPos;
                Vector3 shootDir = targetPos - shootStartPoint;
                shootDir.Normalize();

                // 服务器逻辑
                var bulletType = (BulletType)bulletTypeByte;
                var clientFacades = worldFacades.ClientWorldFacades;
                var fieldEntity = clientFacades.Repo.FiledRepo.Get(1);

                var bulletEntity = clientFacades.Domain.BulletDomain.SpawnBullet(fieldEntity.transform, bulletType);
                var bulletRepo = clientFacades.Repo.BulletRepo;
                var bulletId = bulletRepo.BulletCount;
                bulletEntity.MoveComponent.SetCurPos(shootStartPoint);
                bulletEntity.MoveComponent.SetForward(shootDir);
                bulletEntity.MoveComponent.ActivateMoveVelocity(shootDir);
                switch (bulletType)
                {
                    case BulletType.DefaultBullet:
                        break;
                    case BulletType.Grenade:
                        break;
                    case BulletType.Hooker:
                        var hookerEntity = (HookerEntity)bulletEntity;
                        hookerEntity.SetMasterWRid(roleEntity.EntityId);
                        hookerEntity.SetMasterGrabPoint(roleEntity.transform);
                        break;
                }
                bulletEntity.SetMasterId(wRid);
                bulletEntity.SetEntityId(bulletId);
                bulletEntity.gameObject.SetActive(true);
                bulletRepo.Add(bulletEntity);
                Debug.Log($"服务器逻辑[生成子弹] serveFrame {serveFrame} connId {connId}:  bulletType:{bulletTypeByte.ToString()} bulletId:{bulletId}  MasterWRid:{wRid}  起点：{shootStartPoint} 终点：{targetPos} 飞行方向:{shootDir}");

                var rqs = worldFacades.Network.BulletReqAndRes;
                connIdList.ForEach((otherConnId) =>
                {
                    rqs.SendRes_BulletSpawn(otherConnId, serveFrame, bulletType, bulletId, wRid, shootDir);
                });
            }
        }

        void Tick_BulletLife(int nextFrame)
        {
            var tearDownList = worldFacades.ClientWorldFacades.Domain.BulletDomain.Tick_BulletLife(NetworkConfig.FIXED_DELTA_TIME);
            if (tearDownList.Count == 0) return;

            tearDownList.ForEach((bulletEntity) =>
            {

                Queue<WorldRoleLogicEntity> effectRoleQueue = new Queue<WorldRoleLogicEntity>();
                var bulletType = bulletEntity.BulletType;
                if (bulletType == BulletType.DefaultBullet)
                {
                    bulletEntity.TearDown();
                }
                else if (bulletEntity is GrenadeEntity grenadeEntity)
                {
                    Debug.Log("爆炸");
                    grenadeEntity.TearDown();
                    var roleRepo = worldFacades.ClientWorldFacades.Repo.RoleRepo;
                    roleRepo.Foreach((role) =>
                    {
                        var dis = Vector3.Distance(role.MoveComponent.CurPos, bulletEntity.MoveComponent.CurPos);
                        if (dis < 7f)
                        {
                            var dir = role.MoveComponent.CurPos - bulletEntity.MoveComponent.CurPos;
                            var extraV = dir.normalized * 10f;
                            role.MoveComponent.AddExtraVelocity(extraV);
                            role.MoveComponent.Tick_Rigidbody(fixedDeltaTime);
                            role.SetRoleState(RoleState.Move);
                            effectRoleQueue.Enqueue(role);
                        }
                    });
                }
                else if (bulletEntity is HookerEntity hookerEntity)
                {
                    hookerEntity.TearDown();
                }

                var bulletRepo = worldFacades.ClientWorldFacades.Repo.BulletRepo;
                bulletRepo.TryRemove(bulletEntity);

                var bulletRqs = worldFacades.Network.BulletReqAndRes;
                var roleRqs = worldFacades.Network.WorldRoleReqAndRes;
                connIdList.ForEach((connId) =>
                {
                    // 广播子弹销毁消息
                    bulletRqs.SendRes_BulletTearDown(connId, serveFrame, bulletType, bulletEntity.MasterId, bulletEntity.EntityId, bulletEntity.MoveComponent.CurPos);
                });
                while (effectRoleQueue.TryDequeue(out var role))
                {
                    Debug.Log($"角色击飞发送");
                    connIdList.ForEach((connId) =>
                    {
                        // 广播被影响角色的最新状态消息
                        roleRqs.SendUpdate_WRoleState(connId, serveFrame, role);
                    });
                }

            });

            serveFrame = nextFrame;
        }

        void Tick_ActiveHookersBehaviour(int nextFrame)
        {
            var activeHookers = worldFacades.ClientWorldFacades.Domain.BulletDomain.GetActiveHookerList();
            List<WorldRoleLogicEntity> roleList = new List<WorldRoleLogicEntity>();
            var rqs = worldFacades.Network.WorldRoleReqAndRes;
            bool hasHookerLoose = false;
            activeHookers.ForEach((hooker) =>
            {
                var master = worldFacades.ClientWorldFacades.Repo.RoleRepo.GetByEntityId(hooker.MasterId);
                if (!hooker.TickHooker(out float force))
                {
                    master.SetRoleState(RoleState.Normal);
                    hasHookerLoose = true;
                    //发送爪钩断开后的角色状态帧
                    connIdList.ForEach((connId) =>
                    {
                        rqs.SendUpdate_WRoleState(connId, serveFrame, master);
                    });
                    return;
                }

                var masterMC = master.MoveComponent;
                var hookerEntityMC = hooker.MoveComponent;
                var dir = hookerEntityMC.CurPos - masterMC.CurPos;
                var dis = Vector3.Distance(hookerEntityMC.CurPos, masterMC.CurPos);
                dir.Normalize();
                var v = dir * force * fixedDeltaTime;
                masterMC.AddExtraVelocity(v);
                master.SetRoleState(RoleState.Hooking);

                if (!roleList.Contains(master)) roleList.Add(master);
            });

            roleList.ForEach((master) =>
            {
                //发送爪钩作用力后的角色状态帧
                connIdList.ForEach((connId) =>
                {
                    rqs.SendUpdate_WRoleState(connId, serveFrame, master);
                });
            });

            if (roleList.Count != 0 || hasHookerLoose) serveFrame = nextFrame;
        }
        #endregion

        #region [Item]
        void Tick_ItemPickUp(int nextFrame)
        {
            if (itemPickUpDic.TryGetValue(nextFrame, out var itemPickQueue))
            {
                serveFrame = nextFrame;
                while (itemPickQueue.TryPeek(out var msgStruct))
                {
                    itemPickQueue.Dequeue();

                    int connId = msgStruct.connId;
                    var msg = msgStruct.msg;
                    // TODO:Add judgement like 'Can He Pick It Up?'
                    var repo = worldFacades.ClientWorldFacades.Repo;
                    var roleRepo = repo.RoleRepo;
                    var role = roleRepo.GetByEntityId(msg.wRid);
                    ItemType itemType = (ItemType)msg.itemType;
                    var itemDomain = worldFacades.ClientWorldFacades.Domain.ItemDomain;
                    bool isPickUpSucceed = itemDomain.TryPickUpItem(itemType, msg.entityId, repo, role);
                    if (isPickUpSucceed)
                    {
                        var rqs = worldFacades.Network.ItemReqAndRes;
                        connIdList.ForEach((connId) =>
                        {
                            rqs.SendRes_ItemPickUp(connId, serveFrame, msg.wRid, itemType, msg.entityId);
                        });
                    }
                    else
                    {
                        Debug.Log($"{itemType.ToString()}物品拾取失败");
                    }
                }
            }
        }

        #endregion

        #region [Weapon]

        void Tick_WeaponShoot(int nextFrame)
        {
            if (weaponShootDic.TryGetValue(nextFrame, out var queue))
            {
                serveFrame = nextFrame;
                var clientFacades = worldFacades.ClientWorldFacades;
                var weaponRepo = clientFacades.Repo.WeaponRepo;
                var roleRepo = clientFacades.Repo.RoleRepo;
                var bulletRepo = clientFacades.Repo.BulletRepo;

                var weaponRqs = worldFacades.Network.WeaponReqAndRes;
                var bulletRqs = worldFacades.Network.BulletReqAndRes;

                var fieldEntity = clientFacades.Repo.FiledRepo.Get(1);

                while (queue.TryPeek(out var msgStruct))
                {
                    queue.Dequeue();
                    var msg = msgStruct.msg;
                    var masterId = msg.masterId;

                    if (roleRepo.TryGetByEntityId(masterId, out var master))
                    {
                        if (master.WeaponComponent.TryWeaponShoot())
                        {
                            //子弹生成
                            float targetPosX = msg.targetPosX / 10000f;
                            float targetPosY = msg.targetPosY / 10000f;
                            float targetPosZ = msg.targetPosZ / 10000f;
                            Vector3 targetPos = new Vector3(targetPosX, targetPosY, targetPosZ);
                            var shootStartPoint = master.ShootPointPos;
                            Vector3 shootDir = targetPos - shootStartPoint;
                            shootDir.Normalize();

                            var bulletType = master.WeaponComponent.CurrentWeapon.bulletType;
                            var bulletEntity = clientFacades.Domain.BulletDomain.SpawnBullet(fieldEntity.transform, bulletType);
                            var bulletId = bulletRepo.BulletCount;
                            bulletEntity.MoveComponent.SetCurPos(shootStartPoint);
                            bulletEntity.MoveComponent.SetForward(shootDir);
                            bulletEntity.MoveComponent.ActivateMoveVelocity(shootDir);
                            bulletEntity.SetMasterId(masterId);
                            bulletEntity.SetEntityId(bulletId);
                            bulletEntity.gameObject.SetActive(true);
                            bulletRepo.Add(bulletEntity);

                            connIdList.ForEach((connId) =>
                            {
                                weaponRqs.SendRes_WeaponShoot(connId, serveFrame, masterId);
                                bulletRqs.SendRes_BulletSpawn(connId, serveFrame, bulletType, bulletId, masterId, shootDir);
                            });
                            Debug.Log($"生成子弹bulletType:{bulletType.ToString()} bulletId:{bulletId}  MasterWRid:{masterId}  起点：{shootStartPoint} 终点：{targetPos} 飞行方向:{shootDir}");
                        }
                    }
                }
            }
        }

        void Tick_WeaponReload(int nextFrame)
        {
            if (weaponReloadDic.TryGetValue(nextFrame, out var queue))
            {
                serveFrame = nextFrame;
                var weaponRepo = worldFacades.ClientWorldFacades.Repo.WeaponRepo;
                var roleRepo = worldFacades.ClientWorldFacades.Repo.RoleRepo;
                var rqs = worldFacades.Network.WeaponReqAndRes;
                while (queue.TryPeek(out var msgStruct))
                {
                    queue.Dequeue();
                    var msg = msgStruct.msg;
                    var masterId = msg.masterId;
                    if (roleRepo.TryGetByEntityId(masterId, out var master))
                    {
                        if (master.TryWeaponReload())
                        {
                            //TODO: 装弹时间过后才发送回客户端
                            connIdList.ForEach((connId) =>
                            {
                                rqs.SendRes_WeaponReloaded(connId, serveFrame, masterId);
                            });
                        }
                    }
                }

            }
        }

        void Tick_WeaponDrop(int nextFrame)
        {
            if (weaponDropDic.TryGetValue(nextFrame, out var queue))
            {
                serveFrame = nextFrame;
                var weaponRepo = worldFacades.ClientWorldFacades.Repo.WeaponRepo;
                var roleRepo = worldFacades.ClientWorldFacades.Repo.RoleRepo;
                var rqs = worldFacades.Network.WeaponReqAndRes;
                while (queue.TryPeek(out var msgStruct))
                {
                    queue.Dequeue();
                    var msg = msgStruct.msg;
                    var entityId = msg.entityId;
                    var masterId = msg.masterId;
                    if (roleRepo.TryGetByEntityId(masterId, out var master)
                    && master.WeaponComponent.TryDropWeapon(entityId, out var weapon))
                    {
                        // 服务器逻辑
                        worldFacades.ClientWorldFacades.Domain.WeaponDomain.ReuseWeapon(weapon, master.MoveComponent.CurPos);

                        connIdList.ForEach((connId) =>
                        {
                            rqs.SendRes_WeaponDrop(connId, serveFrame, masterId, entityId);
                        });
                    }
                }
            }
        }

        #endregion

        #endregion

        #region [Physics]

        // 地形造成的减速 TODO:滑铲加速
        void Tick_Physics_Collision_Role(int nextFrame)
        {
            var physicsDomain = worldFacades.ClientWorldFacades.Domain.PhysicsDomain;
            var roleList = physicsDomain.Tick_AllRoleHitEnter(fixedDeltaTime);
            var rqs = worldFacades.Network.WorldRoleReqAndRes;
            roleList.ForEach((role) =>
            {
                connIdList.ForEach((connId) =>
                {
                    rqs.SendUpdate_WRoleState(connId, serveFrame, role);
                });
            });

            if (roleList.Count != 0) serveFrame = nextFrame;
        }

        void Tick_Physics_Collision_Bullet(int nextFrame)
        {
            var physicsDomain = worldFacades.ClientWorldFacades.Domain.PhysicsDomain;
            physicsDomain.Refresh_BulletHit();

            var bulletDomain = worldFacades.ClientWorldFacades.Domain.BulletDomain;
            var bulletRepo = worldFacades.ClientWorldFacades.Repo.BulletRepo;
            List<BulletEntity> removeList = new List<BulletEntity>();
            bulletRepo.Foreach((bullet) =>
            {
                bool isHitSomething = false;
                if (bullet.HitRoleQueue.TryDequeue(out var wrole))
                {
                    isHitSomething = true;
                    // Server Logic
                    wrole.HealthComponent.HurtByBullet(bullet);
                    wrole.MoveComponent.HitByBullet(bullet);
                    if (wrole.HealthComponent.IsDead)
                    {
                        wrole.TearDown();
                        wrole.Reborn();
                    }
                    // Notice Client
                    var rqs = worldFacades.Network.BulletReqAndRes;
                    connIdList.ForEach((connId) =>
                    {
                        rqs.SendRes_BulletHitRole(connId, serveFrame, bullet.EntityId, wrole.EntityId);
                    });

                }
                if (bullet.HitFieldQueue.TryDequeue(out var field))
                {
                    isHitSomething = true;
                    // TODO:Server Logic
                    // Notice Client
                    var rqs = worldFacades.Network.BulletReqAndRes;
                    connIdList.ForEach((connId) =>
                    {
                        rqs.SendRes_BulletHitWall(connId, serveFrame, bullet);
                    });
                }

                if (isHitSomething)
                {
                    serveFrame = nextFrame;
                    // Server Logic
                    if (bullet.BulletType == BulletType.DefaultBullet)
                    {
                        // 普通子弹的逻辑，只是单纯的移除
                        removeList.Add(bullet);
                    }
                    if (bullet is HookerEntity hookerEntity)
                    {
                        // 爪钩逻辑
                        if (field != null) hookerEntity.TryGrabSomthing(field.transform);
                        if (wrole != null) hookerEntity.TryGrabSomthing(wrole.transform);
                    }
                    else if (bullet is GrenadeEntity grenadeEntity)
                    {
                        // 手雷逻辑: 速度清零
                        grenadeEntity.MoveComponent.SetMoveVelocity(Vector3.zero);
                    }

                }
            });
            removeList.ForEach((bullet) =>
            {
                bullet.TearDown();
                bulletRepo.TryRemove(bullet);
            });
        }

        void Tick_Physics_Movement_Role(float fixedDeltaTime)
        {
            var domain = worldFacades.ClientWorldFacades.Domain.WorldRoleDomain;
            domain.Tick_RoleRigidbody(fixedDeltaTime);
        }

        void Tick_Physics_Movement_Bullet(float fixedDeltaTime)
        {
            var domain = worldFacades.ClientWorldFacades.Domain.BulletDomain;
            domain.Tick_Bullet(fixedDeltaTime);
        }

        #endregion

        // ====== Network
        // Role
        void OnWoldRoleOpt(int connId, FrameOptReqMsg msg)
        {
            if (!wRoleOptQueueDic.TryGetValue(serveFrame + 1, out var optQueue))
            {
                optQueue = new List<FrameOptReqMsgStruct>();
                wRoleOptQueueDic[serveFrame + 1] = optQueue;
            }

            optQueue.Add(new FrameOptReqMsgStruct { connId = connId, msg = msg });
        }

        void OnWoldRoleJump(int connId, FrameJumpReqMsg msg)
        {
            if (!jumpOptDic.TryGetValue(serveFrame + 1, out var jumpOptList))
            {
                jumpOptList = new List<FrameJumpReqMsgStruct>();
                jumpOptDic[serveFrame + 1] = jumpOptList;
            }

            jumpOptList.Add(new FrameJumpReqMsgStruct { connId = connId, msg = msg });
        }

        void OnWoldRoleSpawn(int connId, FrameWRoleSpawnReqMsg msg)
        {
            wRoleSpawnDic.TryAdd(serveFrame + 1, new FrameWRoleSpawnReqMsgStruct { connId = connId, msg = msg });
            // TODO:连接服和世界服分离
            connIdList.Add(connId);
            // 创建场景
            sceneSpawnTrigger = true;
        }

        void OnBulletSpawn(int connId, FrameBulletSpawnReqMsg msg)
        {
            bulletSpawnDic.TryAdd(serveFrame + 1, new FrameBulletSpawnReqMsgStruct { connId = connId, msg = msg });
        }

        // ========= Item
        void OnItemPickUp(int connId, FrameItemPickReqMsg msg)
        {
            if (!itemPickUpDic.TryGetValue(serveFrame + 1, out var msgStruct))
            {
                msgStruct = new Queue<FrameItemPickUpReqMsgStruct>();
                itemPickUpDic[serveFrame + 1] = msgStruct;
            }

            msgStruct.Enqueue(new FrameItemPickUpReqMsgStruct { connId = connId, msg = msg });
        }

        // =========== Weapon
        void OnWeaponShoot(int connId, FrameWeaponShootReqMsg msg)
        {
            if (!weaponShootDic.TryGetValue(serveFrame + 1, out var msgStruct))
            {
                msgStruct = new Queue<FrameWeaponShootReqMsgStruct>();
                weaponShootDic[serveFrame + 1] = msgStruct;
            }

            msgStruct.Enqueue(new FrameWeaponShootReqMsgStruct { connId = connId, msg = msg });
            Debug.Log("收到武器射击请求");
        }

        void OnWeaponReload(int connId, FrameWeaponReloadReqMsg msg)
        {
            if (!weaponReloadDic.TryGetValue(serveFrame + 1, out var msgStruct))
            {
                msgStruct = new Queue<FrameWeaponReloadReqMsgStruct>();
                weaponReloadDic[serveFrame + 1] = msgStruct;
            }

            msgStruct.Enqueue(new FrameWeaponReloadReqMsgStruct { connId = connId, msg = msg });
            Debug.Log("收到武器换弹请求");
        }

        void OnWeaponDrop(int connId, FrameWeaponDropReqMsg msg)
        {
            if (!weaponDropDic.TryGetValue(serveFrame + 1, out var msgStruct))
            {
                msgStruct = new Queue<FrameWeaponDropReqMsgStruct>();
                weaponDropDic[serveFrame + 1] = msgStruct;
            }

            msgStruct.Enqueue(new FrameWeaponDropReqMsgStruct { connId = connId, msg = msg });
            Debug.Log("收到武器丢弃请求");
        }

        // ====== Scene Spawn Method
        async void SpawWorldChooseScene()
        {
            // Load Scene And Spawn Field
            var domain = worldFacades.ClientWorldFacades.Domain;
            var fieldEntity = await domain.WorldSpawnDomain.SpawnCityScene();
            fieldEntity.SetFieldId(1);
            var fieldEntityRepo = worldFacades.ClientWorldFacades.Repo.FiledRepo;
            fieldEntityRepo.Add(fieldEntity);
            fieldEntityRepo.SetPhysicsScene(fieldEntity.gameObject.scene.GetPhysicsScene());
            isSceneSpawn = true;

            // 生成场景资源，并回复客户端
            List<ItemType> itemTypeList = new List<ItemType>();
            List<byte> subTypeList = new List<byte>();
            AssetPointEntity[] assetPointEntities = fieldEntity.transform.GetComponentsInChildren<AssetPointEntity>();
            for (int i = 0; i < assetPointEntities.Length; i++)
            {
                var assetPoint = assetPointEntities[i];
                ItemGenProbability[] itemGenProbabilities = assetPoint.itemGenProbabilityArray;
                float totalWeight = 0;
                for (int j = 0; j < itemGenProbabilities.Length; j++) totalWeight += itemGenProbabilities[j].weight;
                float lRange = 0;
                float rRange = 0;
                float randomNumber = Random.Range(0f, 1f);
                for (int j = 0; j < itemGenProbabilities.Length; j++)
                {
                    ItemGenProbability igp = itemGenProbabilities[j];
                    if (igp.weight <= 0) continue;
                    rRange = lRange + igp.weight / totalWeight;
                    if (randomNumber >= lRange && randomNumber < rRange)
                    {
                        itemTypeList.Add(igp.itemType);
                        subTypeList.Add(igp.subType);
                        break;
                    }
                    lRange = rRange;
                }
            }

            int count = itemTypeList.Count;
            ushort[] entityIdArray = new ushort[count];
            byte[] itemTypeByteArray = new byte[count];
            Debug.Log($"服务器地图物件资源开始生成[数量:{count}]----------------------------------------------------");
            int index = 0;
            itemTypeList.ForEach((itemType) =>
            {
                var parent = assetPointEntities[index];
                var subtype = subTypeList[index];
                itemTypeByteArray[index] = (byte)itemType;
                // 生成武器资源
                var itemDomain = worldFacades.ClientWorldFacades.Domain.ItemDomain;
                var item = itemDomain.SpawnItem(itemType, subtype);

                ushort entityId = 0;
                switch (itemType)
                {
                    case ItemType.Default:
                        break;
                    case ItemType.Weapon:
                        var weaponEntity = item.GetComponent<WeaponEntity>();
                        var weaponRepo = worldFacades.ClientWorldFacades.Repo.WeaponRepo;
                        entityId = weaponRepo.WeaponIdAutoIncreaseId;
                        weaponEntity.Ctor();
                        weaponEntity.SetEntityId(entityId);
                        weaponRepo.Add(weaponEntity);
                        Debug.Log($"生成武器资源:{entityId}");
                        entityIdArray[index] = entityId;
                        break;
                    case ItemType.BulletPack:
                        var bulletPackEntity = item.GetComponent<BulletPackEntity>();
                        var bulletPackRepo = worldFacades.ClientWorldFacades.Repo.BulletPackRepo;
                        entityId = bulletPackRepo.bulletPackAutoIncreaseId;
                        bulletPackEntity.Ctor();
                        bulletPackEntity.SetEntityId(entityId);
                        bulletPackRepo.Add(bulletPackEntity);
                        Debug.Log($"生成子弹包资源:{entityId}");
                        entityIdArray[index] = entityId;
                        bulletPackRepo.bulletPackAutoIncreaseId++;
                        break;
                    case ItemType.Pill:
                        break;

                }

                item.transform.SetParent(parent.transform);
                item.transform.localPosition = Vector3.zero;
                item.name += entityId;

                index++;
            });

            Debug.Log($"地图物件资源生成完毕******************************************************");

            var rqs = worldFacades.Network.ItemReqAndRes;
            connIdList.ForEach((connId) =>
            {
                rqs.SendRes_ItemSpawn(connId, serveFrame, itemTypeByteArray, subTypeList.ToArray(), entityIdArray);
            });
        }

    }

}