using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Voronoi : MonoBehaviour
{
    [Header("Voronoi Settings")]
    [SerializeField] private Material mat;
    [SerializeField] private int textureHeight;
    [SerializeField] private int textureWidth;
    [Range(1, 40)]
    public int numClumpTypes = 4;
    [Range(1, 100)]
    public int numClumps = 10;


    private RenderTexture voronoiRT;
    // Start is called before the first frame update
    void Start()
    {
        if(voronoiRT == null)
        {
            voronoiRT = new RenderTexture(textureWidth, textureHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            voronoiRT.enableRandomWrite = true;
            voronoiRT.Create();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (mat == null) return;
        if (voronoiRT == null) return;
        mat.SetFloat("_NumClumpTypes", numClumpTypes);
        mat.SetFloat("_NumClumps", numClumps);

        Graphics.Blit(null, voronoiRT, mat, 0);
    }

    void OnGUI()
    {
        if (voronoiRT == null) return;

        GUI.DrawTexture(new Rect(20, 20, 512, 512), voronoiRT, ScaleMode.ScaleToFit);
    }

    void OnDestroy()
    {
        if(voronoiRT != null)
        {
            voronoiRT.Release();
            if (Application.isPlaying)
            {
                Destroy(voronoiRT);
            }
            else
            {
                DestroyImmediate(voronoiRT);
            }
        }
    }
}
