using UnityEditor;
using UnityEngine;

public sealed class TakeoverAssetPostprocessor : AssetPostprocessor
{
    private const string SpriteRoot = "Assets/AssetRaw/Actor/Takeover/Sprites/";
    private const string MapRoot = "Assets/AssetRaw/Scenes/Takeover/Maps/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(SpriteRoot) && !assetPath.StartsWith(MapRoot)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = assetPath.StartsWith(MapRoot) ? 2048 : 512;
        importer.spritePixelsPerUnit = 100f;
    }
}
