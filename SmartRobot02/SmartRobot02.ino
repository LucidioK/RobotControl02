/*
 * {'operation':'id'}
 * {'operation':'readsensors'}
 * {'operation':'constantreadsensors'}
 * {'operation':'stopconstantreadsensors'}
 * {'operation':'stop'}
 * {'operation':'noserialoutput'}
 * {'operation':'motor','l':200,'r':200}
 * {'operation':'timedmotor','l':200,'r':200,'t':5000}
 * {'operation':'normalserialoutput'}
 * {'operation':'verboseserialoutput'}
 */
#include <SPI.h>
#include <ArduinoJson.h>
// #include <Adafruit_SI1145.h>
#include <Adafruit_LSM303_Accel.h>
#include <Adafruit_LSM303DLH_Mag.h>
#include <Adafruit_Sensor.h>
#include <L298NX2.h>
//#include "VoltageReader.h"
#include <VL53L0X.h>

enum RobotStateEnum
{
  NONE,
  MOVING,
  STOPPED,
  NOSERIALOUTPUT
};

enum SerialVerboseEnum
{
  NORMAL, NOSERIAL, VERBOSE
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
SerialVerboseEnum          serialVerbose = NORMAL;
//VoltageReader              voltageReader    (A0, 47000, 33000);

VL53L0X                        distance;
// Adafruit_SI1145                uv      = Adafruit_SI1145();
Adafruit_LSM303_Accel_Unified  accel   = Adafruit_LSM303_Accel_Unified(54321);
Adafruit_LSM303DLH_Mag_Unified mag     = Adafruit_LSM303DLH_Mag_Unified(12345);
bool                           accelOK = false;
bool                           magOK   = false;
bool                           constantReadSensors = false;
// bool                           uvOK    = false;

String outStatus;
String sensorValues;
int lPower = 0, rPower = 0;
float currentDistanceInCentimeters = 0;
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

void verbose(String s)
{
  if (serialVerbose == VERBOSE)
  {
    if (s == "<NewLine>")
    {
      Serial.println("");
    }
    else
    {
      Serial.print(s);
    }
  }
}

void controlMotors(int l, int r)
{
  lPower = l; rPower = r;
  verbose("--> controlMotors l=");verbose(String(l));verbose(" r=");verbose(String(r));verbose("<NewLine>");
  if (l == 0 && r == 0)
  {
    //Serial.println("Stop!");
    motors.stop();
  }
/*  else if (l == r)
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
    motors.setSpeed(abs(l));
  }*/
  else
  {
    verbose("Move!");
    if (l > 0) 
    {
      verbose(" forwardA ");
      motors.forwardA();
    }
    else 
    {
      verbose(" backwardA ");
      motors.backwardA();
    }
    
    verbose(String(l));
    motors.setSpeedA(abs(l));
    
    if (r > 0) 
    { 
      verbose(" forwardB ");
      motors.forwardB(); 
    }
    else 
    { 
      verbose(" backwardB ");
      motors.backwardB();
    }
    verbose(String(r));
    motors.setSpeedB(abs(r));  
    verbose("<NewLine>");
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

    if (operation == "constantreadsensors")
    {
      constantReadSensors = true;
      return;
    }


    if (operation == "stopconstantreadsensors")
    {
      constantReadSensors = false;
      return;
    }
    
    if (operation == "noserialoutput")
    {
      serialVerbose = NOSERIAL;
      return;
    }

    if (operation == "normalserialoutput")
    {
      serialVerbose = NORMAL;
      return;      
    }

    if (operation == "verboseserialoutput")
    {
      serialVerbose = VERBOSE;
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
  if (abs(heading) < 0.1)
  {
    mag.getEvent(&event);
    heading = (atan2(event.magnetic.y,event.magnetic.x) * 180) / 3.14159; 
  }
  
  if (heading < 0)
  {
    heading = 360 + heading;
  }

  return heading;
}

String quote = String('"');
String strRep(String key, String val)
{
  return (quote + key + quote +":" + quote + val + quote + ",");
}
String intRep(String key, int val)
{
  return (quote + key + quote +":" + String(val) + ",");
}
String fltRep(String key, float val)
{
  return (quote + key + quote +":" + String(val, 1) + ",");
}

void sendSensorValues()
{
  if (robotState == NOSERIALOUTPUT)
  {
    return;
  }
  
  sensorValues = "{";
  sensorValues += strRep("dataType", "sensorvalues");
  sensorValues += strRep("state", getStateName());
  sensorValues += intRep("l", lPower);
  sensorValues += intRep("r", rPower);
  sensorValues += fltRep("compass", getCompassHeading());
  sensorValues += fltRep("distance", currentDistanceInCentimeters);
  if (accelOK)
  {
    sensors_event_t event;
    accel.getEvent(&event);
    sensorValues += fltRep("accelX", event.acceleration.x);
    sensorValues += fltRep("accelY", event.acceleration.y);
    sensorValues += fltRep("accelZ", event.acceleration.z);
  }
  else
  {
    outStatus += "accelNOK;";
  }

//  sensorValues += fltRep("voltage", voltageReader.Get());
  sensorValues += strRep("status", outStatus);
  sensorValues.remove(sensorValues.length() -1);
  sensorValues += "}";
  Serial.println(sensorValues);
 
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
  Serial.println("Device is ready 20210719 1051");  
  Serial.println("Accepted commands: See comments at top of this source code.");  
}

unsigned long milliseconds = 0;
void loop() 
{
  outStatus = "";
  currentDistanceInCentimeters = distance.readRangeContinuousMillimeters() / 10; 
  if (currentDistanceInCentimeters < 15 && (lPower != 0 || rPower != 0))
  {
    controlMotors(0, 0);
  }
  
  readAndDispatchCommands();
  if (constantReadSensors && (millis() - milliseconds > 100))
  {
    milliseconds = millis();
    sendSensorValues();
  }
}
