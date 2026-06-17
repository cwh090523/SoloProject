using UnityEngine;

public class ShopAreaGate : MonoBehaviour
{
    [SerializeField] private GameStateManager stateManager;
    [SerializeField] private GameObject[] blockers;
    [SerializeField] private bool openDuringRestockOnly = true;
    [SerializeField] private bool alwaysOpen;
    [SerializeField] private bool closeShopWhenRestockEnds = true;
    [SerializeField] private bool ejectPlayerWhenRestockEnds = true;
    [SerializeField] private Collider shopArea;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform exitPoint;

    private bool _wasRestock;

    private void Awake()
    {
        if (stateManager == null)
            stateManager = FindFirstObjectByType<GameStateManager>();

        ResolvePlayerRoot();
        _wasRestock = stateManager != null && stateManager.CurrentState == GameState.Restock;
        RefreshGate();
    }

    private void OnEnable()
    {
        if (stateManager != null)
            stateManager.StateChanged += HandleStateChanged;

        RefreshGate();
    }

    private void OnDisable()
    {
        if (stateManager != null)
            stateManager.StateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState state)
    {
        bool isRestock = state == GameState.Restock;
        if (_wasRestock && !isRestock)
            HandleRestockEnded();

        _wasRestock = isRestock;
        RefreshGate();
    }

    private void RefreshGate()
    {
        bool shouldOpen = alwaysOpen || stateManager != null && (!openDuringRestockOnly || stateManager.CurrentState == GameState.Restock);
        bool shouldBlock = !shouldOpen;

        if (blockers == null || blockers.Length == 0)
        {
            gameObject.SetActive(shouldBlock);
            return;
        }

        for (int i = 0; i < blockers.Length; i++)
        {
            if (blockers[i] != null)
                blockers[i].SetActive(shouldBlock);
        }
    }

    private void HandleRestockEnded()
    {
        if (closeShopWhenRestockEnds)
        {
            Shop.ShopPresenter shopPresenter = FindFirstObjectByType<Shop.ShopPresenter>();
            if (shopPresenter != null && shopPresenter.IsOpen)
                shopPresenter.Hide();
        }

        if (ejectPlayerWhenRestockEnds)
            EjectPlayerFromShop();
    }

    private void EjectPlayerFromShop()
    {
        ResolvePlayerRoot();

        if (playerRoot == null || exitPoint == null)
            return;

        if (!IsPlayerInsideShopArea())
            return;

        Rigidbody playerRigidbody = playerRoot.GetComponent<Rigidbody>();
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.position = exitPoint.position;
            playerRigidbody.rotation = exitPoint.rotation;
            return;
        }

        playerRoot.SetPositionAndRotation(exitPoint.position, exitPoint.rotation);
    }

    private bool IsPlayerInsideShopArea()
    {
        if (shopArea == null)
            return true;

        Vector3 playerPosition = playerRoot.position;
        Vector3 closestPoint = shopArea.ClosestPoint(playerPosition);
        return (closestPoint - playerPosition).sqrMagnitude <= 0.0001f;
    }

    private void ResolvePlayerRoot()
    {
        if (playerRoot != null)
            return;

        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            playerRoot = player.transform;
    }
}
