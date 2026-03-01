using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public interface IBlendSource
    {
        float CurBlendWeight { get; set; }
    }

    public class MultiSourceBlender<T> : List<T> where T : IBlendSource
    {
        private int curTargetSourceIdx;
        private float blendTime;
        private float blendVelocity;
        public T CurTargetSource => this[curTargetSourceIdx];
        public float CurTargetWeight
        {
            get => CurTargetSource.CurBlendWeight;
            set => CurTargetSource.CurBlendWeight = value;
        }

        public MultiSourceBlender(float blendTime) : base()
        {
            this.blendTime = blendTime;
            curTargetSourceIdx = -1;
        }

        public float GetTargetWeight(int index)
        {
            return this[index].CurBlendWeight;
        }

        public void RegisterSource(T source, bool snapTo = false)
        {
            if (!Contains(source))
            {
                Add(source);
                if (snapTo || curTargetSourceIdx < 0)
                {
                    curTargetSourceIdx = Count - 1;
                    SnapTo(curTargetSourceIdx);
                }
                else
                {
                    source.CurBlendWeight = 0f;
                }
            }
        }

        public void SetTargetIndex(int index, bool snapTo = false)
        {
            curTargetSourceIdx = index;
            if (snapTo)
            {
                SnapTo(index);
            }
        }

        public void Update(float dt)
        {
            if (curTargetSourceIdx < 0 || curTargetSourceIdx >= Count)
            {
                return;
            }
            float curTargetWeight = CurTargetWeight;
            curTargetWeight = Mathf.SmoothDamp(curTargetWeight, 1, ref blendVelocity, blendTime, Mathf.Infinity, dt);
            float totalSpareWeight = Mathf.Clamp01(1 - curTargetWeight);
            CurTargetWeight = 1 - totalSpareWeight;
            float curSpareWeight = 0f;
            for (int i = 0; i < Count; i++)
            {
                if (i != curTargetSourceIdx)
                {
                    curSpareWeight += this[i].CurBlendWeight;
                }
            }

            float scale = curSpareWeight > 0 ? totalSpareWeight / curSpareWeight : 0f;
            for (int i = 0; i < Count; i++)
            {
                if (i != curTargetSourceIdx)
                {
                    if (curSpareWeight > 0)
                    {
                        this[i].CurBlendWeight *= scale;
                    }
                }
            }

            Normalize();
        }

        public void SnapTo(int index = -1)
        {
            if (index < 0 || index >= Count)
            {
                index = curTargetSourceIdx;
            }
            for (int i = 0; i < Count; i++)
            {
                if (i == index)
                {
                    this[i].CurBlendWeight = 1;
                }
                else
                {
                    this[i].CurBlendWeight = 0;
                }
            }
        }

        private void Normalize()
        {
            float sum = 0f;
            for (int i = 0; i < Count; i++)
            {
                sum += this[i].CurBlendWeight;
            }

            if (sum <= 0f)
            {
                return;
            }

            for(int i = 0; i < Count; i++)
            {
                this[i].CurBlendWeight /= sum;
            }
        }
    }
}
