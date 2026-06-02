// Assets/Scripts/Editor/RoomBuilder.cs
// Menu: Tools → Build Simple Room
//
// Tạo phòng đơn giản với sàn, 4 tường, trần — normals hướng VÀO TRONG,
// colliders sẵn, materials URP-compatible. Phù hợp cho Spatial Audio Sandbox.

using UnityEngine;
using UnityEditor;

public class RoomBuilder : EditorWindow
{
    private float roomWidth  = 10f;   // X
    private float roomHeight = 4f;    // Y
    private float roomDepth  = 12f;   // Z

    private Color floorColor   = new Color(0.25f, 0.22f, 0.20f); // gỗ tối
    private Color wallColor    = new Color(0.85f, 0.83f, 0.78f); // trắng kem
    private Color ceilingColor = new Color(0.95f, 0.95f, 0.95f); // trắng

    private bool addLighting = true;

    [MenuItem("Tools/Build Simple Room")]
    private static void ShowWindow()
    {
        GetWindow<RoomBuilder>("Room Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Room Dimensions", EditorStyles.boldLabel);
        roomWidth  = EditorGUILayout.FloatField("Width  (X)", roomWidth);
        roomHeight = EditorGUILayout.FloatField("Height (Y)", roomHeight);
        roomDepth  = EditorGUILayout.FloatField("Depth  (Z)", roomDepth);

        GUILayout.Space(10);
        GUILayout.Label("Colors", EditorStyles.boldLabel);
        floorColor   = EditorGUILayout.ColorField("Floor",   floorColor);
        wallColor    = EditorGUILayout.ColorField("Walls",   wallColor);
        ceilingColor = EditorGUILayout.ColorField("Ceiling", ceilingColor);

        GUILayout.Space(10);
        addLighting = EditorGUILayout.Toggle("Add Room Lights", addLighting);

        GUILayout.Space(15);
        if (GUILayout.Button("Build Room", GUILayout.Height(35)))
        {
            BuildRoom();
        }
    }

    private void BuildRoom()
    {
        // Root object
        GameObject room = new GameObject("Room");
        Undo.RegisterCreatedObjectUndo(room, "Build Room");

        float hw = roomWidth  / 2f;
        float hd = roomDepth  / 2f;

        // ═══════════════════════════════════════
        //  FLOOR  — normals hướng lên (0,1,0)
        // ═══════════════════════════════════════
        CreateQuad(room.transform, "Floor",
            new Vector3(-hw, 0,  hd),   // top-left
            new Vector3( hw, 0,  hd),   // top-right
            new Vector3( hw, 0, -hd),   // bottom-right
            new Vector3(-hw, 0, -hd),   // bottom-left
            Vector3.up,
            floorColor);

        // ═══════════════════════════════════════
        //  CEILING — normals hướng xuống (0,-1,0)
        // ═══════════════════════════════════════
        CreateQuad(room.transform, "Ceiling",
            new Vector3(-hw, roomHeight, -hd),
            new Vector3( hw, roomHeight, -hd),
            new Vector3( hw, roomHeight,  hd),
            new Vector3(-hw, roomHeight,  hd),
            Vector3.down,
            ceilingColor);

        // ═══════════════════════════════════════
        //  WALL BACK  (Z = -hd) — normals hướng vào (0,0,1)
        // ═══════════════════════════════════════
        CreateQuad(room.transform, "Wall_Back",
            new Vector3(-hw, 0,          -hd),
            new Vector3( hw, 0,          -hd),
            new Vector3( hw, roomHeight, -hd),
            new Vector3(-hw, roomHeight, -hd),
            Vector3.forward,
            wallColor);

        // ═══════════════════════════════════════
        //  WALL FRONT (Z = +hd) — normals hướng vào (0,0,-1)
        // ═══════════════════════════════════════
        CreateQuad(room.transform, "Wall_Front",
            new Vector3( hw, 0,          hd),
            new Vector3(-hw, 0,          hd),
            new Vector3(-hw, roomHeight, hd),
            new Vector3( hw, roomHeight, hd),
            Vector3.back,
            wallColor);

        // ═══════════════════════════════════════
        //  WALL LEFT  (X = -hw) — normals hướng vào (1,0,0)
        // ═══════════════════════════════════════
        CreateQuad(room.transform, "Wall_Left",
            new Vector3(-hw, 0,          hd),
            new Vector3(-hw, 0,         -hd),
            new Vector3(-hw, roomHeight,-hd),
            new Vector3(-hw, roomHeight, hd),
            Vector3.right,
            wallColor);

        // ═══════════════════════════════════════
        //  WALL RIGHT (X = +hw) — normals hướng vào (-1,0,0)
        // ═══════════════════════════════════════
        CreateQuad(room.transform, "Wall_Right",
            new Vector3(hw, 0,         -hd),
            new Vector3(hw, 0,          hd),
            new Vector3(hw, roomHeight, hd),
            new Vector3(hw, roomHeight,-hd),
            Vector3.left,
            wallColor);

        // ═══════════════════════════════════════
        //  LIGHTING (tùy chọn)
        // ═══════════════════════════════════════
        if (addLighting)
        {
            // Point light trung tâm
            GameObject mainLight = new GameObject("RoomLight_Center");
            mainLight.transform.SetParent(room.transform);
            mainLight.transform.localPosition = new Vector3(0, roomHeight - 0.5f, 0);
            Light lt = mainLight.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.range = Mathf.Max(roomWidth, roomDepth) * 1.2f;
            lt.intensity = 1.5f;
            lt.color = new Color(1f, 0.95f, 0.9f); // warm white

            // 2 đèn phụ 2 bên
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject sideLight = new GameObject($"RoomLight_Side_{(i < 0 ? "L" : "R")}");
                sideLight.transform.SetParent(room.transform);
                sideLight.transform.localPosition = new Vector3(hw * i * 0.6f, roomHeight - 0.5f, 0);
                Light sl = sideLight.AddComponent<Light>();
                sl.type = LightType.Point;
                sl.range = Mathf.Max(roomWidth, roomDepth) * 0.7f;
                sl.intensity = 0.8f;
                sl.color = new Color(1f, 0.97f, 0.92f);
            }
        }

        // ═══════════════════════════════════════
        //  AUDIO REVERB ZONE
        // ═══════════════════════════════════════
        GameObject reverbObj = new GameObject("AudioReverbZone");
        reverbObj.transform.SetParent(room.transform);
        reverbObj.transform.localPosition = new Vector3(0, roomHeight / 2f, 0);
        AudioReverbZone arv = reverbObj.AddComponent<AudioReverbZone>();
        arv.reverbPreset = AudioReverbPreset.Room;
        arv.minDistance = Mathf.Min(roomWidth, roomDepth) * 0.3f;
        arv.maxDistance = Mathf.Max(roomWidth, roomDepth) * 0.8f;

        Selection.activeGameObject = room;
        SceneView.lastActiveSceneView?.FrameSelected();

        EditorUtility.DisplayDialog("Room Built",
            $"Phòng {roomWidth}x{roomHeight}x{roomDepth}m đã được tạo.\n\n" +
            "• Normals hướng vào trong — không bị backface culling\n" +
            "• BoxCollider (2 chiều) trên tất cả bề mặt\n" +
            "• AudioReverbZone preset: Room\n\n" +
            "Di chuyển Player vào bên trong phòng (Y ≈ 1.5) để test.",
            "OK");
    }

    private const float WALL_THICKNESS = 0.15f;

    // ─────────────────────────────────────────────────
    //  Tạo 1 quad (2 triangles) với normals hướng chỉ định
    //  + BoxCollider (khối hộp mỏng) chặn được CẢ HAI CHIỀU
    // ─────────────────────────────────────────────────
    private void CreateQuad(Transform parent, string name,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
        Vector3 normal, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;

        Mesh mesh = new Mesh();
        mesh.name = name + "_Mesh";

        mesh.vertices = new Vector3[] { v0, v1, v2, v3 };
        mesh.normals  = new Vector3[] { normal, normal, normal, normal };

        // UV mapping
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };

        // 2 triangles — winding order hướng VÀO TRONG (front face nhìn từ bên trong phòng)
        mesh.triangles = new int[] { 0, 2, 3, 0, 1, 2 };
        mesh.RecalculateBounds();

        // ── Mesh Filter + Renderer ──
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();

        // Tạo material — tương thích cả Built-in, URP, HDRP
        Material mat = CreateMaterial(name + "_Mat", color);
        mr.sharedMaterial = mat;

        // ── BoxCollider — khối hộp mỏng, chặn cả 2 chiều ──
        // Tính center và size từ 4 đỉnh + độ dày theo hướng normal
        Vector3 center = (v0 + v1 + v2 + v3) / 4f;

        // Tính kích thước bề mặt từ bounding box của 4 đỉnh
        Vector3 min = Vector3.Min(Vector3.Min(v0, v1), Vector3.Min(v2, v3));
        Vector3 max = Vector3.Max(Vector3.Max(v0, v1), Vector3.Max(v2, v3));
        Vector3 extent = max - min;

        // Thêm độ dày theo hướng normal
        Vector3 size = extent + Vector3.Scale(
            new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z)),
            Vector3.one * WALL_THICKNESS
        );

        // Đảm bảo mọi chiều tối thiểu = WALL_THICKNESS
        size.x = Mathf.Max(size.x, WALL_THICKNESS);
        size.y = Mathf.Max(size.y, WALL_THICKNESS);
        size.z = Mathf.Max(size.z, WALL_THICKNESS);

        BoxCollider bc = go.AddComponent<BoxCollider>();
        bc.center = center;
        bc.size   = size;

        // Layer Default — đảm bảo CharacterController va chạm được
        go.layer = 0;
    }

    private Material CreateMaterial(string matName, Color color)
    {
        Material mat;

        // Thử tìm URP Lit shader trước
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit != null)
        {
            mat = new Material(urpLit);
            mat.name = matName;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.3f);
            // Double-sided rendering
            if (mat.HasProperty("_Cull"))
                mat.SetFloat("_Cull", 0); // 0 = Off = cả 2 mặt
            return mat;
        }

        // Thử HDRP Lit
        Shader hdrpLit = Shader.Find("HDRP/Lit");
        if (hdrpLit != null)
        {
            mat = new Material(hdrpLit);
            mat.name = matName;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_DoubleSidedEnable", 1);
            return mat;
        }

        // Fallback: Built-in Standard
        mat = new Material(Shader.Find("Standard"));
        mat.name = matName;
        mat.SetColor("_Color", color);
        mat.SetFloat("_Glossiness", 0.3f);
        mat.SetInt("_Cull", 0); // double-sided
        return mat;
    }
}