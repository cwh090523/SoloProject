using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class NavMeshBakeHelper
{
    private const string WalkableLayerName = "Walkable";
    private const string NavBlockerLayerName = "NavBlocker";
    private const string MapObjectName = "Map";
    private const int NotWalkableArea = 1;

    [MenuItem("Tools/Solo Project/NavMesh/1. Configure Map Surface")]
    public static void ConfigureMapSurface()
    {
        int walkableLayer = LayerMask.NameToLayer(WalkableLayerName);
        if (walkableLayer < 0)
        {
            Debug.LogError($"Missing '{WalkableLayerName}' layer. Add it in ProjectSettings > Tags and Layers.");
            return;
        }

        int navBlockerLayer = LayerMask.NameToLayer(NavBlockerLayerName);
        if (navBlockerLayer < 0)
        {
            Debug.LogError($"Missing '{NavBlockerLayerName}' layer. Add it in ProjectSettings > Tags and Layers.");
            return;
        }

        GameObject map = GameObject.Find(MapObjectName);
        if (map == null)
        {
            Debug.LogError($"Could not find '{MapObjectName}' object.");
            return;
        }

        NavMeshSurface mainSurface = map.GetComponent<NavMeshSurface>();
        if (mainSurface == null)
            mainSurface = map.AddComponent<NavMeshSurface>();

        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        foreach (NavMeshSurface surface in surfaces)
        {
            if (surface != mainSurface)
                surface.enabled = false;
        }

        mainSurface.enabled = true;
        mainSurface.collectObjects = CollectObjects.Children;
        mainSurface.layerMask = (1 << walkableLayer) | (1 << navBlockerLayer);
        mainSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        mainSurface.defaultArea = 0;
        mainSurface.ignoreNavMeshAgent = true;
        mainSurface.ignoreNavMeshObstacle = true;

        EditorUtility.SetDirty(mainSurface);
        EditorSceneManager.MarkSceneDirty(map.scene);
        Debug.Log("Configured Map NavMeshSurface for Walkable/NavBlocker layers and Physics Colliders.");
    }

    [MenuItem("Tools/Solo Project/NavMesh/2. Set Selected Walkable")]
    public static void SetSelectedWalkable()
    {
        int walkableLayer = LayerMask.NameToLayer(WalkableLayerName);
        if (walkableLayer < 0)
        {
            Debug.LogError($"Missing '{WalkableLayerName}' layer. Add it in ProjectSettings > Tags and Layers.");
            return;
        }

        int changedCount = 0;
        foreach (GameObject selected in Selection.gameObjects)
            changedCount += SetWalkableRecursive(selected.transform, walkableLayer, false);

        MarkActiveSceneDirty();
        Debug.Log($"Set {changedCount} objects to Walkable layer.");
    }

    [MenuItem("Tools/Solo Project/NavMesh/3. Set Selected Walkable And Add Mesh Colliders")]
    public static void SetSelectedWalkableAndAddMeshColliders()
    {
        int walkableLayer = LayerMask.NameToLayer(WalkableLayerName);
        if (walkableLayer < 0)
        {
            Debug.LogError($"Missing '{WalkableLayerName}' layer. Add it in ProjectSettings > Tags and Layers.");
            return;
        }

        int changedCount = 0;
        foreach (GameObject selected in Selection.gameObjects)
            changedCount += SetWalkableRecursive(selected.transform, walkableLayer, true);

        MarkActiveSceneDirty();
        Debug.Log($"Set {changedCount} objects to Walkable layer and added missing MeshColliders.");
    }

    [MenuItem("Tools/Solo Project/NavMesh/4. Bake Active Scene")]
    public static void BakeActiveScene()
    {
        ConfigureMapSurface();

        GameObject map = GameObject.Find(MapObjectName);
        if (map == null)
            return;

        NavMeshSurface surface = map.GetComponent<NavMeshSurface>();
        if (surface == null)
            return;

        surface.BuildNavMesh();
        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkSceneDirty(map.scene);
        Debug.Log("Baked NavMesh for the active scene.");
    }

    [MenuItem("Tools/Solo Project/NavMesh/Set Selected Nav Blocker Volumes")]
    public static void SetSelectedNavBlockerVolumes()
    {
        int navBlockerLayer = LayerMask.NameToLayer(NavBlockerLayerName);
        if (navBlockerLayer < 0)
        {
            Debug.LogError($"Missing '{NavBlockerLayerName}' layer. Add it in ProjectSettings > Tags and Layers.");
            return;
        }

        int changedCount = 0;
        foreach (GameObject selected in Selection.gameObjects)
            changedCount += SetNavBlockerRecursive(selected.transform, navBlockerLayer);

        MarkActiveSceneDirty();
        Debug.Log($"Set {changedCount} objects to NavBlocker layer with Not Walkable volumes.");
    }

    private static int SetWalkableRecursive(Transform root, int layer, bool addMeshColliders)
    {
        int count = 0;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            Undo.RecordObject(child.gameObject, "Set Walkable Layer");
            child.gameObject.layer = layer;
            count++;

            if (addMeshColliders)
                AddMeshColliderIfNeeded(child.gameObject);

            EditorUtility.SetDirty(child.gameObject);
        }

        return count;
    }

    private static void AddMeshColliderIfNeeded(GameObject gameObject)
    {
        if (gameObject.GetComponent<Collider>() != null)
            return;

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return;

        MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(gameObject);
        meshCollider.sharedMesh = meshFilter.sharedMesh;
        EditorUtility.SetDirty(meshCollider);
    }

    private static int SetNavBlockerRecursive(Transform root, int layer)
    {
        int count = 0;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            Undo.RecordObject(child.gameObject, "Set Nav Blocker Layer");
            child.gameObject.layer = layer;

            NavMeshModifierVolume volume = child.GetComponent<NavMeshModifierVolume>();
            if (volume == null)
                volume = Undo.AddComponent<NavMeshModifierVolume>(child.gameObject);

            volume.area = NotWalkableArea;
            FitVolumeToColliderOrRenderer(child.gameObject, volume);

            EditorUtility.SetDirty(child.gameObject);
            EditorUtility.SetDirty(volume);
            count++;
        }

        return count;
    }

    private static void FitVolumeToColliderOrRenderer(GameObject gameObject, NavMeshModifierVolume volume)
    {
        Bounds? bounds = null;

        Collider collider = gameObject.GetComponent<Collider>();
        if (collider != null)
            bounds = collider.bounds;
        else
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
                bounds = renderer.bounds;
        }

        if (!bounds.HasValue)
            return;

        Bounds worldBounds = bounds.Value;
        Vector3 localCenter = gameObject.transform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = gameObject.transform.InverseTransformVector(worldBounds.size);
        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

        volume.center = localCenter;
        volume.size = localSize + new Vector3(0.15f, 0.4f, 0.15f);
    }

    private static void MarkActiveSceneDirty()
    {
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
