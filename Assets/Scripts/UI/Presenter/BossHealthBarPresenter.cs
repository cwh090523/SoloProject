using UnityEngine;
using UnityEngine.UIElements;

namespace UI.Presenter
{
    public class BossHealthBarPresenter : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private BossHealthTarget bossTarget;
        [SerializeField] private bool autoFindBoss = true;

        private VisualElement _root;
        private VisualElement _fill;
        private Label _nameLabel;
        private Label _healthLabel;
        private Health _boundHealth;

        private void Awake()
        {
            if (document == null)
                document = GetComponent<UIDocument>();

            BindElements();
            BindBoss(bossTarget);
            RefreshVisibility();
        }

        private void OnEnable()
        {
            BossHealthTarget.Registered += HandleBossRegistered;
            BossHealthTarget.Unregistered += HandleBossUnregistered;

            if (autoFindBoss && bossTarget == null)
                BindBoss(FindActiveBoss());
        }

        private void OnDisable()
        {
            BossHealthTarget.Registered -= HandleBossRegistered;
            BossHealthTarget.Unregistered -= HandleBossUnregistered;
            UnbindHealth();
        }

        private void HandleBossRegistered(BossHealthTarget target)
        {
            if (!autoFindBoss || target == null)
                return;

            if (bossTarget == null || _boundHealth == null || _boundHealth.IsDead)
                BindBoss(target);
        }

        private void HandleBossUnregistered(BossHealthTarget target)
        {
            if (target == null || target != bossTarget)
                return;

            BindBoss(autoFindBoss ? FindActiveBoss() : null);
        }

        private void BindElements()
        {
            if (document == null || document.rootVisualElement == null)
                return;

            VisualElement visualRoot = document.rootVisualElement;
            _root = visualRoot.Q<VisualElement>("BossHealthRoot");
            _fill = visualRoot.Q<VisualElement>("BossHealthFill");
            _nameLabel = visualRoot.Q<Label>("BossNameLabel");
            _healthLabel = visualRoot.Q<Label>("BossHealthLabel");
        }

        private void BindBoss(BossHealthTarget target)
        {
            UnbindHealth();

            bossTarget = target;
            _boundHealth = bossTarget == null ? null : bossTarget.Health;

            if (_boundHealth != null)
            {
                _boundHealth.HealthChanged += HandleHealthChanged;
                _boundHealth.Died += HandleDied;
                Refresh(_boundHealth.CurrentHealth, _boundHealth.MaxHealth);
            }
            else
            {
                RefreshVisibility();
            }
        }

        private void UnbindHealth()
        {
            if (_boundHealth == null)
                return;

            _boundHealth.HealthChanged -= HandleHealthChanged;
            _boundHealth.Died -= HandleDied;
            _boundHealth = null;
        }

        private void HandleHealthChanged(float currentHealth, float maxHealth)
        {
            Refresh(currentHealth, maxHealth);
        }

        private void HandleDied()
        {
            if (bossTarget == null || bossTarget.HideOnDeath)
                SetVisible(false);
        }

        private void Refresh(float currentHealth, float maxHealth)
        {
            if (_root == null || _fill == null)
                return;

            float normalized = maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
            _fill.style.width = Length.Percent(normalized * 100f);

            if (_nameLabel != null)
                _nameLabel.text = bossTarget == null ? "BOSS" : bossTarget.DisplayName;

            if (_healthLabel != null)
                _healthLabel.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";

            bool shouldShow = currentHealth > 0f && (bossTarget == null || bossTarget.ShowWhenFullHealth || currentHealth < maxHealth);
            SetVisible(shouldShow);
        }

        private void RefreshVisibility()
        {
            if (_boundHealth != null)
                Refresh(_boundHealth.CurrentHealth, _boundHealth.MaxHealth);
            else
                SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_root != null)
                _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static BossHealthTarget FindActiveBoss()
        {
            BossHealthTarget[] targets = FindObjectsByType<BossHealthTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < targets.Length; i++)
            {
                Health health = targets[i].Health;
                if (health != null && !health.IsDead)
                    return targets[i];
            }

            return null;
        }
    }
}
