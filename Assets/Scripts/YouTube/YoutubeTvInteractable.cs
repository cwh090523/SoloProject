using ScriptableObjectScripts;
using UnityEngine;
using PlayerInput = ScriptableObjectScripts.PlayerInput;

namespace YouTube
{
    [RequireComponent(typeof(Collider))]
    public class YoutubeTvInteractable : MonoBehaviour
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private YoutubeTvPlayer tvPlayer;
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private Transform ownerRoot;
        [SerializeField] private float interactDistance = 4f;
        [SerializeField] private LayerMask interactLayers = ~0;
        [SerializeField] private bool logDebugState;

        private void Awake()
        {
            Collider trigger = GetComponent<Collider>();
            trigger.isTrigger = true;

            if (ownerRoot == null)
                ownerRoot = transform;

            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (playerInput != null)
                playerInput.OnInteractKeyPressed += HandleInteract;
        }

        private void OnDisable()
        {
            if (playerInput != null)
                playerInput.OnInteractKeyPressed -= HandleInteract;
        }

        private void HandleInteract()
        {
            ResolveReferences();

            if (!IsLookingAtTv())
            {
                LogBlocked("player is not looking at TV");
                return;
            }

            if (tvPlayer == null)
            {
                LogBlocked("YoutubeTvPlayer is missing");
                return;
            }

            tvPlayer.TogglePlay();
        }

        private void ResolveReferences()
        {
            if (tvPlayer == null)
                tvPlayer = GetComponentInParent<YoutubeTvPlayer>();

            if (interactionCamera == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                    interactionCamera = player.GetComponentInChildren<Camera>();
            }

            if (interactionCamera == null)
                interactionCamera = Camera.main;

            if (playerInput == null)
            {
                PlayerInput[] inputs = Resources.FindObjectsOfTypeAll<PlayerInput>();
                playerInput = inputs.Length > 0 ? inputs[0] : null;
            }
        }

        private bool IsLookingAtTv()
        {
            if (interactionCamera == null)
                return false;

            Ray ray = new Ray(interactionCamera.transform.position, interactionCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayers, QueryTriggerInteraction.Collide))
                return false;

            Transform hitTransform = hit.collider.transform;
            return hitTransform == ownerRoot || hitTransform.IsChildOf(ownerRoot);
        }

        private void LogBlocked(string reason)
        {
            if (logDebugState)
                Debug.Log($"{name} YouTube TV interaction blocked: {reason}", this);
        }
    }
}
