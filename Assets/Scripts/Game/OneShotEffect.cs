using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>한 번 재생하고 사라지는 이펙트. 애니메이션이 끝나면 자신을 지운다.</summary>
    public sealed class OneShotEffect : MonoBehaviour
    {
        public SpriteAnimator Animator;

        private void LateUpdate()
        {
            if (Animator == null || Animator.Finished) Destroy(gameObject);
        }
    }
}
