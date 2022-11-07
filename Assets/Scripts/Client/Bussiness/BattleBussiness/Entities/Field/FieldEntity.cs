using UnityEngine;

namespace Game.Client.Bussiness
{

    public class FieldEntity : MonoBehaviour
    {
        byte entityId;
        public byte EntityId => entityId;
        public void SetEntityId(byte entityId) => this.entityId = entityId;

        public CinemachineComponent CameraComponent { get; private set; }

        public Transform Role_Group_Logic { get; private set; }
        public Transform Role_Group_Renderer { get; private set; }

        [SerializeField]
        Transform[] bornPoints;

        byte[] bornPointFlags;

        public void Ctor()
        {
            CameraComponent = new CinemachineComponent();
            bornPointFlags = new byte[bornPoints.Length];

            Role_Group_Logic = this.transform.Find("Role_Group_Logic");
            Role_Group_Renderer = this.transform.Find("Role_Group_Renderer");
            Debug.Assert(Role_Group_Logic != null);
            Debug.Assert(Role_Group_Renderer != null);
        }

        public void Reset()
        {
            for (int i = 0; i < bornPointFlags.Length; i++)
            {
                bornPointFlags[i] = 0;
            }
        }

        public Vector3 GetRandomBornPos()
        {
            int randomIndex = Random.Range(0, bornPoints.Length);
            int count = 0;
            while (bornPointFlags[randomIndex] != 0)
            {
                randomIndex = Random.Range(0, bornPoints.Length);
                count++;
                if (count > 10000)
                {
                    // - Temporary Way: Invoid Dead Loop
                    break;
                }
            }

            bornPointFlags[randomIndex]++;

            return bornPoints[randomIndex].position;
        }

        public PhysicsScene GetPhysicsScene()
        {
            return gameObject.scene.GetPhysicsScene();
        }

    }

}