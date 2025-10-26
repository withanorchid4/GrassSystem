using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassWatcher : MonoBehaviour
{
    // Start is called before the first frame update
    public Material mat;
    public Mesh mesh;
    void Start()
    {
        mesh = GrassMesh.GetGrassMesh();
        var meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;
        var meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = mat;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
