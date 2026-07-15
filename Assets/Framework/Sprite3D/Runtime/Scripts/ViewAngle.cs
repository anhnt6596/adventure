using UnityEngine;

// Self-contained view-angle helper for the sealed Sprite3D module.
// Inlined from the jam's MathUtils to keep this module free of game deps.
public static class ViewAngleUtil
{
    // 0..7, one slot per 45° sector; 0 = facing camera-forward sector.
    public static int GetViewType8(float angleY)
    {
        angleY %= 360f;
        if (angleY < 0) angleY += 360f;

        angleY += 22.5f;
        if (angleY >= 360f) angleY -= 360f;

        return Mathf.FloorToInt(angleY / 45f);
    }
}
