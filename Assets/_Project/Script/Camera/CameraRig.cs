using System.Collections.Generic;
using UnityEngine;

public class CameraRig : MonoBehaviour
{
    [SerializeField] Transform target;

    [Header("Orbit")]
    [SerializeField] float pitch = 45f;
    [SerializeField] float yaw = 0f;
    [SerializeField] float distance = 12f;
    [SerializeField] Vector3 pivotOffset;

    [Header("Follow")]
    [SerializeField, Range(0.001f, 1f)] float smooth = 0.05f;

    [Header("Rotate (Q/E snap)")]
    [SerializeField] float snapSpeed = 8f;

    ICameraMode _mode;
    float _targetYaw;
    readonly List<ICameraModifier> _modifiers = new List<ICameraModifier>();

    public Transform Target { get => target; set => target = value; }
    public float Pitch { get => pitch; set => pitch = value; }
    public float Yaw { get => yaw; set => yaw = value; }
    public float Distance { get => distance; set => distance = value; }
    public Vector3 PivotOffset { get => pivotOffset; set => pivotOffset = value; }
    public float Smooth => smooth;

    public Vector3 Pivot => new Vector3(target.position.x, 0f, target.position.z) + pivotOffset;
    public Vector3 OrbitPosition => Pivot + Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);

    void Awake()
    {
        _mode ??= new FollowMode();
        _targetYaw = yaw;
    }

    void Start()
    {
        if (target == null) return;
        _mode.Enter(this);
        var (pos, rot) = _mode.Solve(this, 0f);
        transform.SetPositionAndRotation(pos, rot);
    }

    void LateUpdate()
    {
        if (target == null || _mode == null) return;

        yaw = Mathf.Repeat(Mathf.LerpAngle(yaw, _targetYaw, Time.deltaTime * snapSpeed), 360f);

        var (pos, rot) = _mode.Solve(this, Time.deltaTime);
        for (int i = 0; i < _modifiers.Count; i++)
            (pos, rot) = _modifiers[i].Apply(this, pos, rot);

        transform.SetPositionAndRotation(pos, rot);
    }

    public void SetMode(ICameraMode mode)
    {
        if (mode == null) return;
        _mode?.Exit(this);
        _mode = mode;
        _mode.Enter(this);
    }

    public void AddModifier(ICameraModifier modifier)
    {
        if (modifier != null && !_modifiers.Contains(modifier)) _modifiers.Add(modifier);
    }

    public void RemoveModifier(ICameraModifier modifier) => _modifiers.Remove(modifier);

    public void RotateYaw(float step) => _targetYaw = Mathf.Repeat(_targetYaw + step, 360f);
}
