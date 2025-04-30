using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using Random = UnityEngine.Random;

public class ScentController : MonoBehaviour
{
    // Device settings
    [SerializeField] private string broadcastIP = "192.168.1.255";
    [SerializeField] private int udpPort = 5010;
    
    // Scent parameters
    [SerializeField] private int testScentNumber = 1;
    [SerializeField] private int intensity = 100;
    [SerializeField] private int fanActive = 10000;
    [SerializeField] private int valveDelay = 1000;
    
    // Skid detection parameters
    [SerializeField] private float skidSpeedThreshold = 15f;  // Speed in m/s (about 54 km/h)
    [SerializeField] private float skidYawRateThreshold = 80f; // Degrees per second
    [SerializeField] private int burnedRubberScentNumber = 10; // Scent number for burned rubber
    private float checkInterval = 0.1f; // Check for conditions every X seconds
    private float scentCooldown = 3f; // Minimum time between scent triggers
    
    // Brake detection parameters
    [SerializeField] private float minSpeedForBraking = 30; // Minimum speed before deceleration is considered
    [SerializeField] private float decelerationThreshold = -10f; // Negative value: m/s² (more negative = harder braking)
    
    // Testing parameters
    [SerializeField] private bool useAudioForTesting = false; // Toggle to use audio instead of scent
    [SerializeField] private AudioClip skidSound; // Sound to play when skid is detected
    [SerializeField] private AudioClip brakeSound; // Sound to play when hard braking is detected
    [SerializeField] private bool debugVisuals = true;
    
    private UdpClient udpClient;
    private bool clientInitialized = false;
    private Rigidbody vehicleRigidbody;
    private float lastSkidTime = -10f;
    private Vector3 lastRotation;
    private float currentYawRate;
    private Vector3 lastVelocity;
    private Vector3 currentAcceleration;
    private AudioSource audioSource;
    private string lastTriggerReason = "";
    
    private void Start()
    {
        InitializeUdpClient();
        
        vehicleRigidbody = GameObject.FindWithTag("Vehicle").GetComponent<Rigidbody>();
        if (vehicleRigidbody == null)
        {
            Debug.LogError("Vehicle rigidbody not found! Make sure the vehicle has a 'Vehicle' tag.");
        }
        else
        {
            Debug.Log("Vehicle rigidbody found successfully.");
            lastRotation = vehicleRigidbody.rotation.eulerAngles;
            lastVelocity = vehicleRigidbody.velocity;
        }
        
        if (useAudioForTesting && !TryGetComponent(out audioSource))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.priority = 0;
            Debug.Log("Added AudioSource component for testing sounds");
            
            if (skidSound == null)
            {
                Debug.LogWarning("No skid sound assigned. Will use beep for testing.");
            }
            
            if (brakeSound == null)
            {
                Debug.LogWarning("No brake sound assigned. Will use beep for testing.");
            }
        }
        
        StartCoroutine(CheckVehicleConditions());
    }
    
    private void InitializeUdpClient()
    {
        try
        {
            udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            clientInitialized = true;
            Debug.Log("UDP client initialized successfully");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize UDP client: {e.Message}");
            clientInitialized = false;
        }
    }
    
    private void Update()
    {
        // Check if key 9 is pressed (for testing)
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9))
        {
            if (useAudioForTesting)
            {
                PlayTestBeep(440, 0.5f);
                Debug.Log("Test beep played (Key 9 pressed)");
            }
            else
            {
                TriggerScent(testScentNumber);
            }
        }
        
        if (vehicleRigidbody != null)
        {
            // Calculate yaw rate (angular velocity around Y axis)
            Vector3 currentRotation = vehicleRigidbody.rotation.eulerAngles;
            float yawDifference = Mathf.DeltaAngle(lastRotation.y, currentRotation.y);
            currentYawRate = Mathf.Abs(yawDifference / Time.deltaTime);
            lastRotation = currentRotation;

            currentAcceleration = (vehicleRigidbody.velocity - lastVelocity) / Time.deltaTime;
            lastVelocity = vehicleRigidbody.velocity;
        }
    }
    
    private void OnGUI()
    {
        if (debugVisuals && vehicleRigidbody != null)
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 18;
            style.fontStyle = FontStyle.Bold;
            
            float currentSpeed = vehicleRigidbody.velocity.magnitude;
            float forwardAccel = Vector3.Dot(currentAcceleration, vehicleRigidbody.transform.forward);
            
            GUI.Label(new Rect(10, 10, 400, 30), $"Speed: {currentSpeed:F1} m/s ({currentSpeed * 3.6f:F1} km/h)", style);
            GUI.Label(new Rect(10, 40, 400, 30), $"Yaw Rate: {currentYawRate:F1} deg/s", style);
            GUI.Label(new Rect(10, 70, 400, 30), $"Acceleration: {forwardAccel:F1} m/s²", style);
            
            if (!string.IsNullOrEmpty(lastTriggerReason))
            {
                style.normal.textColor = Color.yellow;
                GUI.Label(new Rect(10, 100, 400, 30), $"Last Trigger: {lastTriggerReason}", style);
            }
        }
    }
    
    private IEnumerator CheckVehicleConditions()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            
            if (vehicleRigidbody != null && Time.time - lastSkidTime > scentCooldown)
            {
                bool isSkidding = CheckSkidCondition();
                bool isHardBraking = CheckHardBrakingCondition();
                
                if (isSkidding || isHardBraking)
                {
                    if (useAudioForTesting)
                    {
                        if (isSkidding)
                        {
                            PlaySkidSound();
                            lastTriggerReason = "Skidding";
                        }
                        else if (isHardBraking)
                        {
                            PlayBrakeSound();
                            lastTriggerReason = "Hard Braking";
                        }
                    }
                    else
                    {
                        // Trigger burned rubber scent
                        TriggerScent(burnedRubberScentNumber);
                        lastTriggerReason = isSkidding ? "Skidding" : "Hard Braking";
                    }
                    
                    lastSkidTime = Time.time;
                }
            }
        }
    }
    
    private bool CheckSkidCondition()
    {
        float currentSpeed = vehicleRigidbody.velocity.magnitude;
        
        if (currentSpeed > skidSpeedThreshold && currentYawRate > skidYawRateThreshold)
        {
            Debug.Log($"Skid detected! Speed: {currentSpeed:F1} m/s, Yaw Rate: {currentYawRate:F1} deg/s");
            return true;
        }
        
        return false;
    }
    
    private bool CheckHardBrakingCondition()
    {
        float currentSpeed = vehicleRigidbody.velocity.magnitude;
        float forwardAccel = Vector3.Dot(currentAcceleration, vehicleRigidbody.transform.forward);
        
        // We need to be moving at decent speed and have strong negative acceleration (deceleration)
        if (currentSpeed > minSpeedForBraking && forwardAccel < decelerationThreshold)
        {
            Debug.Log($"Hard braking detected! Speed: {currentSpeed:F1} m/s, Deceleration: {forwardAccel:F1} m/s²");
            return true;
        }
        
        return false;
    }
    
    private void PlaySkidSound()
    {
        if (audioSource != null)
        {
            if (skidSound != null)
            {
                audioSource.clip = skidSound;
                audioSource.Play();
            }
            else
            {
                StartCoroutine(PlaySequenceBeep(660, 0.2f, 880, 0.2f));
            }
        }
    }
    
    private void PlayBrakeSound()
    {
        if (audioSource != null)
        {
            if (brakeSound != null)
            {
                audioSource.clip = brakeSound;
                audioSource.Play();
            }
            else
            {
                PlayTestBeep(220, 0.5f);
            }
        }
    }
    
    private void PlayTestBeep(float frequency, float duration)
    {
        if (audioSource != null)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.FloorToInt(sampleRate * duration);
            AudioClip clip = AudioClip.Create("TestBeep", sampleCount, 1, sampleRate, false);
            
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                // Create a simple sine wave with a slight fade out
                float fade = 1.0f - (t / duration);
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * t) * fade;
            }
            
            clip.SetData(samples, 0);
            audioSource.PlayOneShot(clip);
        }
    }
    
    private IEnumerator PlaySequenceBeep(float freq1, float dur1, float freq2, float dur2)
    {
        PlayTestBeep(freq1, dur1);
        yield return new WaitForSeconds(dur1);
        PlayTestBeep(freq2, dur2);
    }
    
    public void TriggerScent(int scentNumber, int customIntensity = -1, int customFanActive = -1, int customValveDelay = -1)
    {
        // Ensure the client is initialized
        if (!clientInitialized)
        {
            Debug.LogWarning("UDP client not initialized. Attempting to reinitialize...");
            InitializeUdpClient();
            
            if (!clientInitialized)
            {
                Debug.LogError("Failed to reinitialize UDP client. Cannot send scent trigger.");
                return;
            }
        }
        
        int actualIntensity = customIntensity > 0 ? customIntensity : intensity;
        int actualFanActive = customFanActive > 0 ? customFanActive : fanActive;
        int actualValveDelay = customValveDelay > 0 ? customValveDelay : valveDelay;
        
        // Format: [random_id],OUT,[scent_number],[intensity],1,[fan_active],[valve_delay]
        int messageId = Random.Range(10000, 100000);
        string scentStr = scentNumber.ToString("D2");
        string message = $"{messageId},OUT,{scentStr},{actualIntensity},1,{actualFanActive:D5},{actualValveDelay}";
        
        byte[] sendBytes = Encoding.ASCII.GetBytes(message);
        StartCoroutine(SendMessageMultipleTimes(sendBytes, 3));
        
        Debug.Log($"Sent scent trigger: {message}");
    }
    
    private IEnumerator SendMessageMultipleTimes(byte[] data, int times)
    {
        if (udpClient == null)
        {
            Debug.LogError("UDP client is null when trying to send message");
            yield break;
        }
        
        for (int i = 0; i < times; i++)
        {
            try
            {
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(broadcastIP), udpPort);
                udpClient.Send(data, data.Length, endPoint);
                Debug.Log($"Successfully sent packet {i+1}/{times}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error sending UDP packet: {e.Message}");
            }
            
            yield return new WaitForSeconds(0.05f);
        }
    }
    
    private void OnDestroy()
    {
        if (udpClient != null)
        {
            try
            {
                udpClient.Close();
                Debug.Log("UDP client closed");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error closing UDP client: {e.Message}");
            }
        }
    }
}