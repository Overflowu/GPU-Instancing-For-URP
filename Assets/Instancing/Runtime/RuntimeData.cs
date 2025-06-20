using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Aperture.Instancing.Runtime
{
    internal class RuntimeData
    {
        internal RenderData renderData;
        internal List<Renderer> renderers;

        internal Bounds bounds;
        internal uint[] args;

        internal SegmentedStructBuffer instancingBuffer;

        internal ComputeBuffer lodVisibilityBuffer;
        internal ComputeBuffer argsBuffer;

        public float[] lodSizes = new float[]
        {
            1000, 1000, 1000, 1000,
            1000, 1000, 1000, 1000,
            1000, 1000, 1000, 1000,
            1000, 1000, 1000, 1000
        };

        internal RuntimeData(RenderData data)
        {
            renderData = data; 
            renderers = new List<Renderer>();
            instancingBuffer = new SegmentedStructBuffer(renderData.useAdditonalData);

            for (int i = 0; i < renderData.lods.Length; i++)
            {
                LOD lod = renderData.lods[i];
                Renderer renderer = new Renderer();
                renderer.mesh = lod.mesh;
                renderer.materials = lod.materials;

                renderers.Add(renderer);

                bounds.Encapsulate(renderer.mesh.bounds);
            }

            for (int i = 0; i < lodSizes.Length; i++)
            {
                if (i < renderData.lods.Length)
                {
                    lodSizes[i] = renderData.lods[i].screenRelativeTransitionHeight;
                }
            }
        }

        internal IHandle AddSegment(int count)
        {
            return instancingBuffer.AddSegment(count);
        }

        internal void RemoveSegment(IHandle handle)
        {
            instancingBuffer.RemoveSegment(handle);
        }

        private void UpdateLocalToWorldMatricesBuffer()
        {
            if (instancingBuffer != null)
            {
                instancingBuffer.Update();

                for (int i = 0; i < renderers.Count; i++)
                {
                    Renderer renderer = renderers[i];

                    instancingBuffer.SetLocalToWorldMatricesBuffer(renderer.materialPropertyBlock, ShaderProperties.BUFFER_LOACLTOWORLDMATRICES);

                    if(instancingBuffer.UseAdditionalData)
                    {
                        instancingBuffer.SetAdditionalDataBuffer(renderer.materialPropertyBlock, ShaderProperties.BUFFER_ADDITIONALDATA);
                    }
                }
            }
        }

        private void UpdateLodVisibilityBuffer()
        {
            if (lodVisibilityBuffer == null || lodVisibilityBuffer.count != instancingBuffer.Count)
            {
                if (lodVisibilityBuffer != null)
                {
                    lodVisibilityBuffer.Release();
                }
                lodVisibilityBuffer = new ComputeBuffer(instancingBuffer.Count, sizeof(uint));
            }
        }

        private void UpdateVisibleIndicesBuffer()
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer.visibleIndicesBuffer == null || renderer.visibleIndicesBuffer.count != instancingBuffer.Count)
                {
                    if (renderer.visibleIndicesBuffer != null)
                    {
                        renderer.visibleIndicesBuffer.Release();
                    }
                    renderer.visibleIndicesBuffer = new ComputeBuffer(instancingBuffer.Count, sizeof(uint), ComputeBufferType.Append);
                    renderer.materialPropertyBlock.SetBuffer(ShaderProperties.BUFFER_VISIBLEINDICES, renderer.visibleIndicesBuffer);
                }
            }
        }

        private void UpdateArgsBuffer()
        {
            if (argsBuffer == null)
            {
                int totalSubMeshCount = 0;
                for (int lodIndex = 0; lodIndex < renderers.Count; lodIndex++)
                {
                    Renderer renderer = renderers[lodIndex];
                    totalSubMeshCount += renderer.mesh.subMeshCount;
                }

                //args = new uint[5 * totalSubMeshCount];
                args = new uint[5 * renderers.Count];

                int argOffset = 0;
                for (int lodIndex = 0; lodIndex < renderers.Count; lodIndex++)
                {
                    Renderer renderer = renderers[lodIndex];
                    renderer.argsBufferOffset = argOffset;

                     for (int subMeshIndex = 0; subMeshIndex < renderer.mesh.subMeshCount; subMeshIndex++)
                     {
                        //offset: index count per instance
                        args[argOffset++] = renderer.mesh.GetIndexCount(subMeshIndex);
                        //instance count
                        args[argOffset++] = (uint)instancingBuffer.Count;
                        //start index location
                        args[argOffset++] = renderer.mesh.GetIndexStart(subMeshIndex);
                        //base vertex location
                        args[argOffset++] = renderer.mesh.GetBaseVertex(subMeshIndex);
                        //start instance location
                        args[argOffset++] = 0;
                    }
                }

                if (args.Length > 0)
                {
                    argsBuffer = new ComputeBuffer(args.Length, sizeof(uint), ComputeBufferType.IndirectArguments);
                    argsBuffer.SetData(args);
                }
            }
        }

        internal void UpdateBuffers()
        {
            UpdateLocalToWorldMatricesBuffer();

            if (instancingBuffer.Count == 0)
                return;

            UpdateLodVisibilityBuffer();
            UpdateVisibleIndicesBuffer();
            UpdateArgsBuffer();
        }

        internal void ReleaseBuffers()
        {
            for(int i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];

                renderer.materialPropertyBlock.Clear();

                if(renderer.visibleIndicesBuffer != null)
                {
                    renderer.visibleIndicesBuffer.Release();
                }
            }
            renderers.Clear();

            if (instancingBuffer != null)
            {
                instancingBuffer.Release();
            }

            if(lodVisibilityBuffer != null)
            {
                lodVisibilityBuffer.Release();
            }

            if (argsBuffer != null)
            {
                argsBuffer.Release();
            }
        }
    }
}