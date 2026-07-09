using UnityEngine;

public class EngineController : MonoBehaviour
{
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

    // Unity内のスケール合わせ（1mmを何単位とするか。1mm = 0.001m とするなら 0.001f）
    private const float ScaleFactor = 0.01f; 

    private float currentAngle = 0f;
    // 直列3気筒のクランク位相（1番: 0°, 2番: 240°, 3番: 480° ※あるいは120°間隔）
    private readonly float[] phases = new float[] { 0f, 240f, 480f };

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
        }
    }
}