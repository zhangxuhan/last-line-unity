using UnityEditor;
using UnityEngine;

public sealed class Task5AssetImportSettings : AssetPostprocessor
{
    private const string Root = "Assets/Resources/Task5/";

    private void OnPreprocessTexture()
    {
        bool isArt = assetPath.StartsWith(Root + "Art/");
        bool isUi = assetPath.StartsWith(Root + "UI/");
        if (!isArt && !isUi) return;
        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = isUi ? TextureImporterType.Default : TextureImporterType.Sprite;
        if (isArt) importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 64f;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.isReadable = isUi;
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
