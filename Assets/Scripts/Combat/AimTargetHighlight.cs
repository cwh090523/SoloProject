using System.Collections.Generic;
using UnityEngine;

public class AimTargetHighlight : MonoBehaviour
{
    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Color highlightColor = new Color(1f, 0.82f, 0.25f, 1f);
    [SerializeField] private float highlightIntensity = 0.85f;
    [SerializeField] private bool useEmission = true;
    [SerializeField] private Color emissionColor = new Color(1f, 0.68f, 0.12f, 1f);
    [SerializeField] private float emissionIntensity = 1.75f;

    private readonly List<Material> _materials = new List<Material>();
    private readonly List<MaterialState> _materialStates = new List<MaterialState>();
    private bool _isHighlighted;

    private void Awake()
    {
        CacheMaterials();
    }

    private void OnDisable()
    {
        SetHighlighted(false);
    }

    public void SetHighlighted(bool isHighlighted)
    {
        if (_isHighlighted == isHighlighted)
            return;

        _isHighlighted = isHighlighted;

        for (int i = 0; i < _materials.Count; i++)
        {
            Material material = _materials[i];
            MaterialState state = _materialStates[i];
            if (material == null)
                continue;

            if (state.HasBaseColor)
            {
                Color color = isHighlighted
                    ? Color.Lerp(state.BaseColor, highlightColor, highlightIntensity)
                    : state.BaseColor;

                material.SetColor(state.BaseColorProperty, color);
            }

            if (useEmission && state.HasEmission)
            {
                if (isHighlighted)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", emissionColor * emissionIntensity);
                }
                else
                {
                    material.SetColor("_EmissionColor", state.BaseEmissionColor);

                    if (!state.EmissionWasEnabled)
                        material.DisableKeyword("_EMISSION");
                }
            }
        }
    }

    private void CacheMaterials()
    {
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>();

        _materials.Clear();
        _materialStates.Clear();

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null)
                continue;

            Material[] targetMaterials = targetRenderer.materials;
            for (int j = 0; j < targetMaterials.Length; j++)
            {
                Material material = targetMaterials[j];
                _materials.Add(material);
                _materialStates.Add(CreateMaterialState(material));
            }
        }
    }

    private static MaterialState CreateMaterialState(Material material)
    {
        MaterialState state = new MaterialState();
        if (material == null)
            return state;

        if (material.HasProperty("_BaseColor"))
        {
            state.HasBaseColor = true;
            state.BaseColorProperty = "_BaseColor";
            state.BaseColor = material.GetColor("_BaseColor");
        }
        else if (material.HasProperty("_Color"))
        {
            state.HasBaseColor = true;
            state.BaseColorProperty = "_Color";
            state.BaseColor = material.GetColor("_Color");
        }

        if (material.HasProperty("_EmissionColor"))
        {
            state.HasEmission = true;
            state.BaseEmissionColor = material.GetColor("_EmissionColor");
            state.EmissionWasEnabled = material.IsKeywordEnabled("_EMISSION");
        }

        return state;
    }

    private struct MaterialState
    {
        public bool HasBaseColor;
        public string BaseColorProperty;
        public Color BaseColor;
        public bool HasEmission;
        public Color BaseEmissionColor;
        public bool EmissionWasEnabled;
    }
}
