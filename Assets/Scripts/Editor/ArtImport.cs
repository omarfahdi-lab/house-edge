using UnityEditor;

namespace HouseEdge.Editor
{
    // the character layers and room photos are UI images: no mipmaps, clamped edges, and characters stay uncompressed
    // so their flat colours don't band
    public sealed class ArtImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            bool characters = assetPath.Contains("/Resources/Characters/") || assetPath.Contains("/Resources/Portraits/") || assetPath.Contains("/Resources/Gallery/");
            bool backgrounds = assetPath.Contains("/Resources/Backgrounds/");
            if (!characters && !backgrounds) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = assetPath.Contains("/Resources/Characters/");
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            bool gallery = assetPath.Contains("/Resources/Gallery/");
            importer.textureCompression = assetPath.Contains("/Resources/Characters/") ? TextureImporterCompression.Uncompressed
                : gallery ? TextureImporterCompression.Compressed : TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = backgrounds ? 2048 : gallery ? 512 : 1024;
            // hundreds of gallery paintings: crunch them so the build stays small
            importer.crunchedCompression = gallery;
            importer.compressionQuality = 60;
        }
    }
}
