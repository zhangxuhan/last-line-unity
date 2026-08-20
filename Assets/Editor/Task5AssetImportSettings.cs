using UnityEditor;
using UnityEngine;

public sealed class Task5AssetImportSettings : AssetPostprocessor
{
    private const string Root = "Assets/Resources/Task5/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root + "Art/")) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64f;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Compressed;
    }

    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Root + "Audio/")) return;
        AudioImporter importer = (AudioImporter)assetImporter;
        importer.forceToMono = true;
        importer.loadInBackground = false;
        importer.defaultSampleSettings = new AudioImporterSampleSettings
        {
            loadType = AudioClipLoadType.DecompressOnLoad,
            compressionFormat = AudioCompressionFormat.Vorbis,
            quality = 0.5f,
            sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate,
            preloadAudioData = true
        };
    }
}
