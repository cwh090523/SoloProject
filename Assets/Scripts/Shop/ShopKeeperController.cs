using UnityEngine;
using UnityEngine.AI;

namespace Shop
{
    [RequireComponent(typeof(NavMeshAgent))]
    public class ShopKeeperController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [SerializeField] private GameStateManager stateManager;
        [SerializeField] private Collider shopArea;

        [Header("Look")]
        [SerializeField] private bool lookAtPlayerDuringRestock = true;
        [SerializeField] private float lookRange = 7f;
        [SerializeField] private float rotationSpeed = 360f;

        [Header("Animation")]
        [SerializeField] private string idleStateName = "IDLE";
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string isMovingParameter = "IsMoving";
        [SerializeField] private float crossFadeDuration = 0.08f;

        private string _currentState;

        private void Awake()
        {
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (stateManager == null)
                stateManager = FindFirstObjectByType<GameStateManager>();

            ResolvePlayer();
            StopMoving();
        }

        private void Update()
        {
            ResolvePlayer();
            StopMoving();
            UpdateAnimation(false);

            if (!CanLookAtPlayer())
                return;

            float distanceToPlayer = Vector3.Distance(transform.position, player.position);
            if (distanceToPlayer <= lookRange)
                FacePlayer();
        }

        private bool CanLookAtPlayer()
        {
            if (!lookAtPlayerDuringRestock || player == null)
                return false;

            return stateManager == null || stateManager.CurrentState == GameState.Restock;
        }

        private void StopMoving()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }

            UpdateAnimation(false);
        }

        private void FacePlayer()
        {
            Vector3 direction = player.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(direction),
                rotationSpeed * Time.deltaTime);
        }

        private void ResolvePlayer()
        {
            if (player != null)
                return;

            PlayerController controller = FindFirstObjectByType<PlayerController>();
            if (controller != null)
                player = controller.transform;
        }

        private void UpdateAnimation(bool isMoving)
        {
            if (animator == null)
                return;

            if (!string.IsNullOrWhiteSpace(speedParameter))
                animator.SetFloat(speedParameter, isMoving ? agent.velocity.magnitude : 0f);

            if (!string.IsNullOrWhiteSpace(isMovingParameter))
                animator.SetBool(isMovingParameter, isMoving);

            CrossFade(idleStateName);
        }

        private void CrossFade(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName) || _currentState == stateName)
                return;

            int stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, stateHash))
                return;

            animator.CrossFadeInFixedTime(stateHash, crossFadeDuration);
            _currentState = stateName;
        }
    }
}
