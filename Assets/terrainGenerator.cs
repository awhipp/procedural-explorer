using UnityEngine;

public class terrainGenerator : MonoBehaviour
{
    /**
     * Initialization Method
     **/
    void Start()
    {
        initTerrain();
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
        Terrain terrain = this.GetComponent<Terrain>();
        generateMap(terrain, Random.Range(3f, 7f));
        paintMap(terrain);

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
        float frq = Random.Range(500f, 1500f); // how smooth/rough? (low = rough, high = smooth)
        float amp = Random.Range(0.3f,0.5f); // how strong? (0.5 is ideal)
        int octaves = Random.Range(8, 15); // number of octaves
        // declare the data array
        int size = Mathf.ClosestPowerOfTwo(Random.Range(512, 2048)); // terrain width
        float[,] dataArray = new float[size, size];

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
        for (y = 0; y < size; y++)
        {
            for (x = 0; x < size; x++)
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
        if (terrainData.heightmapResolution != size)
            terrainData.heightmapResolution = size;

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
                float z = splatWeights[0] + splatWeights[1] + splatWeights[2] + splatWeights[3];

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

}

