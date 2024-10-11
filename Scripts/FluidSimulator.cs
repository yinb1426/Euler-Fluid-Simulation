using UnityEngine;

public class FluidSimulator : MonoBehaviour
{
    [SerializeField]
    ComputeShader _ComputeShader;

    [SerializeField]
    Material _Material;

    [SerializeField]
    float _DeltaT = 0.03f;

    [SerializeField]
    float _VelocityDiffusion = 1.5f;

    [SerializeField]
    float _DensityDiffusion = 1.0f;

    [SerializeField]
    float _Radius = 1.0f;

    [SerializeField]
    float _MomentumStrength = 1.0f;

    [SerializeField]
    bool _ViscosityOn = true;

    [SerializeField, Range(0.01f, 2.0f)]
    float _Viscosity = 0.5f;

    [SerializeField]
    Texture2D _BackgroundTexture;

    private int textureResolution = 1024;
    private Vector2 previousMousePosition;
    private Vector2 mouseDirection;
    private int maxJacobiIteration = 100;
    private Texture2D backgroundTexture;

    RenderTexture dye, newDye, velocity, newVelocity, pressure, newPressure, divergence;

    private Vector2 GetMousePositionAndDirection(ref Vector2 prevPos)
    {
        Vector2 mouseDir = Vector2.zero;
        if (Input.GetMouseButton(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                if (prevPos.Equals(new Vector2(-1, -1)))
                {
                    prevPos = hit.textureCoord * textureResolution;
                }
                else
                {
                    mouseDir = (hit.textureCoord * textureResolution - prevPos).normalized;
                    prevPos = hit.textureCoord * textureResolution;
                }
            }
        }
        else
            prevPos = new Vector2(-1, -1);
        return mouseDir;

    }

    private Texture2D DuplicateTexture(Texture2D source)
    {
        RenderTexture renderTex = RenderTexture.GetTemporary(
                    source.width,
                    source.height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Linear);

        Graphics.Blit(source, renderTex);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = renderTex;
        Texture2D readableText = new Texture2D(source.width, source.height);
        readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
        readableText.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(renderTex);
        return readableText;
    }

    // Start is called before the first frame update
    void Awake()
    {
        backgroundTexture = DuplicateTexture(_BackgroundTexture);
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = _Material;

        dye = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.ARGBHalf) { enableRandomWrite = true, useMipMap = false, wrapMode = TextureWrapMode.Clamp};
        newDye = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.ARGBHalf) { enableRandomWrite = true, useMipMap = false, wrapMode = TextureWrapMode.Clamp };
        velocity = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.RGHalf) { enableRandomWrite = true, useMipMap = false, wrapMode = TextureWrapMode.Clamp };
        newVelocity = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.RGHalf) { enableRandomWrite = true, useMipMap = false, wrapMode = TextureWrapMode.Clamp };
        pressure = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.RHalf) { enableRandomWrite = true, useMipMap = false, wrapMode = TextureWrapMode.Clamp }; ;
        newPressure = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.RHalf) { enableRandomWrite = true, useMipMap = false, wrapMode = TextureWrapMode.Clamp };
        divergence = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.RHalf) { enableRandomWrite = true, useMipMap = false, wrapMode = TextureWrapMode.Clamp };

        dye.Create();
        newDye.Create();
        velocity.Create();
        newVelocity.Create();
        pressure.Create();
        newPressure.Create();
        divergence.Create();
    }

    // Compute Shader Settings
    static readonly int
        textureResolutionId = Shader.PropertyToID("textureResolution"),
        deltaTId = Shader.PropertyToID("deltaT"),
        velocityDiffusionId = Shader.PropertyToID("velocityDiffusion"),
        densityDiffusionId = Shader.PropertyToID("densityDiffusion"),
        mousePosId = Shader.PropertyToID("mousePos"),
        mouseDirId = Shader.PropertyToID("mouseDir"),
        mouseColorId = Shader.PropertyToID("mouseColor"),
        radiusId = Shader.PropertyToID("radius"),
        momentumStrengthId = Shader.PropertyToID("momentumStrength"),
        viscosityId = Shader.PropertyToID("viscosity");

    static readonly int
        dyeId = Shader.PropertyToID("dye"),
        newDyeId = Shader.PropertyToID("newDye"),
        velocityId = Shader.PropertyToID("velocity"),
        newVelocityId = Shader.PropertyToID("newVelocity"),
        pressureId = Shader.PropertyToID("pressure"),
        newPressureId = Shader.PropertyToID("newPressure"),
        divergenceId = Shader.PropertyToID("divergence");

    private int groupsX;
    private int groupsY;

    private int advectionVelocityKernelHandle;
    private int advectionDyeKernelHandle;
    private int viscosityDiffusionKernelHandle;
    private int applyForceKernelHandle;
    private int divergenceKernelHandle;
    private int pressureJacobiKernelHandle;
    private int subtractGradientKernelHandle;

    void OnEnable()
    {
        groupsX = Mathf.CeilToInt(textureResolution / 32f);
        groupsY = Mathf.CeilToInt(textureResolution / 16f);

        _ComputeShader.SetInt(textureResolutionId, textureResolution);

        advectionVelocityKernelHandle = _ComputeShader.FindKernel("AdvectionVelocity");
        _ComputeShader.SetTexture(advectionVelocityKernelHandle, velocityId, velocity);
        _ComputeShader.SetTexture(advectionVelocityKernelHandle, newVelocityId, newVelocity);

        advectionDyeKernelHandle = _ComputeShader.FindKernel("AdvectionDye");
        _ComputeShader.SetTexture(advectionDyeKernelHandle, velocityId, velocity);
        _ComputeShader.SetTexture(advectionDyeKernelHandle, dyeId, dye);
        _ComputeShader.SetTexture(advectionDyeKernelHandle, newDyeId, newDye);

        viscosityDiffusionKernelHandle = _ComputeShader.FindKernel("ViscosityDiffusion");
        _ComputeShader.SetTexture(viscosityDiffusionKernelHandle, velocityId, velocity);
        _ComputeShader.SetTexture(viscosityDiffusionKernelHandle, newVelocityId, newVelocity);

        applyForceKernelHandle = _ComputeShader.FindKernel("ApplyForce");
        _ComputeShader.SetTexture(applyForceKernelHandle, dyeId, dye);
        _ComputeShader.SetTexture(applyForceKernelHandle, velocityId, velocity);

        divergenceKernelHandle = _ComputeShader.FindKernel("Divergence");
        _ComputeShader.SetTexture(divergenceKernelHandle, velocityId, velocity);
        _ComputeShader.SetTexture(divergenceKernelHandle, divergenceId, divergence);

        pressureJacobiKernelHandle = _ComputeShader.FindKernel("PressureJacobi");
        _ComputeShader.SetTexture(pressureJacobiKernelHandle, pressureId, pressure);
        _ComputeShader.SetTexture(pressureJacobiKernelHandle, divergenceId, divergence);
        _ComputeShader.SetTexture(pressureJacobiKernelHandle, newPressureId, newPressure);

        subtractGradientKernelHandle = _ComputeShader.FindKernel("SubtractGradient");
        _ComputeShader.SetTexture(subtractGradientKernelHandle, pressureId, pressure);
        _ComputeShader.SetTexture(subtractGradientKernelHandle, velocityId, velocity);
    }

    void OnDestroy()
    {
        dye.Release();
        newDye.Release();
        velocity.Release();
        newVelocity.Release();
        pressure.Release();
        newPressure.Release();
        divergence.Release();
    }

    // Update is called once per frame
    void Update()
    {
        _ComputeShader.SetFloat(deltaTId, _DeltaT);
        _ComputeShader.SetFloat(velocityDiffusionId, _VelocityDiffusion);
        _ComputeShader.SetFloat(densityDiffusionId, _DensityDiffusion);
        _ComputeShader.SetFloat(radiusId, _Radius * 10.0f);
        _ComputeShader.SetFloat(momentumStrengthId, _MomentumStrength);
        _ComputeShader.SetFloat(viscosityId, _Viscosity);

        // Get Mouse Movement
        mouseDirection = GetMousePositionAndDirection(ref previousMousePosition);
        _ComputeShader.SetVector(mousePosId, previousMousePosition);
        _ComputeShader.SetVector(mouseDirId, mouseDirection);
        _ComputeShader.SetVector(mouseColorId, backgroundTexture.GetPixel((int)previousMousePosition.x, (int)previousMousePosition.y));

        // Advection
        _ComputeShader.Dispatch(advectionVelocityKernelHandle, groupsX, groupsY, 1);
        _ComputeShader.Dispatch(advectionDyeKernelHandle, groupsX, groupsY, 1);
        Graphics.CopyTexture(newVelocity, velocity);
        Graphics.CopyTexture(newDye, dye);

        // Diffusion
        if(_ViscosityOn)
        {
            for (int i = 0; i < maxJacobiIteration; i++)
            {
                _ComputeShader.Dispatch(viscosityDiffusionKernelHandle, groupsX, groupsY, 1);
                Graphics.CopyTexture(newVelocity, velocity);
            }
        }

        // Force
        _ComputeShader.Dispatch(applyForceKernelHandle, groupsX, groupsY, 1);

        // Projection
        _ComputeShader.Dispatch(divergenceKernelHandle, groupsX, groupsY, 1);
        for (int i = 0; i < maxJacobiIteration; i++)
        {
            _ComputeShader.Dispatch(pressureJacobiKernelHandle, groupsX, groupsY, 1);
            Graphics.CopyTexture(newPressure, pressure);
        }
        _ComputeShader.Dispatch(subtractGradientKernelHandle, groupsX, groupsY, 1);

        // Material Set Texture
        _Material.SetTexture("_FlowMap", velocity);
        _Material.SetTexture("_DyeTexture", dye);
        _Material.SetTexture("_BackgroundTexture", _BackgroundTexture);
    }
}
