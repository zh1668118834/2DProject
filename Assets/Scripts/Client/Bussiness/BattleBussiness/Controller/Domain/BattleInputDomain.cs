using UnityEngine;
using ZeroFrame.AllMath;
using Game.Client.Bussiness.BattleBussiness.Facades;

namespace Game.Client.Bussiness.BattleBussiness.Controller.Domain
{

    public class BattleInputDomain
    {

        BattleFacades battleFacades;

        public BattleInputDomain()
        {

        }

        public void Inject(BattleFacades battleFacades)
        {
            this.battleFacades = battleFacades;
        }

        public void UpdateCameraByCameraView(Vector2 inputAxis)
        {
            var owner = battleFacades.Repo.RoleLogicRepo.Owner;
            if (owner == null) return;

            var curFieldEntity = battleFacades.Repo.FieldRepo.CurFieldEntity;
            if (curFieldEntity == null) return;

            var cameraComponent = curFieldEntity.CameraComponent;
            var currentCam = cameraComponent.CurrentCamera;
            var cameraView = cameraComponent.CurrentCameraView;

            var roleRenderer = owner.roleRenderer;
            Vector3 trackPos = roleRenderer.transform.position;

            if (cameraView == CameraView.FirstView)
            {
                trackPos += roleRenderer.transform.forward * 0.5f;
                trackPos.y -= 1.2f;

                currentCam.AddEulerAngleX(-inputAxis.y);
                currentCam.AddEulerAngleY(inputAxis.x);
                owner.LocomotionComponent.SetEulerAngleY(currentCam.EulerAngles);
            }

            roleRenderer.SetCamTrackingPos(trackPos);
        }

        public Vector3 GetMoveDirByCameraView(BattleRoleLogicEntity owner, Vector3 moveAxis, CameraView cameraView)
        {
            Vector3 moveDir = Vector3.zero;
            if (cameraView == CameraView.ThirdView)
            {
                moveDir = moveAxis;

            }
            return moveDir.normalized;
        }

        public Vector3 GetShotPointByCameraView(CameraView cameraView, BattleRoleLogicEntity roleLogicEntity)
        {
            var mainCam = Camera.main;
            if (mainCam == null) return Vector3.zero;

            switch (cameraView)
            {
                case CameraView.FirstView:
                    var ray = mainCam.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        return hit.point;
                    }

                    break;
                case CameraView.ThirdView:
                    var roleTrans = roleLogicEntity.LocomotionComponent.Position;
                    var forward = roleLogicEntity.transform.forward;
                    var pos = roleLogicEntity.transform.position + forward * 10f;
                    return pos;
            }

            return Vector3.zero;
        }

    }

}