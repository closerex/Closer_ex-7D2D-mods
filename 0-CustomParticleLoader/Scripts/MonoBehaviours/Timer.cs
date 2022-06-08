using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 计时器
/// <para>ZhangYu 2018-04-08</para>
/// <para>code from: https://segmentfault.com/a/1190000015325310 </para>
/// </summary>
public class Timer : MonoBehaviour
{
    // delay in second
    public float delay = 0;
    // interval in second
    public float interval = 1;
    // repeat count
    public int repeatCount = 1;
    // automatically start
    public bool autoStart = false;
    // automatically destroy
    public bool autoDestory = true;
    // current time
    public float currentTime = 0;
    // current count
    public int currentCount = 0;
    // event when interval reached
    public UnityEvent onIntervalEvent;
    // event when timer completed
    public UnityEvent onCompleteEvent;
    // callback delegate
    public delegate void TimerCallback(Timer timer);
    // last interval time
    private float lastTime = 0;
    // interval callback
    private TimerCallback onIntervalCall;
    // complete callback
    private TimerCallback onCompleteCall;

    private void Start()
    {
        enabled = autoStart;
    }

    private void FixedUpdate()
    {
        if (!enabled) return;
        addInterval(Time.deltaTime);
    }

    /// <summary> add interval </summary>
    private void addInterval(float deltaTime)
    {
        currentTime += deltaTime;
        if (currentTime < delay) return;
        if (currentTime - lastTime >= interval)
        {
            currentCount++;
            lastTime = currentTime;
            if (repeatCount <= 0)
            {
                // repeate forever
                if (currentCount == int.MaxValue) reset();
                if (onIntervalCall != null) onIntervalCall(this);
                if (onIntervalEvent != null) onIntervalEvent.Invoke();
            }
            else
            {
                if (currentCount < repeatCount)
                {
                    if (onIntervalCall != null) onIntervalCall(this);
                    if (onIntervalEvent != null) onIntervalEvent.Invoke();
                }
                else
                {
                    stop();
                    if (onCompleteCall != null) onCompleteCall(this);
                    if (onCompleteEvent != null) onCompleteEvent.Invoke();
                    if (autoDestory && !enabled) Destroy(this);
                }
            }
        }
    }

    public void start()
    {
        enabled = autoStart = true;
    }

    public void start(float time, TimerCallback onComplete)
    {
        start(time, 1, null, onComplete);
    }

    public void start(float interval, int repeatCount, TimerCallback onComplete)
    {
        start(interval, repeatCount, null, onComplete);
    }

    public void start(float interval, int repeatCount, TimerCallback onInterval, TimerCallback onComplete)
    {
        this.interval = interval;
        this.repeatCount = repeatCount;
        onIntervalCall = onInterval;
        onCompleteCall = onComplete;
        reset();
        enabled = autoStart = true;
    }

    public void stop()
    {
        enabled = autoStart = false;
    }

    public void reset()
    {
        lastTime = currentTime = currentCount = 0;
    }

    public void restart()
    {
        reset();
        start();
    }

}