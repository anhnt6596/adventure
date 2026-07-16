using UnityEngine;

public class ShakeModifier : ICameraModifier
{
    float _amplitude;
    float _duration;
    float _timeLeft;

    public void Play(float amplitude, float duration)
    {
        _amplitude = amplitude;
        _duration = duration;
        _timeLeft = duration;
    }

    public (Vector3, Quaternion) Apply(CameraRig rig, Vector3 position, Quaternion rotation)
    {
        if (_timeLeft <= 0f) return (position, rotation);

        _timeLeft -= Time.deltaTime;
        float k = _duration > 0f ? Mathf.Clamp01(_timeLeft / _duration) : 0f;
        return (position + Random.insideUnitSphere * (_amplitude * k), rotation);
    }
}
