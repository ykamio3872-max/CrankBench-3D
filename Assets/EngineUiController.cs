using UnityEngine;
using UnityEngine.UI;
using TMPro;
using XCharts.Runtime;

public class EngineUiController : MonoBehaviour
{
    [Header("References to 3D Engine")]
    [SerializeField] private EngineController engineController;

    [Header("UI Controls (Inputs)")]
    [SerializeField] private Slider rpmSlider;
    [SerializeField] private Slider boreSlider;       // ★追加
    [SerializeField] private Slider strokeSlider;
    [SerializeField] private Slider conrodSlider;
    [SerializeField] private Slider boostSlider;      // ★追加

    [Header("UI Value Displays (Inputs - スライダーの隣)")]
    [SerializeField] private TextMeshProUGUI rpmText;
    [SerializeField] private TextMeshProUGUI boreText;      // ★追加
    [SerializeField] private TextMeshProUGUI strokeText;
    [SerializeField] private TextMeshProUGUI conrodText;
    [SerializeField] private TextMeshProUGUI boostText;     // ★追加

    [Header("UI Status Displays (Outputs - 画面右下)")]
    [SerializeField] private TextMeshProUGUI angleText;
    [SerializeField] private TextMeshProUGUI pressureText;
    [SerializeField] private TextMeshProUGUI temperatureText;
    [SerializeField] private TextMeshProUGUI strokePhaseText;

    [Header("UI Graphs (XCharts)")]
    [SerializeField] private LineChart pvChart; // LineChart を使用

    void Start()
    {
        if (engineController == null || rpmSlider == null || boreSlider == null || strokeSlider == null || conrodSlider == null || boostSlider == null)
        {
            Debug.LogError("UI Managerにスライダー等のオブジェクトが正しく紐付けられていません！");
            return;
        }

        // 1. 初期値を同期
        rpmSlider.value = engineController.rpm;
        boreSlider.value = engineController.bore;
        strokeSlider.value = engineController.stroke;
        conrodSlider.value = engineController.conrodLength;
        boostSlider.value = engineController.boost;

        UpdateSliderTexts();

        // 2. リスナー登録
        rpmSlider.onValueChanged.AddListener(OnRpmChanged);
        boreSlider.onValueChanged.AddListener(OnBoreChanged);
        strokeSlider.onValueChanged.AddListener(OnStrokeChanged);
        conrodSlider.onValueChanged.AddListener(OnConrodChanged);
        boostSlider.onValueChanged.AddListener(OnBoostChanged);

        UpdateSimulation();
    }

    void Update()
    {
        if (engineController != null)
        {
            float currentAngle = engineController.GetCurrentAngle(); 
            var currentPoint = engineController.GetSimulationPointAtAngle(currentAngle);

            if (angleText != null) angleText.text = $"クランク角: {currentAngle:F0}°";
            if (pressureText != null) pressureText.text = $"筒内圧力: {currentPoint.pressureMpa:F3} MPa";
            if (temperatureText != null) temperatureText.text = $"筒内温度: {(currentPoint.temperatureK - 273.15f):F1} ℃";
            
            UpdateStrokePhaseText(currentAngle);
        }
    }

    private void OnRpmChanged(float value) { engineController.rpm = value; UpdateSliderTexts(); UpdateSimulation(); }
    private void OnBoreChanged(float value) { engineController.bore = value; UpdateSliderTexts(); UpdateSimulation(); }
    private void OnStrokeChanged(float value) { engineController.stroke = value; UpdateSliderTexts(); UpdateSimulation(); }
    private void OnConrodChanged(float value) { engineController.conrodLength = value; UpdateSliderTexts(); UpdateSimulation(); }
    private void OnBoostChanged(float value) { engineController.boost = value; UpdateSliderTexts(); UpdateSimulation(); }

    private void UpdateSliderTexts()
    {
        if (rpmText != null) rpmText.text = $"{rpmSlider.value:F0} rpm";
        if (boreText != null) boreText.text = $"{boreSlider.value:F1} mm";
        if (strokeText != null) strokeText.text = $"{strokeSlider.value:F1} mm";
        if (conrodText != null) conrodText.text = $"{conrodSlider.value:F1} mm";
        if (boostText != null) boostText.text = $"{boostSlider.value:F2} bar";
    }

    private void UpdateSimulation()
    {
        engineController.RecalculatePhysics();
        UpdateGraphs();
    }

    private void UpdateGraphs()
    {
        if (pvChart == null) return;
        
        // 1. グラフの古いデータをクリア
        pvChart.ClearData(); 
        
        var simPoints = engineController.GetSimulationPoints(); 
        if (simPoints == null || simPoints.Length == 0) return;

        // 【修正：Listのインデックス指定 [0] を使用して自動ソートを解除】
        // if (pvChart.series != null && pvChart.series.Count > 0)
        // {
        //     // バージョンによるプロパティ名の違いを吸収するため、try-catch、または直接指定します
        //     // 通常、3.x系は大文字スタートの ShowDataSort です
        //     pvChart.series[0].showDataSort = false;
        // }

        // 2. 4つ飛ばしでプロット（負荷軽減）
        for (int i = 0; i < simPoints.Length; i += 4) 
        {
            // X軸：Volume(cc)、Y軸：Pressure(MPa)
            pvChart.AddData(0, (float)simPoints[i].volumeCc, (float)simPoints[i].pressureMpa);
        }
        
        // 3. グラフを画面に再描画
        pvChart.RefreshChart(); 
    }

    private void UpdateStrokePhaseText(float angle)
    {
        if (strokePhaseText == null) return;
        float normalizedAngle = angle % 720f;
        if (normalizedAngle < 0) normalizedAngle += 720f;

        if (normalizedAngle >= 0 && normalizedAngle < 180) {
            strokePhaseText.text = "行程: ① 吸気 (Intake)";
            strokePhaseText.color = new Color(0.3f, 0.6f, 1.0f);
        } else if (normalizedAngle >= 180 && normalizedAngle < 360) {
            strokePhaseText.text = "行程: ② 圧縮 (Compression)";
            strokePhaseText.color = new Color(1.0f, 0.7f, 0.3f);
        } else if (normalizedAngle >= 360 && normalizedAngle < 540) {
            strokePhaseText.text = "行程: ③ 燃焼膨張 (Power)";
            strokePhaseText.color = new Color(1.0f, 0.3f, 0.3f);
        } else {
            strokePhaseText.text = "行程: ④ 排気 (Exhaust)";
            strokePhaseText.color = Color.gray;
        }
    }
}