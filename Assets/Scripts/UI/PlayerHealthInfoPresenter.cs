using UI.ViewModel;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerHealthInfoPresenter : MonoBehaviour
{
    [SerializeField] private UIDocument document;
    [SerializeField] private Health playerHealth;
    [SerializeField] private HealthModelSO healthModel;
    [SerializeField] private string playerObjectName = "Player3";

    private VisualElement _container;
    private VisualElement _barTrack;
    private VisualElement _barFill;
    private Label _currentHealthLabel;
    private Label _maxHealthLabel;
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

        Refresh();
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.HealthChanged -= HandleHealthChanged;
    }

    private void ResolveReferences()
    {
        if (document == null)
            document = GetComponent<UIDocument>();

        if (playerHealth != null)
            return;

        GameObject player = GameObject.Find(playerObjectName);
        if (player != null)
            playerHealth = player.GetComponent<Health>();

        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerController>()?.GetComponent<Health>();
    }

    private void BindElements()
    {
        if (document == null || document.rootVisualElement == null)
            return;

        VisualElement root = document.rootVisualElement;
        _container = root.Q<VisualElement>("PlayerInfoContainer");
        _barTrack = root.Q<VisualElement>("BarTrack");
        _barFill = root.Q<VisualElement>("BarFill");
        _currentHealthLabel = root.Q<Label>("CurrentHealthLabel");
        _maxHealthLabel = root.Q<Label>("MaxHealthLabel");
        _stateLabel = root.Q<Label>("HealthStateLabel");

        if (_stateLabel == null && _container != null)
        {
            Label[] labels = _container.Query<Label>().ToList().ToArray();
            if (labels.Length > 0)
                _stateLabel = labels[labels.Length - 1];
        }

        if (_container != null && healthModel != null)
            _container.dataSource = healthModel;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        SetHealth(currentHealth, maxHealth);
    }

    private void Refresh()
    {
        if (playerHealth != null)
            SetHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        else if (healthModel != null)
            ApplyToView(healthModel.currentHealth, healthModel.maxHealth, healthModel.normalizedHealth);
    }

    private void SetHealth(float currentHealth, float maxHealth)
    {
        int current = Mathf.RoundToInt(currentHealth);
        int max = Mathf.RoundToInt(maxHealth);
        float normalized = maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);

        if (healthModel != null)
        {
            healthModel.currentHealth = current;
            healthModel.maxHealth = max;
            healthModel.normalizedHealth = normalized;
        }

        ApplyToView(current, max, normalized);
    }

    private void ApplyToView(int currentHealth, int maxHealth, float normalizedHealth)
    {
        if (_currentHealthLabel != null)
            _currentHealthLabel.text = currentHealth.ToString();

        if (_maxHealthLabel != null)
            _maxHealthLabel.text = maxHealth.ToString();

        if (_barFill != null)
        {
            _barFill.style.width = new StyleLength(new Length(normalizedHealth * 100f, LengthUnit.Percent));
            _barFill.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
            _barFill.style.backgroundColor = Color.Lerp(Color.red, Color.green, normalizedHealth);
        }

        if (_stateLabel != null)
            _stateLabel.text = GetHealthStateText(normalizedHealth);
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
