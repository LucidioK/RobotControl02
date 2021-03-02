/*
 * {'operation':'id'}
 * {'operation':'readsensors'}
 * {'operation':'stop'}
 * {'operation':'noserialoutput'}
 * {'operation':'motor','l':200,'r':200}
 * {'operation':'timedmotor','l':200,'r':200,'t':5000}
 */
#include <SPI.h>
#include <ArduinoJson.h>
// #include <Adafruit_SI1145.h>
#include <Adafruit_LSM303_Accel.h>
#include <Adafruit_LSM303DLH_Mag.h>
#include <Adafruit_Sensor.h>
#include <L298NX2.h>
#include "VoltageReader.h"
#include <VL53L0X.h>

enum RobotStateEnum
{
  NONE,
  MOVING,
  STOPPED,
  NOSERIALOUTPUT
};

const unsigned int EN_A = 9;
const unsigned int IN1_A = 8;
const unsigned int IN2_A = 7;

const unsigned int IN1_B = 4;
const unsigned int IN2_B = 5;
const unsigned int EN_B = 3;

// Initialize both motors
L298NX2 motors(EN_A, IN1_A, IN2_A, EN_B, IN1_B, IN2_B);

RobotStateEnum             robotState = NONE;
VoltageReader              voltageReader    (A0, 47000, 33000);

VL53L0X                        distance;
// Adafruit_SI1145                uv      = Adafruit_SI1145();
Adafruit_LSM303_Accel_Unified  accel   = Adafruit_LSM303_Accel_Unified(54321);
Adafruit_LSM303DLH_Mag_Unified mag     = Adafruit_LSM303DLH_Mag_Unified(12345);
bool                           accelOK = false;
bool                           magOK   = false;
// bool                           uvOK    = false;

String outStatus;
int lPower = 0, rPower = 0;
String noneStr = "NONE", movnStr = "MOVN", caliStr = "CALI", stopStr = "STOP", unknStr = "UNKN";
String getStateName()
{
  switch (robotState)
  {
    case NONE:        return noneStr;
    case MOVING:      return movnStr;
    case STOPPED:     return stopStr;
    default:          return unknStr;
  }
}

void controlMotors(int l, int r)
{
  lPower = l; rPower = r;
  if (l == 0 && r == 0)
  {
    Serial.println("Stop!");
    motors.stop();
  }
  else if (l == r)
  {
    Serial.print("Same speed");
    if (l > 0) 
    { 
      Serial.print(" forward ");
      motors.forward(); 
    }
    else 
    { 
      Serial.print(" backward ");
      motors.backward(); 
    }
    Serial.println(l);
    motors.setSpeed(l);
  }
  else
  {
    Serial.print("Different speeds");
    if (l > 0) 
    {
      Serial.print(" forwardA ");
      motors.forwardA();
    }
    else 
    {
      Serial.print(" backwardA ");
      motors.backwardA();
    }
    
    Serial.print(l);
    motors.setSpeedA(l);
    
    if (r > 0) 
    { 
      Serial.print(" forwardB ");
      motors.forwardB(); 
    }
    else 
    { 
      Serial.print(" backwardB ");
      motors.backwardB();
    }
    Serial.print(r);
    motors.setSpeedB(r);  
    Serial.print("");
  }
}

void stop()
{
  controlMotors(0, 0);
  robotState = STOPPED;
}

String getJsonStringValue(String json, String key)
{
  key = "'" + key + "'";
  int keyPos = json.indexOf(key);
  if (keyPos >= 0)
  {
    int startPos = keyPos + key.length() + 2; // plus 2 to skip :'
    int endPos = json.indexOf("'", startPos);
    return json.substring(startPos, endPos);
  }

  return "";
}

int getJsonIntValue(String json, String key)
{
  key = "'" + key + "'";
  int keyPos = json.indexOf(key);
  if (keyPos >= 0)
  {
    int startPos = keyPos + key.length() + 1; // plus 2 to skip :
    int endPos = startPos + 1;
    while (json[endPos] >= '0' && json[endPos] <= '9' && endPos < json.length()) endPos++;
    return json.substring(startPos, endPos).toInt();
  }

  return 0;
}

void readAndDispatchCommands()
{
  if (Serial.available() > 0)
  {
    String s = Serial.readStringUntil('\n');
    s.replace("\"","'");
    s.toLowerCase();
    outStatus += s;
    outStatus += ";";

    String operation = getJsonStringValue(s, "operation");
    
    if (operation == "stop")
    {
      stop();
      return;
    }
    
    if (operation == "id")
    {
      Serial.println("SmartRobot02.");
      return;
    }

    if (operation == "readsensors")
    {
      sendSensorValues();
      return;
    }

    if (operation == "noserialoutput")
    {
      robotState = NOSERIALOUTPUT;
      return;
    }

    int l,r;
    if (operation == "motor" || operation == "timedmotor")
    {
      l = getJsonIntValue(s, "l");
      r = getJsonIntValue(s, "r");
    }
    
    if (operation == "motor")
    {
      robotState = MOVING;
      controlMotors(l, r);
      return;
    }

    if (operation == "timedmotor")
    {
      robotState = MOVING;
      int t = getJsonIntValue(s, "t");
      controlMotors(l, r);
      delay(t);
      stop();
      return;
    }
  }
}

float getCompassHeading()
{
  if (!magOK)
  {
    outStatus += "magNOK;";
    return -1.0;
  }
  sensors_event_t event;
  mag.getEvent(&event);
 
  float heading = (atan2(event.magnetic.y,event.magnetic.x) * 180) / 3.14159;
 
  if (heading < 0)
  {
    heading = 360 + heading;
  }

  return heading;
}

float roundTo1Digit(float n)
{
  int n2 = (int)(n * 10);
  n = (float)n2 / 10.0;
  return n;
}

String quote = String('"');
String strRep(String key, String val)
{
  return (quote + key + quote +":" + quote + val + quote + ",");
}
String intRep(String key, int val)
{
  return (quote + key + quote +":" + quote + String(val) + quote + ",");
}
String fltRep(String key, float val)
{
  return (quote + key + quote +":" + quote + String(val, 1) + quote + ",");
}

void sendSensorValues()
{
  if (robotState == NOSERIALOUTPUT)
  {
    return;
  }
  
  String sv = "{";
  sv += strRep("dataType", "sensorvalues");
  sv += strRep("state", getStateName());
  sv += intRep("l", lPower);
  sv += intRep("r", rPower);
  if (accelOK)
  {
    sensors_event_t event;
    accel.getEvent(&event);
    sv += fltRep("accelX", event.acceleration.x);
    sv += fltRep("accelY", event.acceleration.y);
    sv += fltRep("accelZ", event.acceleration.z);
  }
  else
  {
    outStatus += "accelNOK;";
  }

  sv += fltRep("compass", getCompassHeading());

  sv += fltRep("distance", distance.readRangeContinuousMillimeters() / 10);
  sv += fltRep("voltage", voltageReader.Get());
  sv += strRep("status", outStatus);
  sv.remove(sv.length() -1);
  sv += "}";
  Serial.println(sv);
 
}

void initializeDistance()
{
  Wire.begin();

  distance.init();
  distance.setTimeout(500);

  // Start continuous back-to-back mode (take readings as
  // fast as possible).  To use continuous timed mode
  // instead, provide a desired inter-measurement period in
  // ms (e.g. distance.startContinuous(100)).
  distance.startContinuous();
}

//void initializeUV()
//{
//  uvOK = uv.begin();
//}

void initializeMag()
{
  mag.enableAutoRange(true);
  magOK   = mag.begin();
}

void initializeAccel()
{
  accelOK = accel.begin();

  if (accelOK)
  {
    accel.setRange(LSM303_RANGE_4G);
  }
}

void setup() 
{
  Serial.begin(115200);
  initializeDistance();
//  initializeUV();
  initializeMag();
  initializeAccel();

  stop();
  Serial.println("Device is ready 20210301 1200");  
  Serial.println("Accepted commands:");  
  Serial.println("{'operation':'id'}");
  Serial.println("{'operation':'readsensors'}");
  Serial.println("{'operation':'stop'}");
  Serial.println("{'operation':'noserialoutput'}");
  Serial.println("{'operation':'motor','l':200,'r':200}");
  Serial.println("{'operation':'timedmotor','l':200,'r':200,'t':5000}  ");
}

void loop() 
{
  outStatus = "";
  readAndDispatchCommands();
}
