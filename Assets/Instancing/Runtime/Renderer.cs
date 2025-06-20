using System.Collections.Generic;
using UnityEngine;

namespace Aperture.Instancing.Runtime
{
    internal class Renderer
    {
        internal Mesh mesh;
        internal Material[] materials;

        internal MaterialPropertyBlock materialPropertyBlock;

        internal ComputeBuffer visibleIndicesBuffer;

        internal int argsBufferOffset;

        public Renderer()
        {
            materialPropertyBlock = new MaterialPropertyBlock();
        }
    }
}
