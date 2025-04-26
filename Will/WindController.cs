using System.IO.Ports;
using UnityEngine;

public class WindController : MonoBehaviour 
{
    [SerializeField] private string portName = "COM6"; // Change this to match your Arduino port
    [SerializeField] private int baudRate = 9600;
    [SerializeField] private float updateInterval = 0.1f;
    
    private SerialPort serialPort;
    private float timer = 0f;
    private Rigidbody vehicleRigidbody;
    
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
        
        // Get reference to vehicle physics
       vehicleRigidbody = GameObject.FindWithTag("Vehicle").GetComponent<Rigidbody>();
    }
    
    void Update() 
    {
        // Update at specified interval to avoid flooding the serial connection
        timer += Time.deltaTime;
        if (timer >= updateInterval) {
            timer = 0f;
            UpdateWindSpeed();
        }
    }
    
    void UpdateWindSpeed() 
    {
        if (serialPort != null && serialPort.IsOpen) {
            // Calculate wind speed based on vehicle speed
            float vehicleSpeed = vehicleRigidbody.velocity.magnitude * 3.6f; // Convert to km/h
            float windSpeed = CalculateWindIntensity(vehicleSpeed);
            
            // Send speed value to Arduino
            serialPort.WriteLine(windSpeed.ToString("F1"));
        }
    }
    
    float CalculateWindIntensity(float speed) 
    {
        // Simple linear mapping of vehicle speed to fan speed
        return Mathf.Clamp(speed * 2f, 0f, 255f);
    }
    
    void OnApplicationQuit() 
    {
        // Ensure fan is turned off and connection is closed when application ends
        if (serialPort != null && serialPort.IsOpen) {
            serialPort.WriteLine("0");
            serialPort.Close();
        }
    }
}