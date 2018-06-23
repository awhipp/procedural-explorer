using UnityEngine;
using System.Linq; // used for Sum of array

public class terrainGenerator : MonoBehaviour
{
    private int  m_terrainSize; // terrain width
    private TerrainData ogData;

    /**
     * Initialization Method
     **/
    void Start()
    {
        ogData = this.GetComponent<Terrain>().terrainData;
        initTerrain();
    }

    void OnApplicationQuit()
    {
        this.GetComponent<Terrain>().terrainData = ogData;
    }

    /**
     * Called once per frame
     **/
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            initTerrain();
        }
    }

    /**
     * Initializing the terrain with:
     * + Procedural Terrain Heightmap
     * + Splat Map based on Terrain Elevation and Steepness
     **/
    void initTerrain()
    {
        m_terrainSize = Mathf.ClosestPowerOfTwo(Random.Range(512, 2048));

        Terrain terrain = this.GetComponent<Terrain>();

        generateMap(terrain, Random.Range(3f, 7f));
        paintMap(terrain);
        fillTreeInstances(terrain);
        fillDetailMap(terrain);

        // Place Player ontop of Terrain
        GameObject player = ((GameObject)GameObject.Find("Player"));
        Vector3 temp = player.transform.position;
        temp.x = Random.Range(100, terrain.terrainData.size.x - 100);
        temp.z = Random.Range(100, terrain.terrainData.size.z - 100);
        temp.y = terrain.SampleHeight(temp) + 2f;
        player.transform.position = temp;
    }

    /**
     * Generates the Procedural Terrain
     **/
    void generateMap(Terrain terrain, float tileSize)
    {
        TerrainData terrainData = terrain.terrainData;
        // Generate Terrain using dataArray as height values

        Vector2 offset = Vector2.zero;
        float frq = Random.Range(500f, 1000f); // how smooth/rough? (low = rough, high = smooth)
        float amp = Random.Range(0.3f,0.45f); // how strong? (0.5 is ideal)
        int octaves = Random.Range(8, 15); // number of octaves
        // declare the data array
        float[,] dataArray = new float[m_terrainSize, m_terrainSize];

        // variables used in calculations
        float noise;
        float gain;

        int x;
        int y;
        int i;

        Vector2 sample;

        float randomNoise = Random.Range(0f, 0.1f);
        float randomGain = Random.Range(0.9f, 1.0f);

        // generate noise
        for (y = 0; y < m_terrainSize; y++)
        {
            for (x = 0; x < m_terrainSize; x++)
            {
                noise = randomNoise;
                gain = randomGain;

                for (i = 0; i < octaves; i++)
                {
                    sample.x = offset.x + (x * (gain / frq));
                    sample.y = offset.y + (y * (gain / frq));

                    noise += Mathf.PerlinNoise(sample.x, sample.y) * (amp / gain);
                    gain *= 2.0f;
                }

                dataArray[x, y] = noise;
            }
        }
        if (terrainData.heightmapResolution != m_terrainSize)
            terrainData.heightmapResolution = m_terrainSize;

        terrainData.SetHeights(0, 0, dataArray);
    }

    /**
    * Paints the splatmap ontop of the terrain
    **/
    void paintMap(Terrain terrain)
    {
        TerrainData terrainData = terrain.terrainData;

        // Splatmap data is stored internally as a 3d array of floats, so declare a new empty array ready for your custom splatmap data:
        float[,,] splatmapData = new float[terrainData.alphamapWidth, terrainData.alphamapHeight, terrainData.alphamapLayers];

        for (int y = 0; y < terrainData.alphamapHeight; y++)
        {
            for (int x = 0; x < terrainData.alphamapWidth; x++)
            {
                // Normalise x/y coordinates to range 0-1 
                float y_01 = (float)y / (float)terrainData.alphamapHeight;
                float x_01 = (float)x / (float)terrainData.alphamapWidth;

                // Sample the height at this location (note GetHeight expects int coordinates corresponding to locations in the heightmap array)
                float height = terrainData.GetHeight(Mathf.RoundToInt(y_01 * terrainData.heightmapHeight), Mathf.RoundToInt(x_01 * terrainData.heightmapWidth));

                // Calculate the normal of the terrain (note this is in normalised coordinates relative to the overall terrain dimensions)
                Vector3 normal = terrainData.GetInterpolatedNormal(y_01, x_01);

                // Calculate the steepness of the terrain
                float steepness = terrainData.GetSteepness(y_01, x_01);

                // Setup an array to record the mix of texture weights at this point
                float[] splatWeights = new float[terrainData.alphamapLayers];

                // CHANGE THE RULES BELOW TO SET THE WEIGHTS OF EACH TEXTURE ON WHATEVER RULES YOU WANT

                // Texture[0] has constant influence
                splatWeights[0] = 0.5f;
                // Texture[1] is stronger at lower altitudes
                splatWeights[1] = Mathf.Clamp01((terrainData.heightmapHeight - height));

                // Texture[2] stronger on flatter terrain
                // Note "steepness" is unbounded, so we "normalise" it by dividing by the extent of heightmap height and scale factor
                // Subtract result from 1.0 to give greater weighting to flat surfaces
                splatWeights[2] = 1.0f - Mathf.Clamp01(steepness * steepness / (terrainData.heightmapHeight / 5.0f));

                // Texture[3] increases with height but only on surfaces facing positive Z axis 
                splatWeights[3] = height * Mathf.Clamp01(normal.z);

                // Sum of all textures weights must add to 1, so calculate normalization factor from sum of weights
                float z = splatWeights.Sum();

                // Loop through each terrain texture
                for (int i = 0; i < terrainData.alphamapLayers; i++)
                {

                    // Normalize so that sum of all texture weights = 1
                    splatWeights[i] /= z;

                    // Assign this point to the splatmap array
                    splatmapData[x, y, i] = splatWeights[i];
                }
            }
        }

        // Finally assign the new splatmap to the terrainData:
        terrainData.SetAlphamaps(0, 0, splatmapData);
    }


    public GameObject m_tree0, m_tree1;

    void fillTreeInstances(Terrain terrain)
    {
        terrain.treeDistance = Random.Range(1000f,2000f); //The distance at which trees will no longer be drawn
        terrain.treeBillboardDistance = Random.Range(600f, 1000f); //The distance at which trees meshes will turn into tree billboards
        terrain.treeCrossFadeLength = Random.Range(15f, 25f); //As trees turn to billboards there transform is rotated to match the meshes, a higher number will make this transition smoother
        terrain.treeMaximumFullLODCount = ((int) Random.Range(450f, 550f)); //The maximum number of trees that will be drawn in a certain area. 

        TreePrototype[] m_treeProtoTypes = new TreePrototype[2];

        m_treeProtoTypes[0] = new TreePrototype();
        m_treeProtoTypes[0].prefab = m_tree0;

        m_treeProtoTypes[1] = new TreePrototype();
        m_treeProtoTypes[1].prefab = m_tree1;

        terrain.terrainData.treePrototypes = m_treeProtoTypes;

        PerlinNoise m_treeNoise = new PerlinNoise(Random.Range(0, 100));
 
        int m_treeSpacing = ((int) Random.Range(128f, 256f)); // 96 to 256
        float m_treeFrq = m_terrainSize;

        for (int x = 0; x < m_terrainSize; x += m_treeSpacing)
        {
            for (int z = 0; z < m_terrainSize; z += m_treeSpacing)
            {

                float unit = 1.0f / (m_terrainSize - 1);

                float offsetX = Random.value * unit * m_treeSpacing;
                float offsetZ = Random.value * unit * m_treeSpacing;

                float normX = x * unit + offsetX;
                float normZ = z * unit + offsetZ;

                // Get the steepness value at the normalized coordinate.
                float angle = terrain.terrainData.GetSteepness(normX, normZ);

                // Steepness is given as an angle, 0..90 degrees. Divide
                // by 90 to get an alpha blending value in the range 0..1.
                float frac = angle / 90.0f;

                if (frac < 0.2f) //make sure tree are not on steep slopes
                {
                    float worldPosX = x;
                    float worldPosZ = z;

                    float noise = m_treeNoise.FractalNoise2D(worldPosX, worldPosZ, 3, m_treeFrq, 1.0f);
                    float ht = terrain.terrainData.GetInterpolatedHeight(normX, normZ);

                    if (noise > 0.0f)
                    {
                        TreeInstance temp = new TreeInstance();
                        temp.position = new Vector3(normX, ht, normZ);
                        temp.prototypeIndex = Random.Range(0, 2);
                        temp.widthScale = 1f;
                        temp.heightScale = 1f;
                        temp.color = Color.white;
                        temp.lightmapColor = Color.white;
                       

                        terrain.AddTreeInstance(temp);
                    }
                }

            }
        }

        terrain.terrainData.SetHeights(0, 0, new float[,] { { } });
        terrain.Flush();

        (terrain.GetComponent(typeof(TerrainCollider)) as TerrainCollider).enabled = false;
        (terrain.GetComponent(typeof(TerrainCollider)) as TerrainCollider).enabled = true;
    }

    public Texture2D m_detail0, m_detail1;
    public DetailRenderMode detailMode;

    private int m_detailObjectDistance = 2048; //The distance at which details will no longer be drawn
    private float m_detailObjectDensity = 32; //Creates more dense details within patch
    private int m_detailResolutionPerPatch = 32; //The size of detail patch. A higher number may reduce draw calls as details will be batch in larger patches
    private float m_wavingGrassStrength = 0.4f;
    private float m_wavingGrassAmount = 0.2f;
    private float m_wavingGrassSpeed = 0.4f;
    private Color m_wavingGrassTint = Color.white;
    private Color m_grassHealthyColor = Color.gray;
    private Color m_grassDryColor = Color.white;
    private float m_detailFrq = 256;

    void fillDetailMap(Terrain terrain)
    {
        DetailPrototype[] m_detailProtoTypes = new DetailPrototype[0];

        terrain.terrainData.detailPrototypes = m_detailProtoTypes;

        m_detailProtoTypes = new DetailPrototype[2];

        float minHeight = 0.25f;
        float maxHeight = 1f;
        float minWidth = 0.25f;
        float maxWidth = 1f;

        m_detailProtoTypes[0] = new DetailPrototype();
        m_detailProtoTypes[0].prototypeTexture = m_detail0;
        m_detailProtoTypes[0].renderMode = detailMode;
        m_detailProtoTypes[0].healthyColor = m_grassHealthyColor;
        m_detailProtoTypes[0].dryColor = m_grassDryColor;
        m_detailProtoTypes[0].minHeight = minHeight;
        m_detailProtoTypes[0].maxHeight = maxHeight;
        m_detailProtoTypes[0].minWidth = minWidth;
        m_detailProtoTypes[0].maxWidth = maxWidth;


        m_detailProtoTypes[1] = new DetailPrototype();
        m_detailProtoTypes[1].prototypeTexture = m_detail1;
        m_detailProtoTypes[1].renderMode = detailMode;
        m_detailProtoTypes[1].healthyColor = m_grassHealthyColor;
        m_detailProtoTypes[1].dryColor = m_grassDryColor;
        m_detailProtoTypes[1].minHeight = minHeight;
        m_detailProtoTypes[1].maxHeight = maxHeight;
        m_detailProtoTypes[1].minWidth = minWidth;
        m_detailProtoTypes[1].maxWidth = maxWidth;

        terrain.terrainData.detailPrototypes = m_detailProtoTypes;

        PerlinNoise m_detailNoise = new PerlinNoise(Random.Range(0, 1000));

        //each layer is drawn separately so if you have a lot of layers your draw calls will increase 
        int[,] detailMap0 = new int[m_terrainSize, m_terrainSize];
        int[,] detailMap1 = new int[m_terrainSize, m_terrainSize];
       
        for (int x = 0; x < m_terrainSize; x++)
        {
            for (int z = 0; z < m_terrainSize; z++)
            {
                detailMap0[z, x] = 0;
                detailMap1[z, x] = 0;

                // Get the steepness value at the normalized coordinate.
                float angle = terrain.terrainData.GetSteepness(x, z);

                // Steepness is given as an angle, 0..90 degrees. Divide
                // by 90 to get an alpha blending value in the range 0..1.
                float frac = angle / 90.0f;

                if (frac < 0.2f)
                {
                    float worldPosX = x;
                    float worldPosZ = z;

                    float noise = m_detailNoise.FractalNoise2D(worldPosX, worldPosZ, 3, m_detailFrq, 1.0f);

                    if (noise > 0.0f)
                    {
                        float rnd = Random.value;
                        if (rnd < 0.5f)
                            detailMap0[z, x] = 1;
                        else
                            detailMap1[z, x] = 1;
                    }
                }

            }
        }

        terrain.terrainData.wavingGrassStrength = m_wavingGrassStrength;
        terrain.terrainData.wavingGrassAmount = m_wavingGrassAmount;
        terrain.terrainData.wavingGrassSpeed = m_wavingGrassSpeed;
        terrain.terrainData.wavingGrassTint = m_wavingGrassTint;
        terrain.detailObjectDensity = m_detailObjectDensity;
        terrain.detailObjectDistance = m_detailObjectDistance;
        terrain.terrainData.SetDetailResolution(m_terrainSize, m_detailResolutionPerPatch);

        terrain.terrainData.SetDetailLayer(0, 0, 0, detailMap0);
        terrain.terrainData.SetDetailLayer(0, 0, 1, detailMap1);
        terrain.Flush();
    }
}

