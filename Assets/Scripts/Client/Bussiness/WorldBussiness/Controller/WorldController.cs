using System.Collections.Generic;
using UnityEngine;
using Game.Generic;
using Game.Client.Bussiness.WorldBussiness.Shot;
using Game.Client.Bussiness.WorldBussiness.Facades;
using Game.Protocol.World;
using Game.Infrastructure.Network;
using Game.Client.Bussiness.EventCenter;
using System;
using Game.Infrastructure.Generic;

namespace Game.Client.Bussiness.WorldBussiness.Controller
{

    public class WorldController
    {
        WorldFacades worldFacades;
        int worldClientFrame;
        float fixedDeltaTime => UnityEngine.Time.fixedDeltaTime;
        // 生成队列
        Queue<FrameWRoleSpawnResMsg> roleSpawnQueue;
        Queue<FrameBulletSpawnResMsg> bulletSpawnQueue;
        // 物理事件队列
        Queue<FrameBulletHitRoleResMsg> bulletHitRoleQueue;
        Queue<FrameBulletTearDownResMsg> bulletTearDownQueue;

        // 人物状态同步队列
        Queue<WRoleStateUpdateMsg> stateQueue;

        bool isSync;

        public WorldController()
        {
            // Between Bussiness
            NetworkEventCenter.RegistLoginSuccess(EnterWorldChooseScene);

            roleSpawnQueue = new Queue<FrameWRoleSpawnResMsg>();
            bulletSpawnQueue = new Queue<FrameBulletSpawnResMsg>();
            stateQueue = new Queue<WRoleStateUpdateMsg>();
            bulletHitRoleQueue = new Queue<FrameBulletHitRoleResMsg>();
            bulletTearDownQueue = new Queue<FrameBulletTearDownResMsg>();
        }

        public void Inject(WorldFacades worldFacades)
        {
            this.worldFacades = worldFacades;

            var roleRqs = worldFacades.Network.WorldRoleReqAndRes;
            roleRqs.RegistRes_WorldRoleSpawn(OnWorldRoleSpawn);
            roleRqs.RegistUpdate_WRole(OnWRoleSync);

            var bulletRqs = worldFacades.Network.BulletReqAndRes;
            bulletRqs.RegistRes_BulletSpawn(OnBulletSpawn);
            bulletRqs.RegistRes_BulletHitRole(OnBulletHitRole);
            bulletRqs.RegistRes_BulletTearDown(OnBulletTearDown);


        }

        public void Tick()
        {
            int nextFrame = worldClientFrame + 1;

            //1
            Tick_RoleSpawn(nextFrame);
            Tick_BulletSpawn(nextFrame);
            Tick_BulletHitRole(nextFrame);
            Tick_BulletLife(nextFrame);
            Tick_RoleStateSync(nextFrame);
            //2
            Tick_Input();

            // Physics Simulation
            if (worldFacades.Repo.FiledEntityRepo.CurFieldEntity == null) return;
            // Tick_BulletLife();//服务器回传结束
            Tick_Physics_RoleMovement(fixedDeltaTime);
            Tick_Physics_BulletMovement();
            var physicsScene = worldFacades.Repo.FiledEntityRepo.CurPhysicsScene;
            physicsScene.Simulate(fixedDeltaTime);
        }

        void Tick_RoleSpawn(int nextFrame)
        {
            if (roleSpawnQueue.TryPeek(out var spawn))
            {
                roleSpawnQueue.Dequeue();
                worldClientFrame = nextFrame;

                Debug.Log($"生成人物帧 : {worldClientFrame}");
                var wRoleId = spawn.wRoleId;
                var repo = worldFacades.Repo;
                var fieldEntity = repo.FiledEntityRepo.Get(1);
                var domain = worldFacades.Domain.WorldRoleSpawnDomain;
                var entity = domain.SpawnWorldRole(fieldEntity.transform);
                entity.SetWRid(wRoleId);
                var roleRepo = repo.WorldRoleRepo;
                roleRepo.Add(entity);

                if (spawn.isOwner)
                {
                    roleRepo.SetOwner(entity);
                    worldFacades.CinemachineExtra.FollowSolo(entity.transform, 3f);
                    // worldFacades.CinemachineExtra.LookAtSolo(entity.CamTrackingObj, 3f);
                }

                Debug.Log(spawn.isOwner ? $"生成自身角色 : WRid:{entity.WRid}" : $"生成其他角色 : WRid:{entity.WRid}");
            }
        }

        void Tick_BulletSpawn(int nextFrame)
        {
            if (bulletSpawnQueue.TryPeek(out var bulletSpawn))
            {
                bulletSpawnQueue.Dequeue();
                worldClientFrame = nextFrame;

                var bulletId = bulletSpawn.bulletId;
                var bulletType = bulletSpawn.bulletType;
                var masterWRid = bulletSpawn.wRid;
                var masterWRole = worldFacades.Repo.WorldRoleRepo.Get(masterWRid);
                var shootStartPoint = masterWRole.ShootPointPos;
                Vector3 shootDir = new Vector3(bulletSpawn.shootDirX / 100f, bulletSpawn.shootDirY / 100f, bulletSpawn.shootDirZ / 100f);
                Debug.Log($"生成子弹帧 {worldClientFrame}: masterWRid:{masterWRid}   起点位置：{shootStartPoint} 飞行方向{shootDir}");
                var fieldEntity = worldFacades.Repo.FiledEntityRepo.Get(1);
                var bulletEntity = worldFacades.Domain.BulletDomain.SpawnBullet(fieldEntity.transform, (BulletType)bulletType);
                bulletEntity.MoveComponent.SetCurPos(shootStartPoint);
                bulletEntity.MoveComponent.AddMoveVelocity(shootDir);
                bulletEntity.SetWRid(masterWRid);
                bulletEntity.SetBulletId(bulletId);
                var bulletRepo = worldFacades.Repo.BulletEntityRepo;
                bulletRepo.Add(bulletEntity);
            }
        }

        void Tick_RoleStateSync(int nextFrame)
        {
            if (stateQueue.TryPeek(out var stateMsg))
            {
                stateQueue.Dequeue();
                worldClientFrame = stateMsg.serverFrameIndex;

                RoleState roleState = (RoleState)stateMsg.roleState;
                float x = stateMsg.x / 10000f;
                float y = stateMsg.y / 10000f;
                float z = stateMsg.z / 10000f;
                float eulerX = stateMsg.eulerX / 10000f;
                float eulerY = stateMsg.eulerY / 10000f;
                float eulerZ = stateMsg.eulerZ / 10000f;
                float moveVelocityX = stateMsg.moveVelocityX / 10000f;
                float moveVelocityY = stateMsg.moveVelocityY / 10000f;
                float moveVelocityZ = stateMsg.moveVelocityZ / 10000f;
                float extraVelocityX = stateMsg.extraVelocityX / 10000f;
                float extraVelocityY = stateMsg.extraVelocityY / 10000f;
                float extraVelocityZ = stateMsg.extraVelocityZ / 10000f;

                Vector3 pos = new Vector3(x, y, z);
                Vector3 eulerAngle = new Vector3(eulerX, eulerY, eulerZ);
                Vector3 moveVelocity = new Vector3(moveVelocityX, moveVelocityY, moveVelocityZ);
                Vector3 extraVelocity = new Vector3(extraVelocityX, extraVelocityY, extraVelocityZ);

                var entity = worldFacades.Repo.WorldRoleRepo.Get(stateMsg.wRid);
                if (entity == null)
                {
                    Debug.Log($"人物状态同步帧(entity丢失，重新生成)");

                    var wRoleId = stateMsg.wRid;
                    var repo = worldFacades.Repo;
                    var fieldEntity = repo.FiledEntityRepo.Get(1);
                    var domain = worldFacades.Domain.WorldRoleSpawnDomain;
                    entity = domain.SpawnWorldRole(fieldEntity.transform);
                    entity.SetWRid(wRoleId);

                    var roleRepo = repo.WorldRoleRepo;
                    roleRepo.Add(entity);
                    if (stateMsg.isOwner && roleRepo.Owner == null)
                    {
                        Debug.Log($"生成Owner  wRid:{entity.WRid})");
                        roleRepo.SetOwner(entity);
                        worldFacades.CinemachineExtra.FollowSolo(entity.transform, 3f);
                        worldFacades.CinemachineExtra.LookAtSolo(entity.CamTrackingObj, 3f);
                    }
                }
                var log = $"人物状态同步帧 : {worldClientFrame}  wRid:{stateMsg.wRid} 人物状态：{roleState.ToString()}  位置: {pos}  移动速度: {moveVelocity}  额外速度: {extraVelocity}  旋转角:{eulerAngle}  ";
                Debug.Log($"<color=#ff0000>{log}</color>");

                switch (roleState)
                {
                    case RoleState.Idle:
                        entity.AnimatorComponent.PlayIdle();
                        break;
                    case RoleState.Move:
                        entity.MoveComponent.SetCurPos(pos);
                        entity.MoveComponent.SetRotaionEulerAngle(eulerAngle);
                        entity.MoveComponent.SetMoveVelocity(moveVelocity);
                        entity.MoveComponent.SetExtraVelocity(extraVelocity);

                        entity.AnimatorComponent.PlayRun();
                        break;
                    case RoleState.Jump:
                        entity.MoveComponent.SetJumpVelocity();
                        entity.AnimatorComponent.PlayRun();
                        break;
                }

                entity.SetRoleStatus(roleState);
            }
        }

        void Tick_BulletHitRole(int nextFrame)
        {
            if (bulletHitRoleQueue.TryPeek(out var bulletHitRole))
            {
                bulletHitRoleQueue.Dequeue();
                worldClientFrame = nextFrame;

                var bulletRepo = worldFacades.Repo.BulletEntityRepo;
                var roleRepo = worldFacades.Repo.WorldRoleRepo;
                var bullet = bulletRepo.GetByBulletId(bulletHitRole.bulletId);
                var role = roleRepo.Get(bulletHitRole.wRid);

                role.HealthComponent.HurtByBullet(bullet);
                role.MoveComponent.HitByBullet(bullet);
                if (role.HealthComponent.IsDead)
                {
                    role.TearDown();
                    role.Reborn();
                }

                GameObject.Destroy(bullet.gameObject);
            }
        }

        void Tick_Input()
        {
            //没有角色就没有移动
            var owner = worldFacades.Repo.WorldRoleRepo.Owner;
            if (owner == null || owner.IsDead) return;

            var input = worldFacades.InputComponent;
            if (input.pressJump)
            {
                byte rid = owner.WRid;
                worldFacades.Network.WorldRoleReqAndRes.SendReq_WRoleJump(worldClientFrame, rid);
            }

            if (input.shootPoint != Vector3.zero)
            {
                // TODO: 是否满足条件
                byte rid = owner.WRid;
                worldFacades.Network.BulletReqAndRes.SendReq_BulletSpawn(worldClientFrame, BulletType.Default, rid, input.shootPoint);
            }

            if (input.grenadeThrowPoint != Vector3.zero)
            {
                // TODO: 是否满足条件
                byte rid = owner.WRid;
                worldFacades.Network.BulletReqAndRes.SendReq_BulletSpawn(worldClientFrame, BulletType.Grenade, rid, input.grenadeThrowPoint);
            }

            if (input.moveAxis != Vector3.zero)
            {
                var moveDir = input.moveAxis;
                if (!WillHitOtherRole(owner, moveDir))
                {
                    byte rid = owner.WRid;
                    worldFacades.Network.WorldRoleReqAndRes.SendReq_WRoleMove(worldClientFrame, rid, moveDir);
                }
            }
            input.Reset();
        }

        void Tick_BulletLife(int nextFrame)
        {
            while (bulletTearDownQueue.TryDequeue(out var msg))
            {
                var bulletId = msg.bulletId;
                var bulletType = msg.bulletType;
                var bulletRepo = worldFacades.Repo.BulletEntityRepo;
                var bulletEntity = bulletRepo.GetByBulletId(bulletId);

                Vector3 pos = new Vector3(msg.posX / 10000f, msg.posY / 10000f, msg.posZ / 10000f);
                bulletEntity.MoveComponent.SetCurPos(pos);

                if (bulletEntity.BulletType == BulletType.Default)
                {
                    bulletEntity.TearDown();
                }

                if (bulletEntity.BulletType == BulletType.Grenade)
                {
                    ((GrenadeEntity)bulletEntity).TearDown();
                }

                bulletRepo.TryRemove(bulletEntity);
            }
        }

        void Tick_Physics_RoleMovement(float deltaTime)
        {
            var domain = worldFacades.Domain.WorldRoleSpawnDomain;
            domain.Tick_RoleMovement(deltaTime);
        }

        void Tick_Physics_BulletMovement()
        {
            var domain = worldFacades.Domain.BulletDomain;
            domain.Tick_BulletMovement();
        }

        bool WillHitOtherRole(WorldRoleEntity roleEntity, Vector3 moveDir)
        {
            var roleRepo = worldFacades.Repo.WorldRoleRepo;
            var array = roleRepo.GetAll();
            for (int i = 0; i < array.Length; i++)
            {
                var r = array[i];
                if (r.WRid == roleEntity.WRid) continue;

                var pos1 = r.MoveComponent.CurPos;
                var pos2 = roleEntity.MoveComponent.CurPos;
                if (Vector3.Distance(pos1, pos2) < 1f)
                {
                    var betweenV = pos1 - pos2;
                    betweenV.Normalize();
                    moveDir.Normalize();
                    var cosVal = Vector3.Dot(moveDir, betweenV);
                    Debug.Log(cosVal);
                    if (cosVal > 0) return true;
                }
            }

            return false;
        }

        // == Server Response ==
        // ROLE 
        void OnWRoleSync(WRoleStateUpdateMsg msg)
        {
            stateQueue.Enqueue(msg);
        }

        void OnWorldRoleSpawn(FrameWRoleSpawnResMsg msg)
        {
            // Debug.Log("加入角色生成队列");
            roleSpawnQueue.Enqueue(msg);
        }

        // BULLET
        void OnBulletSpawn(FrameBulletSpawnResMsg msg)
        {
            Debug.Log($"加入子弹生成队列");
            bulletSpawnQueue.Enqueue(msg);
        }

        void OnBulletHitRole(FrameBulletHitRoleResMsg msg)
        {
            Debug.Log("加入子弹击中队列");
            bulletHitRoleQueue.Enqueue(msg);
        }

        void OnBulletTearDown(FrameBulletTearDownResMsg msg)
        {
            Debug.Log("加入子弹销毁队列");
            bulletTearDownQueue.Enqueue(msg);
        }

        // Network Event Center
        async void EnterWorldChooseScene()
        {
            // Load Scene And Spawn Field
            var domain = worldFacades.Domain;
            var fieldEntity = await domain.WorldSpawnDomain.SpawnWorldChooseScene();
            fieldEntity.SetFieldId(1);
            var fieldEntityRepo = worldFacades.Repo.FiledEntityRepo;
            var physicsScene = fieldEntity.gameObject.scene.GetPhysicsScene();
            fieldEntityRepo.Add(fieldEntity);
            fieldEntityRepo.SetPhysicsScene(physicsScene);
            // Send Spawn Role Message
            var rqs = worldFacades.Network.WorldRoleReqAndRes;
            rqs.SendReq_WolrdRoleSpawn(worldClientFrame);
        }

    }

}