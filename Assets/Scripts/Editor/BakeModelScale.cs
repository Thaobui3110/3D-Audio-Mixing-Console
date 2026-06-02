// Assets/Scripts/Editor/BakeModelScale.cs
// Menu: Tools → Bake Scale Into Mesh

using UnityEngine;
using UnityEditor;

public class BakeModelScale : EditorWindow
{
    [MenuItem("Tools/Bake Scale Into Mesh")]
    private static void Bake()
    {
        var selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Error", "Chọn model root trong Hierarchy trước.", "OK");
            return;
        }

        Vector3 rootScale = selected.transform.localScale;
        if (rootScale == Vector3.one)
        {
            EditorUtility.DisplayDialog("Info", "Scale đã là (1,1,1), không cần bake.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(selected, "Bake Scale Into Mesh");

        int count = 0;

        foreach (var mf in selected.GetComponentsInChildren<MeshFilter>())
        {
            Mesh original = mf.sharedMesh;
            if (original == null) continue;

            // Tạo copy mesh mới (không sửa mesh gốc)
            Mesh newMesh = Object.Instantiate(original);
            newMesh.name = original.name + "_baked";

            // Tính scale tổng từ root đến object này
            // (chỉ bake root scale, giữ nguyên child local transforms)
            Vector3[] verts = newMesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].x *= rootScale.x;
                verts[i].y *= rootScale.y;
                verts[i].z *= rootScale.z;
            }
            newMesh.vertices = verts;
            newMesh.RecalculateBounds();
            newMesh.RecalculateNormals();

            mf.sharedMesh = newMesh;

            // Update MeshCollider nếu có
            var mc = mf.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = newMesh;

            count++;
        }

        // Cũng scale position của tất cả children
        foreach (Transform child in selected.transform)
        {
            child.localPosition = Vector3.Scale(child.localPosition, rootScale);
        }

        // Reset root scale về (1,1,1)
        selected.transform.localScale = Vector3.one;

        EditorUtility.DisplayDialog("Done",
            $"Đã bake scale ({rootScale.x}, {rootScale.y}, {rootScale.z}) vào {count} meshes.\n" +
            "Root scale đã reset về (1, 1, 1).",
            "OK");
    }
}