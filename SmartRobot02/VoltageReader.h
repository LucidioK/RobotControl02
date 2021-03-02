
class VoltageReader {
public:
	VoltageReader(int analogPort, float r1, float r2) {
	  _analogPort = analogPort;
	  _r1         = r1;
	  _r2         = r2;
	}
	
	float Get() {
	  float value       = analogRead(_analogPort);
	  float voltage     = value * (5.0/1024)*((_r1 + _r2)/ _r2);		
	  return voltage;
	}
	
private:
	int _analogPort;
	float _r1,_r2;
};
