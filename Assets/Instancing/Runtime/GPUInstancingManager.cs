using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aperture.Instancing.Runtime
{
    public class GPUInstancingManager : MonoBehaviour
    {
        private static Dictionary<RenderData, RuntimeData> s_RenderData2RuntimeData = new Dictionary<RenderData, RuntimeData>();

        public static int COMPUTE_MAX_LOD_BUFFER = 3;

        [SerializeField]
        private ComputeShader m_CullingCompute;
        [SerializeField]
        private ComputeShader m_LodSortCompute;

        private Plane[] m_FrustumPlanes;
        private Vector4[] m_CullingPlanes;

        private void Awake()
        {
            m_FrustumPlanes = new Plane[6];
            m_CullingPlanes = new Vector4[6];
        }

        private void Start()
        {
            if (m_CullingCompute == null)
                m_CullingCompute = Resources.Load<ComputeShader>("Shaders/Culling");

            if(m_LodSortCompute == null)
                m_LodSortCompute = Resources.Load<ComputeShader>("Shaders/LodSort");
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginFrameRendering += OnBeginFrameRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRendering;
        }

        private void OnBeginFrameRendering(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            CommandBuffer commandBuffer = new CommandBuffer();

            foreach (RuntimeData runtimeData in s_RenderData2RuntimeData.Values)
            {
                UpdateBuffers(runtimeData);
            }

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];

                foreach (RuntimeData runtimeData in s_RenderData2RuntimeData.Values)
                {
                    Cull(runtimeData, camera);
                    SortLOD(runtimeData);
                    Render(runtimeData, camera);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (RuntimeData runtimeData in s_RenderData2RuntimeData.Values)
            {
                runtimeData.ReleaseBuffers();
            }
            s_RenderData2RuntimeData.Clear();
        }

        public static IHandle Alloc(RenderData renderData, int size)
        {
            if (!s_RenderData2RuntimeData.TryGetValue(renderData, out RuntimeData runtimeData))
            {
                runtimeData = new RuntimeData(renderData);
                s_RenderData2RuntimeData.Add(renderData, runtimeData);
            }
            return runtimeData.AddSegment(size);
        }

        public static void Dealloc(RenderData renderData, IHandle handle)
        {
            if(s_RenderData2RuntimeData.TryGetValue(renderData, out RuntimeData runtimeData))
            {
                runtimeData.RemoveSegment(handle);

                if(runtimeData.instancingBuffer.Count == 0)
                {
                    s_RenderData2RuntimeData.Remove(renderData);
                }
            }
        }

        private void UpdateBuffers(RuntimeData runtimeData)
        {
            runtimeData.UpdateBuffers();
        }

        private void UpdateCullingPlanes(Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, m_FrustumPlanes);

            for (int i = 0; i < 6; i++)
            {
                m_CullingPlanes[i] = new Vector4(m_FrustumPlanes[i].normal.x, m_FrustumPlanes[i].normal.y, m_FrustumPlanes[i].normal.z, m_FrustumPlanes[i].distance);
            }
        }

        private void Cull(RuntimeData runtimeData, Camera camera)
        {
            UpdateCullingPlanes(camera);

            m_CullingCompute.SetInt(ShaderProperties.COMPUTE_PARAMS_COUNT, runtimeData.instancingBuffer.Count);
            m_CullingCompute.SetVector(ShaderProperties.CULLING_PARAMS_CAMERAPARAMETERS, new Vector4(camera.transform.position.x, camera.transform.position.y, camera.transform.position.z, Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5f)));

            m_CullingCompute.SetBool(ShaderProperties.CULLING_PARAMS_IS_DISTANCE_CULLING, runtimeData.renderData.isDistanceCulling);
            m_CullingCompute.SetFloat(ShaderProperties.CULLING_PARAMS_MIN_DISTANCE, runtimeData.renderData.minDistance);
            m_CullingCompute.SetFloat(ShaderProperties.CULLING_PARAMS_MAX_DISTANCE, runtimeData.renderData.maxDistance);

            m_CullingCompute.SetBool(ShaderProperties.CULLING_PARAMS_IS_FRUSTUM_CULLING, runtimeData.renderData.isFrustumCulling);
            m_CullingCompute.SetVectorArray(ShaderProperties.CULLING_PARAMS_FRUSTUMPLANES, m_CullingPlanes);
            m_CullingCompute.SetVector(ShaderProperties.CULLING_PARAMS_BOUNDMIN, runtimeData.bounds.min);
            m_CullingCompute.SetVector(ShaderProperties.CULLING_PARAMS_BOUNDMAX, runtimeData.bounds.max);

            m_CullingCompute.SetInt(ShaderProperties.LOD_PARAMS_COUNT, runtimeData.renderData.lods.Length);
            m_CullingCompute.SetFloats(ShaderProperties.LOD_PARAMS_SIZES, runtimeData.lodSizes);

            runtimeData.instancingBuffer.SetLocalToWorldMatricesBuffer(m_CullingCompute, 0, ShaderProperties.BUFFER_LOACLTOWORLDMATRICES);
            m_CullingCompute.SetBuffer(0, ShaderProperties.BUFFER_LODVISIBILITY, runtimeData.lodVisibilityBuffer);

            m_CullingCompute.GetKernelThreadGroupSizes(0, out uint x, out uint y, out uint z);
            m_CullingCompute.Dispatch(0, Mathf.CeilToInt((float)runtimeData.instancingBuffer.Count / (int)x), 1, 1);
        }

        private void SortLOD(RuntimeData runtimeData)
        {
            int lodCount = runtimeData.renderers.Count;
            int lodShift = 0;
            
            while(lodCount - lodShift > 0)
            {
                int kernelIndex = Math.Min(lodCount - lodShift, COMPUTE_MAX_LOD_BUFFER) - 1;
                m_LodSortCompute.SetInt(ShaderProperties.COMPUTE_PARAMS_COUNT, runtimeData.instancingBuffer.Count);
                m_LodSortCompute.SetInt(ShaderProperties.LOD_PARAMS_SHIFT, lodShift);
                m_LodSortCompute.SetBuffer(kernelIndex, ShaderProperties.BUFFER_LODVISIBILITY, runtimeData.lodVisibilityBuffer);

                for (int lodIndex = 0; lodIndex < Math.Min(lodCount - lodShift, COMPUTE_MAX_LOD_BUFFER); lodIndex++)
                {
                    Renderer renderer = runtimeData.renderers[lodIndex + lodShift];
                    renderer.visibleIndicesBuffer.SetCounterValue(0);

                    m_LodSortCompute.SetBuffer(kernelIndex, ShaderProperties.BUFFER_VISIBLEINDICESLOD[lodIndex], renderer.visibleIndicesBuffer);
                }

                m_LodSortCompute.GetKernelThreadGroupSizes(0, out uint x, out uint y, out uint z);
                m_LodSortCompute.Dispatch(kernelIndex, Mathf.CeilToInt((float)runtimeData.instancingBuffer.Count / (int)x), 1, 1);
                lodShift += COMPUTE_MAX_LOD_BUFFER;
            }

            for (int i = 0; i < runtimeData.renderers.Count; i++)
            {
                Renderer renderer = runtimeData.renderers[i];

                for(int subMeshIndex = 0; subMeshIndex < renderer.mesh.subMeshCount; subMeshIndex++)
                {
                    ComputeBuffer.CopyCount(renderer.visibleIndicesBuffer, runtimeData.argsBuffer, (renderer.argsBufferOffset + subMeshIndex * 5 + 1) * sizeof(uint));
                }
            }
        }

        private void Render(RuntimeData runtimeData, Camera camera)
        {
            for(int lodIndex = 0; lodIndex < runtimeData.renderers.Count; lodIndex++)
            {
                Renderer renderer = runtimeData.renderers[lodIndex];

                for(int subMeshIndex = 0; subMeshIndex < renderer.mesh.subMeshCount; subMeshIndex++)
                {
                    Graphics.DrawMeshInstancedIndirect(
                        renderer.mesh,
                        subMeshIndex,
                        renderer.materials[subMeshIndex],
                        new Bounds(camera.transform.position, Vector3.one * 100),
                        runtimeData.argsBuffer,
                        (renderer.argsBufferOffset + subMeshIndex * 5) * sizeof(uint),
                        renderer.materialPropertyBlock,
                        runtimeData.renderData.shadowCastingMode,
                        true,
                        runtimeData.renderData.layer,
                        camera,
                        runtimeData.renderData.lightProbeUsage
                        );
                }
            }
        }
    }
}
