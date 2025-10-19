using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace TerrainGeneration
{
    public class TerrainHeightProcessor : MonoBehaviour
    {
        // --- User-facing settings ---
        [Header("Input Height Map Tiles")]
        [Tooltip("An array of 8 height map tiles. Assumes a 4x2 grid layout (a1, b1, c1, d1, a2, b2, c2, d2).")]
        [SerializeField] private Texture2D[] heightMapTiles = new Texture2D[8];

        // --- Public properties ---
        public RenderTexture processedHeightMap { get; private set; }

        // Grid dimensions based on the 8 tiles
        private const int GRID_WIDTH = 4;
        private const int GRID_HEIGHT = 2;
        // A safe upper limit for texture dimensions on modern GPUs.
        private const int MAX_TEXTURE_DIMENSION = 16384;


        /// <summary>
        /// Stitches 8 input tiles together and outputs a single large RenderTexture.
        /// Automatically downscales if the combined size exceeds GPU limits.
        /// </summary>
        public RenderTexture ProcessHeightMap()
        {
            if (!ValidateInputTiles())
            {
                return null;
            }

            // Get original dimensions from the first tile
            int tileWidth = heightMapTiles[0].width;
            int tileHeight = heightMapTiles[0].height;
            long totalOriginalWidth = (long)tileWidth * GRID_WIDTH;
            long totalOriginalHeight = (long)tileHeight * GRID_HEIGHT;

            // --- STAGE 0: Calculate Scaling Factor ---
            // Determine if the full-size stitched texture would exceed GPU limits.
            float scale = 1.0f;
            if (totalOriginalWidth > MAX_TEXTURE_DIMENSION)
            {
                scale = (float)MAX_TEXTURE_DIMENSION / totalOriginalWidth;
            }
            if (totalOriginalHeight > MAX_TEXTURE_DIMENSION)
            {
                // If height also exceeds, choose the smaller scale factor to maintain aspect ratio
                scale = Mathf.Min(scale, (float)MAX_TEXTURE_DIMENSION / totalOriginalHeight);
            }

            if (scale < 1.0f)
            {
                Debug.LogWarning($"Source textures are too large to be stitched together at full resolution. Downscaling to fit within {MAX_TEXTURE_DIMENSION}px limits.");
            }

            // Calculate the final, scaled dimensions for all textures.
            int finalWidth = Mathf.FloorToInt(totalOriginalWidth * scale);
            int finalHeight = Mathf.FloorToInt(totalOriginalHeight * scale);
            int scaledTileWidth = finalWidth / GRID_WIDTH;
            int scaledTileHeight = finalHeight / GRID_HEIGHT;


            // --- STAGE 1: Stitching ---
            // Create a temporary color map to stitch the tiles into.
            RenderTexture stitchedColorMap = ComputeHelper.CreateRenderTexture(
                finalWidth, finalHeight, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm, "Stitched Color Input"
            );

            // Create a smaller temporary render texture with the scaled tile dimensions to handle format conversion.
            RenderTexture tempTile = ComputeHelper.CreateRenderTexture(
                scaledTileWidth, scaledTileHeight, FilterMode.Bilinear, GraphicsFormat.R8G8B8A8_UNorm, "Temp Tile"
            );

            for (int i = 0; i < heightMapTiles.Length; i++)
            {
                // Blit the source tile to the smaller temp texture, which handles scaling and format conversion.
                Graphics.Blit(heightMapTiles[i], tempTile);

                // Copy the scaled tile into the correct region of the larger stitched map.
                int gridX = i % GRID_WIDTH;
                int gridY = i / GRID_WIDTH;
                int destX = gridX * scaledTileWidth;
                // Invert Y destination to account for texture coordinate differences.
                int destY = (GRID_HEIGHT - 1 - gridY) * scaledTileHeight;

                Graphics.CopyTexture(
                    tempTile, 0, 0, 0, 0, scaledTileWidth, scaledTileHeight,
                    stitchedColorMap, 0, 0, destX, destY
                );
            }

            // Clean up the small temporary texture.
            tempTile.Release();
            Destroy(tempTile);


            // --- STAGE 2: Final Format Conversion ---
            // Create the final output texture with the desired R16_UNorm format.
            GraphicsFormat format = GraphicsFormat.R16_UNorm;
            processedHeightMap = ComputeHelper.CreateRenderTexture(
                finalWidth, finalHeight, FilterMode.Trilinear, format, "World Heights", useMipMaps: true
            );

            // Blit from the temporary color map to the final processed map to convert the format.
            Graphics.Blit(stitchedColorMap, processedHeightMap);

            // --- Cleanup ---
            stitchedColorMap.Release();
            Destroy(stitchedColorMap);

            // Generate mipmaps for the final processed texture
            processedHeightMap.GenerateMips();
            return processedHeightMap;
        }


        /// <summary>
        /// Validates the input tiles to ensure they are ready for processing.
        /// </summary>
        private bool ValidateInputTiles()
        {
            if (heightMapTiles == null || heightMapTiles.Length != 8)
            {
                Debug.LogError("Height Map Tiles array must contain exactly 8 textures.");
                return false;
            }

            int firstTileWidth = 0;
            int firstTileHeight = 0;

            for (int i = 0; i < heightMapTiles.Length; i++)
            {
                if (heightMapTiles[i] == null)
                {
                    Debug.LogError($"Height Map Tile at index {i} is not assigned.");
                    return false;
                }

                if (i == 0)
                {
                    firstTileWidth = heightMapTiles[i].width;
                    firstTileHeight = heightMapTiles[i].height;
                }
                else if (heightMapTiles[i].width != firstTileWidth || heightMapTiles[i].height != firstTileHeight)
                {
                    Debug.LogError("All height map tiles must have the same dimensions.");
                    return false;
                }
            }
            return true;
        }


        /// <summary>
        /// Releases allocated resources.
        /// </summary>
        public void Release()
        {
            ComputeHelper.Release(processedHeightMap);
            if (heightMapTiles != null)
            {
                for (int i = 0; i < heightMapTiles.Length; i++)
                {
                    if (heightMapTiles[i] != null)
                    {
                        Resources.UnloadAsset(heightMapTiles[i]);
                    }
                }
            }
        }

        void OnDestroy() => Release();
    }
}

