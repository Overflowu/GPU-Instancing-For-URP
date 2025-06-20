using System;
using UnityEngine;

namespace Aperture.Instancing.Runtime
{

    public interface IHandle
    {
        /// <summary>
        /// 处理数据初始化
        /// </summary>
        event Action<ComputeBuffer, int, int> onInitialize;

        /// <summary>
        /// 处理数据更新
        /// </summary>
        event Action<ComputeBuffer, int, int> onUpdate;

        /// <summary>
        /// 重建时保留被废弃的数据
        /// </summary>
        event Action<ComputeBuffer, int, int> onRelease;

        /// <summary>
        /// 处理额外数据初始化
        /// </summary>
        event Action<ComputeBuffer, int, int> onInitializeAdditionalData;

        /// <summary>
        /// 处理额外数据更新
        /// </summary>
        event Action<ComputeBuffer, int, int> onUpdateAdditionalData;

        /// <summary>
        /// 重建时保留被废弃的额外数据
        /// </summary>
        event Action<ComputeBuffer, int, int> onReleaseAdditionalData;
    }

    internal enum State
    {
        Existed,
        Increased,
        Removed,
    }

    internal class Segment : IHandle
    {

        public event Action<ComputeBuffer, int, int> onInitialize;
        public event Action<ComputeBuffer, int, int> onUpdate;
        public event Action<ComputeBuffer, int, int> onRelease;

        public event Action<ComputeBuffer, int, int> onInitializeAdditionalData;
        public event Action<ComputeBuffer, int, int> onUpdateAdditionalData;
        public event Action<ComputeBuffer, int, int> onReleaseAdditionalData;

        internal int Start { get; set; }
        internal int Count { get; set; }
        internal State State { get; set; }

        internal Segment(int start, int count, State state)
        {
            Start = start;
            Count = count;
            State = state;
        }

        internal void Initialize(ComputeBuffer localToWorldMatricesBuffer, ComputeBuffer addtionalDataBuffer)
        {
            onInitialize?.Invoke(localToWorldMatricesBuffer, Start, Count);
            onInitializeAdditionalData?.Invoke(addtionalDataBuffer, Start, Count);
        }

        internal void Update(ComputeBuffer localToWorldMatricesBuffer, ComputeBuffer addtionalDataBuffer)
        {
            onUpdate?.Invoke(localToWorldMatricesBuffer, Start, Count);
            onUpdateAdditionalData?.Invoke(addtionalDataBuffer, Start, Count);
        }

        internal void Release(ComputeBuffer localToWorldMatricesBuffer, ComputeBuffer addtionalDataBuffer)
        {
            onRelease?.Invoke(localToWorldMatricesBuffer, Start, Count);
            onReleaseAdditionalData?.Invoke(addtionalDataBuffer, Start, Count);
        }
    }
}
