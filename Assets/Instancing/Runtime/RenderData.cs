using UnityEngine;
using UnityEngine.Rendering;

namespace Aperture.Instancing.Runtime
{
    [System.Serializable]
    public class LOD
    {
        public float screenRelativeTransitionHeight;

        public Mesh mesh;
        public Material[] materials;
    }

    [CreateAssetMenu]
    public class RenderData : ScriptableObject
    {
        public LayerMask layer;

        //Lighting
        public ShadowCastingMode shadowCastingMode;
        public LightProbeUsage lightProbeUsage;

        public bool useAdditonalData;

        public LOD[] lods;

        public bool isDistanceCulling;
        public float minDistance = 0;
        public float maxDistance = 500;

        public bool isFrustumCulling;

        public bool isOcclusionCulling;
    }
}

