// I
// S
// M-FF+FF
//
#include <L298NX2.h>
const unsigned int EN_A  = 9,
                   IN1_A = 8,
                   IN2_A = 7,
                  
                   IN1_B = 4,
                   IN2_B = 5,
                   EN_B  = 3;
                   
int                lPower = 0, 
                   rPower = 0;

// Initialize both motors
L298NX2                       motors(EN_A, IN1_A, IN2_A, EN_B, IN1_B, IN2_B);

// Distance sensor
#include <VL53L0X.h>
VL53L0X                        distance;
float                          currentDistanceInCentimeters = 0;

#include <Adafruit_Sensor.h>

// Accellerator
#include <Adafruit_LSM303_Accel.h>
Adafruit_LSM303_Accel_Unified  accel   = Adafruit_LSM303_Accel_Unified(54321);
bool                           accelOK = false;

// Compass / Magnet sensor
#include <Adafruit_LSM303DLH_Mag.h>
Adafruit_LSM303DLH_Mag_Unified mag     = Adafruit_LSM303DLH_Mag_Unified(12345);
bool                           magOK   = false;

void controlMotors(int l, int r)
{
  lPower = l; rPower = r;
  if (l == 0 && r == 0)
  {
    motors.stop();
    return;
  }

  (l > 0) ?  motors.forwardA() : motors.backwardA();
  motors.setSpeedA(abs(l));
    
  (r > 0) ? motors.forwardB() : motors.backwardB();
  motors.setSpeedB(abs(r));  
}

void stop()
{
  controlMotors(0, 0);
}

float getCompassHeading()
{
  if (!magOK)
  {
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

  heading = round(heading);
  return heading;
}


void sendSensorValues()
{
  String sensorValues = "";
  if (accelOK)
  {
    sensors_event_t event;
    accel.getEvent(&event);
    sensorValues += "X" + String(event.acceleration.x, 1);
    sensorValues += "Y" + String(event.acceleration.y, 1);
    sensorValues += "Z" + String(event.acceleration.z, 1);
  }

  if (magOK)
  {
    sensorValues += "M" + String(getCompassHeading(), 1);
  }

  sensorValues += "D" + String(currentDistanceInCentimeters);
  sensorValues += "L" + String(lPower);
  sensorValues += "R" + String(rPower);

  sensorValues += ";";

   Serial.println(sensorValues);
}

void initializeDistance()
{
  Wire.begin();

  distance.init();
  distance.setTimeout(500);
  distance.startContinuous();
}


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


// Converts a hex number in the format +FF or -FF to integer.
int xtoi(String s, int start)
{
  int signal = s[start] == '+' ? 1 : -1;
  start++;
  int tens   = (s[start] < '9') ? s[start] - '0' : s[start] - '7';
  start++;
  int ones   = (s[start] < '9') ? s[start] - '0' : s[start] - '7';

  return (tens * 16) + ones;
}

void setup() 
{
  Serial.begin(115200);
  Serial.println("Starting...");  
  initializeDistance();
  Serial.println("1");  

  initializeMag();
  Serial.println("2");  
  initializeAccel();

  Serial.println("3");  
  stop();
  Serial.println("Device is ready 20220507 1525");  
  Serial.println("Accepted commands: See comments at top of this source code.");  
}

void loop() 
{
  currentDistanceInCentimeters = distance.readRangeContinuousMillimeters() / 10; 
  if (currentDistanceInCentimeters < 15 && (lPower != 0 || rPower != 0))
  {
    controlMotors(0, 0);
  }  

  if (Serial.available() > 0)
  {
    String s = Serial.readStringUntil('\n');
    s.toUpperCase();
    switch (s[0])
    {
      case 'I':
        Serial.println("SmartRobot03.");
        break;        
      case 'M':
        lPower = xtoi(s, 1);
        rPower = xtoi(s, 4);
        controlMotors(lPower, rPower);
        // yes, I want to fall through to get sensor.
      case 'S':
        sendSensorValues();
        break;
    }
  }
}
