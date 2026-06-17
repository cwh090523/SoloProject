using System.Collections.Generic;
using UnityEngine;

public class AimTargetHighlight : MonoBehaviour
{
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [SerializeField] private Renderer[] renderers;
    [SerializeField] private Color outlineColor = new Color(1f, 0.74f, 0.18f, 1f);
    [SerializeField, Range(0.001f, 0.08f)] private float outlineWidth = 0.025f;
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private bool includeInactiveRenderers;

    private readonly List<GameObject> _outlineObjects = new List<GameObject>();
    private Material _runtimeOutlineMaterial;
    private bool _isHighlighted;

    private void Awake()
    {
        CacheRenderers();
        BuildOutlineObjects();
        SetOutlineObjectsActive(false);
    }

    private void OnDisable()
    {
        SetHighlighted(false);
    }

    private void OnDestroy()
    {
        if (_runtimeOutlineMaterial != null)
            Destroy(_runtimeOutlineMaterial);
    }

    public void SetHighlighted(bool isHighlighted)
    {
        if (_isHighlighted == isHighlighted)
            return;

        _isHighlighted = isHighlighted;
        SetOutlineObjectsActive(isHighlighted);
    }

    private void CacheRenderers()
    {
        if (renderers != null && renderers.Length > 0)
            return;

        renderers = GetComponentsInChildren<Renderer>(includeInactiveRenderers);
    }

    private void BuildOutlineObjects()
    {
        _outlineObjects.Clear();

        Material material = GetOutlineMaterial();
        if (material == null || renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer sourceRenderer = renderers[i];
            if (sourceRenderer == null || sourceRenderer is ParticleSystemRenderer || sourceRenderer is SpriteRenderer)
                continue;

            if (sourceRenderer.GetComponentInParent<AimTargetHighlight>() != this)
                continue;

            GameObject outlineObject = CreateOutlineObject(sourceRenderer, material);
            if (outlineObject != null)
                _outlineObjects.Add(outlineObject);
        }
    }

    private Material GetOutlineMaterial()
    {
        if (outlineMaterial != null)
        {
            outlineMaterial.SetColor(OutlineColorId, outlineColor);
            outlineMaterial.SetFloat(OutlineWidthId, outlineWidth);
            return outlineMaterial;
        }

        if (_runtimeOutlineMaterial != null)
            return _runtimeOutlineMaterial;

        Shader outlineShader = Shader.Find("Custom/EnemyOutline");
        if (outlineShader == null)
        {
            Debug.LogWarning("Custom/EnemyOutline shader was not found.", this);
            return null;
        }

        _runtimeOutlineMaterial = new Material(outlineShader)
        {
            name = "Runtime Enemy Outline Material"
        };
        _runtimeOutlineMaterial.SetColor(OutlineColorId, outlineColor);
        _runtimeOutlineMaterial.SetFloat(OutlineWidthId, outlineWidth);
        return _runtimeOutlineMaterial;
    }

    private GameObject CreateOutlineObject(Renderer sourceRenderer, Material material)
    {
        SkinnedMeshRenderer sourceSkinnedRenderer = sourceRenderer as SkinnedMeshRenderer;
        if (sourceSkinnedRenderer != null)
            return CreateSkinnedOutlineObject(sourceSkinnedRenderer, material);

        MeshRenderer sourceMeshRenderer = sourceRenderer as MeshRenderer;
        if (sourceMeshRenderer != null)
            return CreateMeshOutlineObject(sourceMeshRenderer, material);

        return null;
    }

    private GameObject CreateSkinnedOutlineObject(SkinnedMeshRenderer sourceRenderer, Material material)
    {
        if (sourceRenderer.sharedMesh == null)
            return null;

        GameObject outlineObject = CreateChildOutlineObject(sourceRenderer.transform);
        SkinnedMeshRenderer outlineRenderer = outlineObject.AddComponent<SkinnedMeshRenderer>();
        outlineRenderer.sharedMesh = sourceRenderer.sharedMesh;
        outlineRenderer.bones = sourceRenderer.bones;
        outlineRenderer.rootBone = sourceRenderer.rootBone;
        outlineRenderer.localBounds = sourceRenderer.localBounds;
        outlineRenderer.updateWhenOffscreen = true;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.sharedMaterials = CreateMaterialArray(sourceRenderer.sharedMaterials.Length, material);
        return outlineObject;
    }

    private GameObject CreateMeshOutlineObject(MeshRenderer sourceRenderer, Material material)
    {
        MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null)
            return null;

        GameObject outlineObject = CreateChildOutlineObject(sourceRenderer.transform);
        MeshFilter outlineFilter = outlineObject.AddComponent<MeshFilter>();
        outlineFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.sharedMaterials = CreateMaterialArray(sourceRenderer.sharedMaterials.Length, material);
        return outlineObject;
    }

    private static GameObject CreateChildOutlineObject(Transform sourceTransform)
    {
        GameObject outlineObject = new GameObject($"{sourceTransform.name}_Outline");
        outlineObject.transform.SetParent(sourceTransform, false);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale = Vector3.one;
        return outlineObject;
    }

    private static Material[] CreateMaterialArray(int count, Material material)
    {
        int materialCount = Mathf.Max(1, count);
        Material[] materials = new Material[materialCount];
        for (int i = 0; i < materialCount; i++)
            materials[i] = material;

        return materials;
    }

    private void SetOutlineObjectsActive(bool isActive)
    {
        for (int i = 0; i < _outlineObjects.Count; i++)
        {
            if (_outlineObjects[i] != null)
                _outlineObjects[i].SetActive(isActive);
        }
    }
}
