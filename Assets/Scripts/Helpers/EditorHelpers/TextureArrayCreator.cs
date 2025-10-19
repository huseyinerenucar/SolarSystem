using UnityEditor;
using UnityEngine;

public class TextureArrayCreator : EditorWindow
{
    public Texture2D[] sourceTextures = new Texture2D[32];
    private string assetPath = "Assets/GeneratedTextureArray.asset";

    [MenuItem("Tools/Texture Array Creator")]
    public static void ShowWindow()
    {
        GetWindow<TextureArrayCreator>("Texture Array Creator");
    }

    void OnGUI()
    {
        GUILayout.Label("Create a Texture2DArray", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Assign your 32 source textures below in the correct order (A1..H1, A2..H2, etc.). All textures must have the same dimensions and format.", MessageType.Info);

        assetPath = EditorGUILayout.TextField("Save Path", assetPath);

        EditorGUILayout.Space();
        ScriptableObject target = this;
        SerializedObject so = new(target);
        SerializedProperty texturesProperty = so.FindProperty("sourceTextures");

        EditorGUILayout.PropertyField(texturesProperty, true);
        so.ApplyModifiedProperties();

        EditorGUILayout.Space();

        if (GUILayout.Button("Create Texture2DArray"))
            CreateTextureArray();
    }

    private void CreateTextureArray()
    {
        if (sourceTextures == null || sourceTextures.Length == 0 || sourceTextures[0] == null)
        {
            Debug.LogError("No source textures assigned.");
            return;
        }

        Texture2D firstTexture = sourceTextures[0];
        int width = firstTexture.width;
        int height = firstTexture.height;
        TextureFormat format = firstTexture.format;

        Texture2DArray textureArray = new(width, height, sourceTextures.Length, format, true, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Trilinear,
            anisoLevel = 16
        };

        for (int i = 0; i < sourceTextures.Length; i++)
        {
            Texture2D tex = sourceTextures[i];
            if (tex == null)
            {
                Debug.LogWarning($"Texture at index {i} is null. Skipping.");
                continue;
            }
            if (tex.width != width || tex.height != height)
            {
                Debug.LogError($"Texture at index {i} has different dimensions! Expected {width}x{height}, but got {tex.width}x{tex.height}.");
                return;
            }
            Graphics.CopyTexture(tex, 0, 0, textureArray, i, 0);
        }

        AssetDatabase.CreateAsset(textureArray, assetPath);
        Debug.Log($"Successfully created Texture2DArray at {assetPath}");
    }
}