using System.Collections.Generic;
using Game.Client.Bussiness.BattleBussiness.Generic;
using UnityEngine;

namespace Game.Client.Bussiness.BattleBussiness
{

    public class BulletEntity : PhysicsEntity
    {
        // ID Info
        IDComponent idComponent;
        public IDComponent IDComponent => idComponent;

        // Master Info
        int masterEntityID;
        public int MasterEntityID => masterEntityID;
        public void SetMasterEntityId(int v) => this.masterEntityID = v;

        // Bullet Info
        [SerializeField]
        BulletType bulletType = BulletType.DefaultBullet;
        public BulletType BulletType => bulletType;
        public void SetBulletType(BulletType bulletType) => this.bulletType = bulletType;

        [SerializeField]
        protected LocomotionComponent locomotionComponent;
        public LocomotionComponent LocomotionComponent => locomotionComponent;

        [SerializeField]
        protected HitPowerModel hitPowerModel;
        public HitPowerModel HitPowerModel => hitPowerModel;

        // Life 
        [SerializeField]
        float lifeTime;
        public float LifeTime => lifeTime;
        public void SetLifeTime(float lifeTime) => this.lifeTime = lifeTime;
        public void ReduceLifeTime(float time) => this.lifeTime -= time;

        // Damage Coefficient
        float damageCoefficient;

        float existTime;
        public float ExistTime => existTime;
        public void AddExistTime(float time) => existTime += time;

        public void Ctor()
        {
            damageCoefficient = 1f;

            idComponent = new IDComponent();
            idComponent.SetEntityType(EntityType.Bullet);

            locomotionComponent.Inject(transform.GetComponent<Rigidbody>());
            Init();
        }

        protected virtual void Init() { }

        public virtual void TearDown()
        {
            Debug.Log($"摧毁子弹  {bulletType.ToString()} {idComponent.EntityID}");
            Destroy(gameObject);
        }

        public void SetPosition(Vector3 pos)
        {
            locomotionComponent.SetPosition(pos);
            transform.position = pos;
        }

        public void FaceTo(Vector3 forward)
        {
            locomotionComponent.FaceTo(forward);
            transform.rotation = locomotionComponent.RB.rotation;
        }

        public float GetDamageByCoefficient(float coefficient)
        {
            var realDamage = hitPowerModel.damage * coefficient;
            Debug.Log($"GetDamageByCoefficient {realDamage}");
            return realDamage;
        }

    }

}