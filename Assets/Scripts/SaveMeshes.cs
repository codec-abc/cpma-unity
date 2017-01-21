using UnityEditor;
using UnityEngine;

// look at https://github.com/mikezila/uQuake3

[CustomEditor(typeof(SaveMeshes))]
public class SaveMeshesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Save Meshes"))
        {
            Debug.Log("Save Meshes");
            SaveMeshes script = (SaveMeshes)target;
            script.Run();
        }
    }
}

public class SaveMeshes : MonoBehaviour
{
    [SerializeField]
    GameObject root;

    internal void Run()
    {
        var i = 0;

        AssetDatabase.CreateFolder("Assets", "Maps");
        AssetDatabase.CreateFolder("Assets/Maps", root.name);

        foreach (Transform child in root.transform)
        {
            AssetDatabase.CreateAsset
            (
                UnityEngine.Object.Instantiate(child.GetComponent<MeshFilter>().sharedMesh), 
                "Assets/Maps/" + root.name+ "/" + i + ".asset"
            );
            i++;
        }
        AssetDatabase.SaveAssets();
    }
}
