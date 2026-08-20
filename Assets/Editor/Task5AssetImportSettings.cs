using UnityEditor;
using UnityEngine;

public sealed class Task5AssetImportSettings : AssetPostprocessor
{
    private const string Root = "Assets/Resources/Task5/";

    private void OnPreprocessTexture()
    {
        bool isArt = assetPath.StartsWith(Root + "Art/");
        bool isEnvironmentSprite = assetPath.Contains("/Environment/tree_");
        bool isRuntimeAtlas = assetPath.StartsWith(Root + "UI/");
        if (!isArt && !isEnvironmentSprite && !isRuntimeAtlas) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.textureType = isRuntimeAtlas ? TextureImporterType.Default : TextureImporterType.Sprite;
        if (isArt || isEnvironmentSprite) importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = isEnvironmentSprite ? 256f : 64f;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = isRuntimeAtlas;
        importer.textureCompression = TextureImporterCompression.Compressed;
    }

    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Root + "Audio/")) return;
        AudioImporter importer = (AudioImporter)assetImporter;
        importer.forceToMono = true;
        importer.loadInBackground = false;
        bool isMusic = assetPath.EndsWith("/bgm.mp3");
        importer.defaultSampleSettings = new AudioImporterSampleSettings
        {
            loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad,
            compressionFormat = AudioCompressionFormat.Vorbis,
            quality = isMusic ? 0.6f : 0.5f,
            sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate,
            preloadAudioData = true
        };
    }
}
