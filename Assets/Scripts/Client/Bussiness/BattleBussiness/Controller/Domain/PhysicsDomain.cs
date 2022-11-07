using System.Collections.Generic;
using UnityEngine;
using Game.Client.Bussiness.BattleBussiness.Facades;
using Game.Client.Bussiness.BattleBussiness.Generic;

namespace Game.Client.Bussiness.BattleBussiness.Controller.Domain
{

    public class PhysicsDomain
    {
        BattleFacades battleFacades;

        public PhysicsDomain()
        {
        }

        public void Inject(BattleFacades facades)
        {
            this.battleFacades = facades;
        }

        public List<CollisionExtra> GetHitField_ColliderList(PhysicsEntity physicsEntity) => GetCollisionExtraList(physicsEntity, "Field");
        public List<CollisionExtra> GetHitItem_ColliderList(PhysicsEntity physicsEntity) => GetCollisionExtraList(physicsEntity, "Item");
        public List<CollisionExtra> GetHitRole_ColliderList(PhysicsEntity physicsEntity) => GetCollisionExtraList(physicsEntity, "Role");

        public void Tick_Physics_Collections_Role_Field()
        {
            // - Role
            var roleRepo = battleFacades.Repo.RoleLogicRepo;
            roleRepo.Foreach((role) =>
            {
                PhysicsEntityHitField(role, role.LocomotionComponent);
            });
        }

        public List<HitFieldModel> Tick_Physics_Collections_Bullet_Field()
        {
            List<HitFieldModel> list = new List<HitFieldModel>();
            Transform hitTrans = null;
            var bulletRepo = battleFacades.Repo.BulletRepo;
            var bulletDomain = battleFacades.Domain.BulletLogicDomain;

            bulletRepo.Foreach((bullet) =>
            {
                bool hashit = false;
                var hitFieldList = GetHitField_ColliderList(bullet);
                hitFieldList.ForEach((ce) =>
                {
                    if (ce.status != CollisionStatus.Enter)
                    {
                        return;
                    }
                    ce.status = CollisionStatus.Stay;

                    HitFieldModel hitFieldModel = new HitFieldModel();
                    hitFieldModel.hitter = bullet.IDComponent;
                    hitFieldModel.fieldCE = ce;
                    list.Add(hitFieldModel);

                    hitTrans = ce.GetCollider().transform;
                    hashit = true;
                });

                if (hashit)
                {
                    bulletDomain.ApplyBulletHitEffector(bullet, hitTrans);
                }
            });

            return list;
        }

        void PhysicsEntityHitField(PhysicsEntity entity, LocomotionComponent moveComponent)
        {
            var entityPos = entity.Position;

            // 墙体撞击的速度管理
            var fieldColliderList = GetHitField_ColliderList(entity);
            int enterGroundCount = 0;
            int hitWallCount = 0;
            fieldColliderList.ForEach((collisionExtra) =>
            {
                var go = collisionExtra.gameObject;
                var hitDir = collisionExtra.hitDir;
                if (collisionExtra.status != CollisionStatus.Exit)
                {
                    if (collisionExtra.fieldType == FieldType.Ground) enterGroundCount++;
                    else if (collisionExtra.fieldType == FieldType.Wall) hitWallCount++;
                }

                if (collisionExtra.status == CollisionStatus.Enter)
                {
                    collisionExtra.status = CollisionStatus.Stay;
                    if (go.tag == "Jumpboard")
                    {
                        moveComponent.JumpboardSpeedUp();
                    }
                }
                else if (collisionExtra.status == CollisionStatus.Stay)
                {

                }
                else if (collisionExtra.status == CollisionStatus.Exit)
                {
                    var leaveDir = -hitDir;
                    if (collisionExtra.fieldType == FieldType.Wall) hitWallCount--;
                    else if (collisionExtra.fieldType == FieldType.Ground) enterGroundCount--;
                }

            });

            // 撞击状态管理
            if (enterGroundCount <= 0)
            {
                moveComponent.LeaveGround();
            }
            else
            {
                moveComponent.EnterGound();
            }

            if (hitWallCount <= 0)
            {
                moveComponent.LeaveWall();
            }
            else
            {
                moveComponent.EnterWall();
            }
        }

        List<CollisionExtra> GetCollisionExtraList(PhysicsEntity physicsEntity, string layerName)
        {
            List<CollisionExtra> collisionList = new List<CollisionExtra>();
            List<CollisionExtra> removeList = new List<CollisionExtra>();
            physicsEntity.HitCollisionExtraListForeach((collisionExtra) =>
            {

                if (collisionExtra.status == CollisionStatus.Exit)
                {
                    removeList.Add(collisionExtra);
                }

                Collider collider = collisionExtra.GetCollider();
                if (collider == null || collider.enabled == false)
                {
                    // 目标被摧毁,等价于Exit
                    collisionExtra.status = CollisionStatus.Exit;
                    removeList.Add(collisionExtra);
                }

                if (collisionExtra.layerName == layerName)
                {
                    collisionList.Add(collisionExtra);   //本帧依然添加进List
                }
            });

            removeList.ForEach((ce) =>
            {
                physicsEntity.RemoveHitCollisionExtra(ce);
            });

            return collisionList;
        }

    }

}