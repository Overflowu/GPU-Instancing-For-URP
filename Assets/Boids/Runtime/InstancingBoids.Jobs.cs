using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

partial class InstancingBoids : MonoBehaviour
{
    [BurstCompile]
    public struct UpdateArrayJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Matrix4x4> LocalToWorld;
        [WriteOnly]
        public NativeArray<Vector3> BoidPositions;
        [WriteOnly]
        public NativeArray<Quaternion> BoidRotations;
        [WriteOnly]
        public NativeArray<Vector3> BoidScales;

        public void Execute(int index)
        {
            BoidPositions[index] = LocalToWorld[index].GetPosition();
            BoidRotations[index] = LocalToWorld[index].rotation;
            BoidScales[index] = LocalToWorld[index].lossyScale;
        }
    }

    [BurstCompile]
    public struct BoidsJob : IJobParallelFor
    {
        public NativeArray<Vector3> BoidVelocities;

        [ReadOnly]
        public NativeArray<Vector3> BoidPositions;
        [ReadOnly]
        public NativeArray<Quaternion> BoidRotations;
        [ReadOnly]
        public NativeArray<Vector3> BoidScales;
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

        [WriteOnly]
        public NativeArray<Matrix4x4> LocalToWorld;

        Vector3 GetSeparationVector(Vector3 current, Vector3 targetPos)
        {
            Vector3 diff = current - targetPos;
            float diffLen = diff.magnitude;
            float scaler = Mathf.Clamp01(1.0f - diffLen / NeighborDist);
            return diff * (scaler / diffLen);
        }

        public void Execute(int index)
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

            BoidVelocities[index] = velocity;

            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            if (rotation != currentRotation)
            {
                float ip = Mathf.Exp(-RotationCoeff * DeltaTime);
                rotation = Quaternion.Slerp(rotation, currentRotation, ip);
            }
            Vector3 position = BoidPositions[index] + (velocity * DeltaTime);
            Vector3 scale = BoidScales[index];

            LocalToWorld[index] = Matrix4x4.TRS(position, rotation, scale);
        }
    }
}
