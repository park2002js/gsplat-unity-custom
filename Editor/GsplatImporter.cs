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
        // [수정] 기본 압축 방식을 Uncompressed로 고정
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

            // 원본 렌더링 에셋 등록
            ctx.AddObjectToAsset("gsplatAsset", gsplatAsset);
            ctx.SetMainObject(gsplatAsset);

            // =========================================================
            // [추가] 투트랙 뼈대: 물리 전용 에셋을 Sub-Asset으로 추가
            if (gsplatAsset is GsplatAssetUncompressed uncompressedSource)
            {
                // 물리 전용 에셋 인스턴스 생성
                GsplatAssetPhysics physicsAsset = ScriptableObject.CreateInstance<GsplatAssetPhysics>();
                physicsAsset.name = "PhysicsCollisionData";
                
                // 원본 데이터를 넘겨주며 물리 데이터 구축 함수 실행
                physicsAsset.BuildPhysicsData(uncompressedSource);

                // Sub-Asset으로 추가하여 하나의 파일로 묶음
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