using System.IO.Ports;
using System.Collections.Generic;
using UnityEngine;

public class WindController : MonoBehaviour 
{
    [Header("Arduino Connection")]
    [SerializeField] private string portName = "COM6";
    [SerializeField] private int baudRate = 9600;
    [SerializeField] private float updateInterval = 0.1f;
    
    [Header("Wind Settings")]
    [SerializeField] private float speedMultiplier = 0.8f;
    [SerializeField] private float steeringMultiplier = 1.0f; // Adjust how much steering affects wind direction
    [SerializeField] private float minSpeed = 10.0f; // Minimum speed before wind starts
    [SerializeField] private float maxSpeed = 200.0f;
    
    [Header("FPS Counter Settings")]
    [SerializeField] private float fpsAverageDuration = 30f; // Duration in seconds for FPS averaging
    [SerializeField] private float fpsUpdateInterval = 0.5f; // How often to update the FPS counter
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    
    private SerialPort serialPort;
    private float timer = 0f;
    private Rigidbody vehicleRigidbody;
    private float currentRotationValue = 0f;
    private float currentSpeedValue = 0f;
    private float steeringInput = 0f;
    
    // FPS tracking
    private float fpsAccumulator = 0;
    private int fpsFrames = 0;
    private float currentFps = 0;
    private Queue<FpsDataPoint> fpsHistory = new Queue<FpsDataPoint>();
    private float fpsHistoryTotalTime = 0f;
    private int fpsHistoryTotalFrames = 0;
    private float averageFps = 0f;
    
    // Simple struct to store FPS data points
    private struct FpsDataPoint
    {
        public float timeInterval;
        public int frameCount;
        
        public FpsDataPoint(float time, int frames)
        {
            timeInterval = time;
            frameCount = frames;
        }
    }
    
    void Start() 
    {
        // Connect to the Arduino
        serialPort = new SerialPort(portName, baudRate);
        try {
            serialPort.Open();
            Debug.Log("Connected to Arduino on " + portName);
        } catch (System.Exception e) {
            Debug.LogError("Failed to connect: " + e.Message);
        }
        
        vehicleRigidbody = GameObject.FindWithTag("Vehicle").GetComponent<Rigidbody>();
        
        if (vehicleRigidbody == null) {
            Debug.LogError("Vehicle rigidbody not found! Make sure the vehicle has a 'Vehicle' tag.");
        }
    }
    
    void Update() 
    {
        // FPS calculation
        fpsAccumulator += Time.deltaTime;
        fpsFrames++;
        
        // Update FPS display at regular intervals
        if (fpsAccumulator >= fpsUpdateInterval)
        {
            currentFps = fpsFrames / fpsAccumulator;
            
            // Add this interval to our history
            fpsHistory.Enqueue(new FpsDataPoint(fpsAccumulator, fpsFrames));
            fpsHistoryTotalTime += fpsAccumulator;
            fpsHistoryTotalFrames += fpsFrames;
            
            // Remove old entries if we exceed the average duration
            while (fpsHistoryTotalTime > fpsAverageDuration && fpsHistory.Count > 0)
            {
                FpsDataPoint oldestEntry = fpsHistory.Dequeue();
                fpsHistoryTotalTime -= oldestEntry.timeInterval;
                fpsHistoryTotalFrames -= oldestEntry.frameCount;
            }
            
            // Calculate the average FPS over the entire history
            if (fpsHistoryTotalTime > 0)
            {
                averageFps = fpsHistoryTotalFrames / fpsHistoryTotalTime;
            }
            
            // Reset for next interval
            fpsFrames = 0;
            fpsAccumulator = 0;
        }

        UpdateSteeringInput();
        
        timer += Time.deltaTime;
        if (timer >= updateInterval) {
            timer = 0f;
            UpdateWindEffects();
        }
    }
    
    void UpdateSteeringInput()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        
        if (Mathf.Approximately(horizontalInput, 0f) && vehicleRigidbody != null) {
            float yawRate = vehicleRigidbody.angularVelocity.y;
            horizontalInput = Mathf.Clamp(yawRate / 2.0f, -1.0f, 1.0f);
        }
        
        steeringInput = Mathf.Lerp(steeringInput, horizontalInput, Time.deltaTime * 5f);
    }
    
    void UpdateWindEffects() 
    {
        if (serialPort != null && serialPort.IsOpen && vehicleRigidbody != null) {
            float vehicleSpeed = vehicleRigidbody.velocity.magnitude * 3.6f;
            float normalizedSpeed = CalculateWindIntensity(vehicleSpeed);
            float rotationValue = steeringInput * steeringMultiplier;
            
            currentSpeedValue = normalizedSpeed;
            currentRotationValue = rotationValue;
            
            string command = $"{normalizedSpeed:F1},{rotationValue:F2}\n";
            serialPort.Write(command);
        }
    }
    
    float CalculateWindIntensity(float speed) 
    {
        // No wind below minimum speed
        if (speed < minSpeed) {
            return 0f;
        }
        
        float normalizedSpeed = (speed - minSpeed) / (maxSpeed - minSpeed);
        normalizedSpeed = Mathf.Clamp01(normalizedSpeed);
        float curvedValue = normalizedSpeed * normalizedSpeed * (3 - 2 * normalizedSpeed);
        return curvedValue * 200f * speedMultiplier;
    }
    
    void OnGUI()
    {
        if (showDebugInfo)
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 18;
            style.fontStyle = FontStyle.Bold;
            
            // Display both current and average FPS
            GUI.Label(new Rect(10, 100, 400, 30), $"Current FPS: {currentFps:F1}", style);
            GUI.Label(new Rect(10, 130, 400, 30), $"Avg FPS (30s): {averageFps:F1}", style);
            
            // Your existing debug info
            GUI.Label(new Rect(10, 160, 400, 30), $"Wind Speed: {currentSpeedValue:F1}", style);
            GUI.Label(new Rect(10, 190, 400, 30), $"Wind Direction: {currentRotationValue:F2}", style);
            GUI.Label(new Rect(10, 220, 400, 30), "Fan Power:", style);
            
            // Left fan
            float leftFanPower = CalculateFanPower(currentSpeedValue, currentRotationValue, true);
            GUI.Label(new Rect(10, 250, 100, 30), "Left:", style);
            GUI.Box(new Rect(60, 255, Mathf.Lerp(10, 200, leftFanPower/255f), 20), "");
            
            // Center fan
            float centerFanPower = CalculateFanPower(currentSpeedValue, 0, false);
            GUI.Label(new Rect(10, 280, 100, 30), "Center:", style);
            GUI.Box(new Rect(60, 285, Mathf.Lerp(10, 200, centerFanPower/255f), 20), "");
            
            // Right fan
            float rightFanPower = CalculateFanPower(currentSpeedValue, currentRotationValue, false);
            GUI.Label(new Rect(10, 310, 100, 30), "Right:", style);
            GUI.Box(new Rect(60, 315, Mathf.Lerp(10, 200, rightFanPower/255f), 20), "");
        }
    }
    
    float CalculateFanPower(float speed, float rotation, bool isLeftFan)
    {
        if (speed < minSpeed) {
            return 0;
        }
        
        float baseSpeed = speed > maxSpeed ? 255 : Mathf.Lerp(50, 255, (speed - minSpeed) / (maxSpeed - minSpeed));
        
        if (isLeftFan) {
            if (rotation < 0) { // Turning left
                float leftReduction = Mathf.Abs(rotation);
                return baseSpeed * (1.0f - leftReduction * 0.7f);
            } 
            else if (rotation > 0) { // Turning right
                float rightIncrease = rotation;
                return baseSpeed * (1.0f + rightIncrease * 0.3f);
            }
        } 
        else { // Right fan
            if (rotation < 0) { // Turning left
                float leftIncrease = Mathf.Abs(rotation);
                return baseSpeed * (1.0f + leftIncrease * 0.3f);
            } 
            else if (rotation > 0) { // Turning right
                float rightReduction = rotation;
                return baseSpeed * (1.0f - rightReduction * 0.7f);
            }
        }
        
        return baseSpeed;
    }
    
    void OnApplicationQuit() 
    {
        // Ensure fans are turned off and connection is closed when application ends
        if (serialPort != null && serialPort.IsOpen) {
            serialPort.WriteLine("0,0");
            serialPort.Close();
        }
    }
}