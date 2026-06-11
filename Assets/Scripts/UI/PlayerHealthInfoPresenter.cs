using UI.ViewModel;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerHealthInfoPresenter : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private Health playerHealth;
    [SerializeField] private PlayerStamina playerStamina;
    [SerializeField] private PlayerInfohModelSO playerInfohModel;
    [SerializeField] private string playerObjectName = "Player3";

    private VisualElement _container;
    private VisualElement _healthBarFill;
    private VisualElement _staminaBarFill;
    private Label _currentHealthLabel;
    private Label _maxHealthLabel;
    private Label _currentStaminaLabel;
    private Label _maxStaminaLabel;
    private Label _stateLabel;

    private void Awake()
    {
        ResolveReferences();
        BindElements();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindElements();

        if (playerHealth != null)
            playerHealth.HealthChanged += HandleHealthChanged;

        if (playerStamina != null)
            playerStamina.StaminaChanged += HandleStaminaChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.HealthChanged -= HandleHealthChanged;

        if (playerStamina != null)
            playerStamina.StaminaChanged -= HandleStaminaChanged;
    }

    private void ResolveReferences()
    {
        if (document == null)
            document = GetComponent<UIDocument>();

        if (playerHealth != null && playerStamina != null)
            return;

        GameObject player = GameObject.Find(playerObjectName);
        if (player != null)
        {
            if (playerHealth == null)
                playerHealth = player.GetComponent<Health>();

            if (playerStamina == null)
                playerStamina = player.GetComponent<PlayerStamina>();
        }

        if (playerHealth == null || playerStamina == null)
        {
            PlayerController playerController = FindFirstObjectByType<PlayerController>();
            if (playerController != null)
            {
                if (playerHealth == null)
                    playerHealth = playerController.GetComponent<Health>();

                if (playerStamina == null)
                    playerStamina = playerController.GetComponent<PlayerStamina>();
            }
        }
    }

    private void BindElements()
    {
        if (document == null || document.rootVisualElement == null)
            return;

        VisualElement root = document.rootVisualElement;
        _container = root.Q<VisualElement>("PlayerInfoContainer");
        _healthBarFill = root.Q<VisualElement>("HealthBarFill");
        _staminaBarFill = root.Q<VisualElement>("StaminaBarFill");
        _currentHealthLabel = root.Q<Label>("CurrentHealthLabel");
        _maxHealthLabel = root.Q<Label>("MaxHealthLabel");
        _currentStaminaLabel = root.Q<Label>("CurrentStaminaLabel");
        _maxStaminaLabel = root.Q<Label>("MaxStaminaLabel");
        _stateLabel = root.Q<Label>("HealthStateLabel");

        if (_stateLabel == null && _container != null)
        {
            Label[] labels = _container.Query<Label>().ToList().ToArray();
            if (labels.Length > 0)
                _stateLabel = labels[labels.Length - 1];
        }

        if (_container != null && playerInfohModel != null)
            _container.dataSource = playerInfohModel;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        SetHealth(currentHealth, maxHealth);
    }

    private void HandleStaminaChanged(float currentStamina, float maxStamina)
    {
        SetStamina(currentStamina, maxStamina);
    }

    private void Refresh()
    {
        if (playerHealth != null)
            SetHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);

        if (playerStamina != null)
            SetStamina(playerStamina.CurrentStamina, playerStamina.MaxStamina);

        if (playerHealth == null && playerStamina == null && playerInfohModel != null)
        {
            ApplyHealthToView(playerInfohModel.currentHealth, playerInfohModel.maxHealth, playerInfohModel.normalizedHealth);
            ApplyStaminaToView(playerInfohModel.currentStamina, playerInfohModel.maxStamina, playerInfohModel.normalizedStamina);
        }
    }

    private void SetHealth(float currentHealth, float maxHealth)
    {
        int current = Mathf.RoundToInt(currentHealth);
        int max = Mathf.RoundToInt(maxHealth);
        float normalized = maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);

        if (playerInfohModel != null)
        {
            playerInfohModel.currentHealth = current;
            playerInfohModel.maxHealth = max;
            playerInfohModel.normalizedHealth = normalized;
        }

        ApplyHealthToView(current, max, normalized);
    }

    private void SetStamina(float currentStamina, float maxStamina)
    {
        int current = Mathf.RoundToInt(currentStamina);
        int max = Mathf.RoundToInt(maxStamina);
        float normalized = maxStamina <= 0f ? 0f : Mathf.Clamp01(currentStamina / maxStamina);

        if (playerInfohModel != null)
        {
            playerInfohModel.currentStamina = current;
            playerInfohModel.maxStamina = max;
            playerInfohModel.normalizedStamina = normalized;
        }

        ApplyStaminaToView(current, max, normalized);
    }

    private void ApplyHealthToView(int currentHealth, int maxHealth, float normalizedHealth)
    {
        if (_currentHealthLabel != null)
            _currentHealthLabel.text = currentHealth.ToString();

        if (_maxHealthLabel != null)
            _maxHealthLabel.text = maxHealth.ToString();

        if (_healthBarFill != null)
        {
            _healthBarFill.style.width = new StyleLength(new Length(normalizedHealth * 100f, LengthUnit.Percent));
            _healthBarFill.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
            _healthBarFill.style.backgroundColor = Color.Lerp(Color.red, Color.green, normalizedHealth);
        }

        if (_stateLabel != null)
            _stateLabel.text = GetHealthStateText(normalizedHealth);
    }

    private void ApplyStaminaToView(int currentStamina, int maxStamina, float normalizedStamina)
    {
        if (_currentStaminaLabel != null)
            _currentStaminaLabel.text = currentStamina.ToString();

        if (_maxStaminaLabel != null)
            _maxStaminaLabel.text = maxStamina.ToString();

        if (_staminaBarFill != null)
        {
            _staminaBarFill.style.width = new StyleLength(new Length(normalizedStamina * 100f, LengthUnit.Percent));
            _staminaBarFill.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
            _staminaBarFill.style.backgroundColor = Color.Lerp(Color.red, new Color(0.39f, 0.58f, 0.93f), normalizedStamina);
        }
    }

    private static string GetHealthStateText(float normalizedHealth)
    {
        if (normalizedHealth < 1f / 3f)
            return "Danger";

        if (normalizedHealth < 2f / 3f)
            return "Warning";

        return "Healthy";
    }
}
