using UnityEditor;

namespace HouseEdge.Editor
{
    // the character layers and room photos are UI images: no mipmaps, clamped edges, and characters stay uncompressed
    // so their flat colours don't band
    public sealed class ArtImport : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            bool characters = assetPath.Contains("/Resources/Characters/");
            bool backgrounds = assetPath.Contains("/Resources/Backgrounds/");
            if (!characters && !backgrounds) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = characters;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = characters ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = backgrounds ? 2048 : 1024;
        }
    }
}
