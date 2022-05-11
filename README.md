# RobotControl02
Simple robot control with Arduino and C#.

This is a robot that detects objects via AI, then rotates towards the object and charges towards the object.

![Robot02](https://user-images.githubusercontent.com/1053501/167752068-96307dea-36e6-46ed-a36d-6fc3354e860e.jpg) ![Robot03](https://user-images.githubusercontent.com/1053501/167752089-615b9807-a7fa-40a2-a886-9ea1e767858d.jpg)

Here is the physical diagram:
![RobotDiagram](https://user-images.githubusercontent.com/1053501/167751524-b4ce5a1c-0eb4-47c1-a0aa-5f73831ad90d.jpg)

The diagram is in file RobotDiagram.fzz.

Folder SmartRobot02 has the Arduino code.
Folder RobotControl.ClassLibrary has the AI and PC/Robot Communication.
Folder RobotControl.UI has the UI.

## How to build
For the C# part, that runs on the PC, open RobotControl02.sln on Visual Studio then build.
For the Arduino part, start the Arduino IDE and load the file SmartRobot02.ino from folder SmartRobot02.

