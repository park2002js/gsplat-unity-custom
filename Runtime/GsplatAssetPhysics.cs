using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gsplat
{
    // 렌더링과 독립적으로 물리/충돌 데이터만 다루는 투트랙 에셋
    public class GsplatAssetPhysics : ScriptableObject
    {
        [Header("Physics Transform Data (Sorted for BVH)")]
        [HideInInspector] public Vector3[] positions;
        [HideInInspector] public Vector4[] rotations;
        [HideInInspector] public Vector3[] scales;

        // 차후 BVH 트리의 리프 노드 정보(인덱스 범위 등)를 담을 변수들이 이곳에 추가될 예정
        // public BVHNodeInfo[] bvhNodes; 

        public void BuildPhysicsData(GsplatAssetUncompressed source)
        {
            Debug.Log("[Physics] 물리 충돌용 투트랙 뼈대 생성 시작...");

            int count = (int)source.SplatCount;
            
            // VRAM에 1차원 배열로 올리기 위한 배열 초기화 [cite: 9]
            positions = new Vector3[count];
            rotations = new Vector4[count];
            scales = new Vector3[count];

            // -------------------------------------------------------------
            // [현재 뼈대 단계]
            // 일단은 원본 데이터를 그대로 복사만 해둡니다.
            // 다음 스텝에서 이 부분을 지우고 'BVH 기반 재정렬(Sorting)' 로직이 들어갑니다.
            // -------------------------------------------------------------
            for (int i = 0; i < count; i++)
            {
                positions[i] = source.Positions[i];
                rotations[i] = source.Rotations[i];
                scales[i] = source.Scales[i];
            }

            Debug.Log($"[Physics] {count}개의 가우시안 물리 데이터 에셋화 완료!");
        }
    }
}