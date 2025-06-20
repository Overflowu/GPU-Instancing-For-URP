using UnityEngine;

namespace Aperture.Instancing.Runtime
{
    public static class ShaderProperties
    {
        public static readonly int COMPUTE_PARAMS_COUNT = Shader.PropertyToID("_Count");

        public static readonly int CULLING_PARAMS_CAMERAPARAMETERS = Shader.PropertyToID("_CameraParameters");
        public static readonly int CULLING_PARAMS_IS_DISTANCE_CULLING = Shader.PropertyToID("_IsDistanceCulling");
        public static readonly int CULLING_PARAMS_MIN_DISTANCE = Shader.PropertyToID("_MinDistance");
        public static readonly int CULLING_PARAMS_MAX_DISTANCE = Shader.PropertyToID("_MaxDistance");
        public static readonly int CULLING_PARAMS_IS_FRUSTUM_CULLING = Shader.PropertyToID("_IsFrustumCulling");
        public static readonly int CULLING_PARAMS_FRUSTUMPLANES = Shader.PropertyToID("_FrustumPlanes");
        public static readonly int CULLING_PARAMS_BOUNDMIN = Shader.PropertyToID("_BoundMin");
        public static readonly int CULLING_PARAMS_BOUNDMAX = Shader.PropertyToID("_BoundMax");
        public static readonly int CULLING_PARAMS_IS_OCCLUSION_CULLING = Shader.PropertyToID("_IsOcclusionCulling");

        public static readonly int LOD_PARAMS_SIZES = Shader.PropertyToID("_LodSizes");
        public static readonly int LOD_PARAMS_COUNT = Shader.PropertyToID("_LodCount");
        public static readonly int LOD_PARAMS_SHIFT = Shader.PropertyToID("_LodShift");

        public static readonly int BUFFER_LOACLTOWORLDMATRICES = Shader.PropertyToID("_LocalToWorldMatrices");
        public static readonly int BUFFER_ADDITIONALDATA = Shader.PropertyToID("_AdditionalData");
        public static readonly int BUFFER_LODVISIBILITY = Shader.PropertyToID("_LodVisibility");
        public static readonly int BUFFER_VISIBLEINDICES = Shader.PropertyToID("_VisibleIndices");
        public static readonly int[] BUFFER_VISIBLEINDICESLOD =
        {
            Shader.PropertyToID("_VisibleIndicesLod0"),
            Shader.PropertyToID("_VisibleIndicesLod1"),
            Shader.PropertyToID("_VisibleIndicesLod2")
        };
    }
}
