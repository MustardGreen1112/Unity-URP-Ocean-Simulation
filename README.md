# Unity-URP-Ocean-Simulation
Unity URP Ocean, implementing Simulating Ocean Water by Jerry Tessendorf. 
# How to use
Download the project and open the folder as a Unity Project. 
There's a demo scene in Scenes/IFFTOcean_Sample which you can refer to. To integrate the ocean to your own scene, here's what you should do: 
* Import your favorate shape to unity. 
* Attach WaveManager.cs to the shape.
* Tweak the parameters in WaveSettings for your WaveManager.cs.
* Bake the scene's light so the ocean can sample from the environmental map. 
* Adjust the oceam material in Materials/ to get a satisfied color. 
# Implementation detail
In short, I implemented IFFT in Unity's compute shader and used it to calculate the vertex displacement from the dispersion relation and statistical spectrum models from real ocean (Phillips spectrum). Please refer to [this link](https://mustard-cg.com/projects/oceansimulation) for more details. 
# How it looks like 
![](Demo/demovideo.mp4.mov)