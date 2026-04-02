// Copyright (c) 2025 Yize Wu
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Gsplat.Editor
{
    [ScriptedImporter(1, "ply")]
    public class GsplatImporter : ScriptedImporter
    {
        // [modified] fixed Compression Mode by Uncompressed
        public CompressionMode Compression = CompressionMode.Uncompressed;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            GsplatAsset gsplatAsset = Compression switch
            {
                CompressionMode.Uncompressed => ScriptableObject.CreateInstance<GsplatAssetUncompressed>(),
                CompressionMode.Spark => ScriptableObject.CreateInstance<GsplatAssetSpark>(),
                _ => throw new ArgumentOutOfRangeException()
            };

            try
            {
                gsplatAsset.LoadFromPly(ctx.assetPath, (info, progress) => EditorUtility.DisplayProgressBar(
                    "Importing Gsplat Asset", info, progress));
            }
            catch (Exception e)
            {
                if (GsplatSettings.Instance.ShowImportErrors)
                    Debug.LogError($"{ctx.assetPath} import error: {e.Message}");
                return;
            }

            // origin rendering asset
            ctx.AddObjectToAsset("gsplatAsset", gsplatAsset);
            ctx.SetMainObject(gsplatAsset);

            // =========================================================
            // [add] two track Frame: Add Physics Asset as Sub-Asset
            if (gsplatAsset is GsplatAssetUncompressed uncompressedSource)
            {
                // Physics Asset Instance
                GsplatAssetPhysics physicsAsset = ScriptableObject.CreateInstance<GsplatAssetPhysics>();
                physicsAsset.name = "PhysicsCollisionData";
                
                // Build Physics Data (from origin source)
                physicsAsset.BuildPhysicsData(uncompressedSource);

                // Add as Sub-Asset and Bundle source data into a single physics file
                ctx.AddObjectToAsset("physicsAsset", physicsAsset);
            }
            // =========================================================

        }
    }


    public class GsplatReferenceRestorer : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var plyReimported = importedAssets.Any(path => path.EndsWith(".ply", StringComparison.OrdinalIgnoreCase));
            if (!plyReimported) return;

            var renderers = UnityEngine.Object.FindObjectsByType<GsplatRenderer>(FindObjectsSortMode.None);
            foreach (var renderer in renderers)
            {
                if (renderer.GsplatAsset || string.IsNullOrEmpty(renderer.AssetGuid)) continue;
                var path = AssetDatabase.GUIDToAssetPath(renderer.AssetGuid);
                if (string.IsNullOrEmpty(path)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<GsplatAsset>(path);
                if (!asset) continue;
                renderer.GsplatAsset = asset;
                renderer.ReloadAsset();
                EditorUtility.SetDirty(renderer);
            }
        }
    }
}