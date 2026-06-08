using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UI.ViewModel
{
    [CreateAssetMenu(fileName = "health view model", menuName = "Agent/UI/Health view model", order = 0)]
    public class HealthModelSO : ScriptableObject
    {
        public string characterName;
        public int currentHealth;
        public int maxHealth;

        public float normalizedHealth;

        private void OnValidate()
        {
            normalizedHealth = maxHealth > 0 ? currentHealth / (float)maxHealth : 0f;
        }

        public static HealthModelSO CreateInstanceFromOriginal(HealthModelSO original)
        {
            HealthModelSO newInstance = CreateInstance<HealthModelSO>();
            newInstance.characterName = original.characterName;
            newInstance.currentHealth = original.currentHealth;
            newInstance.maxHealth = original.maxHealth;
            return newInstance;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void RegisterConverters()
        {
            ConverterGroup barGroup = new ConverterGroup("Health bar Converter");
            barGroup.AddConverter((ref float normalizedHealth) =>
                new StyleColor(Color.Lerp(Color.red, Color.green, normalizedHealth)));
            barGroup.AddConverter((ref float normalizedMaxHealth) => normalizedMaxHealth switch
            {
                >= 0 and < 1 / 3.0f => "Danger",
                >= 1 / 3.0f and < 2 / 3.0f => "Warning",
                _ => "Healthy"
            });

            ConverterGroups.RegisterConverterGroup(barGroup);


            ConverterGroup widthGroup = new ConverterGroup("Float width Converter");
            widthGroup.AddConverter((ref float normalizedHealth)
                => new StyleLength(new Length(normalizedHealth * 100, LengthUnit.Percent)));
            ConverterGroups.RegisterConverterGroup(widthGroup);


            ConverterGroup intGroup = new ConverterGroup("Int to String Converter");
            intGroup.AddConverter((ref int value) => value.ToString());
            ConverterGroups.RegisterConverterGroup(intGroup);
        }
    }
}