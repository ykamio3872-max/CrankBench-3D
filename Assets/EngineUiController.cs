using UnityEngine;
using UnityEngine.UIElements; // ★変更: uGUI/TMProではなくUIElementsを使う

public class EngineUiController : MonoBehaviour
{
    [Header("References to 3D Engine")]
    [SerializeField] private EngineController engineController; // ★ここはそのまま再利用

    // UI Toolkitでは、Inspectorからアタッチするのではなくスクリプト内で取得します
    private Slider rpmSlider;
    private Slider boreSlider;
    private Slider strokeSlider;
    private Slider conrodSlider;
    private Slider boostSlider;

    private Label rpmText;
    private Label boreText;
    private Label strokeText;
    private Label conrodText;
    private Label boostText;

    private Label angleText;
    private Label pressureText;
    private Label temperatureText;
    private Label strokePhaseText;

    void Start() // ★Startではなく、UIが有効になったタイミングで取得します
    {
        if (engineController == null)
        {
            Debug.LogError("EngineControllerが紐付けられていません！");
            return;
        }

        // UI Documentからルート要素を取得
        var root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null)
        {
            Debug.LogWarning("rootVisualElementが取得できませんでした。");
            return;
        }

        // 1. UI Builderで設定した「Name（ID）」を元に部品を取得
        // ※ UI Builder側でこれらのNameを設定しておく必要があります
        rpmSlider = root.Q<Slider>("slider-rpm");
        boreSlider = root.Q<Slider>("slider-bore");
        strokeSlider = root.Q<Slider>("slider-stroke");
        conrodSlider = root.Q<Slider>("slider-conrod");
        boostSlider = root.Q<Slider>("slider-boost");

        rpmText = root.Q<Label>("text-rpm");
        boreText = root.Q<Label>("text-bore");
        strokeText = root.Q<Label>("text-stroke");
        conrodText = root.Q<Label>("text-conrod");
        boostText = root.Q<Label>("text-boost");

        angleText = root.Q<Label>("text-angle");
        pressureText = root.Q<Label>("text-pressure");
        temperatureText = root.Q<Label>("text-temperature");
        strokePhaseText = root.Q<Label>("text-stroke-phase");

        angleText = root.Q<Label>("text-angle");
        if (angleText == null) Debug.LogError("❌ 'text-angle' というNameのLabelが見つかりません！");

        pressureText = root.Q<Label>("text-pressure");
        if (pressureText == null) Debug.LogError("❌ 'text-pressure' というNameのLabelが見つかりません！");

        temperatureText = root.Q<Label>("text-temperature");
        if (temperatureText == null) Debug.LogError("❌ 'text-temperature' というNameのLabelが見つかりません！");

        strokePhaseText = root.Q<Label>("text-stroke-phase");
        if (strokePhaseText == null) Debug.LogError("❌ 'text-stroke-phase' というNameのLabelが見つかりません！");
        
        // 2. 初期値を同期
        if (rpmSlider != null) rpmSlider.value = engineController.rpm;
        if (boreSlider != null) boreSlider.value = engineController.bore;
        if (strokeSlider != null) strokeSlider.value = engineController.stroke;
        if (conrodSlider != null) conrodSlider.value = engineController.conrodLength;
        if (boostSlider != null) boostSlider.value = engineController.boost;

        UpdateSliderTexts();

        // 3. リスナー登録 (UI Toolkitの書き方)
        if (rpmSlider != null) rpmSlider.RegisterValueChangedCallback(evt => OnRpmChanged(evt.newValue));
        if (boreSlider != null) boreSlider.RegisterValueChangedCallback(evt => OnBoreChanged(evt.newValue));
        if (strokeSlider != null) strokeSlider.RegisterValueChangedCallback(evt => OnStrokeChanged(evt.newValue));
        if (conrodSlider != null) conrodSlider.RegisterValueChangedCallback(evt => OnConrodChanged(evt.newValue));
        if (boostSlider != null) boostSlider.RegisterValueChangedCallback(evt => OnBoostChanged(evt.newValue));

        UpdateSimulation();

        Debug.Log($"[UI診断] rpmSlider: {rpmSlider != null} / angleText: {angleText != null} / pressureText: {pressureText != null}");
    }

    void Update()
    {
        if (engineController != null)
        {
            float currentAngle = engineController.GetCurrentAngle(); 

            Debug.Log($"[リアルタイム確認] 現在の角度: {currentAngle}");
            var currentPoint = engineController.GetSimulationPointAtAngle(currentAngle); //[cite: 1]

            if (angleText != null) angleText.text = $"クランク角: {currentAngle:F0}°";
            if (pressureText != null) pressureText.text = $"筒内圧力: {currentPoint.pressureMpa:F3} MPa";
            if (temperatureText != null) temperatureText.text = $"筒内温度: {(currentPoint.temperatureK - 273.15f):F1} ℃";
            
            UpdateStrokePhaseText(currentAngle); //[cite: 1]
        }
    }

    // イベントリスナー
    private void OnRpmChanged(float value) { engineController.rpm = value; UpdateSliderTexts(); UpdateSimulation(); } //[cite: 1]
    private void OnBoreChanged(float value) { engineController.bore = value; UpdateSliderTexts(); UpdateSimulation(); } //[cite: 1]
    private void OnStrokeChanged(float value) { engineController.stroke = value; UpdateSliderTexts(); UpdateSimulation(); } //[cite: 1]
    private void OnConrodChanged(float value) { engineController.conrodLength = value; UpdateSliderTexts(); UpdateSimulation(); } //[cite: 1]
    private void OnBoostChanged(float value) { engineController.boost = value; UpdateSliderTexts(); UpdateSimulation(); } //[cite: 1]

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
        engineController.RecalculatePhysics(); //[cite: 1]
        UpdateGraphs();
    }

    private void UpdateGraphs()
    {
        // ※ グラフの処理については下記「XChartsについて」を参照
    }

    private void UpdateStrokePhaseText(float angle)
    {
        if (strokePhaseText == null) return;
        float normalizedAngle = angle % 720f;
        if (normalizedAngle < 0) normalizedAngle += 720f;

        // UI Toolkitでは style.color に Color 構造体を渡します
        if (normalizedAngle >= 0 && normalizedAngle < 180) {
            strokePhaseText.text = "行程: ① 吸気 (Intake)"; //[cite: 1]
            strokePhaseText.style.color = new StyleColor(new Color(0.3f, 0.6f, 1.0f)); //[cite: 1]
        } else if (normalizedAngle >= 180 && normalizedAngle < 360) {
            strokePhaseText.text = "行程: ② 圧縮 (Compression)"; //[cite: 1]
            strokePhaseText.style.color = new StyleColor(new Color(1.0f, 0.7f, 0.3f)); //[cite: 1]
        } else if (normalizedAngle >= 360 && normalizedAngle < 540) {
            strokePhaseText.text = "行程: ③ 燃焼膨張 (Power)"; //[cite: 1]
            strokePhaseText.style.color = new StyleColor(new Color(1.0f, 0.3f, 0.3f)); //[cite: 1]
        } else {
            strokePhaseText.text = "行程: ④ 排気 (Exhaust)"; //[cite: 1]
            strokePhaseText.style.color = new StyleColor(Color.gray); //[cite: 1]
        }
    }
}