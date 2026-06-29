using System.IO;
using UnityEditor;
using UnityEngine;

namespace CyberMinefield.Editor
{
    [InitializeOnLoad]
    internal static class CyberMinefieldBuildAssetPreparer
    {
        private const string MaterialFolder = "Assets/CyberMinefield/Resources/Materials";
        private const string TileMaterialPath = MaterialFolder + "/CyberTileRuntime.mat";
        private const string CompressionSessionKey = "CyberMinefield.BuildAssetCompression.v1";

        static CyberMinefieldBuildAssetPreparer()
        {
            EditorApplication.delayCall += EnsureBuildAssets;
        }

        private static void EnsureBuildAssets()
        {
            EnsureTileMaterial();

            if (SessionState.GetBool(CompressionSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(CompressionSessionKey, true);
            OptimizeImportedAssets();
        }

        private static void EnsureTileMaterial()
        {
            Shader shader = FindTileShader();
            if (shader == null)
            {
                Debug.LogWarning("Cyber Minefield could not find a supported tile shader for build.");
                return;
            }

            EnsureFolder(MaterialFolder);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(TileMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, TileMaterialPath);
            }

            material.shader = shader;
            material.name = "CyberTileRuntime";
            material.enableInstancing = true;
            material.SetColor("_BaseColor", new Color(0.28f, 0.45f, 0.38f));
            material.SetColor("_Color", new Color(0.28f, 0.45f, 0.38f));
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        private static Shader FindTileShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Standard");
        }

        private static void EnsureFolder(string folderPath)
        {
            string current = "Assets";
            string[] parts = folderPath.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void OptimizeImportedAssets()
        {
            int changedCount = 0;
            changedCount += OptimizeTextures("Assets/CyberMinefield/Resources");
            changedCount += OptimizeModels("Assets/CyberMinefield");
            changedCount += OptimizeAudio("Assets/CyberMinefield/Resources/Audio");

            if (changedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"Cyber Minefield optimized {changedCount} imported assets for build performance.");
            }
        }

        private static int OptimizeTextures(string root)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                return 0;
            }

            int changedCount = 0;
            string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { root });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                int maxSize = path.Contains("/UI/") ? 512 : 1024;
                bool changed = false;
                bool useMipmaps = !path.Contains("/UI/");
                if (importer.isReadable)
                {
                    importer.isReadable = false;
                    changed = true;
                }

                if (importer.mipmapEnabled != useMipmaps)
                {
                    importer.mipmapEnabled = useMipmaps;
                    changed = true;
                }

                if (importer.streamingMipmaps != useMipmaps)
                {
                    importer.streamingMipmaps = useMipmaps;
                    changed = true;
                }

                if (importer.maxTextureSize != maxSize)
                {
                    importer.maxTextureSize = maxSize;
                    changed = true;
                }

                if (importer.compressionQuality != 90)
                {
                    importer.compressionQuality = 90;
                    changed = true;
                }

                if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
                {
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    changed = true;
                }

                if (!importer.crunchedCompression)
                {
                    importer.crunchedCompression = true;
                    changed = true;
                }

                changed |= ConfigureTexturePlatform(importer, "DefaultTexturePlatform", maxSize);
                changed |= ConfigureTexturePlatform(importer, "Standalone", maxSize);

                if (!changed)
                {
                    continue;
                }

                importer.SaveAndReimport();
                changedCount++;
            }

            return changedCount;
        }

        private static bool ConfigureTexturePlatform(TextureImporter importer, string platformName, int maxSize)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(platformName);
            bool changed = false;

            if (settings.maxTextureSize != maxSize)
            {
                settings.maxTextureSize = maxSize;
                changed = true;
            }

            if (settings.textureCompression != TextureImporterCompression.CompressedHQ)
            {
                settings.textureCompression = TextureImporterCompression.CompressedHQ;
                changed = true;
            }

            if (!settings.crunchedCompression)
            {
                settings.crunchedCompression = true;
                changed = true;
            }

            if (settings.compressionQuality != 90)
            {
                settings.compressionQuality = 90;
                changed = true;
            }

            if (!settings.overridden && platformName != "DefaultTexturePlatform")
            {
                settings.overridden = true;
                changed = true;
            }

            if (changed)
            {
                importer.SetPlatformTextureSettings(settings);
            }

            return changed;
        }

        private static int OptimizeModels(string root)
        {
            int changedCount = 0;
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { root });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }

                bool changed = false;
                if (importer.meshCompression != ModelImporterMeshCompression.Medium)
                {
                    importer.meshCompression = ModelImporterMeshCompression.Medium;
                    changed = true;
                }

                if (!importer.optimizeMeshPolygons)
                {
                    importer.optimizeMeshPolygons = true;
                    changed = true;
                }

                if (!importer.optimizeMeshVertices)
                {
                    importer.optimizeMeshVertices = true;
                    changed = true;
                }

                if (importer.importCameras)
                {
                    importer.importCameras = false;
                    changed = true;
                }

                if (importer.importLights)
                {
                    importer.importLights = false;
                    changed = true;
                }

                if (importer.importVisibility)
                {
                    importer.importVisibility = false;
                    changed = true;
                }

                if (importer.importBlendShapes)
                {
                    importer.importBlendShapes = false;
                    changed = true;
                }

                if (!changed)
                {
                    continue;
                }

                importer.SaveAndReimport();
                changedCount++;
            }

            return changedCount;
        }

        private static int OptimizeAudio(string root)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                return 0;
            }

            int changedCount = 0;
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { root });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                {
                    continue;
                }

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                bool isMusic = path.ToLowerInvariant().Contains("music") || new FileInfo(path).Length > 512 * 1024;
                AudioClipLoadType desiredLoadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;

                bool changed = false;
                if (settings.loadType != desiredLoadType)
                {
                    settings.loadType = desiredLoadType;
                    changed = true;
                }

                if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
                {
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    changed = true;
                }

                float desiredQuality = isMusic ? 0.55f : 0.75f;
                if (Mathf.Abs(settings.quality - desiredQuality) > 0.001f)
                {
                    settings.quality = desiredQuality;
                    changed = true;
                }

                if (settings.preloadAudioData == isMusic)
                {
                    settings.preloadAudioData = !isMusic;
                    changed = true;
                }

                if (!changed)
                {
                    continue;
                }

                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                changedCount++;
            }

            return changedCount;
        }

    }
}
