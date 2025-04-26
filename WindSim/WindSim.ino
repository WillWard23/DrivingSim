int fanPin = 9; // PWM pin connected to the fan

void setup() {
  Serial.begin(9600); // Initialize serial communication
  pinMode(fanPin, OUTPUT); // Set the fan pin as an output
  analogWrite(fanPin, 0); // Initialize fan speed to 0 (off)
}

void loop() {
  if (Serial.available() > 0) {
    // Read the speed value from Unity
    float speed = Serial.parseFloat();
    
    // Map the received speed to a PWM value (0-255)
    int fanSpeed = 0;
    
    if (speed < 10) {
      // Turn off fan for speeds below 10
      fanSpeed = 0;
    } else if (speed > 100) {
      // Maximum fan speed for speeds above 100
      fanSpeed = 255;
    } else {
      // Linear mapping for speeds between 10 and 100
      fanSpeed = map(speed, 10, 100, 50, 255);
    }
    
    // Apply the speed to the fan
    analogWrite(fanPin, fanSpeed);
    
    // Clear any remaining data in the buffer
    while(Serial.available() > 0) {
      Serial.read();
    }
  }
}