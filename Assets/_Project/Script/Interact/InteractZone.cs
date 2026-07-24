using UnityEngine;
using VContainer;

// A proximity trigger: when the actor's position is inside the shape, OnActorEnter fires (once),
// OnActorExit when they leave. No priority, no filtering — every overlapping zone fires.
// Subclass it (e.g. Portal) to give a zone behaviour.
public abstract class InteractZone : MonoBehaviour
{
    [SerializeField] protected ZoneShape shape = ZoneShape.DefaultCircle;

    InteractField _field;

    public Vector3 Center => transform.position;
    public float BoundingRadius => shape.BoundingRadius;
    public bool Contains(Vector3 worldPoint) => shape.Contains(transform, worldPoint);

    // Injected when the map (or scene) it lives in is injected; that's also when it joins the field.
    [Inject]
    public void ConstructZone(InteractField field)
    {
        _field = field;
        _field.Register(this);
    }

    protected virtual void Start()
    {
        if (_field == null)
            Debug.LogError($"[{GetType().Name}] not injected — its map must be instantiated through the DI container (MapService does this), or added to GameScope's Auto Inject list.", this);
    }

    protected virtual void OnDestroy() => _field?.Unregister(this);

    public virtual void OnActorEnter(MCController actor) { }
    public virtual void OnActorExit(MCController actor) { }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (shape.kind == ZoneKind.Circle)
        {
            const int seg = 32;
            Vector3 prev = new Vector3(shape.radius, 0, 0);
            for (int i = 1; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 next = new Vector3(Mathf.Cos(a) * shape.radius, 0, Mathf.Sin(a) * shape.radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
        else
        {
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(shape.size.x, 0f, shape.size.y));
        }
    }
}
