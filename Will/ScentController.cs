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
    [SerializeField] private int scentNumber = 9;  // New field for scent number
    [SerializeField] private int intensity = 100;
    [SerializeField] private int fanActive = 5000;
    [SerializeField] private int valveDelay = 1000;
    
    private UdpClient udpClient;
    private bool clientInitialized = false;
    
    private void Start()
    {
        InitializeUdpClient();
    }
    
    private void InitializeUdpClient()
    {
        try
        {
            // Initialize UDP client
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
        // Check if key 9 is pressed
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9))
        {
            TriggerScent(scentNumber);  // Use the inspector-specified scent number
        }
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
        
        // Use custom parameters if provided, otherwise use default values
        int actualIntensity = customIntensity > 0 ? customIntensity : intensity;
        int actualFanActive = customFanActive > 0 ? customFanActive : fanActive;
        int actualValveDelay = customValveDelay > 0 ? customValveDelay : valveDelay;
        
        // Format: [random_id],OUT,[scent_number],[intensity],1,[fan_active],[valve_delay]
        int messageId = Random.Range(10000, 100000);
        string scentStr = scentNumber.ToString("D2");  // Pad with zero if single digit
        string message = $"{messageId},OUT,{scentStr},{actualIntensity},1,{actualFanActive:D5},{actualValveDelay}";
        
        byte[] sendBytes = Encoding.ASCII.GetBytes(message);
        
        // Send 3 times for reliability
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
            
            yield return new WaitForSeconds(0.05f);  // Small delay between sends
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