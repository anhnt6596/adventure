using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class LightManager : MonoBehaviour
{
    public Material darknessMaterial, fogMaterial;
    public Color ambientColor = new Color(0.1f, 0.1f, 0.15f, 1f);
    public Color fogColor = new Color(0.1f, 0.1f, 0.15f, 1f);
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

    private void CheckLightTexture()
    {
        float aspect = (float)Screen.width / Screen.height;
        int width = Mathf.RoundToInt(baseResolution * aspect);
        int height = baseResolution;

        if (LightTexture == null || LightTexture.width != width || LightTexture.height != height)
        {
            if (LightTexture) LightTexture.Release();
            LightTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
            LightTexture.name = "LightMap";
            lightCam.targetTexture = LightTexture;
        }
    }
}
