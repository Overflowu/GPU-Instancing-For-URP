using Aperture.Instancing.Runtime;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// 完全GPU驱动的Boids
/// </summary>
public class GPUDrivenBoids : MonoBehaviour
{
    #region BOIDS

    public int spawnCount = 10;

    public float spawnRadius = 4.0f;

    [Range(0.1f, 20.0f)]
    public float velocity = 6.0f;
    [Range(0.0f, 0.9f)]
    public float velocityVariation = 0.5f;
    [Range(0.1f, 20.0f)]
    public float rotationCoeff = 4.0f;
    [Range(0.1f, 10.0f)]
    public float neighborDist = 2.0f;

    public ComputeShader boidsComputeShader;
    public Texture2D noiseTexture;

    #endregion

    #region INSTANCING

    public RenderData renderData;
    private IHandle m_Handle;

    #endregion

    private NativeArray<Matrix4x4> boidLocalToWorldMatrices;

    private void Awake()
    {
        boidLocalToWorldMatrices = new NativeArray<Matrix4x4>(spawnCount, Allocator.Persistent);
        for (int i = 0; i < boidLocalToWorldMatrices.Length; i++)
        {
            boidLocalToWorldMatrices[i] = Matrix4x4.TRS(transform.position + Random.insideUnitSphere * spawnRadius, Quaternion.identity, (Random.value * 5.0f + 0.5f) * Vector3.one);
        }
    }

    void OnEnable()
    {
        m_Handle = GPUInstancingManager.Alloc(renderData, boidLocalToWorldMatrices.Length);
        m_Handle.onInitialize += OnInitialize;
        m_Handle.onUpdate += OnUpdate;

    }

    private void OnInitialize(ComputeBuffer computeBuffer, int start, int size)
    {
        computeBuffer.SetData(boidLocalToWorldMatrices, 0, start, size);
    }

    private void OnUpdate(ComputeBuffer computeBuffer, int start, int size)
    {
        boidsComputeShader.SetBuffer(0, "_LocalToWorldBuffer", computeBuffer);
        boidsComputeShader.SetInt("_BufferOffset", start);
        boidsComputeShader.SetInt("_BufferSize", size);
        boidsComputeShader.SetTexture(0, "_NoiseTexture", noiseTexture);
        boidsComputeShader.SetMatrix("_ControllerTransform", transform.localToWorldMatrix);
        boidsComputeShader.SetFloat("_Velocity", velocity);
        boidsComputeShader.SetFloat("_VelocityVariation", velocityVariation);
        boidsComputeShader.SetFloat("_RotationCoeff", rotationCoeff);
        boidsComputeShader.SetFloat("_NeighborDist", neighborDist);
        boidsComputeShader.SetFloat("_Time", Time.time);
        boidsComputeShader.SetFloat("_DeltaTime", Time.deltaTime);

        boidsComputeShader.Dispatch(0, Mathf.CeilToInt((float)spawnCount / 64), 1, 1);
    }

    private void OnDisable()
    {
        m_Handle.onInitialize -= OnInitialize;
        m_Handle.onUpdate -= OnUpdate;

        GPUInstancingManager.Dealloc(renderData, m_Handle);
    }

    private void OnDestroy()
    {
        boidLocalToWorldMatrices.Dispose();
    }
}
