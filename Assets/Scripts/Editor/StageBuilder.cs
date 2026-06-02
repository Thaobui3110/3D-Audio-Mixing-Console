// Assets/Scripts/Editor/StageBuilder.cs
// Menu: Tools → Build Stage
//
// Tạo sân khấu bên trong phòng: bục sân khấu, backdrop, đèn màu spotlights.
// Đặt sân khấu ở cuối phòng (Z dương), player đứng phía trước nhìn vào.

using UnityEngine;
using UnityEditor;

public class StageBuilder : EditorWindow
{
    // ── Stage dimensions ──
    private float stageWidth  = 8f;
    private float stageDepth  = 4f;
    private float stageHeight = 0.5f;   // chiều cao bục

    // ── Backdrop ──
    private float backdropHeight = 3.5f;
    private Color backdropColor  = new Color(0.08f, 0.08f, 0.12f); // xám đậm

    // ── Stage surface ──
    private Color stageTopColor  = new Color(0.18f, 0.12f, 0.08f); // gỗ sẫm
    private Color stageFrontColor = new Color(0.10f, 0.10f, 0.10f); // đen

    // ── Position offset (tương đối với Room center) ──
    private float stageZ = 3f;  // đẩy về phía sau phòng

    // ── Lights ──
    private bool addStageLights = true;
    private float lightIntensity = 2.5f;

    [MenuItem("Tools/Build Stage")]
    private static void ShowWindow()
    {
        GetWindow<StageBuilder>("Stage Builder");
    }

    private void OnGUI()
    {
        GUILayout.Label("Stage Dimensions", EditorStyles.boldLabel);
        stageWidth  = EditorGUILayout.FloatField("Width",  stageWidth);
        stageDepth  = EditorGUILayout.FloatField("Depth",  stageDepth);
        stageHeight = EditorGUILayout.FloatField("Platform Height", stageHeight);

        GUILayout.Space(8);
        GUILayout.Label("Backdrop", EditorStyles.boldLabel);
        backdropHeight = EditorGUILayout.FloatField("Backdrop Height", backdropHeight);
        backdropColor  = EditorGUILayout.ColorField("Backdrop Color",  backdropColor);

        GUILayout.Space(8);
        GUILayout.Label("Colors", EditorStyles.boldLabel);
        stageTopColor   = EditorGUILayout.ColorField("Stage Top (sàn)",   stageTopColor);
        stageFrontColor = EditorGUILayout.ColorField("Stage Front (mặt)", stageFrontColor);

        GUILayout.Space(8);
        GUILayout.Label("Position", EditorStyles.boldLabel);
        stageZ = EditorGUILayout.FloatField("Z Offset (from center)", stageZ);

        GUILayout.Space(8);
        GUILayout.Label("Lighting", EditorStyles.boldLabel);
        addStageLights = EditorGUILayout.Toggle("Add Stage Lights", addStageLights);
        if (addStageLights)
            lightIntensity = EditorGUILayout.FloatField("Light Intensity", lightIntensity);

        GUILayout.Space(15);
        if (GUILayout.Button("Build Stage", GUILayout.Height(35)))
        {
            Build();
        }
    }

    private void Build()
    {
        GameObject stage = new GameObject("Stage");
        Undo.RegisterCreatedObjectUndo(stage, "Build Stage");

        float hw = stageWidth / 2f;
        float hd = stageDepth / 2f;
        float y0 = 0f;
        float y1 = stageHeight;

        // Offset Z — đặt sân khấu ở phía sau phòng
        float zBack  = stageZ + hd;
        float zFront = stageZ - hd;

        // ═══════════════════════════════════════════
        //  BỤC SÂN KHẤU (hộp: top, front, left, right)
        // ═══════════════════════════════════════════

        // ── Top (sàn sân khấu) — nhìn từ trên xuống ──
        CreateQuad(stage.transform, "Stage_Top",
            new Vector3(-hw, y1,  zBack),
            new Vector3( hw, y1,  zBack),
            new Vector3( hw, y1,  zFront),
            new Vector3(-hw, y1,  zFront),
            Vector3.up, stageTopColor, true);

        // ── Front (mặt trước bục) — player nhìn thấy ──
        CreateQuad(stage.transform, "Stage_Front",
            new Vector3( hw, y0, zFront),
            new Vector3(-hw, y0, zFront),
            new Vector3(-hw, y1, zFront),
            new Vector3( hw, y1, zFront),
            Vector3.back, stageFrontColor, true);

        // ── Left side ──
        CreateQuad(stage.transform, "Stage_Left",
            new Vector3(-hw, y0, zFront),
            new Vector3(-hw, y0, zBack),
            new Vector3(-hw, y1, zBack),
            new Vector3(-hw, y1, zFront),
            Vector3.right, stageFrontColor, false);

        // ── Right side ──
        CreateQuad(stage.transform, "Stage_Right",
            new Vector3(hw, y0, zBack),
            new Vector3(hw, y0, zFront),
            new Vector3(hw, y1, zFront),
            new Vector3(hw, y1, zBack),
            Vector3.left, stageFrontColor, false);

        // ═══════════════════════════════════════════
        //  BACKDROP (phông nền phía sau sân khấu)
        // ═══════════════════════════════════════════
        float bdY0 = y1;
        float bdY1 = y1 + backdropHeight;

        CreateQuad(stage.transform, "Backdrop",
            new Vector3( hw, bdY0, zBack),
            new Vector3(-hw, bdY0, zBack),
            new Vector3(-hw, bdY1, zBack),
            new Vector3( hw, bdY1, zBack),
            Vector3.back, backdropColor, true);

        // ── 2 cánh gà (side wings) ──
        float wingDepth = stageDepth * 0.6f;

        // Wing left
        CreateQuad(stage.transform, "Wing_Left",
            new Vector3(-hw, bdY0, zBack),
            new Vector3(-hw, bdY0, zBack - wingDepth),
            new Vector3(-hw, bdY1, zBack - wingDepth),
            new Vector3(-hw, bdY1, zBack),
            Vector3.right, backdropColor, false);

        // Wing right
        CreateQuad(stage.transform, "Wing_Right",
            new Vector3(hw, bdY0, zBack - wingDepth),
            new Vector3(hw, bdY0, zBack),
            new Vector3(hw, bdY1, zBack),
            new Vector3(hw, bdY1, zBack - wingDepth),
            Vector3.left, backdropColor, false);

        // ═══════════════════════════════════════════
        //  TRUSS BAR (thanh treo đèn phía trên)
        // ═══════════════════════════════════════════
        float trussY = bdY1 - 0.3f;
        GameObject truss = GameObject.CreatePrimitive(PrimitiveType.Cube);
        truss.name = "TrussBar";
        truss.transform.SetParent(stage.transform);
        truss.transform.localPosition = new Vector3(0, trussY, zFront + stageDepth * 0.3f);
        truss.transform.localScale = new Vector3(stageWidth + 0.5f, 0.08f, 0.08f);

        // Material tối cho truss
        Renderer trussRend = truss.GetComponent<Renderer>();
        Material trussMat = CreateMaterial("Truss_Mat", new Color(0.15f, 0.15f, 0.15f));
        trussRend.sharedMaterial = trussMat;

        // ═══════════════════════════════════════════
        //  ĐÈN SÂN KHẤU (Spotlights màu)
        // ═══════════════════════════════════════════
        if (addStageLights)
        {
            GameObject lightsParent = new GameObject("StageLights");
            lightsParent.transform.SetParent(stage.transform);

            float trussZ = zFront + stageDepth * 0.3f;

            // ── Đèn treo trên truss (chiếu xuống sân khấu) ──
            Color[] spotColors = new Color[]
            {
                new Color(1.0f, 0.2f, 0.2f),  // đỏ
                new Color(0.2f, 0.5f, 1.0f),  // xanh dương
                new Color(1.0f, 0.9f, 0.3f),  // vàng
                new Color(0.3f, 1.0f, 0.4f),  // xanh lá
                new Color(0.8f, 0.2f, 1.0f),  // tím
            };

            int spotCount = Mathf.Min(spotColors.Length, Mathf.FloorToInt(stageWidth / 1.5f));
            float spotSpacing = stageWidth / (spotCount + 1);

            for (int i = 0; i < spotCount; i++)
            {
                float x = -hw + spotSpacing * (i + 1);
                Color c = spotColors[i % spotColors.Length];

                // Spotlight housing (cylinder nhỏ)
                GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                housing.name = $"SpotHousing_{i + 1}";
                housing.transform.SetParent(lightsParent.transform);
                housing.transform.localPosition = new Vector3(x, trussY - 0.15f, trussZ);
                housing.transform.localScale = new Vector3(0.15f, 0.1f, 0.15f);
                Renderer hr = housing.GetComponent<Renderer>();
                hr.sharedMaterial = CreateMaterial($"Housing_{i}_Mat", new Color(0.1f, 0.1f, 0.1f));

                // Spotlight
                GameObject spotGO = new GameObject($"Spot_{i + 1}_{ColorName(c)}");
                spotGO.transform.SetParent(lightsParent.transform);
                spotGO.transform.localPosition = new Vector3(x, trussY - 0.25f, trussZ);
                spotGO.transform.localRotation = Quaternion.Euler(60f, 0, 0); // chiếu chéo xuống

                Light spot = spotGO.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.color = c;
                spot.intensity = lightIntensity;
                spot.range = 12f;
                spot.spotAngle = 45f;
                spot.innerSpotAngle = 25f;
                spot.shadows = LightShadows.Soft;
            }

            // ── 2 đèn wash (Point Light) hai bên sân khấu ──
            Color[] washColors = new Color[]
            {
                new Color(0.3f, 0.3f, 1.0f),  // xanh dương nhạt bên trái
                new Color(1.0f, 0.3f, 0.5f),  // hồng bên phải
            };

            for (int side = -1; side <= 1; side += 2)
            {
                int idx = side < 0 ? 0 : 1;
                GameObject wash = new GameObject($"WashLight_{(side < 0 ? "L" : "R")}");
                wash.transform.SetParent(lightsParent.transform);
                wash.transform.localPosition = new Vector3(
                    hw * side * 0.85f,
                    y1 + 1.5f,
                    stageZ
                );
                Light wl = wash.AddComponent<Light>();
                wl.type = LightType.Point;
                wl.color = washColors[idx];
                wl.intensity = lightIntensity * 0.6f;
                wl.range = stageWidth * 0.8f;
            }

            // ── Đèn footlight (dưới mép sân khấu chiếu lên) ──
            int footCount = 4;
            float footSpacing = stageWidth / (footCount + 1);
            Color[] footColors = new Color[]
            {
                new Color(1f, 0.6f, 0.2f),    // cam
                new Color(0.4f, 0.8f, 1f),    // cyan
                new Color(1f, 0.3f, 0.6f),    // hồng
                new Color(0.5f, 1f, 0.5f),    // xanh lá nhạt
            };

            for (int i = 0; i < footCount; i++)
            {
                float x = -hw + footSpacing * (i + 1);
                GameObject foot = new GameObject($"Footlight_{i + 1}");
                foot.transform.SetParent(lightsParent.transform);
                foot.transform.localPosition = new Vector3(x, y1 + 0.05f, zFront + 0.1f);
                foot.transform.localRotation = Quaternion.Euler(-30f, 0, 0); // chiếu lên

                Light fl = foot.AddComponent<Light>();
                fl.type = LightType.Spot;
                fl.color = footColors[i % footColors.Length];
                fl.intensity = lightIntensity * 0.5f;
                fl.range = 5f;
                fl.spotAngle = 60f;
                fl.innerSpotAngle = 30f;
            }
        }

        // ═══════════════════════════════════════════
        //  MONITOR WEDGE (loa monitor trên sân khấu) — trang trí
        // ═══════════════════════════════════════════
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject wedge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wedge.name = $"MonitorWedge_{(side < 0 ? "L" : "R")}";
            wedge.transform.SetParent(stage.transform);
            wedge.transform.localPosition = new Vector3(
                hw * side * 0.6f,
                y1 + 0.1f,
                zFront + 0.5f
            );
            wedge.transform.localScale = new Vector3(0.5f, 0.2f, 0.35f);
            wedge.transform.localRotation = Quaternion.Euler(-15f, 0, 0);

            Renderer wr = wedge.GetComponent<Renderer>();
            wr.sharedMaterial = CreateMaterial($"Monitor_{side}_Mat", new Color(0.08f, 0.08f, 0.08f));
        }

        Selection.activeGameObject = stage;
        SceneView.lastActiveSceneView?.FrameSelected();

        EditorUtility.DisplayDialog("Stage Built",
            $"Sân khấu {stageWidth}x{stageDepth}m đã được tạo tại Z={stageZ}.\n\n" +
            "• Bục sân khấu + backdrop + cánh gà\n" +
            "• Thanh truss treo đèn\n" +
            "• Spotlights màu + wash lights + footlights\n" +
            "• Monitor wedges trang trí\n\n" +
            "Đặt speaker trên sân khấu để test spatial audio.",
            "OK");
    }

    // ─────────────────────────────────────────────────
    private void CreateQuad(Transform parent, string name,
        Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
        Vector3 normal, Color color, bool addCollider)
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
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0), new Vector2(1, 0),
            new Vector2(1, 1), new Vector2(0, 1)
        };
        mesh.triangles = new int[] { 0, 2, 3, 0, 1, 2 };
        mesh.RecalculateBounds();

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = CreateMaterial(name + "_Mat", color);

        if (addCollider)
        {
            Vector3 center = (v0 + v1 + v2 + v3) / 4f;
            Vector3 min = Vector3.Min(Vector3.Min(v0, v1), Vector3.Min(v2, v3));
            Vector3 max = Vector3.Max(Vector3.Max(v0, v1), Vector3.Max(v2, v3));
            Vector3 size = max - min;
            size.x = Mathf.Max(size.x, 0.15f);
            size.y = Mathf.Max(size.y, 0.15f);
            size.z = Mathf.Max(size.z, 0.15f);

            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.center = center;
            bc.size   = size;
        }

        go.layer = 0;
    }

    private Material CreateMaterial(string matName, Color color)
    {
        Material mat;
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit != null)
        {
            mat = new Material(urpLit);
            mat.name = matName;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.2f);
            if (mat.HasProperty("_Cull"))
                mat.SetFloat("_Cull", 0);
            return mat;
        }

        mat = new Material(Shader.Find("Standard"));
        mat.name = matName;
        mat.SetColor("_Color", color);
        mat.SetFloat("_Glossiness", 0.2f);
        mat.SetInt("_Cull", 0);
        return mat;
    }

    private string ColorName(Color c)
    {
        if (c.r > 0.7f && c.g < 0.4f && c.b < 0.4f) return "Red";
        if (c.r < 0.4f && c.b > 0.7f) return "Blue";
        if (c.r > 0.7f && c.g > 0.7f) return "Yellow";
        if (c.g > 0.7f && c.r < 0.5f) return "Green";
        if (c.r > 0.5f && c.b > 0.7f) return "Purple";
        return "Color";
    }
}