using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KFCommonUtilityLib
{
    public interface IValueDamper<T>
    {
        T CurrentValue { get; }
        T TargetValue { get; set; }
        T UpdateDamper();
        void Reset(T value = default);
    }

    public struct FloatValueDamper : IValueDamper<float>
    {
        public float currentValue;
        public float currentVelocity;
        public float targetValue;
        public float targetTime;

        public float CurrentValue => currentValue;

        public float TargetValue { get => targetValue; set => targetValue = value; }

        public float UpdateDamper()
        {
            return currentValue = Mathf.SmoothDamp(currentValue, targetValue, ref currentVelocity, targetTime);
        }

        public void Reset(float value = 0)
        {
            currentValue = targetValue = value;
            currentVelocity = 0;
        }
    }

    public class Vector3ValueDamper : IValueDamper<Vector3>
    {
        public Vector3 currentValue;
        public Vector3 currentVelocity;
        public Vector3 targetValue;
        public float targetTime;

        public Vector3 CurrentValue => currentValue;

        public Vector3 TargetValue { get => targetValue; set => targetValue = value; }

        public Vector3 UpdateDamper()
        {
            return currentValue = Vector3.SmoothDamp(currentValue, targetValue, ref currentVelocity, targetTime);
        }

        public void Reset(Vector3 value = default)
        {
            currentValue = value;
            currentVelocity = Vector3.zero;
        }
    }

    public class QuaternionValueDamper : IValueDamper<Quaternion>
    {
        public Quaternion currentValue;
        public Quaternion currentVelocity;
        public Quaternion targetValue;
        public float targetTime;

        public Quaternion CurrentValue => currentValue;
        public Quaternion TargetValue { get => targetValue; set => targetValue = value; }

        public Quaternion UpdateDamper()
        {
            return currentValue = QuaternionUtil.SmoothDamp(currentValue, targetValue, ref currentVelocity, targetTime);
        }

        public void Reset(Quaternion value = default)
        {
            currentValue = value;
            currentVelocity = Quaternion.identity;
        }
    }
}
