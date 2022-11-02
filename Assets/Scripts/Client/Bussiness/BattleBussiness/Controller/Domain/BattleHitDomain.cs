using UnityEngine;
using Game.Client.Bussiness.BattleBussiness.Facades;
using Game.Client.Bussiness.BattleBussiness.Generic;

namespace Game.Client.Bussiness.BattleBussiness.Controller.Domain
{

    public class BattleHitDomain
    {
        BattleFacades battleFacades;

        public BattleHitDomain()
        {
        }

        public void Inject(BattleFacades battleFacades)
        {
            this.battleFacades = battleFacades;
        }

        public bool TryHitActor(IDComponent attackerIDC, IDComponent victimIDC, in HitPowerModel hitPowerModel)
        {
            var arbitService = battleFacades.ArbitrationService;
            if (!arbitService.IsHitSuccess(attackerIDC, victimIDC, hitPowerModel))
            {
                Debug.Log($"打击失败!");
                return false;
            }

            // - Hit Apply
            ApplyBulletHitRole(attackerIDC, victimIDC, hitPowerModel);

            return true;
        }

        void ApplyBulletHitRole(IDComponent attackerIDC, IDComponent victimIDC, in HitPowerModel hitPowerModel)
        {
            if (attackerIDC.EntityType != EntityType.Bullet
            || victimIDC.EntityType != EntityType.BattleRole)
            {
                return;
            }

            var arbitService = battleFacades.ArbitrationService;
            var bullet = battleFacades.Repo.BulletRepo.Get(attackerIDC.EntityID);
            var role = battleFacades.Repo.RoleLogicRepo.Get(victimIDC.EntityID);

            if (bullet.BulletType == BulletType.DefaultBullet)
            {
                // - Damage
                var domain = battleFacades.Domain.RoleDomain;
                float damage = domain.TryReceiveDamage(role, hitPowerModel.damage);
                arbitService.AddHitRecord(attackerIDC, victimIDC, damage);
                // - Physics
                var addV = bullet.LocomotionComponent.Velocity * hitPowerModel.hitVelocityCoefficient;
                role.LocomotionComponent.AddExtraVelocity(addV);
            }

            // - State
            role.StateComponent.EnterBeHit(hitPowerModel.freezeMaintainFrame);
        }

    }

}