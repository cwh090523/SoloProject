using ScriptableObjectScripts;
using Shop;
using UnityEngine;
using PlayerInput = ScriptableObjectScripts.PlayerInput;

[RequireComponent(typeof(Collider))]
public class ShopInteractable : MonoBehaviour
{
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private GameStateManager stateManager;
    [SerializeField] private ShopPresenter shopPresenter;
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

        if (!IsLookingAtOwner())
        {
            LogBlocked("player is not looking at shop owner");
            return;
        }

        if (stateManager == null)
        {
            LogBlocked("GameStateManager is missing");
            return;
        }

        if (stateManager.CurrentState != GameState.Restock)
        {
            LogBlocked($"current state is {stateManager.CurrentState}");
            return;
        }

        if (shopPresenter == null)
        {
            LogBlocked("ShopPresenter is missing");
            return;
        }

        shopPresenter.Open();
    }

    private void ResolveReferences()
    {
        if (stateManager == null)
            stateManager = FindFirstObjectByType<GameStateManager>();

        if (shopPresenter == null)
            shopPresenter = ShopPresenter.EnsureForCurrentScene();

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

    private bool IsLookingAtOwner()
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
            Debug.Log($"{name} shop interaction blocked: {reason}", this);
    }
}
