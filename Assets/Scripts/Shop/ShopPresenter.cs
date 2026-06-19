using System;
using System.Collections.Generic;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Shop
{
    public class ShopPresenter : MonoBehaviour
    {
        private const string ShopUiResourcePath = "UI/ShopUI";
        private const string DefaultItemListResourcePath = "Shop/DefaultShopItemList";
        private const string DefaultPurchaseSoundResourcePath = "Audio/BuySound";

        [SerializeField] private UIDocument document;
        [SerializeField] private GameStateManager stateManager;
        [SerializeField] private Health playerHealth;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCamera playerCamera;
        [SerializeField] private PlayerStamina playerStamina;
        [SerializeField] private PlayerWeapon playerWeapon;
        [SerializeField] private AimTargetScanner aimTargetScanner;
        [SerializeField] private PlayerDebugHealOnKey debugHealOnKey;
        [SerializeField] private Rigidbody playerRigidbody;
        [SerializeField] private PlayerWallet wallet;

        [Header("Stock")]
        [SerializeField] private ShopItemListSO itemList;
        [SerializeField, Min(1)] private int visibleSlotCount = 4;
        [SerializeField] private bool avoidDuplicateVisibleItems = true;
        [SerializeField] private float slotEnterDelay = 0.06f;
        [SerializeField] private float slotAnimationDuration = 0.22f;

        [Header("Audio")]
        [SerializeField] private AudioSource purchaseAudioSource;
        [SerializeField] private AudioClip purchaseSound;
        [SerializeField, Range(0f, 1f)] private float purchaseSoundVolume = 0.85f;

        private readonly List<ShopItemDefinition> _stock = new List<ShopItemDefinition>();
        private readonly List<ShopSlotView> _slotViews = new List<ShopSlotView>();
        private readonly HashSet<string> _purchasedUniqueIds = new HashSet<string>();

        private VisualElement _root;
        private VisualElement _slotsContainer;
        private Label _moneyLabel;
        private Label _messageLabel;
        private Button _closeButton;
        private bool _inputBlockedByShop;
        private bool _wasPlayerControllerEnabled;
        private bool _wasPlayerCameraEnabled;
        private bool _wasPlayerWeaponEnabled;
        private bool _wasAimTargetScannerEnabled;
        private bool _wasDebugHealOnKeyEnabled;
        private bool _wasCursorVisible;
        private CursorLockMode _previousCursorLockState;
        private bool _isBuying;

        public bool IsOpen => _root != null && _root.style.display == DisplayStyle.Flex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            CreateForCurrentScene();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CreateForCurrentScene();
        }

        private static void CreateForCurrentScene()
        {
            EnsureForCurrentScene();
        }

        public static ShopPresenter EnsureForCurrentScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.isLoaded || activeScene.name.ToUpperInvariant().Contains("TITLE"))
                return null;

            ShopPresenter existingPresenter = FindFirstObjectByType<ShopPresenter>();
            if (existingPresenter != null)
                return existingPresenter;

            GameStateManager manager = FindFirstObjectByType<GameStateManager>();
            if (manager == null)
                return null;

            VisualTreeAsset visualTree = Resources.Load<VisualTreeAsset>(ShopUiResourcePath);
            if (visualTree == null)
            {
                Debug.LogWarning($"Shop UI resource not found: Resources/{ShopUiResourcePath}.uxml");
                return null;
            }

            UIDocument existingDocument = FindFirstObjectByType<UIDocument>();

            GameObject shopObject = new GameObject("Shop UI");
            UIDocument uiDocument = shopObject.AddComponent<UIDocument>();
            uiDocument.visualTreeAsset = visualTree;

            if (existingDocument != null)
                uiDocument.panelSettings = existingDocument.panelSettings;

            ShopPresenter presenter = shopObject.AddComponent<ShopPresenter>();
            presenter.document = uiDocument;
            presenter.stateManager = manager;
            presenter.itemList = Resources.Load<ShopItemListSO>(DefaultItemListResourcePath);

            return presenter;
        }

        private void Awake()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            ResolveReferences();
            ResolvePurchaseAudio();
            ResolveItemList();
            BindElements();
            BuildSlotViews();
            RollInitialStock();
            Hide();
            Refresh();
        }

        private void OnEnable()
        {
            if (wallet != null)
                wallet.MoneyChanged += HandleMoneyChanged;

            if (_closeButton != null)
                _closeButton.clicked += Hide;
        }

        private void OnDisable()
        {
            RestorePlayerInputAfterShop();

            if (wallet != null)
                wallet.MoneyChanged -= HandleMoneyChanged;

            if (_closeButton != null)
                _closeButton.clicked -= Hide;
        }

        public void Open()
        {
            Open(false);
        }

        public void Open(bool ignoreStateRequirement)
        {
            if (!ignoreStateRequirement && stateManager != null && stateManager.CurrentState != GameState.Restock)
                return;

            ResolveReferences();
            ResolvePurchaseAudio();
            EnsureStock();
            Refresh();

            if (_root != null)
            {
                _root.style.display = DisplayStyle.Flex;
                PanelOpenEffect.Play(_root);
                PlayAllSlotEnterAnimations();
            }

            BlockPlayerInputForShop();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Hide()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;

            RestorePlayerInputAfterShop();

            if (stateManager != null && stateManager.CurrentState == GameState.Restock)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void ResolveReferences()
        {
            if (stateManager == null)
                stateManager = FindFirstObjectByType<GameStateManager>();

            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player == null)
                return;

            if (playerHealth == null)
                playerHealth = player.GetComponent<Health>();

            if (playerController == null)
                playerController = player.GetComponent<PlayerController>();

            if (playerCamera == null)
                playerCamera = player.GetComponent<PlayerCamera>();

            if (playerStamina == null)
                playerStamina = player.GetComponent<PlayerStamina>();

            if (playerWeapon == null)
                playerWeapon = player.GetComponent<PlayerWeapon>();

            if (aimTargetScanner == null)
                aimTargetScanner = player.GetComponent<AimTargetScanner>();

            if (debugHealOnKey == null)
                debugHealOnKey = player.GetComponent<PlayerDebugHealOnKey>();

            if (playerRigidbody == null)
                playerRigidbody = player.GetComponent<Rigidbody>();

            if (wallet == null)
                wallet = player.GetComponent<PlayerWallet>();

            if (wallet == null)
                wallet = player.gameObject.AddComponent<PlayerWallet>();
        }

        private void ResolveItemList()
        {
            if (itemList == null)
                itemList = Resources.Load<ShopItemListSO>(DefaultItemListResourcePath);
        }

        private void BindElements()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            VisualElement root = document.rootVisualElement;
            _root = root.Q<VisualElement>("ShopRoot");
            _slotsContainer = root.Q<VisualElement>("ShopSlotsContainer");
            _moneyLabel = root.Q<Label>("MoneyLabel");
            _messageLabel = root.Q<Label>("MessageLabel");
            _closeButton = root.Q<Button>("CloseShopButton");
        }

        private void BuildSlotViews()
        {
            if (_slotsContainer == null)
                return;

            _slotsContainer.Clear();
            _slotViews.Clear();

            for (int i = 0; i < visibleSlotCount; i++)
            {
                int slotIndex = i;
                ShopSlotView slotView = CreateSlotView();
                slotView.Button.clicked += () => BuySlot(slotIndex);
                _slotsContainer.Add(slotView.Button);
                _slotViews.Add(slotView);
            }
        }

        private ShopSlotView CreateSlotView()
        {
            Button button = new Button();
            button.text = string.Empty;
            button.style.width = Length.Percent(24f);
            button.style.minHeight = 320f;
            button.style.marginLeft = 4f;
            button.style.marginRight = 4f;
            button.style.paddingLeft = 18f;
            button.style.paddingRight = 18f;
            button.style.paddingTop = 18f;
            button.style.paddingBottom = 16f;
            button.style.backgroundColor = new Color(0.17f, 0.13f, 0.09f, 0.96f);
            button.style.borderLeftWidth = 2f;
            button.style.borderRightWidth = 2f;
            button.style.borderTopWidth = 2f;
            button.style.borderBottomWidth = 2f;
            button.style.borderLeftColor = new Color(0.75f, 0.52f, 0.28f, 0.82f);
            button.style.borderRightColor = new Color(0.08f, 0.05f, 0.03f, 0.8f);
            button.style.borderTopColor = new Color(0.9f, 0.72f, 0.44f, 0.72f);
            button.style.borderBottomColor = new Color(0f, 0f, 0f, 0.85f);
            button.style.borderTopLeftRadius = 2f;
            button.style.borderTopRightRadius = 5f;
            button.style.borderBottomLeftRadius = 5f;
            button.style.borderBottomRightRadius = 2f;

            Label title = new Label();
            title.style.height = 58f;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.color = new Color(0.98f, 0.92f, 0.8f, 1f);
            title.style.fontSize = 24f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            Label effect = new Label();
            effect.style.marginTop = 6f;
            effect.style.color = new Color(0.9f, 0.62f, 0.26f, 1f);
            effect.style.fontSize = 17f;
            effect.style.unityFontStyleAndWeight = FontStyle.Bold;
            effect.style.whiteSpace = WhiteSpace.Normal;

            Label description = new Label();
            description.style.marginTop = 18f;
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.color = new Color(0.92f, 0.84f, 0.7f, 0.76f);
            description.style.fontSize = 15f;

            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1f;

            Label price = new Label();
            price.style.height = 48f;
            price.style.unityTextAlign = TextAnchor.MiddleCenter;
            price.style.color = new Color(0.98f, 0.92f, 0.8f, 1f);
            price.style.fontSize = 19f;
            price.style.unityFontStyleAndWeight = FontStyle.Bold;
            price.style.backgroundColor = new Color(0.45f, 0.16f, 0.12f, 1f);
            price.style.borderTopLeftRadius = 2f;
            price.style.borderTopRightRadius = 2f;
            price.style.borderBottomLeftRadius = 2f;
            price.style.borderBottomRightRadius = 2f;

            button.Add(title);
            button.Add(effect);
            button.Add(description);
            button.Add(spacer);
            button.Add(price);

            return new ShopSlotView(button, title, effect, description, price);
        }

        private void RollInitialStock()
        {
            _stock.Clear();

            for (int i = 0; i < visibleSlotCount; i++)
                _stock.Add(PickRandomItem(i));
        }

        private void EnsureStock()
        {
            if (_stock.Count == visibleSlotCount)
                return;

            RollInitialStock();
        }

        private ShopItemDefinition PickRandomItem(int replacingIndex)
        {
            IReadOnlyList<ShopItemDefinition> sourceItems = itemList != null ? itemList.Items : null;
            if (sourceItems == null || sourceItems.Count == 0)
                return null;

            List<ShopItemDefinition> candidates = new List<ShopItemDefinition>();
            for (int i = 0; i < sourceItems.Count; i++)
            {
                ShopItemDefinition item = sourceItems[i];
                if (item == null || IsUniqueItemPurchased(item))
                    continue;

                candidates.Add(item);
            }

            if (avoidDuplicateVisibleItems && candidates.Count > 1)
            {
                for (int i = candidates.Count - 1; i >= 0; i--)
                {
                    ShopItemDefinition candidate = candidates[i];
                    if (IsAlreadyVisible(candidate, replacingIndex) && candidates.Count > 1)
                        candidates.RemoveAt(i);
                }
            }

            if (candidates.Count == 0)
                return null;

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private bool IsAlreadyVisible(ShopItemDefinition item, int replacingIndex)
        {
            for (int i = 0; i < _stock.Count; i++)
            {
                if (i == replacingIndex)
                    continue;

                if (_stock[i] == item)
                    return true;
            }

            return false;
        }

        private bool IsUniqueItemPurchased(ShopItemDefinition item)
        {
            return item.UniquePurchase && _purchasedUniqueIds.Contains(item.UniqueId);
        }

        private void BuySlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _stock.Count)
                return;

            ShopItemDefinition item = _stock[slotIndex];
            if (item == null)
            {
                SetMessage("No item is available in this slot.");
                return;
            }

            if (!CanApplyItem(item, out string reason))
            {
                SetMessage(reason);
                return;
            }

            if (wallet == null)
            {
                SetMessage("Wallet is missing.");
                return;
            }

            if (_isBuying)
                return;

            if (!wallet.TrySpend(item.Price))
            {
                SetMessage("Not enough money.");
                return;
            }

            _isBuying = true;
            PlayPurchaseSound();
            ApplyItem(item);

            if (item.UniquePurchase)
                _purchasedUniqueIds.Add(item.UniqueId);

            SetMessage($"{item.DisplayName} purchased.");
            RefreshMoney();

            ShopSlotView slotView = _slotViews[slotIndex];
            slotView.PlayReplaceAnimation(slotAnimationDuration, () =>
            {
                _stock[slotIndex] = PickRandomItem(slotIndex);
                RefreshSlot(slotIndex);
            }, () =>
            {
                _isBuying = false;
            });
        }

        private bool CanApplyItem(ShopItemDefinition item, out string reason)
        {
            reason = string.Empty;

            switch (item.ItemType)
            {
                case ShopItemType.MaxHealth:
                case ShopItemType.HealthRecovery:
                    if (playerHealth != null)
                        return true;

                    reason = "Player health is missing.";
                    return false;

                case ShopItemType.MaxStamina:
                case ShopItemType.StaminaRecovery:
                    if (playerStamina != null)
                        return true;

                    reason = "Player stamina is missing.";
                    return false;

                case ShopItemType.Damage:
                case ShopItemType.Ammo:
                    if (playerWeapon != null)
                        return true;

                    reason = "Player weapon is missing.";
                    return false;

                case ShopItemType.Weapon:
                    reason = "Weapon purchase needs a weapon inventory system first.";
                    return false;

                default:
                    reason = "Unknown shop item type.";
                    return false;
            }
        }

        private void ApplyItem(ShopItemDefinition item)
        {
            switch (item.ItemType)
            {
                case ShopItemType.MaxHealth:
                    playerHealth.IncreaseMaxHealth(item.Amount, true);
                    break;

                case ShopItemType.HealthRecovery:
                    playerHealth.Heal(item.Amount);
                    break;

                case ShopItemType.MaxStamina:
                    playerStamina.IncreaseMaxStamina(item.Amount, true);
                    break;

                case ShopItemType.StaminaRecovery:
                    playerStamina.IncreaseRecoveryPerSecond(item.Amount);
                    break;

                case ShopItemType.Damage:
                    playerWeapon.IncreaseDamage(item.Amount);
                    break;

                case ShopItemType.Ammo:
                    playerWeapon.AddReserveAmmo(Mathf.RoundToInt(item.Amount));
                    break;
            }
        }

        private void HandleMoneyChanged(int money)
        {
            RefreshMoney();
        }

        private void Refresh()
        {
            RefreshMoney();

            if (itemList == null)
                SetMessage($"Shop item list not found: Resources/{DefaultItemListResourcePath}.asset");

            for (int i = 0; i < _slotViews.Count; i++)
                RefreshSlot(i);
        }

        private void RefreshMoney()
        {
            if (_moneyLabel != null)
                _moneyLabel.text = $"$ {wallet?.Money ?? 0}";
        }

        private void RefreshSlot(int slotIndex)
        {
            ShopSlotView slotView = _slotViews[slotIndex];
            ShopItemDefinition item = slotIndex < _stock.Count ? _stock[slotIndex] : null;

            if (item == null)
            {
                slotView.SetEmpty();
                return;
            }

            slotView.SetItem(item, GetEffectText(item));
        }

        private string GetEffectText(ShopItemDefinition item)
        {
            switch (item.ItemType)
            {
                case ShopItemType.MaxHealth:
                    return $"+{item.Amount:0} MAX HP";

                case ShopItemType.HealthRecovery:
                    return $"+{item.Amount:0} HP";

                case ShopItemType.MaxStamina:
                    return $"+{item.Amount:0} MAX STAMINA";

                case ShopItemType.StaminaRecovery:
                    return $"+{item.Amount:0}/s STAMINA REGEN";

                case ShopItemType.Damage:
                    return $"+{item.Amount:0} DAMAGE";

                case ShopItemType.Ammo:
                    return $"+{Mathf.RoundToInt(item.Amount)} AMMO";

                case ShopItemType.Weapon:
                    return "UNIQUE WEAPON";

                default:
                    return string.Empty;
            }
        }

        private void SetMessage(string message)
        {
            if (_messageLabel != null)
                _messageLabel.text = message;
        }

        private void ResolvePurchaseAudio()
        {
            if (purchaseSound == null)
                purchaseSound = Resources.Load<AudioClip>(DefaultPurchaseSoundResourcePath);

            if (purchaseAudioSource == null)
                purchaseAudioSource = GetComponent<AudioSource>();

            if (purchaseAudioSource == null)
                purchaseAudioSource = gameObject.AddComponent<AudioSource>();

            purchaseAudioSource.playOnAwake = false;
            purchaseAudioSource.loop = false;
            purchaseAudioSource.spatialBlend = 0f;
        }

        private void PlayPurchaseSound()
        {
            if (purchaseAudioSource == null || purchaseSound == null)
                return;

            purchaseAudioSource.PlayOneShot(purchaseSound, purchaseSoundVolume * GameSettings.SfxVolume);
        }

        private void PlayAllSlotEnterAnimations()
        {
            for (int i = 0; i < _slotViews.Count; i++)
            {
                float delay = i * Mathf.Max(0f, slotEnterDelay);
                _slotViews[i].PlayEnterAnimation(slotAnimationDuration, delay);
            }
        }

        private void BlockPlayerInputForShop()
        {
            if (_inputBlockedByShop)
                return;

            _previousCursorLockState = Cursor.lockState;
            _wasCursorVisible = Cursor.visible;
            _wasPlayerControllerEnabled = playerController != null && playerController.enabled;
            _wasPlayerCameraEnabled = playerCamera != null && playerCamera.enabled;
            _wasPlayerWeaponEnabled = playerWeapon != null && playerWeapon.enabled;
            _wasAimTargetScannerEnabled = aimTargetScanner != null && aimTargetScanner.enabled;
            _wasDebugHealOnKeyEnabled = debugHealOnKey != null && debugHealOnKey.enabled;

            if (playerController != null)
                playerController.enabled = false;

            if (playerCamera != null)
                playerCamera.enabled = false;

            if (playerWeapon != null)
                playerWeapon.enabled = false;

            if (aimTargetScanner != null)
                aimTargetScanner.enabled = false;

            if (debugHealOnKey != null)
                debugHealOnKey.enabled = false;

            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }

            _inputBlockedByShop = true;
        }

        private void RestorePlayerInputAfterShop()
        {
            if (!_inputBlockedByShop)
                return;

            if (playerController != null)
                playerController.enabled = _wasPlayerControllerEnabled;

            if (playerCamera != null)
                playerCamera.enabled = _wasPlayerCameraEnabled;

            if (playerWeapon != null)
                playerWeapon.enabled = _wasPlayerWeaponEnabled;

            if (aimTargetScanner != null)
                aimTargetScanner.enabled = _wasAimTargetScannerEnabled;

            if (debugHealOnKey != null)
                debugHealOnKey.enabled = _wasDebugHealOnKeyEnabled;

            _inputBlockedByShop = false;

            Cursor.lockState = _previousCursorLockState;
            Cursor.visible = _wasCursorVisible;
        }

        private sealed class ShopSlotView
        {
            public ShopSlotView(Button button, Label title, Label effect, Label description, Label price)
            {
                Button = button;
                Title = title;
                Effect = effect;
                Description = description;
                Price = price;
            }

            public Button Button { get; }
            private Label Title { get; }
            private Label Effect { get; }
            private Label Description { get; }
            private Label Price { get; }

            public void PlayEnterAnimation(float duration, float delay)
            {
                PlayAnimation(duration, delay, 0f, 1f, 0.92f, 1f, null, null);
            }

            public void PlayReplaceAnimation(float duration, Action onMiddle, Action onComplete)
            {
                float halfDuration = Mathf.Max(0.01f, duration * 0.5f);
                PlayAnimation(halfDuration, 0f, 1f, 0f, 1f, 0.9f, onMiddle, () =>
                {
                    PlayAnimation(halfDuration, 0f, 0f, 1f, 0.9f, 1f, null, onComplete);
                });
            }

            public void SetItem(ShopItemDefinition item, string effectText)
            {
                Button.SetEnabled(true);
                Button.style.opacity = 1f;
                Button.style.scale = new Scale(Vector3.one);
                Title.text = item.DisplayName;
                Effect.text = effectText;
                Description.text = item.Description;
                Price.text = $"$ {item.Price} BUY";
            }

            public void SetEmpty()
            {
                Button.SetEnabled(false);
                Button.style.opacity = 1f;
                Button.style.scale = new Scale(Vector3.one);
                Title.text = "SOLD OUT";
                Effect.text = string.Empty;
                Description.text = "No available item remains in the shop list.";
                Price.text = "-";
            }

            private void PlayAnimation(
                float duration,
                float delay,
                float fromOpacity,
                float toOpacity,
                float fromScale,
                float toScale,
                Action onStart,
                Action onComplete)
            {
                float safeDuration = Mathf.Max(0.01f, duration);
                float startTime = Time.unscaledTime + Mathf.Max(0f, delay);
                bool hasStarted = false;

                Button.style.opacity = fromOpacity;
                Button.style.scale = new Scale(new Vector3(fromScale, fromScale, 1f));

                IVisualElementScheduledItem scheduledItem = null;
                scheduledItem = Button.schedule.Execute(() =>
                {
                    float elapsed = Time.unscaledTime - startTime;
                    if (elapsed < 0f)
                        return;

                    if (!hasStarted)
                    {
                        hasStarted = true;
                        onStart?.Invoke();
                    }

                    float t = Mathf.Clamp01(elapsed / safeDuration);
                    float eased = 1f - Mathf.Pow(1f - t, 3f);
                    float opacity = Mathf.Lerp(fromOpacity, toOpacity, eased);
                    float scale = Mathf.Lerp(fromScale, toScale, eased);

                    Button.style.opacity = opacity;
                    Button.style.scale = new Scale(new Vector3(scale, scale, 1f));

                    if (t < 1f)
                        return;

                    Button.style.opacity = toOpacity;
                    Button.style.scale = new Scale(new Vector3(toScale, toScale, 1f));
                    scheduledItem?.Pause();
                    onComplete?.Invoke();
                }).Every(16);
            }
        }
    }
}
