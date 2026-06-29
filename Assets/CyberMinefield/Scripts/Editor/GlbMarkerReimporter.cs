using System.IO;
using UnityEditor;
using UnityEngine;

namespace CyberMinefield.Editor
{
    [InitializeOnLoad]
    internal static class GlbMarkerReimporter
    {
        private const string SessionKey = "CyberMinefield.GlbMarkerReimporter.Checked.v2";

        static GlbMarkerReimporter()
        {
            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += ReimportMarkerGlbs;
        }

        private static void ReimportMarkerGlbs()
        {
            ReimportIfStillDefault("Assets/CyberMinefield/Resources/Models/flagpole_3d_model.glb");
            ReimportIfStillDefault("Assets/CyberMinefield/Resources/Models/pixel_art_creature_3d_model.glb");
            ReimportIfStillDefault("Assets/CyberMinefield/Resources/Models/CartoonCharacter/cartoon_character_player.glb");
        }

        private static void ReimportIfStillDefault(string assetPath)
        {
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null || importer.GetType().Name != "DefaultImporter")
            {
                return;
            }

            string metaPath = assetPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Debug.Log($"Reimported GLB marker with glTFast: {assetPath}");
        }
    }
}
