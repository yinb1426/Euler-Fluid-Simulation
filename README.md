# 欧拉流模拟效果
## 概述
欧拉流模拟使用Compute Shader，基于N-S方程计算二维流体模拟结果，并通过叠加扩散效果或FlowMap扭曲效果进行可视化。
## 特性
* 基于Stam 1999的“Stable Fluids”方法，使用Compute Shader进行计算。
* 实现平流项、扩散项、粘性项、外力项的并行计算。
* 使用叠加扩散效果，或者使用FlowMap进行背景图层扭曲，实现可视化。
## 参数
| 参数 | 类型 | <center>说明</center> | 建议参考值 |
| :------: | :------: | ------ | :------: |
| ComputeShader | ComputeShader | 欧拉流的计算着色器 | Scripts/FluidSimulatingShader.compute |
| Material | Material | 绘制欧拉流结果的材质 | Materials/FluidMaterial.mat |
| DeltaT | float | 时间步长 | 0.02 |
| VelocityDiffusion | float | 速度扩散系数 | 1.5 |
| DensityDiffusion | float | 密度扩散系数 | 1.0 |
| Radius | float | 鼠标点击时添加流体的半径 | 20 |
| MomentumStrength | float | 冲量系数 | 1.2 |
| ViscosityOn | bool | 是否添加粘性计算 | false | 
| Viscosity | Range(0, 2) | 粘度 | 1.5 |
| BackgroundTexture | Texture2D | 背景纹理 | | 
> 该参数列表仅为FluidSimulator.cs的参数列表
## 使用说明
1. 请使用Unity URP导入所有文件。
2. 本项目提供两种显示方式：叠加扩散、FlowMap扭曲，分别对应Shaders文件夹下两份ShaderGraph文件：FluidDyeShader.shadergraph、FluidFlowMapShader.shadergraph。其中前者无用户需要设置的参数，后者存在名为的参数，用于调整FlowMap带来的UV偏移程度，建议值为0.005。
3. 建议使用平面GO进行实验，将FluidMaterial.mat材质和FluidSimulator.cs脚本挂载到平面GO上，并设置好参数，即可在运行时Game模式下查看实现效果。或者直接打开提供的测试场景Fluid Scene进行实验。
## 实现效果
1. 叠加扩散效果
![DyeDiffusion](https://github.com/yinb1426/Euler-Fluid-Simulation/blob/main/Pictures/DyeDiffusion.png)
2. FlowMap扭曲效果
![FlowMapDistortion](https://github.com/yinb1426/Euler-Fluid-Simulation/blob/main/Pictures/FlowMapDistortion.png)
## 参考
* https://developer.nvidia.com/gpugems/gpugems/part-vi-beyond-triangles/chapter-38-fast-fluid-dynamics-simulation-gpu
* https://forum.taichi-lang.cn/t/topic/4061
* https://github.com/PavelDoGreat/WebGL-Fluid-Simulation?tab=readme-ov-file
