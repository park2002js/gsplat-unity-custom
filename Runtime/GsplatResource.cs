using System.Runtime.InteropServices;
using UnityEngine;

namespace Gsplat
{
    public abstract class GsplatResource
    {
        public bool Uploaded;
        public uint UploadedCount;
        public abstract void Dispose();
    }

    public class GsplatResourceUncompressed : GsplatResource
    {
        public GraphicsBuffer PositionBuffer { get; private set; }
        public GraphicsBuffer ScaleBuffer { get; private set; }
        public GraphicsBuffer RotationBuffer { get; private set; }
        public GraphicsBuffer ColorBuffer { get; private set; }
        public GraphicsBuffer SHBuffer { get; private set; }

        public GsplatResourceUncompressed(uint splatCount, byte shBands)
        {
            if (splatCount == 0)
                return;
            PositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)splatCount,
                Marshal.SizeOf(typeof(Vector3)));
            ScaleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)splatCount,
                Marshal.SizeOf(typeof(Vector3)));
            RotationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)splatCount,
                Marshal.SizeOf(typeof(Vector4)));
            ColorBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)splatCount,
                Marshal.SizeOf(typeof(Vector4)));
            if (shBands > 0)
                SHBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    GsplatUtils.SHBandsToCoefficientCount(shBands) * (int)splatCount, Marshal.SizeOf(typeof(Vector3)));
        }

        public override void Dispose()
        {
            PositionBuffer?.Dispose();
            PositionBuffer = null;
            ScaleBuffer?.Dispose();
            ScaleBuffer = null;
            RotationBuffer?.Dispose();
            RotationBuffer = null;
            ColorBuffer?.Dispose();
            ColorBuffer = null;
            SHBuffer?.Dispose();
            SHBuffer = null;
        }
    }

    public class GsplatResourceSpark : GsplatResource
    {
        public GraphicsBuffer PackedSplatsBuffer { get; private set; }
        public GraphicsBuffer PackedSH1Buffer { get; private set; }
        public GraphicsBuffer PackedSH2Buffer { get; private set; }
        public GraphicsBuffer PackedSH3Buffer { get; private set; }

        public GsplatResourceSpark(uint splatCount, byte shBands) : base()
        {
            if (splatCount == 0)
                return;
            PackedSplatsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)splatCount,
                sizeof(uint) * 4);
            if (shBands >= 1)
                PackedSH1Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)splatCount,
                    sizeof(uint) * 2);
            if (shBands >= 2)
                PackedSH2Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)splatCount,
                    sizeof(uint) * 4);
            if (shBands >= 3)
                PackedSH3Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, (int)splatCount,
                    sizeof(uint) * 4);
        }

        public override void Dispose()
        {
            PackedSplatsBuffer?.Dispose();
            PackedSplatsBuffer = null;
            PackedSH1Buffer?.Dispose();
            PackedSH1Buffer = null;
            PackedSH2Buffer?.Dispose();
            PackedSH2Buffer = null;
            PackedSH3Buffer?.Dispose();
            PackedSH3Buffer = null;
        }
    }

    /*
        물리 계산을 위해 기존의 코드에서 추가된 Class
        GsplatPhysics.compute에 정의된 Physics Data에 해당되는 4개의 Buffer에 데이터를 저장하기 위해
        4개의 Buffer를 생성하고 관리한다.

        데이터를 할당하는 역할은 GsplatAssetPhysics.cs에서 담당한다.
    */
    public class GsplatResourcePhysics : GsplatResource
    {
        public GraphicsBuffer BVHBuffer { get; private set; }
        public GraphicsBuffer PhysPositionBuffer { get; private set; }
        public GraphicsBuffer PhysRotationBuffer { get; private set; }
        public GraphicsBuffer PhysScaleBuffer { get; private set; }

        public GsplatResourcePhysics(int validCount, int bvhNodeCount)
        {
            if (validCount == 0 || bvhNodeCount == 0) return;

            // 1. BVH 트리 버퍼 (32 바이트)
            BVHBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bvhNodeCount, Marshal.SizeOf(typeof(BVHNode1D)));
            
            // 2. 물리 데이터 버퍼들 (Vector3: 12바이트, Vector4: 16바이트)
            PhysPositionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, validCount, Marshal.SizeOf(typeof(Vector3)));
            PhysRotationBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, validCount, Marshal.SizeOf(typeof(Vector4)));
            PhysScaleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, validCount, Marshal.SizeOf(typeof(Vector3)));
        }

        public override void Dispose()
        {
            BVHBuffer?.Dispose();
            BVHBuffer = null;
            
            PhysPositionBuffer?.Dispose();
            PhysPositionBuffer = null;

            PhysRotationBuffer?.Dispose();
            PhysRotationBuffer = null;

            PhysScaleBuffer?.Dispose();
            PhysScaleBuffer = null;
        }
    }
}
