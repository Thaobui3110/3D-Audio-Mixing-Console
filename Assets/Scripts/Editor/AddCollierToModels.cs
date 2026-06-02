// Assets/Scripts/Editor/AddCollidersToModel.cs
// Menu: Tools → Add Colliders To Selected Model

using UnityEngine;
using UnityEditor;

public class AddCollidersToModel : EditorWindow
{
    [MenuItem("Tools/Add Colliders To Selected Model")]
    private static void AddColliders()
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Error", "Chọn model trong Hierarchy trước.", "OK");
            return;
        }

        int count = 0;

        // Thêm MeshCollider cho tất cả MeshFilter trong children
        foreach (var mf in selected.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.GetComponent<Collider>() != null) continue; // đã có collider

            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            count++;
        }

        // Thêm cho SkinnedMeshRenderer (nếu có)
        foreach (var smr in selected.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr.GetComponent<Collider>() != null) continue;

            var mc = smr.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = smr.sharedMesh;
            count++;
        }

        Undo.RegisterFullObjectHierarchyUndo(selected, "Add Colliders");
        EditorUtility.DisplayDialog("Done", $"Đã thêm MeshCollider cho {count} objects.", "OK");
    }
}