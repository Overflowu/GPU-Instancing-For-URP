using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Aperture.Instancing.Runtime
{
    internal class SegmentedStructBuffer
    {
        private static ComputeShader s_CopyMatrix4x4Compute = Resources.Load<ComputeShader>("Shaders/CopyMatrix4x4");
        private static ComputeShader s_CopyVector4Compute = Resources.Load<ComputeShader>("Shaders/CopyVector4");

        public int Count
        {
            get
            {
                return m_Count;
            }
        }
        private int m_Count;

        public bool IsDirty
        {
            get
            {
                return m_IsDirty;
            }
        }
        private bool m_IsDirty;

        private LinkedList<Segment> m_Segments;
        private ComputeBuffer m_LocalToWorldMatricesBuffer;
        private ComputeBuffer m_AdditionalDataBuffer;

        public bool UseAdditionalData
        {
            get
            {
                return m_UseAdditionalData;
            }
        }
        private bool m_UseAdditionalData;

        public SegmentedStructBuffer(bool useAdditionalData)
        {
            m_Segments = new LinkedList<Segment>();
            m_Count = 0;
            m_UseAdditionalData = useAdditionalData;
        }

        public IHandle AddSegment(int count)
        {
            m_IsDirty = true;
            m_Count += count;

            Segment segment = new Segment(m_Count, count, State.Increased);
            m_Segments.AddLast(segment);

            return segment;
        }

        public void RemoveSegment(IHandle handle)
        {
            Segment segment = handle as Segment;
            if(segment != null && segment.State != State.Removed)
            {
                m_IsDirty = true;
                m_Count -= segment.Count;

                segment.State = State.Removed;
            }
        }

        public void RebuildBuffer()
        {
            ComputeBuffer localToWorldMatricesBuffer = new ComputeBuffer(m_Count, UnsafeUtility.SizeOf<Matrix4x4>());
            ComputeBuffer additionalDataBuffer = UseAdditionalData ? new ComputeBuffer(m_Count, UnsafeUtility.SizeOf<Vector4>()) : null;

            LinkedListNode<Segment> current = m_Segments.First;
            while (current != null)
            {
                LinkedListNode<Segment> next = current.Next;

                Segment preivousSegment = current.Previous?.Value;
                Segment currentSegment = current.Value;

                switch (currentSegment.State)
                {
                    case State.Removed:
                        {
                            m_Segments.Remove(current);
                            currentSegment.Release(m_LocalToWorldMatricesBuffer, m_AdditionalDataBuffer);
                        }
                        break;
                    case State.Increased:
                        {
                            currentSegment.Start = (preivousSegment != null) ? (preivousSegment.Start + preivousSegment.Count) : 0;
                            currentSegment.Initialize(localToWorldMatricesBuffer, additionalDataBuffer);
                        }
                        break;
                    case State.Existed:
                        {
                            int srcStart = currentSegment.Start;
                            currentSegment.Start = (preivousSegment != null) ? (preivousSegment.Start + preivousSegment.Count) : 0;

                            CopyMatrix4x4Data(m_LocalToWorldMatricesBuffer, srcStart,
                                localToWorldMatricesBuffer, currentSegment.Start,
                                currentSegment.Count);

                            if(UseAdditionalData)
                            {
                                CopyVector4Data(m_AdditionalDataBuffer, srcStart,
                                    additionalDataBuffer, currentSegment.Start,
                                    currentSegment.Count);
                            }
                        }
                        break;
                }
                currentSegment.State = State.Existed;
                current = next;
            }

            Release();
            
            m_LocalToWorldMatricesBuffer = localToWorldMatricesBuffer;
            m_AdditionalDataBuffer = additionalDataBuffer;
        }

        public void CopyMatrix4x4Data(ComputeBuffer srcComputeBuffer, int srcStart, ComputeBuffer destComputeBuffer, int destStart, int count)
        {
            if(s_CopyMatrix4x4Compute != null)
            {
                s_CopyMatrix4x4Compute.SetBuffer(0, "_Src", srcComputeBuffer);
                s_CopyMatrix4x4Compute.SetInt("_SrcStart", srcStart);
                s_CopyMatrix4x4Compute.SetBuffer(0, "_Dest", destComputeBuffer);
                s_CopyMatrix4x4Compute.SetInt("_DestStart", destStart);
                s_CopyMatrix4x4Compute.SetInt("_Count", count);
                s_CopyMatrix4x4Compute.Dispatch(0, Mathf.CeilToInt((float)count / 64), 1, 1);
            }
        }

        public void CopyVector4Data(ComputeBuffer srcComputeBuffer, int srcStart, ComputeBuffer destComputeBuffer, int destStart, int count)
        {
            if (s_CopyVector4Compute != null)
            {
                s_CopyVector4Compute.SetBuffer(0, "_Src", srcComputeBuffer);
                s_CopyVector4Compute.SetInt("_SrcStart", srcStart);
                s_CopyVector4Compute.SetBuffer(0, "_Dest", destComputeBuffer);
                s_CopyVector4Compute.SetInt("_DestStart", destStart);
                s_CopyVector4Compute.SetInt("_Count", count);
                s_CopyVector4Compute.Dispatch(0, Mathf.CeilToInt((float)count / 64), 1, 1);
            }
        }

        public void GetData(System.Array data)
        {
            m_LocalToWorldMatricesBuffer.GetData(data);
        }

        public void GetAdditionalData(System.Array data)
        {
            m_AdditionalDataBuffer.GetData(data);
        }

        public void UpdateBuffer()
        {
            LinkedListNode<Segment> current = m_Segments.First;
            while (current != null)
            {
                LinkedListNode<Segment> next = current.Next;

                Segment segment = current.Value;
                segment.Update(m_LocalToWorldMatricesBuffer, m_AdditionalDataBuffer);

                current = next;
            }
        }

        public void Update()
        {
            if (m_IsDirty)
            {
                RebuildBuffer();
                m_IsDirty = false;
            }
            UpdateBuffer();
        }

        public void Release()
        {
            if(m_LocalToWorldMatricesBuffer != null)
            {
                m_LocalToWorldMatricesBuffer.Release();
            }

            if(m_AdditionalDataBuffer != null)
            {
                m_AdditionalDataBuffer.Release();
            }
        }

        public void SetLocalToWorldMatricesBuffer(MaterialPropertyBlock materialPropertyBlock, int nameID)
        {
            if(materialPropertyBlock != null)
            {
                materialPropertyBlock.SetBuffer(nameID, m_LocalToWorldMatricesBuffer);
            }
        }

        public void SetAdditionalDataBuffer(MaterialPropertyBlock materialPropertyBlock, int nameID)
        {
            if (materialPropertyBlock != null)
            {
                materialPropertyBlock.SetBuffer(nameID, m_AdditionalDataBuffer);
            }
        }

        public void SetLocalToWorldMatricesBuffer(ComputeShader computeShader, int kernelIndex,  int nameID)
        {
            if(computeShader != null)
            {
                computeShader.SetBuffer(kernelIndex, nameID, m_LocalToWorldMatricesBuffer);
            }
        }
    }
}

