using UnityEditor;

namespace BobosJourney.EditorTools
{
    /// <summary>Drawings dropped into Resources/Story import full-size and uncropped (1920x1080 is not a power of two).</summary>
    public class StoryImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Story/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            // keep the line art crisp in the web build, which caps other textures at 1024
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "WebGL",
                overridden = true,
                maxTextureSize = 2048,
                format = TextureImporterFormat.DXT1Crunched,
                compressionQuality = 70,
            });
        }
    }
}
