using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Jobs;

public class JobSystemBoids : MonoBehaviour
{
    public GameObject boidPrefab;

    public int spawnCount = 10;

    public float spawnRadius = 4.0f;

    public bool horizontalSpawn;

    public bool horizontalMove;

    public float minHeight;

    public float maxHeight;

    [Range(0.1f, 20.0f)]
    public float velocity = 6.0f;

    [Range(0.0f, 0.9f)]
    public float velocityVariation = 0.5f;

    [Range(0.1f, 20.0f)]
    public float rotationCoeff = 4.0f;

    [Range(0.1f, 10.0f)]
    public float neighborDist = 2.0f;

    private Transform[] m_Boids;
    private NativeArray<Vector3> m_BoidPositions;
    private NativeArray<Vector3> m_BoidVelocities;
    private NativeArray<Quaternion> m_BoidRotations;
    private TransformAccessArray m_BoidsTransformArray;
    private TransformJob m_TransJob;
    private UpdateArrayJob m_UpdateArrayJob;
    private JobHandle m_JobHandle;
    private JobHandle m_UpdateDataJobHandle;

    void Start()
    {
        m_BoidPositions = new NativeArray<Vector3>(spawnCount, Allocator.Persistent);
        m_BoidRotations = new NativeArray<Quaternion>(spawnCount, Allocator.Persistent);
        m_BoidVelocities = new NativeArray<Vector3>(spawnCount, Allocator.Persistent);

        m_Boids = new Transform[spawnCount];
        for (int i = 0; i < spawnCount; i++)
        {
            m_Boids[i] = Spawn().transform;

            if(m_Boids[i].TryGetComponent(out Animator animator))
            {
                animator.speed = UnityEngine.Random.Range(1.0f - velocityVariation, 1.0f + velocityVariation);
            }
        }

        m_BoidsTransformArray = new TransformAccessArray(m_Boids);
        for (int i = 0; i < m_BoidRotations.Length; i++)
        {
            m_BoidPositions[i] = UnityEngine.Random.onUnitSphere;
            m_BoidRotations[i] = Quaternion.identity;
        }
    }

    void OnDestroy()
    {
        m_BoidPositions.Dispose();
        m_BoidRotations.Dispose();
        m_BoidVelocities.Dispose();
        m_BoidsTransformArray.Dispose();

        for(int i = 0; i < m_Boids.Length; i++)
        {
            Transform boids = m_Boids[i];
            if(boids != null)
            {
                Destroy(boids.gameObject);
            }
        }
    }

    public GameObject Spawn()
    {
        Vector3 offset = UnityEngine.Random.insideUnitSphere;
        if (horizontalSpawn)
        {
            offset = new Vector3(offset.x, 0.0f, offset.z);
        }
        Vector3 spawnPos = transform.position + offset * spawnRadius;
        return Spawn(spawnPos);
    }

    public GameObject Spawn(Vector3 position)
    {
        Quaternion rotation = Quaternion.identity;
        if (horizontalSpawn)
        {
            rotation = Quaternion.Slerp(transform.rotation, Quaternion.AngleAxis(UnityEngine.Random.value * 360.0f, Vector3.up), 0.3f);
        }
        else
        {
            rotation = Quaternion.Slerp(transform.rotation, UnityEngine.Random.rotation, 0.3f);
        }
        GameObject boid = Instantiate(boidPrefab, position, rotation);

        return boid;
    }

    private void Update()
    {
        m_TransJob = new TransformJob()
        {
            BoidVelocities = m_BoidVelocities,
            BoidPositions = m_BoidPositions,
            BoidRotations = m_BoidRotations,
            ControllerFoward = transform.forward,
            ControllerPosition = transform.position,
            RotationCoeff = rotationCoeff,
            DeltaTime = Time.deltaTime,
            NeighborDist = neighborDist,
            Speed = velocity,
            HorizontalMove = horizontalMove,
            MinHeight = minHeight,
            MaxHeight = maxHeight,
        };

        m_UpdateArrayJob = new UpdateArrayJob()
        {
            BoidPositions = m_BoidPositions,
            BoidRotations = m_BoidRotations,
        };
        m_UpdateDataJobHandle = m_UpdateArrayJob.Schedule(m_BoidsTransformArray);
        m_JobHandle = m_TransJob.Schedule(m_BoidsTransformArray, m_UpdateDataJobHandle);
        m_JobHandle.Complete();
    }

    [BurstCompile]
    public struct TransformJob : IJobParallelForTransform
    {
        public NativeArray<Vector3> BoidVelocities;
        [ReadOnly]
        public NativeArray<Vector3> BoidPositions;
        [ReadOnly]
        public NativeArray<Quaternion> BoidRotations;
        [ReadOnly]
        public Vector3 ControllerFoward;
        [ReadOnly]
        public Vector3 ControllerPosition;
        [ReadOnly]
        public float RotationCoeff;
        [ReadOnly]
        public float DeltaTime;
        [ReadOnly]
        public float NeighborDist;
        [ReadOnly]
        public float Speed;
        [ReadOnly]
        public bool HorizontalMove;
        [ReadOnly]
        public float MinHeight;
        [ReadOnly]
        public float MaxHeight;


        Vector3 GetSeparationVector(Vector3 current, Vector3 targetPos)
        {
            Vector3 diff = current - targetPos;
            float diffLen = diff.magnitude;
            float scaler = Mathf.Clamp01(1.0f - diffLen / NeighborDist);
            return diff * (scaler / diffLen);
        }

        public void Execute(int index, TransformAccess trans)
        {
            float noise = Mathf.PerlinNoise(DeltaTime + index * 0.01f, index) * 2.0f - 1.0f;
            float Speed = this.Speed * (1.0f + noise * 0.5f);
            Vector3 currentPosition = BoidPositions[index];
            Quaternion currentRotation = BoidRotations[index];

            Vector3 separation = Vector3.zero;
            Vector3 alignment = ControllerFoward;
            Vector3 cohesion = ControllerPosition;
            int neighborCount = 0;

            for (int i = 0; i < BoidPositions.Length; i++)
            {
                if (index == i)
                {
                    neighborCount++;
                    continue;
                }
                if ((BoidPositions[i] - BoidPositions[index]).sqrMagnitude <= (NeighborDist) * (NeighborDist))
                {
                    separation += GetSeparationVector(BoidPositions[index], BoidPositions[i]);
                    alignment += (BoidRotations[i] * Vector3.forward);
                    cohesion += BoidPositions[i];
                    neighborCount++;
                }
            }

            float avg = 1.0f / Mathf.Max(1, neighborCount);
            alignment *= avg;
            cohesion *= avg;
            cohesion = (cohesion - currentPosition).normalized;

            Vector3 direction = alignment + cohesion + separation;
            Vector3 accel = alignment * 10f + cohesion * 30f + separation * 35f;
            Vector3 velocity = BoidVelocities[index] + accel * DeltaTime;
            velocity = Vector3.ClampMagnitude(velocity, Speed);

            if(HorizontalMove)
            {
                direction.y = 0f;
                velocity.y = 0f;
            }

            BoidVelocities[index] = velocity;

            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            if (rotation != currentRotation)
            {
                float ip = Mathf.Exp(-RotationCoeff * DeltaTime);
                rotation = Quaternion.Slerp(rotation, currentRotation, ip);
            }
            trans.rotation = rotation;
            Vector3 position = BoidPositions[index] + (velocity * DeltaTime);
            position = new Vector3(position.x, Mathf.Clamp(position.y, MinHeight, MaxHeight), position.z);
            trans.position = position;
        }
    }

    [BurstCompile]
    public struct UpdateArrayJob : IJobParallelForTransform
    {
        [WriteOnly]
        public NativeArray<Vector3> BoidPositions;
        [WriteOnly]
        public NativeArray<Quaternion> BoidRotations;

        public void Execute(int index, TransformAccess trans)
        {
            BoidPositions[index] = trans.position;
            BoidRotations[index] = trans.rotation;
        }
    }
}

