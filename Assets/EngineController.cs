using UnityEngine;
using System.Runtime.InteropServices;

public class EngineController : MonoBehaviour
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SimulationPoint {
        public double crankAngleDeg;
        public double volumeCc;
        public double pistonYMm;
        public double pressureMpa;
        public double temperatureK;
        public double entropyJK;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PerformancePoint {
        public double rpm;
        public double torqueKgfm;
        public double powerPs;
    }

    [DllImport("engine_physics")]
    private static extern void calculate_kinematics_unity(
        double boreMm, double strokeMm, double conrodLengthMm, double compressionRatio,
        int cylinders, double rpm, double boostX100kpa,
        [In, Out] SimulationPoint[] outPoints,
        [In, Out] PerformancePoint[] outPerfPoints,
        ref double outMaxTorqueNm,
        ref double outMaxPowerPs
    );

    [Header("Engine Dimensions (mm)")]
    public float bore = 86.0f;          // ★追加: ボア径
    public float stroke = 68.2f;
    public float conrodLength = 120.0f;
    public float cylinderPitch = 80.0f;

    [Header("Operational Settings")]
    public float rpm = 2000f;
    public float boost = 0.0f;          // ★追加: 過給圧 (bar/100kPa)

    [Header("3D Object References")]
    public Transform crankshaft;
    public Transform[] pistons = new Transform[3];
    public Transform[] conrods = new Transform[3];
    
    [Header("Rendering References")]
    public MeshRenderer[] pistonRenderers = new MeshRenderer[3];

    private const float ScaleFactor = 0.01f; 
    private float currentAngle = 0f;
    private readonly float[] phases = new float[] { 0f, 240f, 480f };

    private SimulationPoint[] simPoints = new SimulationPoint[721];
    private PerformancePoint[] perfPoints = new PerformancePoint[41];
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        RecalculatePhysics();
    }

    void Update()
    {
        // RPMに基づいた正確な回転速度計算（減速しないバグを修正）
        float degPerSecond = (rpm * 360f) / 60f;
        currentAngle = (currentAngle + degPerSecond * Time.deltaTime) % 720f;

        if (crankshaft != null)
        {
            crankshaft.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        }

        float r = (stroke * ScaleFactor) / 2.0f;
        float l = conrodLength * ScaleFactor;

        for (int i = 0; i < 3; i++)
        {
            if (pistons[i] == null || conrods[i] == null) continue;

            float cylAngleRad = (currentAngle + phases[i]) * Mathf.Deg2Rad;
            float pinX = -r * Mathf.Sin(cylAngleRad);
            float pinY = r * Mathf.Cos(cylAngleRad);
            float pinZ = (i - 1) * (cylinderPitch * ScaleFactor);
            Vector3 crankPinPos = new Vector3(pinX, pinY, pinZ);

            float asinTerm = pinX / l;
            float conrodAngleRad = Mathf.Asin(Mathf.Clamp(asinTerm, -1f, 1f));
            float pistonY = r * Mathf.Cos(cylAngleRad) + l * Mathf.Cos(conrodAngleRad);
            Vector3 pistonPinPos = new Vector3(0f, pistonY, pinZ);

            pistons[i].localPosition = pistonPinPos;
            conrods[i].localPosition = crankPinPos;
            conrods[i].up = (pistons[i].position - conrods[i].position).normalized;

            if (i < pistonRenderers.Length && pistonRenderers[i] != null)
            {
                float myCrankAngle = currentAngle + phases[i];
                while (myCrankAngle >= 720f) myCrankAngle -= 720f;
                while (myCrankAngle < 0f) myCrankAngle += 720f;
                int index = Mathf.Clamp((int)myCrankAngle, 0, 720);

                double temperatureK = simPoints[index].temperatureK;
                float t = Mathf.InverseLerp(300f, 2500f, (float)temperatureK);
                float intensity = Mathf.Pow(t, 2) * 5.0f;
                Color emissionColor = new Color(1.0f, 0.3f, 0.0f) * intensity;
                pistonRenderers[i].material.SetColor(EmissionColorId, emissionColor);
            }
        }
    }

    public void RecalculatePhysics()
    {
        double maxTorque = 0; 
        double maxPower = 0;
        
        // 固定値だった部分を UI と連動する変数に差し替え
        calculate_kinematics_unity(
            bore, stroke, conrodLength, 10.0, 3, rpm, boost, 
            simPoints, perfPoints, ref maxTorque, ref maxPower
        );
    }

    public float GetCurrentAngle() { return currentAngle; }

    public SimulationPoint GetSimulationPointAtAngle(float angle)
    {
        int index = Mathf.Clamp(Mathf.RoundToInt(angle), 0, 720);
        if (simPoints != null && index < simPoints.Length) return simPoints[index];
        return new SimulationPoint();
    }

    public SimulationPoint[] GetSimulationPoints() { return simPoints; }
}