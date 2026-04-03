using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Gsplat
{
    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct BVHNode1D
    {
        // 32-Byte 맞추기 (16-byte = float4 1개)
        public Vector3 boundsMin;  // 12 byte
        public int data1;          // 4 byte

        public Vector3 boundsMax;  // 12 byte
        public int data2;          // 4 byte
        
        // ====================================================================
        // 메모리 재활용을 위한 Data1, Data2 규칙
        // data2가 양수(> 0)면 부모 노드 -> data1은 왼쪽 자식 인덱스, data2는 오른쪽 자식 인덱스
        // data2가 음수(<= 0)면 리프 노드 -> data1은 가우시안 시작 인덱스, data2는 -(가우시안 개수)
        // ====================================================================

        // 코드 가독성 및 개발을 위한 'helper property'들 (람다 연산자로 정의되어서, 구조체 용량을 차지하지 않음)
        public bool IsLeaf => data2 <= 0;
        
        // 리프 노드일 때 사용하는 정보
        public int StartIndex => data1;
        public int Count => -data2; // 음수로 저장해 둔 개수를 다시 양수로 바꿔서 읽음

        // 부모(Internal) 노드일 때 사용하는 정보
        public int LeftChild => data1;
        public int RightChild => data2;
    }


    // 렌더링과 독립적으로 물리/충돌 데이터만 다루는 투트랙 에셋
    public class GsplatAssetPhysics : ScriptableObject
    {
        // Physics Transform Data (Sorted for BVH)
        [HideInInspector] public Vector3[] positions;
        [HideInInspector] public Vector4[] rotations;
        [HideInInspector] public Vector3[] scales;


        // Flat BVH Tree (for GPU VRAM)
        [HideInInspector] public BVHNode1D[] flatTree; 

        // 빌드용 임시 데이터 구조체
        private struct BuildData
        {
            public int originalIndex;   // 위의 Transform 배열에서 AssetUncompressed에 정의된 진짜 데이터를 찾아오기 위한 '원래 번호'
            public Bounds bounds;       // AABB 박스 저장
            public Vector3 center;      // 가우시안 점의 중심점 좌표
        }

        public void BuildPhysicsData(GsplatAssetUncompressed source)
        {
            Debug.Log("[Physics] BVH 기반 물리 데이터 정렬 및 구축 시작...");
            int count = (int)source.SplatCount;

            // 1. 빌드용 임시 데이터 생성 
            // 모든 가우시안의 정보를 각각 원소로써 저장하기 위한 빈 배열 생성
            BuildData[] buildData = new BuildData[count];

            // 원본 데이터를 바탕으로 내용 복사하여 BuildData 배열의 각 원소마다 저장
            for (int i = 0; i < count; i++)
            {
                buildData[i].originalIndex = i;
                buildData[i].center = source.Positions[i];
                
                // 가우시안의 Scale을 바탕으로 AABB를 계산한 뒤 저장
                Vector3 extents = source.Scales[i] * 2.0f; // 충돌 마진을 위해 스케일보다 더 큰 값을 사용
                buildData[i].bounds = new Bounds(source.Positions[i], extents);
            }

            // 2. 1차원 BVH 트리 리스트 생성 : 재귀 과정에서의 데이터 추가 편의를 위해 리스트로 선언함
            List<BVHNode1D> nodeList = new List<BVHNode1D>();

            // 3. 재귀적으로 트리 빌드 시작
            BuildRecursive(nodeList, buildData, 0, count, 0);

            // 4. 리스트를 배열로 변환하여 에셋에 저장
            //    : 이 배열은 BVH 트리를 탐색하기 위한 용도이고, 실제 데이터는 멤버 변수로 선언된 Vector 배열에 존재함
            flatTree = nodeList.ToArray();

            // 5. 트리 구조(인덱스 순서)에 맞춰 원본 데이터 재정렬
            positions = new Vector3[count];
            rotations = new Vector4[count];
            scales = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                int origIdx = buildData[i].originalIndex;
                positions[i] = source.Positions[origIdx];
                rotations[i] = source.Rotations[origIdx];
                scales[i] = source.Scales[origIdx];
            }

            Debug.Log($"[Physics] 총 {count}개의 가우시안으로 → BVH 노드 {flatTree.Length}개 생성 및 데이터 정렬 완료");
        }


        // BVH 재귀 구축 로직 (참고 : https://chaeso0.tistory.com/entry/%F0%9F%93%92%EA%B0%9C%EB%B0%9C%EC%9D%BC%EC%A7%80-15-BVHBounding-Volume-Hierarchy%EB%A5%BC-%EC%83%9D%EC%84%B1%ED%95%B4%EB%B3%B4%EC%9E%90)
        private int BuildRecursive(List<BVHNode1D> nodes, BuildData[] data, int start, int end, int depth)
        {
            // 1. 1차원 배열 리스트에 빈 노드를 추가한 뒤, 그 노드의 인덱스를 저장 (이후 최종 결정된 노드 정보가 저장될 위치가 됨)
            int nodeIndex = nodes.Count;
            nodes.Add(new BVHNode1D());

            // 2. 두 포인터 기법을 사용(start, end) : 담당할 가우시안 점들의 갯수와 그 시작을 start와 end로 정함
            int count = end - start;
            Bounds nodeBounds = data[start].bounds;
            for (int i = start + 1; i < end; i++)
            {
                // start에 대항하는 AABB 박스를 늘려서, start+1 ~ end-1까지 start의 박스 안에 포함되도록 만듦
                nodeBounds.Encapsulate(data[i].bounds);
            }

            // [종료 조건 1] 현재의 BVH 노드에 포함된 가우시안 수가 32개 이하거나 최대 깊이 도달 시, 강제로 leaf node화
            if (count <= 32 || depth >= 24)
            {
                var leafNode = new BVHNode1D();
                leafNode.boundsMin = nodeBounds.min;
                leafNode.boundsMax = nodeBounds.max;
                leafNode.data1 = start;     // 시작 인덱스
                leafNode.data2 = -count;    // 개수 (음수로 저장하여 리프임을 표시)
                nodes[nodeIndex] = leafNode;// 첫번째에서 생성한 빈 노드에 현재의 노드 정보를 저장
                return nodeIndex;           // 노드 번호를 부모에게 반환, 부모는 이 노드 번호를 자식으로의 연결 인덱스로 사용
            }

            // 3. SAH 샘플링 : 모든 점들을 중심으로 잘라볼 수 없으니 5등분하여 비교하는 버킷 샘플링 기법을 사용
            // 분할한 결과 중 best들을 저장할 임시 변수들
            int bestAxis = 0;                   // 가장 best인 축 (0:X, 1:Y, 2:Z)         
            float bestSplitValue = 0f;          // 그 축에서 best가 되는 자를 좌표값
            float bestCost = float.MaxValue;    // 비교 갱신 분기를 위한 현재 best 비용
            
            // 각 축마다 5번씩(총 15번) 잘라서 best가 되는 분할점을 찾음
            const int NUM_SAMPLES = 5;
            for (int axis = 0; axis < 3; axis++)
            {
                // 상자의 끝에서 끝을 5등분(1/6, 2/6, 3/6, 4/6, 5/6 지점)한 뒤 비교
                for (int i = 1; i <= NUM_SAMPLES; i++)
                {
                    float splitT = i / (NUM_SAMPLES + 1f);
                    float splitVal = Mathf.Lerp(nodeBounds.min[axis], nodeBounds.max[axis], splitT); // 실제 나뉘지게 될 좌표값을 Mathf.Lerp를 이용해 계산
                    
                    float cost = EvaluateSplitCost(data, start, end, axis, splitVal);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestAxis = axis;
                        bestSplitValue = splitVal;
                    }
                }
            }

            // [종료 조건 2 & 3] 비용 역전이 일어나면 재귀를 그만둔다.(bestCost >= parentCost)
            // 하지만 이 종료조건이 오히려 충분히 깊지 못한 단계에서 일어나 노드가 제대로 분할되지 않을 경우를 고려
            // count가 32보다 크면 무시하고 강제로 쪼갬 
            //      -> 즉 강제 종료할 분기가 필요없으므로 이곳에 있어야 할 비교 및 강제 중단 로직을 없앰

            // // 현재 노드를 쪼개지 않았을 때의 비용
            // float parentCost = EvaluateBoundsCost(nodeBounds.size, count);

            
            // 데이터 파티셔닝 (분할 기준값보다 작으면 왼쪽, 크면 오른쪽으로 배열 내에서 스왑)
            // 정렬되어 있지 않은 데이터가 기준 축을 중심으로 왼쪽과 오른쪽으로 나뉘어짐
            int mid = start;
            for (int i = start; i < end; i++)
            {
                if (data[i].center[bestAxis] < bestSplitValue)
                {
                    // Swap
                    var temp = data[i];
                    data[i] = data[mid];
                    data[mid] = temp;
                    mid++;
                }
            }

            // [예외 처리] 만약 모든 점이 한쪽으로 쏠려서 분할이 안 되었을 경우
            //  -> 무조건 절반으로 강제 보정한 뒤 자식이 이를 기준으로 분할하도록 함 (무한 루프 방지)
            if (mid == start || mid == end)
            {
                mid = start + (count / 2);
            }

            // 자식 노드 재귀 호출 (DFS 처럼 전위 순회 방식으로 List<BVHNode1D> nodes가 채워지게 됨)
            int leftChildIndex = BuildRecursive(nodes, data, start, mid, depth + 1);
            int rightChildIndex = BuildRecursive(nodes, data, mid, end, depth + 1);

            // 부모 노드 정보 업데이트
            var internalNode = new BVHNode1D();
            internalNode.boundsMin = nodeBounds.min;
            internalNode.boundsMax = nodeBounds.max;
            internalNode.data1 = leftChildIndex;
            internalNode.data2 = rightChildIndex; // 양수를 할당하여 부모 노드임을 표시
            nodes[nodeIndex] = internalNode;

            return nodeIndex;
        }

        // 블로그의 evaluateSplit 로직 응용
        // 주어진 좌표값을 기준 점으로 하여 새로 만들어지게 될 2개의 가상의 박스를 임의로 만들고 비교함
        private float EvaluateSplitCost(BuildData[] data, int start, int end, int axis, float splitValue)
        {
            // 단지 비교만을 위해 가상의 박스를 만듦
            Bounds boundsL = new Bounds();
            Bounds boundsR = new Bounds();
            int countL = 0, countR = 0;
            bool initL = false, initR = false; 

            for (int i = start; i < end; i++)
            {
                // 현재 가우시안의 중심점 좌표가 분할 기준 위치(splitValue)보다 작으면 왼쪽 박스에, 크면 오른쪽 박스에 포함
                if (data[i].center[axis] < splitValue)
                {
                    // 첫 번째 점이면 AABB 박스 초기화, 두 번째부터는 AABB 박스를 늘려서 포함시키는 구조
                    if (!initL) { boundsL = data[i].bounds; initL = true; }
                    else { boundsL.Encapsulate(data[i].bounds); }
                    countL++;
                }
                else
                {
                    if (!initR) { boundsR = data[i].bounds; initR = true; }
                    else { boundsR.Encapsulate(data[i].bounds); }
                    countR++;
                }
            }

            // 왼쪽 상자와 오른쪽 상자의 SAH 비용을 각각 구해서 더해, 주어진 기준점에 대한 총 비용을 반환한다.
            float costL = countL > 0 ? EvaluateBoundsCost(boundsL.size, countL) : 0;
            float costR = countR > 0 ? EvaluateBoundsCost(boundsR.size, countR) : 0;
            return costL + costR;
        }

        // AABB 박스의 Cost를 산정하는 SAH 핵심 공식
        // 블로그의 evaluateBoundsCost 로직을 개량
        private float EvaluateBoundsCost(Vector3 size, int count)
        {
            // 직육면체 겉넓이(표면적)의 절반만 구함 -> 절반이므로 어짜피 다른 한쪽도 동일하다는 보장
            float halfArea = (size.x * size.y) + (size.x * size.z) + (size.y * size.z);

            // "표면적 * 점의 갯수"를 SAH의 비용으로 결정
            return halfArea * count;
        }
        /*
            정석 비용 계산식
            비용 = (왼쪽 상자 표면적 ÷ 부모 상자 표면적) × 왼쪽 개수 + (오른쪽 표면적 ÷ 부모 상자 표면적) × 오른쪽 개수

            여기서 부모 표면적은 왼쪽과 오른쪽 상자에게 모두 동일함 + EvaluateSplitCost 속 15번의 기준점 비교를 하는 동안 항상 동일함 -> 제거해도 비교에는 영향 없음

            Half = xy+yz+zx 인데, 기존 로직대로라면 왼쪽 혹은 오른쪽의 박스 전체 크기를 곱하기 위해 이것에 *2를 해야 했음
                그러나 어짜피 15번의 모든 계산과정에서 *2를 한다면, 비교를 하는데 있어서는 이 *2를 하지 않아도됨
                따라서 '왼쪽 상자 반쪽 표면적 * 왼쪽 개수' + '오른쪽 상자 반쪽 표면적 * 오른쪽 개수'가 비교를 위한 최종 SAH 비용이 된 것

        */
    }
}