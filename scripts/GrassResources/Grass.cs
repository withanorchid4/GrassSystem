using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Grass : MonoBehaviour
{
    [SerializeField]
    private ComputeShader grassGenShader;
    [SerializeField]
    private Material mat;
    public Camera cam;
    public Terrain terrain;

    public int tileResolution = 32; //一个区块的分辨率
    public int tileCount = 10;  //有多少个区块

    // public int resolution = 1000;
    // public float grassSpacing = 0.1f;

    [SerializeField, Range(0, 2)]
    public float jitterStrength;

    [Header("Culling")]
    public float distanceCullStartDistance;
    public float distanceCullEndDistance;
    [Range(0f, 1f)]
    public float distanceCullMinimumGrassAmount;  //最远处的密度
    public float frustumCullNearOffset; //允许稍微超出视锥的offset
    public float frustumCullEdgeOffset;

    [Header("Clumping")]
    public int clumpTextureHeight;
    public int clumpTextureWidth;
    public Material clumpingVoronoiMaterial;
    public float clumpScale;
    public List<ClumpParameters> clumpParameters;

    [Header("Wind")]
    [SerializeField] private Texture2D localWindTex;
    [Range(0.0f, 1.0f)]
    [SerializeField] private float localWindStrength = 0.5f;
    [SerializeField] private float localWindScale = 0.01f;
    [SerializeField] private float localWindSpeed = 0.1f;
    [Range(0.0f, 1.0f)]
    [SerializeField] private float localWindRotateAmount = 0.3f;

    private static readonly int resolutionID = Shader.PropertyToID("_Resolution");
    private static readonly int grassSpacingID = Shader.PropertyToID("_GrassSpacing");
    private static readonly int grassBladeBufID = Shader.PropertyToID("_grassPosBuf");
    private static readonly int jitterStrengthID = Shader.PropertyToID("_JitterStrength");

    //地形相关
    private static readonly int heightMapID = Shader.PropertyToID("_HeightMap");
    private static readonly int detailMapID = Shader.PropertyToID("_DetailMap");
    private static readonly int terrainPositionID = Shader.PropertyToID("_TerrainPosition");
    private static readonly int heightMapScaleID = Shader.PropertyToID("_HeightMapScale");
    private static readonly int heightMapMultiplierID = Shader.PropertyToID("_HeightMapMultiplier");

    //剔除相关
    private static readonly int distanceCullStartDistID = Shader.PropertyToID("_DistanceCullStartDist");
    private static readonly int distanceCullEndDistID = Shader.PropertyToID("_DistanceCullEndDist");
    private static readonly int distanceCullMinimumGrassAmountlID = Shader.PropertyToID("_DistanceCullMinimumGrassAmount");
    private static readonly int worldSpaceCameraPositionID = Shader.PropertyToID("_WSpaceCameraPos");
    private static readonly int vpMatrixID = Shader.PropertyToID("_VP_MATRIX");
    private static readonly int frustumCullNearOffsetID = Shader.PropertyToID("_FrustumCullNearOffset");
    private static readonly int frustumCullEdgeOffsetID = Shader.PropertyToID("_FrustumCullEdgeOffset");

    //草丛相关
    private static readonly int clumpParametersID = Shader.PropertyToID("_ClumpParameters"),
            numClumpParametersID = Shader.PropertyToID("_NumClumpParameters"),  //给voronoi shader用的
            clumpTexID = Shader.PropertyToID("ClumpTex"),
            clumpScaleID = Shader.PropertyToID("_ClumpScale"),
            LocalWindTexID = Shader.PropertyToID("_LocalWindTex"),
            LocalWindScaleID = Shader.PropertyToID("_LocalWindScale"),
            LocalWindSpeedID = Shader.PropertyToID("_LocalWindSpeed"),
            LocalWindStrengthID = Shader.PropertyToID("_LocalWindStrength"),
            LocalWindRotateAmountID = Shader.PropertyToID("_LocalWindRotateAmount"),
            TimeID = Shader.PropertyToID("_Time"),
            tilePositionID = Shader.PropertyToID("_TilePosition");
    private ComputeBuffer grassBladeBuffer;


    //下面这些buffer都是indirect draw所需要的buffer（其中position不需要，猜测是compute shader里已经有了）
    private ComputeBuffer meshTrianglesBuffer;
    private ComputeBuffer meshPositionsBuffer;
    private ComputeBuffer meshColorsBuffer;
    private ComputeBuffer meshUvsBuffer;
    private ComputeBuffer argsBuffer; //好像是被用来存其他4个buff的索引的

    private ComputeBuffer clumpParametersBuffer; //传递不同种类的草丛配置的cb
    private const int ARGS_STRIDE = sizeof(int) * 4;
    private Mesh clonedMesh;
    private Bounds bounds;

    private ClumpParameters[] clumpParametersArray;  //草丛配置数据

    private Texture2D clumpTexture; //voronoi shader生成的voronoi贴图

    private float grassSpacing; //计算得出

    private List<Tile> visibleTiles = new List<Tile>();
    private List<Tile> tilesToRender = new List<Tile>();
    private float tileSizeX = 0, tileSizeZ = 0;

    void Awake()
    {
        Initialize();

        bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
    }

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        UpdateGrassTiles();
        UpdateGpuParameters();
    }

    void LateUpdate()
    {
        RenderGrass();
    }

    void Destroy()
    {
        DisposeBuffers();
        DestroyClumpTexture();
    }

    #region 每帧todo
    private void UpdateGrassTiles()
    {
        tilesToRender.Clear();

        if (terrain != null)
        {
            UpdateSurroundingTilesForTerrain(terrain);
        }

        UpdateVisibleTiles();
    }

    private void UpdateSurroundingTilesForTerrain(Terrain terrain)
    {
        Vector3 terrainSize = terrain.terrainData.size;
        tileSizeZ = tileSizeX = terrainSize.x / tileCount;

        Vector3 cameraPositionInTerrainSpace = cam.transform.position - terrain.transform.position;
        int cameraTileXIndex = Mathf.FloorToInt(cameraPositionInTerrainSpace.x / tileSizeX);
        int cameraTileZIndex = Mathf.FloorToInt(cameraPositionInTerrainSpace.z / tileSizeZ);

        for (int xOffset = -1; xOffset <= 2; xOffset++)
        {
            for (int zOffset = -1; zOffset <= 2; zOffset++)
            {
                int tileX = cameraTileXIndex + xOffset;
                int tileZ = cameraTileZIndex + zOffset;

                if (tileX < 0 || tileX >= tileCount || tileZ < 0 || tileZ >= tileCount)
                {
                    continue;
                }

                Bounds tileBounds = CalculateTileBounds(terrain, tileX, tileZ);
                tilesToRender.Add(new Tile(terrain, tileBounds, new Vector2Int(tileX, tileZ)));
            }
        }
    }

    private Bounds CalculateTileBounds(Terrain terrain, int tileXIndex, int tileZIndex)
    {
        Vector3 min = terrain.transform.position + new Vector3(tileXIndex * tileSizeX, -10f, tileZIndex * tileSizeZ);
        Vector3 max = min + new Vector3(tileSizeX, 20f, tileSizeZ);

        Bounds bounds = new Bounds();
        bounds.SetMinMax(min, max);
        return bounds;
    }

    private void UpdateVisibleTiles()
    {
        visibleTiles.Clear();
        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);

        foreach (Tile tile in tilesToRender)
        {
            if (IsVisibleInFrustum(frustumPlanes, tile.bounds))
            {
                visibleTiles.Add(tile);
            }
        }
    }

    private bool IsVisibleInFrustum(Plane[] planes, Bounds bounds)
    {
        return GeometryUtility.TestPlanesAABB(planes, bounds);
    }

    void UpdateGpuParameters()
    {
        grassBladeBuffer.SetCounterValue(0);

        grassGenShader.SetVector(worldSpaceCameraPositionID, cam.transform.position);

        Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 viewProjectionMatrix = projectionMatrix * cam.worldToCameraMatrix;
        grassGenShader.SetMatrix(vpMatrixID, viewProjectionMatrix);

        grassGenShader.SetFloat(TimeID, Time.time);

        // SetupComputeShader();

        // int threadGroupsX = Mathf.CeilToInt(resolution / 8f);
        // int threadGroupsY = Mathf.CeilToInt(resolution / 8f);

        // grassGenShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);
        foreach (var tile in visibleTiles)
        {
            SetupComputeShaderForTile(tile);

            int threadGroupsX = Mathf.CeilToInt(tileResolution / 8f);
            int threadGroupsZ = Mathf.CeilToInt(tileResolution / 8f);

            grassGenShader.Dispatch(0, threadGroupsX, threadGroupsZ, 1);
        }
    }

    private void UpdateClumpParametersBuffer()
    {
        if (clumpParameters.Count > 0)
        {
            if (clumpParametersArray == null || clumpParametersArray.Length != clumpParameters.Count)
            {
                clumpParametersArray = new ClumpParameters[clumpParameters.Count];
            }
            clumpParameters.CopyTo(clumpParametersArray);
            clumpParametersBuffer.SetData(clumpParametersArray);
        }
    }

    // void SetupComputeShader()
    // {
    //     grassGenShader.SetInt(resolutionID, resolution);
    //     grassGenShader.SetFloat(grassSpacingID, grassSpacing);
    //     grassGenShader.SetBuffer(0, grassBladeBufID, grassBladeBuffer);
    //     grassGenShader.SetFloat(jitterStrengthID, jitterStrength);

    //     //terrain相关
    //     if (terrain != null)
    //     {
    //         grassGenShader.SetVector(terrainPositionID, terrain.transform.position);
    //         grassGenShader.SetTexture(0, heightMapID, terrain.terrainData.heightmapTexture);
    //         if (terrain.terrainData.alphamapTextures.Length > 0)
    //         {
    //             grassGenShader.SetTexture(0, detailMapID, terrain.terrainData.alphamapTextures[0]);
    //         }

    //         grassGenShader.SetFloat(heightMapScaleID, terrain.terrainData.size.x);
    //         grassGenShader.SetFloat(heightMapMultiplierID, terrain.terrainData.size.y);
    //     }
    //     //grassBladeBuffer.SetData()

    //     grassGenShader.SetFloat(distanceCullStartDistID, distanceCullStartDistance);
    //     grassGenShader.SetFloat(distanceCullEndDistID, distanceCullEndDistance);
    //     grassGenShader.SetFloat(distanceCullMinimumGrassAmountlID, distanceCullMinimumGrassAmount);
    //     grassGenShader.SetFloat(frustumCullNearOffsetID, frustumCullNearOffset);
    //     grassGenShader.SetFloat(frustumCullEdgeOffsetID, frustumCullEdgeOffset);

    //     grassGenShader.SetVector(worldSpaceCameraPositionID, cam.transform.position);

    //     Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
    //     Matrix4x4 viewProjectionMatrix = projectionMatrix * cam.worldToCameraMatrix;
    //     grassGenShader.SetMatrix(vpMatrixID, viewProjectionMatrix);

    //     UpdateClumpParametersBuffer();
    //     grassGenShader.SetBuffer(0, clumpParametersID, clumpParametersBuffer);
    //     grassGenShader.SetFloat(numClumpParametersID, clumpParameters.Count);
    //     grassGenShader.SetTexture(0, clumpTexID, clumpTexture);
    //     grassGenShader.SetFloat(clumpScaleID, clumpScale);

    //     grassGenShader.SetTexture(0, LocalWindTexID, localWindTex);
    //     grassGenShader.SetFloat(LocalWindScaleID, localWindScale);
    //     grassGenShader.SetFloat(LocalWindSpeedID, localWindSpeed);
    //     grassGenShader.SetFloat(LocalWindStrengthID, localWindStrength);
    //     grassGenShader.SetFloat(LocalWindRotateAmountID, localWindRotateAmount);
    //     grassGenShader.SetFloat(TimeID, Time.time);
    // }

    private void SetupComputeShaderForTile(Tile tile)
    {
        Terrain terrain = tile.terrain;

        grassGenShader.SetInt(resolutionID, tileResolution);
        grassGenShader.SetBuffer(0, grassBladeBufID, grassBladeBuffer);
        grassGenShader.SetFloat(grassSpacingID, grassSpacing);
        grassGenShader.SetFloat(jitterStrengthID, jitterStrength);
        grassGenShader.SetVector(tilePositionID, tile.bounds.min);

        grassGenShader.SetVector(terrainPositionID, terrain.transform.position);
        grassGenShader.SetTexture(0, heightMapID, terrain.terrainData.heightmapTexture);
        if (terrain.terrainData.alphamapTextures.Length > 0)
        {
            grassGenShader.SetTexture(0, detailMapID, terrain.terrainData.alphamapTextures[0]);
        }

        grassGenShader.SetFloat(heightMapScaleID, terrain.terrainData.size.x);
        grassGenShader.SetFloat(heightMapMultiplierID, terrain.terrainData.size.y);

        grassGenShader.SetFloat(distanceCullStartDistID, distanceCullStartDistance);
        grassGenShader.SetFloat(distanceCullEndDistID, distanceCullEndDistance);
        grassGenShader.SetFloat(distanceCullMinimumGrassAmountlID, distanceCullMinimumGrassAmount);
        grassGenShader.SetFloat(frustumCullNearOffsetID, frustumCullNearOffset);
        grassGenShader.SetFloat(frustumCullEdgeOffsetID, frustumCullEdgeOffset);

        UpdateClumpParametersBuffer();
        grassGenShader.SetBuffer(0, clumpParametersID, clumpParametersBuffer);
        grassGenShader.SetTexture(0, clumpTexID, clumpTexture);
        grassGenShader.SetFloat(clumpScaleID, clumpScale);
        grassGenShader.SetFloat(numClumpParametersID, clumpParameters.Count);

        grassGenShader.SetTexture(0, LocalWindTexID, localWindTex);
        grassGenShader.SetFloat(LocalWindScaleID, localWindScale);
        grassGenShader.SetFloat(LocalWindSpeedID, localWindSpeed);
        grassGenShader.SetFloat(LocalWindStrengthID, localWindStrength);
        grassGenShader.SetFloat(LocalWindRotateAmountID, localWindRotateAmount);
    }

    #endregion

    #region 资源初始化与释放

    void Initialize()
    {
        InitializeComputeBuffers();
        SetupMeshBuffers();

        //clumpScale = 1.0f / (resolution * grassSpacing);

        CreateClumpTexture();

        CalculateGrassSpacing();

        if (clumpParameters == null)
        {
            clumpParameters = new List<ClumpParameters>();
        }
    }

    private void CalculateGrassSpacing()
    {
        if (terrain != null)
        {
            grassSpacing = terrain.terrainData.size.x / (tileCount * tileResolution);
        }
    }

    void InitializeComputeBuffers()
    {
        int tileMax = 16; //摄像机最多看到16个区块
        grassBladeBuffer = new ComputeBuffer(tileMax * tileResolution * tileResolution, 14 * sizeof(float), ComputeBufferType.Append);
        grassBladeBuffer.SetCounterValue(0);

        argsBuffer = new ComputeBuffer(1, ARGS_STRIDE, ComputeBufferType.IndirectArguments); //注意此处的buffer是indirectArgu

        //创建草丛配置数组，并用compute buffer
        clumpParametersBuffer = new ComputeBuffer(clumpParameters.Count, sizeof(float) * 10);
        UpdateClumpParametersBuffer();
    }

    #region 为了实现indirect draw，把mesh buffer中的内容提取至compute buffer
    void SetupMeshBuffers()
    {
        clonedMesh = GrassMesh.GetGrassMesh();
        clonedMesh.name = "Grass Instance Mesh";

        CreateComputeBuffersForMesh();  //把meshBuffer的信息填进compute buffer

        argsBuffer.SetData(new int[] { meshTrianglesBuffer.count, 0, 0, 0 });
    }

    //用compute buffer来存mesh的数据：vertex pos，index，uv等等

    private ComputeBuffer CreateBuffer<T>(T[] data, int stride) where T : struct
    {
        ComputeBuffer buffer = new ComputeBuffer(data.Length, stride); //创建一个
        buffer.SetData(data);
        return buffer;
    }

    private void CreateComputeBuffersForMesh()
    {
        int[] triangles = clonedMesh.triangles;
        Vector3[] positions = clonedMesh.vertices;
        Color[] colors = clonedMesh.colors;
        Vector2[] uvs = clonedMesh.uv;

        meshTrianglesBuffer = CreateBuffer<int>(triangles, sizeof(int));
        meshPositionsBuffer = CreateBuffer<Vector3>(positions, sizeof(float) * 3);
        meshColorsBuffer = CreateBuffer<Color>(colors, sizeof(float) * 4);
        meshUvsBuffer = CreateBuffer<Vector2>(uvs, sizeof(float) * 2);

        mat.SetBuffer("trianglesBuffer", meshTrianglesBuffer);
        mat.SetBuffer("colorBuffer", meshColorsBuffer);
        mat.SetBuffer("uvBuffer", meshUvsBuffer);
        mat.SetBuffer("grassBladeBuffer", grassBladeBuffer);

    }

    private void CreateClumpTexture()  //生成voronoi贴图
    {
        clumpingVoronoiMaterial.SetFloat("_NumClumpTypes", clumpParameters.Count);
        RenderTexture clumpVoronoiRenderTexture = RenderTexture.GetTemporary(clumpTextureWidth, clumpTextureHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
        Graphics.Blit(null, clumpVoronoiRenderTexture, clumpingVoronoiMaterial, 0);

        RenderTexture.active = clumpVoronoiRenderTexture;
        clumpTexture = new Texture2D(clumpTextureWidth, clumpTextureHeight, TextureFormat.RGBAHalf, false, true);
        clumpTexture.filterMode = FilterMode.Point;
        clumpTexture.ReadPixels(new Rect(0, 0, clumpTextureWidth, clumpTextureHeight), 0, 0, true);
        clumpTexture.Apply();
        RenderTexture.active = null;

        RenderTexture.ReleaseTemporary(clumpVoronoiRenderTexture);
    }

    #endregion

    void RenderGrass()
    {
        ComputeBuffer.CopyCount(grassBladeBuffer, argsBuffer, sizeof(int)); //把第一个buffer的count拷贝到argsbuffer的第二个位置（第一个位置是int）

        //使用indirectDraw
        //参数分别为：草的material,所有实例的包围盒,片元类型，参数buffer，参数buffer的偏移
        //在哪个相机下绘制（为空则所有都画），材质属性块，是否投射阴影，是否接收阴影，layer
        Graphics.DrawProceduralIndirect(mat, bounds, MeshTopology.Triangles, argsBuffer,
                0, /*cam*/ null, null, UnityEngine.Rendering.ShadowCastingMode.Off, true, gameObject.layer);
    }

    void DisposeBuffers()
    {
        DisposeBuffer(grassBladeBuffer);
        DisposeBuffer(meshTrianglesBuffer);
        DisposeBuffer(meshPositionsBuffer);
        DisposeBuffer(meshColorsBuffer);
        DisposeBuffer(meshUvsBuffer);
        DisposeBuffer(argsBuffer);
        DisposeBuffer(clumpParametersBuffer);
    }
    void DisposeBuffer(ComputeBuffer computeBuffer)
    {
        if (computeBuffer != null)
        {
            computeBuffer.Dispose();
            computeBuffer = null;
        }
    }

    private void DestroyClumpTexture()
    {
        if (clumpTexture != null)
        {
            Destroy(clumpTexture);
            clumpTexture = null;
        }
    }

    #endregion

    private void OnDrawGizmos()
    {
        if (visibleTiles == null) return;

        Color gizmoColor = Color.cyan;
        gizmoColor.a = 0.5f;
        Gizmos.color = gizmoColor;

        foreach (Tile tile in visibleTiles)
        {
            Gizmos.DrawWireCube(tile.bounds.center, tile.bounds.size);
        }
    }
        
}
