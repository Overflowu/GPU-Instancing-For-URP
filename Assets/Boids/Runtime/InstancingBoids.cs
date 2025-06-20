using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

public partial class InstancingBoids : MonoBehaviour
{
    #region BOIDS

    public int spawnCount = 10;
    private int m_CachedCount = 0;

    public float spawnRadius = 4.0f;

    [Range(0.1f, 20.0f)]
    public float velocity = 6.0f;
    [Range(0.0f, 0.9f)]
    public float velocityVariation = 0.5f;
    [Range(0.1f, 20.0f)]
    public float rotationCoeff = 4.0f;
    [Range(0.1f, 10.0f)]
    public float neighborDist = 2.0f;

    #endregion

    #region JOBSYSTEM

    private NativeArray<Matrix4x4> m_BoidLocalToWorldMatrices;

    private NativeArray<Vector3> m_BoidPositions;
    private NativeArray<Quaternion> m_BoidRotations;
    private NativeArray<Vector3> m_BoidScales;
    private NativeArray<Vector3> m_BoidVelocities;

    private BoidsJob m_BoidsJob;
    private UpdateArrayJob m_UpdateArrayJob;

    private JobHandle m_JobHandle;
    private JobHandle m_UpdateDataJobHandle;

    #endregion

    #region CULLING

    public ComputeShader cullingCompute;
    private int m_CullingKernel;
    private ComputeBuffer m_VisibleIndicesBuffer;
    private Plane[] m_FrustumPlanes;
    private Vector4[] m_Planes;

    #endregion

    #region INSTANCING

    public Mesh instanceMesh;
    public Material instanceMaterial;
    public int subMeshIndex;

    private ComputeBuffer m_LocalToWorldMatricesBuffer;
    private ComputeBuffer m_ArgsBuffer;
    private uint[] m_Args = new uint[5] { 0, 0, 0, 0, 0 };
    private MaterialPropertyBlock m_MaterialPropertyBlock;
    //private CommandBuffer m_CommandBuffer;

    #endregion


    private void Start()
    {
        m_BoidPositions = new NativeArray<Vector3>(spawnCount, Allocator.Persistent);
        m_BoidRotations = new NativeArray<Quaternion>(spawnCount, Allocator.Persistent);
        m_BoidScales = new NativeArray<Vector3>(spawnCount, Allocator.Persistent);
        m_BoidVelocities = new NativeArray<Vector3>(spawnCount, Allocator.Persistent);

        m_BoidLocalToWorldMatrices = new NativeArray<Matrix4x4>(spawnCount, Allocator.Persistent);
        for (int i = 0; i < m_BoidLocalToWorldMatrices.Length; i++)
        {
            m_BoidLocalToWorldMatrices[i] = Matrix4x4.TRS(transform.position + Random.insideUnitSphere * spawnRadius, Quaternion.identity, (Random.value + 0.5f) * Vector3.one);
        }

        m_ArgsBuffer = new ComputeBuffer(1, m_Args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        m_MaterialPropertyBlock = new MaterialPropertyBlock();

        m_FrustumPlanes = new Plane[6];
        m_Planes = new Vector4[6];
        m_VisibleIndicesBuffer = new ComputeBuffer(spawnCount, sizeof(uint), ComputeBufferType.Append);
        m_LocalToWorldMatricesBuffer = new ComputeBuffer(spawnCount, Marshal.SizeOf<Matrix4x4>());
        m_CullingKernel = cullingCompute.FindKernel("CSMain");

        //m_CommandBuffer = new CommandBuffer();
        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
    }

    private void OnDestroy()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;

        //if (m_CommandBuffer != null)
        //    m_CommandBuffer.Release();

        if (m_ArgsBuffer != null)
            m_ArgsBuffer.Release();

        if (m_LocalToWorldMatricesBuffer != null)
            m_LocalToWorldMatricesBuffer.Release();

        if (m_VisibleIndicesBuffer != null)
            m_VisibleIndicesBuffer.Release();

        m_BoidPositions.Dispose();
        m_BoidRotations.Dispose();
        m_BoidScales.Dispose();
        m_BoidVelocities.Dispose();

        m_BoidLocalToWorldMatrices.Dispose();

    }

    private void SimulateFlocking()
    {
        m_UpdateArrayJob = new UpdateArrayJob()
        {
            BoidPositions = m_BoidPositions,
            BoidRotations = m_BoidRotations,
            BoidScales = m_BoidScales,
            LocalToWorld = m_BoidLocalToWorldMatrices,
        };
        m_UpdateDataJobHandle = m_UpdateArrayJob.Schedule(m_BoidLocalToWorldMatrices.Length, 64);

        m_BoidsJob = new BoidsJob()
        {
            BoidVelocities = m_BoidVelocities,
            BoidPositions = m_BoidPositions,
            BoidRotations = m_BoidRotations,
            BoidScales = m_BoidScales,
            ControllerFoward = transform.forward,
            ControllerPosition = transform.position,
            RotationCoeff = rotationCoeff,
            DeltaTime = Time.deltaTime,
            NeighborDist = neighborDist,
            Speed = velocity,
            LocalToWorld = m_BoidLocalToWorldMatrices,
        };
        m_JobHandle = m_BoidsJob.Schedule(m_BoidLocalToWorldMatrices.Length, 64, m_UpdateDataJobHandle);
        m_JobHandle.Complete();
    }

    private void FrustumCulling(Camera camera)
    {
        Bounds bounds = instanceMesh.bounds;
        GeometryUtility.CalculateFrustumPlanes(camera, m_FrustumPlanes);
        for(int i = 0; i < 6; i++)
        {
            m_Planes[i] = new Vector4(m_FrustumPlanes[i].normal.x, m_FrustumPlanes[i].normal.y, m_FrustumPlanes[i].normal.z, m_FrustumPlanes[i].distance);
        }
        m_LocalToWorldMatricesBuffer.SetData(m_BoidLocalToWorldMatrices);
        m_VisibleIndicesBuffer.SetCounterValue(0);

        cullingCompute.SetInt("_Count", spawnCount);
        cullingCompute.SetVector("_BoundMin", bounds.min);
        cullingCompute.SetVector("_BoundMax", bounds.max);
        cullingCompute.SetVectorArray("_FrustumPlanes", m_Planes);
        cullingCompute.SetBuffer(m_CullingKernel, "_LocalToWorldMatrices", m_LocalToWorldMatricesBuffer);
        cullingCompute.SetBuffer(m_CullingKernel, "_VisibleIndicesBuffer", m_VisibleIndicesBuffer);

        cullingCompute.Dispatch(m_CullingKernel, Mathf.CeilToInt((float)spawnCount / 64), 1, 1);
    }

    private void UpdateBuffer()
    {
        UpdateInstanceDataBuffer();
        UpdateArgsBuffer();

        m_CachedCount = spawnCount;
    }

    private void UpdateInstanceDataBuffer()
    {
        if (spawnCount != m_CachedCount)
        {
            if (m_MaterialPropertyBlock != null)
            {
                m_MaterialPropertyBlock.SetBuffer("_LocalToWorldMatrices", m_LocalToWorldMatricesBuffer);
                m_MaterialPropertyBlock.SetBuffer("_VisibleIndices", m_VisibleIndicesBuffer);
            }
        }
    }

    private void UpdateArgsBuffer()
    {
        if (spawnCount != m_CachedCount)
        {
            // Ensure submesh index is in range
            if (instanceMesh != null)
                subMeshIndex = Mathf.Clamp(subMeshIndex, 0, instanceMesh.subMeshCount - 1);

            // Indirect args
            if (instanceMesh != null)
            {
                //offset: index count per instance
                m_Args[0] = (uint)instanceMesh.GetIndexCount(subMeshIndex);
                //instance count
                m_Args[1] = (uint)spawnCount;
                //start index location
                m_Args[2] = (uint)instanceMesh.GetIndexStart(subMeshIndex);
                //base vertex location
                m_Args[3] = (uint)instanceMesh.GetBaseVertex(subMeshIndex);
                //start instance location
                m_Args[4] = 0;
            }
            else
            {
                m_Args[0] = m_Args[1] = m_Args[2] = m_Args[3] = m_Args[4] = 0;
            }
            m_ArgsBuffer.SetData(m_Args);
        }
        ComputeBuffer.CopyCount(m_VisibleIndicesBuffer, m_ArgsBuffer, sizeof(uint));
    }

    private void Render(ScriptableRenderContext renderContext)
    {
        if (SystemInfo.supportsInstancing)
        {
            if (SystemInfo.supportsComputeShaders)
            {
                UpdateBuffer();

                //TODO: calcute Bounds
                Graphics.DrawMeshInstancedIndirect(instanceMesh, subMeshIndex, instanceMaterial, new Bounds(Vector3.zero, Vector3.one * 5000.0f), m_ArgsBuffer, 0, m_MaterialPropertyBlock);
            }
        }
    }

    private void Update()
    {
        SimulateFlocking();
    }

    private void BeginCameraRendering(ScriptableRenderContext renderContext, Camera camera)
    {
        //TODO: Camera.main for test in sceneview

        FrustumCulling(/*camera*/ Camera.main);

        Render(renderContext);
    }
}
