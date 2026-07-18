using UnityEngine;

[ExecuteAlways]
public class LightManager : MonoBehaviour
{
    public Material darknessMaterial, fogMaterial;
    public Color ambientColor = new Color(0.1f, 0.1f, 0.15f, 1f);
    public Color fogColor = Color.black;   // additive light on the Fog overlay — this IS the glare: brighter = more "chói", black = off
    public int baseResolution = 512;
    public Camera lightCam;

    private RenderTexture LightTexture;

    [Range(0f, 1f)]
    public float lightIntensity = 1f;

    private void Update()
    {
        CheckLightTexture();

        if (darknessMaterial != null && LightTexture != null)
        {
            darknessMaterial.SetTexture("_LightTex", LightTexture);
            darknessMaterial.SetColor("_DarkColor", ambientColor * lightIntensity);
        }
        if (fogMaterial != null)
        {
            fogMaterial.SetColor("_Color", fogColor);
        }
    }

    private void OnDisable() => DisposeTexture();

    private void CheckLightTexture()
    {
        float aspect = (float)Screen.width / Screen.height;
        int width = Mathf.RoundToInt(baseResolution * aspect);
        int height = baseResolution;

        if (LightTexture != null && LightTexture.width == width && LightTexture.height == height) return;

        DisposeTexture();
        LightTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGBHalf) { name = "LightMap" };
        if (lightCam) lightCam.targetTexture = LightTexture;
    }

    private void DisposeTexture()
    {
        if (LightTexture == null) return;
        if (lightCam) lightCam.targetTexture = null;
        LightTexture.Release();
        // Release() frees the GPU surface but leaks the object -> destroy it too.
        if (Application.isPlaying) Destroy(LightTexture); else DestroyImmediate(LightTexture);
        LightTexture = null;
    }
}
