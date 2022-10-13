using UnityEngine;

namespace Game.Client.Bussiness.BattleBussiness
{

    public class AnimatorComponent
    {

        Animator animator;

        public AnimatorComponent(Animator animator)
        {
            this.animator = animator;
        }

        public void PlayAnimClip(string animClipName)
        {
            animator.Play(animClipName);
        }

        public void PlayIdle()
        {
            if (IsInState("Idle")) return;
            animator.Play("Idle");
        }

        public void PlayRunning()
        {
            if (IsInState("Running")) return;
            animator.Play("Running");
        }

        public void PlayReloading()
        {
            animator.Play("Reloading", 0, 0);
        }

        public void PlayShooting()
        {
            animator.Play("Shooting", 0, 0);
        }

        public void PlayDead()
        {
            animator.Play("Dead", 0, 0);
        }

        public void PlayRollForward()
        {
            animator.CrossFade("RollForward", 0.1f);
        }

        ////////////////////////////////////////
        public void PlayIdleWithGun()
        {
            if (IsInState("Idle_With_Gun")) return;
            animator.Play("Idle_With_Gun");
        }

        public void PlayRunWithGun()
        {
            if (IsInState("Run_WithGun")) return;
            animator.Play("Run_WithGun");
        }

        public void PlayJump()
        {
            animator.CrossFade("Jump", 0.1f);
        }

        public void PlayHooking()
        {
            if (IsInState("Hooking")) return;
            animator.Play("Hooking");
        }

        public bool IsInState(string stateName)
        {
            var curStateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return curStateInfo.IsName(stateName);
        }

        public int GetCurrentClipCurrentFrameIndex()
        {
            var clip = GetCurrentClip();
            var frameRate = clip.frameRate;
            int totalFrame = (int)(clip.length * frameRate);

            var playedTime = GetCurrentClipAlreadyPlayedTime();
            int playedFrame = (int)(playedTime / clip.length) * totalFrame;

            Debug.Log($"frameRate {frameRate} totalFrame {totalFrame} playedFrame:{playedFrame}");
            return playedFrame;
        }

        public AnimationClip GetCurrentClip()
        {
            return animator.GetCurrentAnimatorClipInfo(0)[0].clip;
        }

        public float GetCurrentClipAlreadyPlayedTime()
        {
            return animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        }

    }

}