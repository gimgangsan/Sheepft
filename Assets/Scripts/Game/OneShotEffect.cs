using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 한 번 재생하고 사라지는 이펙트. `Assets/Prefabs/Effects/`의 프리팹에 붙어 있다.
    /// Animator의 첫 상태(`play`)를 한 번 끝까지 재생하면 자신을 지운다.
    /// </summary>
    public sealed class OneShotEffect : MonoBehaviour
    {
        public Animator animator;

        [Tooltip("애니메이션이 끝나지 않아도 이 시간이 지나면 지운다(초).")]
        public float maxSeconds = 3f;

        private float age;

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (animator == null || age >= maxSeconds) { Destroy(gameObject); return; }
            if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f) Destroy(gameObject);
        }
    }
}
