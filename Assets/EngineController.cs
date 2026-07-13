using UnityEngine;
using System.Runtime.InteropServices; // 追加: DllImport用

public class EngineController : MonoBehaviour
{
    // --- 追加: DLL連携用の構造体とインポート ---
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
    // ------------------------------------------

    [Header("Engine Dimensions (mm)")]
    [Tooltip("ピストンのストローク（行程）")]
    public float stroke = 68.2f;
    [Tooltip("コンロッド長（芯間距離）")]
    public float conrodLength = 120.0f;
    [Tooltip("シリンダー間の中心距離（ボアピッチ）")]
    public float cylinderPitch = 80.0f;

    [Header("Operational Settings")]
    [Tooltip("エンジン回転数 (RPM)")]
    public float rpm = 2000f;

    [Header("3D Object References")]
    public Transform crankshaft;
    public Transform[] pistons = new Transform[3];
    public Transform[] conrods = new Transform[3];
    
    // 追加: 燃焼発光させるピストンのレンダラー
    [Header("Rendering References")]
    public MeshRenderer[] pistonRenderers = new MeshRenderer[3];

    // Unity内のスケール合わせ（1mmを何単位とするか。1mm = 0.001m とするなら 0.001f）
    private const float ScaleFactor = 0.01f; 

    private float currentAngle = 0f;
    // 直列3気筒のクランク位相（1番: 0°, 2番: 240°, 3番: 480° ※あるいは120°間隔）
    private readonly float[] phases = new float[] { 0f, 240f, 480f };

    // --- 追加: 発光用データ ---
    private SimulationPoint[] simPoints = new SimulationPoint[721];
    private PerformancePoint[] perfPoints = new PerformancePoint[41];
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    // -------------------------

    void Start()
    {
        // 追加: DLLを呼び出してシミュレーションデータを生成
        double maxTorque = 0; double maxPower = 0;
        // ボア径(86.0)や圧縮比(10.0)などは元のコードの固定値を踏襲していますが、必要に応じて変数化してください
        calculate_kinematics_unity(86.0, stroke, conrodLength, 10.0, 3, rpm, 0.0, simPoints, perfPoints, ref maxTorque, ref maxPower);
    }

    void Update()
    {
        // 1. クランクシャフトの回転角度を進める (Time.deltaTime 同期)
        // deg/sec = (RPM * 360) / 60
        float degPerSecond = (rpm * 360f) / 60f;
        currentAngle = (currentAngle + degPerSecond * Time.deltaTime) % 720f;

        // クランクシャフト自体の回転（Z軸回転を想定。モデルの向きに合わせて調整してください）
        if (crankshaft != null)
        {
            crankshaft.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
        }

        // 2. 各気筒のピストン・コンロッドの運動幾何学計算
        float r = (stroke * ScaleFactor) / 2.0f;
        float l = conrodLength * ScaleFactor;

        for (int i = 0; i < 3; i++)
        {
            if (pistons[i] == null || conrods[i] == null) continue;

            // クランク角の計算（120度位相）
            float cylAngleRad = (currentAngle + phases[i]) * Mathf.Deg2Rad;

            // ① クランクピンの絶対座標を計算
            float pinX = -r * Mathf.Sin(cylAngleRad);
            float pinY = r * Mathf.Cos(cylAngleRad);
            float pinZ = (i - 1) * (cylinderPitch * ScaleFactor);
            
            // エンジン中心（EngineMain）から見たクランクピンの位置
            Vector3 crankPinPos = new Vector3(pinX, pinY, pinZ);

            // ② ピストンの高さ計算
            float asinTerm = pinX / l;
            float conrodAngleRad = Mathf.Asin(Mathf.Clamp(asinTerm, -1f, 1f));
            float pistonY = r * Mathf.Cos(cylAngleRad) + l * Mathf.Cos(conrodAngleRad);
            
            // ピストンピンの位置
            Vector3 pistonPinPos = new Vector3(0f, pistonY, pinZ);

            // ③ オブジェクトへの座標適用
            pistons[i].localPosition = pistonPinPos;

            // 【重要】コンロッドの下端（大端部）をクランクピンの位置に固定
            conrods[i].localPosition = crankPinPos;
            
            // 【重要】コンロッドの上方向（Y軸）を、ピストンピンの座標へ強制的に向かせる
            conrods[i].up = (pistons[i].position - conrods[i].position).normalized;

            // --- 追加: 燃焼発光処理 ---
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
            // --------------------------
        }
    }
}